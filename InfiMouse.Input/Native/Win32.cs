using System.Runtime.InteropServices;

namespace InfiMouse.Input.Native;

/// <summary>
/// SendInput / Win32 API 常量与结构体定义。
/// </summary>
public static class Win32
{
    #region SendInput 常量

    public const int INPUT_MOUSE = 0;
    public const int INPUT_KEYBOARD = 1;
    public const int INPUT_HARDWARE = 2;

    // 鼠标事件标志
    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint MOUSEEVENTF_WHEEL = 0x0800;
    public const uint MOUSEEVENTF_HWHEEL = 0x1000;

    // 键盘事件标志
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    // 系统指标
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    #endregion

    #region 结构体定义

    /// <summary>
    /// MOUSEINPUT 结构体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;           // 绝对模式：0-65535；相对模式：像素偏移
        public int dy;
        public uint mouseData;   // 滚轮：WHEEL_DELTA=120
        public uint dwFlags;
        public uint time;        // 0 = 系统自动填充
        public UIntPtr dwExtraInfo;
    }

    /// <summary>
    /// KEYBDINPUT 结构体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;         // 虚拟键码
        public ushort wScan;       // 硬件扫描码（dwFlags 含 KEYEVENTF_SCANCODE 时使用）
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    /// <summary>
    /// HARDWAREINPUT 结构体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    /// <summary>
    /// INPUT Union 结构体（使用 Explicit 布局）。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT_UNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    /// <summary>
    /// INPUT 结构体。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUT_UNION u;
    }

    #endregion
}
