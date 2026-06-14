using System.Diagnostics;
using InfiMouse.Model;

namespace InfiMouse.Core;

/// <summary>
/// 高精度事件播放引擎：按时间轴顺序逐事件触发，支持播放/暂停/停止/循环。
/// </summary>
public class EventPlayer
{
    private readonly IMouseInjector _mouseInjector;
    private readonly IKeyInjector _keyInjector;
    private readonly IGamepadInjector _gamepadInjector;

    private CancellationTokenSource? _cts;
    private Task? _playTask;
    private readonly object _lock = new();

    /// <summary>播放状态。</summary>
    public PlayState State { get; private set; } = PlayState.Stopped;

    /// <summary>播放进度回调（用于 UI 日志）。</summary>
    public event Action<string>? OnLog;

    /// <summary>播放完成回调。</summary>
    public event Action? OnCompleted;

    public EventPlayer(IMouseInjector mouseInjector, IKeyInjector keyInjector, IGamepadInjector gamepadInjector)
    {
        _mouseInjector = mouseInjector;
        _keyInjector = keyInjector;
        _gamepadInjector = gamepadInjector;
    }

    /// <summary>
    /// 将鼠标运动帧与用户自定义事件合并为统一时间轴。
    /// </summary>
    public List<TimedEvent> BuildTimeline(
        List<MotionFrame> mouseFrames,
        List<TimedEvent> userEvents,
        bool holdLeftButton = false)
    {
        var timeline = new List<TimedEvent>();

        int totalFrames = mouseFrames.Count;

        // 鼠标移动事件
        for (int i = 0; i < totalFrames; i++)
        {
            var frame = mouseFrames[i];

            // 按住左键模式：第一帧前注入按下事件
            if (holdLeftButton && i == 0)
            {
                timeline.Add(new TimedEvent
                {
                    TimestampMs = Math.Max(0, (int)frame.Timestamp.TotalMilliseconds - 1),
                    Type = TimedEventType.LeftButton,
                    Data = new KeyEventData { VkCode = 0x01, IsPress = true }
                });
            }

            timeline.Add(new TimedEvent
            {
                TimestampMs = (int)frame.Timestamp.TotalMilliseconds,
                Type = TimedEventType.MouseMove,
                Data = new MouseMoveData { X = frame.Position.X, Y = frame.Position.Y }
            });

            // 按住左键模式：最后一帧后注入释放事件
            if (holdLeftButton && i == totalFrames - 1)
            {
                timeline.Add(new TimedEvent
                {
                    TimestampMs = (int)frame.Timestamp.TotalMilliseconds + 1,
                    Type = TimedEventType.LeftButton,
                    Data = new KeyEventData { VkCode = 0x01, IsPress = false }
                });
            }
        }

        // 合并用户事件
        timeline.AddRange(userEvents);

        // 按时间戳升序排序
        timeline.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));

        return timeline;
    }

    /// <summary>
    /// 异步播放事件时间轴。
    /// </summary>
    public async Task PlayAsync(List<TimedEvent> timeline, bool loop = false)
    {
        if (timeline.Count == 0) return;

        lock (_lock)
        {
            if (State == PlayState.Playing) return;
            State = PlayState.Playing;
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _playTask = Task.Run(async () =>
        {
            try
            {
                do
                {
                    var sw = Stopwatch.StartNew();
                    int currentIndex = 0;
                    long pausedOffset = 0;

                    while (currentIndex < timeline.Count && !ct.IsCancellationRequested)
                    {
                        var evt = timeline[currentIndex];
                        long elapsed = sw.ElapsedMilliseconds + pausedOffset;
                        long targetMs = evt.TimestampMs;

                        if (elapsed < targetMs)
                        {
                            long waitMs = targetMs - elapsed;
                            if (waitMs > 1)
                                await Task.Delay((int)Math.Min(waitMs, 100), ct);
                            else
                                SpinWait.SpinUntil(() => sw.ElapsedMilliseconds + pausedOffset >= targetMs);
                            continue;
                        }

                        // 收集同一毫秒内的所有事件
                        var batch = new List<TimedEvent>();
                        while (currentIndex < timeline.Count && timeline[currentIndex].TimestampMs <= targetMs + 1)
                        {
                            batch.Add(timeline[currentIndex]);
                            currentIndex++;
                        }

                        await ExecuteBatchAsync(batch);
                    }

                    Log($"播放完成（共 {timeline.Count} 个事件）");
                }
                while (loop && !ct.IsCancellationRequested);

                OnCompleted?.Invoke();
            }
            catch (OperationCanceledException) { /* 正常取消 */ }
            catch (Exception ex)
            {
                Log($"播放异常: {ex.Message}");
            }
            finally
            {
                lock (_lock) { State = PlayState.Stopped; }
            }
        }, ct);

        await Task.CompletedTask;
    }

    /// <summary>暂停播放。</summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (State != PlayState.Playing) return;
            State = PlayState.Paused;
        }
        _cts?.Cancel();
        Log("播放已暂停");
    }

    /// <summary>停止播放。</summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (State == PlayState.Stopped) return;
            State = PlayState.Stopped;
        }
        _cts?.Cancel();
        Log("播放已停止");
    }

    /// <summary>等待播放任务完成。</summary>
    public async Task WaitForCompletionAsync()
    {
        if (_playTask != null)
            await _playTask;
    }

    private Task ExecuteBatchAsync(List<TimedEvent> batch)
    {
        foreach (var evt in batch)
        {
            switch (evt.Type)
            {
                case TimedEventType.MouseMove:
                    if (evt.Data is MouseMoveData mm)
                    {
                        _mouseInjector.InjectAbsolute((float)mm.X, (float)mm.Y);
                        Log($"t={evt.TimestampMs}ms: 鼠标移动到 ({mm.X:F0}, {mm.Y:F0})");
                    }
                    break;

                case TimedEventType.KeyDown:
                case TimedEventType.KeyUp:
                    if (evt.Data is KeyEventData kd)
                    {
                        if (evt.Type == TimedEventType.KeyDown)
                        {
                            _keyInjector.Press(kd.VkCode);
                            Log($"t={evt.TimestampMs}ms: 按下键 0x{kd.VkCode:X2}");
                        }
                        else
                        {
                            _keyInjector.Release(kd.VkCode);
                            Log($"t={evt.TimestampMs}ms: 释放键 0x{kd.VkCode:X2}");
                        }
                    }
                    break;

                case TimedEventType.LeftButton:
                    if (evt.Data is KeyEventData lbd)
                    {
                        _mouseInjector.InjectLeftButton(lbd.IsPress);
                        Log($"t={evt.TimestampMs}ms: 鼠标左键{(lbd.IsPress ? "按下" : "释放")}");
                    }
                    break;

                case TimedEventType.GamepadButton:
                case TimedEventType.GamepadStick:
                case TimedEventType.GamepadTrigger:
                    if (evt.Data is GamepadEventData gd)
                    {
                        _gamepadInjector.SetState(gd);
                        Log($"t={evt.TimestampMs}ms: 手柄事件 (Buttons=0x{gd.ButtonFlags:X4})");
                    }
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private void Log(string message)
    {
        OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}

/// <summary>播放状态枚举。</summary>
public enum PlayState
{
    Stopped,
    Playing,
    Paused,
}

#region 注入器接口（解耦 Core ↔ Input）

/// <summary>鼠标注入器接口。</summary>
public interface IMouseInjector
{
    void InjectAbsolute(float pixelX, float pixelY);
    void InjectRelative(int dx, int dy);
    void InjectLeftButton(bool down);
}

/// <summary>键盘注入器接口。</summary>
public interface IKeyInjector
{
    void Press(ushort vkCode);
    void Release(ushort vkCode);
    void Tap(ushort vkCode, int durationMs);
}

/// <summary>手柄注入器接口。</summary>
public interface IGamepadInjector
{
    void SetState(GamepadEventData data);
}

#endregion
