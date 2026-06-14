using System.Runtime.InteropServices;

namespace InfiMouse.Core;

/// <summary>
/// 坐标转换器：物理像素 ↔ SendInput 归一化坐标（0-65535）。
/// WinUI 3 使用有效像素（逻辑坐标），GetSystemMetrics 返回物理像素。
/// 本转换器在其中架起桥梁，确保 SendInput 的绝对坐标正确注入。
/// </summary>
public static class CoordinateConverter
{
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    /// <summary>获取虚拟屏幕宽度（物理像素）。</summary>
    public static int VirtualScreenWidth => GetSystemMetrics(SM_CXVIRTUALSCREEN);

    /// <summary>获取虚拟屏幕高度（物理像素）。</summary>
    public static int VirtualScreenHeight => GetSystemMetrics(SM_CYVIRTUALSCREEN);

    /// <summary>
    /// 将 WinUI 3 有效像素坐标转换为 SendInput 绝对坐标（0-65535）。
    /// 注意：WinUI 3 的 XAML 坐标是 DPI 缩放后的有效像素，与 GetSystemMetrics 返回的物理像素一致。
    /// （PerMonitorV2 下 WinUI 3 自动处理 DPI 缩放，XAML 坐标即物理像素）
    /// </summary>
    public static (int x, int y) ScreenToNormalized(int pixelX, int pixelY)
    {
        int screenW = VirtualScreenWidth;
        int screenH = VirtualScreenHeight;

        int nx = (int)((long)pixelX * 65536 / screenW);
        int ny = (int)((long)pixelY * 65536 / screenH);

        return (Math.Clamp(nx, 0, 65535), Math.Clamp(ny, 0, 65535));
    }

    /// <summary>将 SendInput 绝对坐标（0-65535）转换回物理像素坐标。</summary>
    public static (int x, int y) NormalizedToScreen(int nx, int ny)
    {
        int screenW = VirtualScreenWidth;
        int screenH = VirtualScreenHeight;

        int pixelX = (int)((long)nx * screenW / 65536);
        int pixelY = (int)((long)ny * screenH / 65536);

        return (pixelX, pixelY);
    }

    /// <summary>批量将 PointF 物理像素坐标转换为归一化坐标。</summary>
    public static List<(int nx, int ny)> ScreenToNormalized(IEnumerable<System.Drawing.PointF> points)
    {
        var result = new List<(int, int)>();
        foreach (var p in points)
        {
            result.Add(ScreenToNormalized((int)Math.Round(p.X), (int)Math.Round(p.Y)));
        }
        return result;
    }
}
