using InfiMouse.Core;
using InfiMouse.Input.Native;

namespace InfiMouse.Input;

/// <summary>
/// 鼠标注入器：将 MotionFrame 序列通过 SendInput 注入到系统。
/// </summary>
public class MouseInjector : IMouseInjector
{
    public MouseInjector()
    {
    }

    /// <summary>
    /// 注入绝对坐标鼠标移动（输入物理像素坐标，内部转换为 0-65535 归一化坐标）。
    /// </summary>
    public void InjectAbsolute(float pixelX, float pixelY)
    {
        var (nx, ny) = CoordinateConverter.ScreenToNormalized((int)Math.Round(pixelX), (int)Math.Round(pixelY));
        SendInputApi.SendMouseMoveAbsolute(nx, ny);
    }

    /// <summary>
    /// 注入相对坐标鼠标移动（像素偏移）。
    /// </summary>
    public void InjectRelative(int dx, int dy)
    {
        SendInputApi.SendMouseMoveRelative(dx, dy);
    }

    /// <summary>
    /// 注入整条路径帧序列（逐帧发送并等待）。
    /// </summary>
    /// <param name="frames">MotionFrame 序列（已含时间戳）</param>
    /// <param name="ct">取消令牌</param>
    public async Task InjectPathAsync(List<MotionFrame> frames, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var frame in frames)
        {
            ct.ThrowIfCancellationRequested();

            long elapsedMs = sw.ElapsedMilliseconds;
            long targetMs = (long)frame.Timestamp.TotalMilliseconds;
            long waitMs = targetMs - elapsedMs;

            if (waitMs > 0)
            {
                if (waitMs > 1)
                    await Task.Delay((int)Math.Min(waitMs, 100), ct);
                else
                    System.Threading.SpinWait.SpinUntil(() => sw.ElapsedMilliseconds >= targetMs);
            }

            InjectAbsolute(frame.Position.X, frame.Position.Y);
        }
    }

    /// <summary>鼠标左键单击。</summary>
    public void LeftClick()
    {
        SendInputApi.SendMouseLeftDown();
        Thread.Sleep(10);
        SendInputApi.SendMouseLeftUp();
    }

    /// <summary>鼠标左键按下/释放。</summary>
    public void InjectLeftButton(bool down)
    {
        if (down)
            SendInputApi.SendMouseLeftDown();
        else
            SendInputApi.SendMouseLeftUp();
    }

    /// <summary>鼠标右键单击。</summary>
    public void RightClick()
    {
        var rightDown = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUT_UNION
            {
                mi = new Win32.MOUSEINPUT { dwFlags = Win32.MOUSEEVENTF_RIGHTDOWN }
            }
        };
        var rightUp = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUT_UNION
            {
                mi = new Win32.MOUSEINPUT { dwFlags = Win32.MOUSEEVENTF_RIGHTUP }
            }
        };
        SendInputApi.Send(new[] { rightDown });
        Thread.Sleep(10);
        SendInputApi.Send(new[] { rightUp });
    }
}
