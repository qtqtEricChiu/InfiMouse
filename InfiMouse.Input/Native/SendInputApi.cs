using System.Runtime.InteropServices;

namespace InfiMouse.Input.Native;

/// <summary>
/// SendInput P/Invoke 封装，提供高层便捷方法。
/// </summary>
public static class SendInputApi
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Win32.INPUT[] pInputs, int cbSize);

    private static readonly int InputSize = Marshal.SizeOf<Win32.INPUT>();

    /// <summary>
    /// 发送原始 INPUT 数组。
    /// </summary>
    public static uint Send(Win32.INPUT[] inputs)
    {
        return SendInput((uint)inputs.Length, inputs, InputSize);
    }

    /// <summary>
    /// 发送绝对坐标鼠标移动（0-65535 归一化坐标）。
    /// </summary>
    public static uint SendMouseMoveAbsolute(int nx, int ny)
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUT_UNION
            {
                mi = new Win32.MOUSEINPUT
                {
                    dx = nx,
                    dy = ny,
                    dwFlags = Win32.MOUSEEVENTF_MOVE | Win32.MOUSEEVENTF_ABSOLUTE,
                    mouseData = 0,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                }
            }
        };
        return Send(new[] { input });
    }

    /// <summary>
    /// 发送相对坐标鼠标移动（像素偏移）。
    /// </summary>
    public static uint SendMouseMoveRelative(int dx, int dy)
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUT_UNION
            {
                mi = new Win32.MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = Win32.MOUSEEVENTF_MOVE,
                    mouseData = 0,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                }
            }
        };
        return Send(new[] { input });
    }

    /// <summary>鼠标左键按下。</summary>
    public static uint SendMouseLeftDown()
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUT_UNION
            {
                mi = new Win32.MOUSEINPUT { dwFlags = Win32.MOUSEEVENTF_LEFTDOWN }
            }
        };
        return Send(new[] { input });
    }

    /// <summary>鼠标左键释放。</summary>
    public static uint SendMouseLeftUp()
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUT_UNION
            {
                mi = new Win32.MOUSEINPUT { dwFlags = Win32.MOUSEEVENTF_LEFTUP }
            }
        };
        return Send(new[] { input });
    }

    /// <summary>键盘按下。</summary>
    public static uint SendKeyDown(ushort vkCode, bool extended = false)
    {
        uint flags = 0;
        if (extended) flags |= Win32.KEYEVENTF_EXTENDEDKEY;

        var input = new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            u = new Win32.INPUT_UNION
            {
                ki = new Win32.KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                }
            }
        };
        return Send(new[] { input });
    }

    /// <summary>键盘释放。</summary>
    public static uint SendKeyUp(ushort vkCode, bool extended = false)
    {
        uint flags = Win32.KEYEVENTF_KEYUP;
        if (extended) flags |= Win32.KEYEVENTF_EXTENDEDKEY;

        var input = new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            u = new Win32.INPUT_UNION
            {
                ki = new Win32.KEYBDINPUT
                {
                    wVk = vkCode,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero,
                }
            }
        };
        return Send(new[] { input });
    }
}
