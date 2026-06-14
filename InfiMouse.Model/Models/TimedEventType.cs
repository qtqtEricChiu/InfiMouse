namespace InfiMouse.Model;

/// <summary>
/// 时间轴事件类型枚举。
/// </summary>
public enum TimedEventType
{
    /// <summary>鼠标移动事件。</summary>
    MouseMove,
    /// <summary>键盘按下。</summary>
    KeyDown,
    /// <summary>键盘释放。</summary>
    KeyUp,
    /// <summary>手柄按键。</summary>
    GamepadButton,
    /// <summary>手柄摇杆。</summary>
    GamepadStick,
    /// <summary>手柄扳机。</summary>
    GamepadTrigger,
    /// <summary>自定义延迟（停顿）。</summary>
    Delay,
    /// <summary>条件检测点。</summary>
    Checkpoint,
    /// <summary>鼠标左键（按下/释放）。</summary>
    LeftButton,
}
