using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiMouse.Core;
using InfiMouse.Input;
using InfiMouse.AntiDetection;
using InfiMouse.Model;
using System.Collections.ObjectModel;

namespace InfiMouse.UI;

/// <summary>
/// 主 ViewModel：聚合核心引擎、注入器和配置，作为 UI 与业务逻辑的桥梁。
/// 使用 CommunityToolkit.Mvvm 实现 ObservableObject + [RelayCommand]。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // ── 注入器（由 App 层创建注入，ViewModel 持有） ──
    public MouseInjector MouseInjector { get; } = new();
    public KeyInjector KeyInjector { get; } = new();
    public GamepadInjector GamepadInjector { get; } = new();

    // ── 核心引擎 ──
    public MotionPlanner MotionPlanner { get; } = new();
    public EventPlayer? EventPlayer { get; private set; }

    // ── 可绑定属性 ──

    [ObservableProperty]
    private AnimationConfig _animation = new();

    [ObservableProperty]
    private AntiDetectionConfig _antiDetection = new();

    [ObservableProperty]
    private ProjectFile _currentProject = new();

    [ObservableProperty]
    private ObservableCollection<PathPoint> _pathPoints = new();

    [ObservableProperty]
    private ObservableCollection<TimedEvent> _timelineEvents = new();

    [ObservableProperty]
    private ObservableCollection<string> _logEntries = new();

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isLooping;

    [ObservableProperty]
    private bool _holdLeftButton;

    [ObservableProperty]
    private int _playbackProgressMs;

    /// <summary>循环状态变化时触发（供 MainWindow 更新提示条）。</summary>
    public event EventHandler<bool>? LoopIndicatorChanged;

    partial void OnIsLoopingChanged(bool value)
    {
        LoopIndicatorChanged?.Invoke(this, value);
    }

    /// <summary>拟人化处理器。</summary>
    public Humanizer? Humanizer { get; private set; }

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;

    public MainViewModel()
    {
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        InitializePlayer();
    }

    /// <summary>初始化 EventPlayer 并绑定日志。</summary>
    public void InitializePlayer()
    {
        Humanizer = new Humanizer(AntiDetection);
        EventPlayer = new EventPlayer(MouseInjector, KeyInjector, GamepadInjector);

        EventPlayer.OnLog += (msg) =>
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            _dispatcher.TryEnqueue(() =>
            {
                LogEntries.Insert(0, $"[{ts}] {msg}");
                if (LogEntries.Count > 500) LogEntries.RemoveAt(LogEntries.Count - 1);
            });
        };

        EventPlayer.OnCompleted += () =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                IsPlaying = false;
                PlaybackProgressMs = 0;

                // 播放完成后自动回到起点
                if (CurrentProject.PathPoints.Count > 0)
                {
                    var first = CurrentProject.PathPoints[0];
                    MouseInjector.InjectAbsolute((float)first.X, (float)first.Y);
                    AddLog("播放完毕，已回到起点");
                }
            });
        };
    }

    /// <summary>记录日志（线程安全，始终在 UI 线程执行）。</summary>
    public void AddLog(string message)
    {
        _dispatcher.TryEnqueue(() =>
        {
            LogEntries.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
            if (LogEntries.Count > 500) LogEntries.RemoveAt(LogEntries.Count - 1);
        });
    }

    // ── 命令 ──

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (IsPlaying) return;

        if (CurrentProject.PathPoints.Count < 2)
        {
            AddLog("错误：至少需要设置起点和终点");
            return;
        }

        try
        {
            var start = new System.Drawing.PointF(
                (float)CurrentProject.PathPoints[0].X,
                (float)CurrentProject.PathPoints[0].Y);
            var end = new System.Drawing.PointF(
                (float)CurrentProject.PathPoints[^1].X,
                (float)CurrentProject.PathPoints[^1].Y);

            var (cp1, cp2) = ControlPointGenerator.Generate(start, end);

            if (CurrentProject.ControlPoint1 != null)
                cp1 = new System.Drawing.PointF((float)CurrentProject.ControlPoint1.X, (float)CurrentProject.ControlPoint1.Y);
            if (CurrentProject.ControlPoint2 != null)
                cp2 = new System.Drawing.PointF((float)CurrentProject.ControlPoint2.X, (float)CurrentProject.ControlPoint2.Y);

            if (Animation.EnableRandomization && Humanizer != null)
                (cp1, cp2) = Humanizer.RandomizeControlPoints(cp1, cp2);

            var curve = new BezierCurve3(start, cp1, cp2, end);
            var frames = MotionPlanner.PlanMotion(curve, Animation.Easing, Animation.TotalDuration, Animation.FrameCount);

            if (Animation.EnableRandomization && Humanizer != null)
            {
                frames = Humanizer.JitterFramePositions(frames);
                frames = Humanizer.JitterTimestamps(frames);
            }

            var timeline = EventPlayer!.BuildTimeline(frames, CurrentProject.Events, HoldLeftButton);

            if (Animation.EnableRandomization && Humanizer != null)
                timeline = Humanizer.InsertRandomPauses(timeline);

            IsPlaying = true;
            AddLog($"开始播放（{timeline.Count} 个事件，{CurrentProject.Events.Count} 个用户事件）");

            await EventPlayer.PlayAsync(timeline, IsLooping);
        }
        catch (Exception ex)
        {
            AddLog($"播放失败: {ex.Message}");
            IsPlaying = false;
        }
    }

    [RelayCommand]
    private void PausePlay()
    {
        EventPlayer?.Pause();
        IsPlaying = false;
        AddLog("播放已暂停");
    }

    [RelayCommand]
    private void StopPlay()
    {
        EventPlayer?.Stop();
        IsPlaying = false;
        AddLog("播放已停止");
    }

    [RelayCommand]
    private void ToggleLoop()
    {
        IsLooping = !IsLooping;
        AddLog(IsLooping ? "已启用循环播放" : "已禁用循环播放");
    }

    [RelayCommand]
    private void ClearProject()
    {
        PathPoints.Clear();
        TimelineEvents.Clear();
        CurrentProject = new ProjectFile();
        AddLog("已新建空白项目");
    }

    [RelayCommand]
    private async Task SaveProjectAsync(string filePath)
    {
        try
        {
            CurrentProject.PathPoints = new List<PathPoint>(PathPoints);
            CurrentProject.Events = new List<TimedEvent>(TimelineEvents);
            CurrentProject.Animation = Animation;
            CurrentProject.AntiDetection = AntiDetection;

            await ProjectSerializer.SaveAsync(CurrentProject, filePath);
            AddLog($"项目已保存: {System.IO.Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            AddLog($"保存失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadProjectAsync(string filePath)
    {
        try
        {
            var project = await ProjectSerializer.LoadAsync(filePath);
            if (project == null)
            {
                AddLog("加载失败：文件格式无效");
                return;
            }

            CurrentProject = project;
            Animation = project.Animation ?? new AnimationConfig();
            AntiDetection = project.AntiDetection ?? new AntiDetectionConfig();

            PathPoints.Clear();
            foreach (var pt in project.PathPoints)
                PathPoints.Add(pt);

            TimelineEvents.Clear();
            foreach (var evt in project.Events)
                TimelineEvents.Add(evt);

            AddLog($"项目已加载: {System.IO.Path.GetFileName(filePath)} ({PathPoints.Count} 个路径点，{TimelineEvents.Count} 个事件)");
        }
        catch (Exception ex)
        {
            AddLog($"加载失败: {ex.Message}");
        }
    }

    /// <summary>添加键盘事件到时间轴（线程安全，始终在 UI 线程执行）。</summary>
    public void AddKeyEvent(ushort vkCode, bool isPress, int timestampMs)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var evt = new TimedEvent
            {
                TimestampMs = timestampMs,
                Type = isPress ? TimedEventType.KeyDown : TimedEventType.KeyUp,
                Data = new KeyEventData { VkCode = vkCode, IsPress = isPress }
            };

            // 按时间戳插入
            int idx = 0;
            while (idx < TimelineEvents.Count && TimelineEvents[idx].TimestampMs <= evt.TimestampMs) idx++;
            TimelineEvents.Insert(idx, evt);

            AddLog($"t={timestampMs}ms: {(isPress ? "按下" : "释放")}键 0x{vkCode:X2}");
        });
    }
}
