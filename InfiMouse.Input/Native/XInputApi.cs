using System.Runtime.InteropServices;

namespace InfiMouse.Input.Native;

/// <summary>
/// XInput API P/Invoke 封装。
/// 调用 xinput1_4.dll（Windows 10+ 内置）。
/// </summary>
public static class XInputApi
{
    private const string XInputDll = "xinput1_4.dll";

    #region 常量

    public const int ERROR_SUCCESS = 0;
    public const int ERROR_DEVICE_NOT_CONNECTED = 1167;

    // 按键位掩码
    public const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    public const ushort XINPUT_GAMEPAD_START = 0x0010;
    public const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    public const ushort XINPUT_GAMEPAD_A = 0x1000;
    public const ushort XINPUT_GAMEPAD_B = 0x2000;
    public const ushort XINPUT_GAMEPAD_X = 0x4000;
    public const ushort XINPUT_GAMEPAD_Y = 0x8000;

    #endregion

    #region 结构体

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;   // 0-65535
        public ushort wRightMotorSpeed;  // 0-65535
    }

    #endregion

    #region P/Invoke

    /// <summary>
    /// 获取指定手柄的状态。
    /// </summary>
    /// <param name="dwUserIndex">手柄索引 0-3</param>
    /// <param name="pState">输出状态</param>
    /// <returns>ERROR_SUCCESS(0) 表示连接成功</returns>
    [DllImport(XInputDll, SetLastError = true)]
    public static extern uint XInputGetState(int dwUserIndex, ref XINPUT_STATE pState);

    /// <summary>
    /// 设置指定手柄的震动。
    /// </summary>
    /// <param name="dwUserIndex">手柄索引 0-3</param>
    /// <param name="pVibration">震动参数</param>
    /// <returns>ERROR_SUCCESS(0) 表示设置成功</returns>
    [DllImport(XInputDll, SetLastError = true)]
    public static extern uint XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    #endregion

    /// <summary>
    /// 检测指定索引的手柄是否已连接。
    /// </summary>
    public static bool IsConnected(int controllerIndex = 0)
    {
        var state = new XINPUT_STATE();
        return XInputGetState(controllerIndex, ref state) == ERROR_SUCCESS;
    }

    /// <summary>
    /// 获取手柄状态。
    /// </summary>
    public static XINPUT_STATE? GetState(int controllerIndex = 0)
    {
        var state = new XINPUT_STATE();
        if (XInputGetState(controllerIndex, ref state) == ERROR_SUCCESS)
            return state;
        return null;
    }
}
