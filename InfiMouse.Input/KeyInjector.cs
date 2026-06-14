using InfiMouse.Core;
using InfiMouse.Input.Native;

namespace InfiMouse.Input;

/// <summary>
/// 键盘注入器：通过 SendInput 模拟按键按下 / 释放 / 连击。
/// </summary>
public class KeyInjector : IKeyInjector
{
    /// <summary>发送 KEY_DOWN。</summary>
    public void Press(ushort vkCode)
    {
        SendInputApi.SendKeyDown(vkCode);
    }

    /// <summary>发送 KEY_UP。</summary>
    public void Release(ushort vkCode)
    {
        SendInputApi.SendKeyUp(vkCode);
    }

    /// <summary>
    /// 按键连击：按下 → 等待 durationMs → 释放。
    /// </summary>
    public void Tap(ushort vkCode, int durationMs)
    {
        Press(vkCode);
        Thread.Sleep(durationMs);
        Release(vkCode);
    }

    /// <summary>
    /// 组合键：按传入顺序依次按下所有键，然后逆序释放。
    /// 例如 Combo(VK_CONTROL, VK_C) 模拟 Ctrl+C。
    /// </summary>
    public void Combo(params ushort[] vkCodes)
    {
        if (vkCodes.Length == 0) return;

        // 依次按下
        foreach (var vk in vkCodes)
            Press(vk);

        Thread.Sleep(50);

        // 逆序释放
        for (int i = vkCodes.Length - 1; i >= 0; i--)
            Release(vkCodes[i]);
    }

    #region 便捷方法

    public void PressW() => Press(VirtualKeyCodes.VK_W);
    public void ReleaseW() => Release(VirtualKeyCodes.VK_W);
    public void TapW(int durationMs = 50) => Tap(VirtualKeyCodes.VK_W, durationMs);

    public void PressA() => Press(VirtualKeyCodes.VK_A);
    public void ReleaseA() => Release(VirtualKeyCodes.VK_A);
    public void TapA(int durationMs = 50) => Tap(VirtualKeyCodes.VK_A, durationMs);

    public void PressS() => Press(VirtualKeyCodes.VK_S);
    public void ReleaseS() => Release(VirtualKeyCodes.VK_S);
    public void TapS(int durationMs = 50) => Tap(VirtualKeyCodes.VK_S, durationMs);

    public void PressD() => Press(VirtualKeyCodes.VK_D);
    public void ReleaseD() => Release(VirtualKeyCodes.VK_D);
    public void TapD(int durationMs = 50) => Tap(VirtualKeyCodes.VK_D, durationMs);

    #endregion
}
