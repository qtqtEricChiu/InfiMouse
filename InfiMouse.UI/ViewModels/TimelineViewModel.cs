using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfiMouse.Model;
using System.Collections.ObjectModel;

namespace InfiMouse.UI;

/// <summary>
/// 时间轴 ViewModel：管理时间轴事件队列，支持增删改查。
/// </summary>
public partial class TimelineViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<TimedEvent> _events = new();

    [ObservableProperty]
    private int _totalDurationMs = 2000;

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>按时间戳升序插入事件。</summary>
    public void InsertEvent(TimedEvent evt)
    {
        int idx = 0;
        while (idx < Events.Count && Events[idx].TimestampMs <= evt.TimestampMs) idx++;
        Events.Insert(idx, evt);
    }

    /// <summary>移除指定事件。</summary>
    [RelayCommand]
    private void RemoveEvent(TimedEvent evt)
    {
        Events.Remove(evt);
    }

    /// <summary>移除选中事件。</summary>
    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Events.Count)
        {
            Events.RemoveAt(SelectedIndex);
            SelectedIndex = -1;
        }
    }

    /// <summary>清空所有事件。</summary>
    [RelayCommand]
    private void ClearAll()
    {
        Events.Clear();
        SelectedIndex = -1;
    }

    /// <summary>添加鼠标移动事件。</summary>
    public void AddMouseMove(int timestampMs, double x, double y)
    {
        InsertEvent(new TimedEvent
        {
            TimestampMs = timestampMs,
            Type = TimedEventType.MouseMove,
            Data = new MouseMoveData { X = x, Y = y }
        });
    }

    /// <summary>添加键盘事件。</summary>
    public void AddKeyEvent(int timestampMs, ushort vkCode, bool isPress, int durationMs = 0)
    {
        InsertEvent(new TimedEvent
        {
            TimestampMs = timestampMs,
            Type = isPress ? TimedEventType.KeyDown : TimedEventType.KeyUp,
            Data = new KeyEventData { VkCode = vkCode, IsPress = isPress, DurationMs = durationMs }
        });
    }

    /// <summary>添加手柄事件。</summary>
    public void AddGamepadEvent(int timestampMs, GamepadEventData data, TimedEventType type = TimedEventType.GamepadButton)
    {
        InsertEvent(new TimedEvent
        {
            TimestampMs = timestampMs,
            Type = type,
            Data = data
        });
    }

    /// <summary>更新总时长（自动修正超出范围的 TimestampMs）。</summary>
    public void UpdateTotalDuration(int newDurationMs)
    {
        TotalDurationMs = newDurationMs;
    }

    /// <summary>获取事件的简短描述文本（不含时间戳，时间戳由 EditorPage 单独渲染）。</summary>
    public static string GetEventSummary(TimedEvent evt)
    {
        return evt.Type switch
        {
            TimedEventType.MouseMove when evt.Data is MouseMoveData mm =>
                $"鼠标 → ({mm.X:F0}, {mm.Y:F0})",
            TimedEventType.KeyDown when evt.Data is KeyEventData kd =>
                $"按下 0x{kd.VkCode:X2}",
            TimedEventType.KeyUp when evt.Data is KeyEventData kd =>
                $"释放 0x{kd.VkCode:X2}",
            TimedEventType.GamepadButton when evt.Data is GamepadEventData gd =>
                $"手柄按钮 0x{gd.ButtonFlags:X4}",
            TimedEventType.GamepadStick when evt.Data is GamepadEventData gd =>
                $"摇杆 L({gd.LeftThumbX},{gd.LeftThumbY}) R({gd.RightThumbX},{gd.RightThumbY})",
            TimedEventType.GamepadTrigger when evt.Data is GamepadEventData gd =>
                $"扳机 L={gd.LeftTrigger} R={gd.RightTrigger}",
            _ => $"{evt.Type}"
        };
    }
}
