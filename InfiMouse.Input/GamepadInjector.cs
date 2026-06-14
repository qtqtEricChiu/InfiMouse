using InfiMouse.Core;
using InfiMouse.Input.Native;
using InfiMouse.Model;

namespace InfiMouse.Input;

/// <summary>
/// 手柄注入器：通过 XInput 设置摇杆、扳机、按键状态。
/// 
/// 重要限制：XInputSetState 仅支持震动控制。
/// 完整的按键/摇杆模拟需要引入 ViGEm 虚拟手柄驱动（NuGet: ViGEm.Net）。
/// 当前实现提供 XInput 震动 + ViGEm 虚拟手柄的抽象接口。
/// </summary>
public class GamepadInjector : IGamepadInjector
{
    private readonly int _controllerIndex;

    public GamepadInjector(int controllerIndex = 0)
    {
        _controllerIndex = controllerIndex;
    }

    /// <summary>
    /// 设置手柄震动。
    /// </summary>
    /// <param name="leftMotor">左马达转速（0-65535）</param>
    /// <param name="rightMotor">右马达转速（0-65535）</param>
    public void SetVibration(ushort leftMotor, ushort rightMotor)
    {
        var vibration = new XInputApi.XINPUT_VIBRATION
        {
            wLeftMotorSpeed = leftMotor,
            wRightMotorSpeed = rightMotor,
        };
        XInputApi.XInputSetState(_controllerIndex, ref vibration);
    }

    /// <summary>停止震动。</summary>
    public void StopVibration()
    {
        SetVibration(0, 0);
    }

    /// <summary>
    /// 根据 GamepadEventData 设置手柄状态。
    /// 当前实现仅处理震动；完整摇杆/按键模拟需集成 ViGEm。
    /// </summary>
    public void SetState(GamepadEventData data)
    {
        // XInput 无法模拟按键/摇杆，此处为占位。
        // 完整实现需调用 ViGEm 接口：
        //
        // _virtualGamepad.SetAxis(ViGEmAxis.LeftThumbX, data.LeftThumbX);
        // _virtualGamepad.SetAxis(ViGEmAxis.LeftThumbY, data.LeftThumbY);
        // ...
        // _virtualGamepad.SetButtonState(ViGEmButton.A, (data.ButtonFlags & XInputApi.XINPUT_GAMEPAD_A) != 0);
    }

    /// <summary>
    /// 检测手柄是否已连接。
    /// </summary>
    public bool IsConnected() => XInputApi.IsConnected(_controllerIndex);

    /// <summary>
    /// 获取真实手柄当前状态（用于事件融合模式）。
    /// </summary>
    public XInputApi.XINPUT_STATE? GetRealState() => XInputApi.GetState(_controllerIndex);
}
