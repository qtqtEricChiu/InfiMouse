namespace InfiMouse.Model;

/// <summary>
/// 反检测 / 拟人化参数配置。
/// </summary>
public class AntiDetectionConfig
{
    /// <summary>轨迹抖动强度（像素）。默认关闭。</summary>
    public double JitterStrength { get; set; } = 0;

    /// <summary>速度波动百分比（0~1，如 0.05 = ±5%）。默认关闭。</summary>
    public double SpeedVariancePercent { get; set; } = 0;

    /// <summary>是否启用过冲效果。</summary>
    public bool EnableOvershoot { get; set; } = false;

    /// <summary>过冲距离（像素）。</summary>
    public double OvershootPixels { get; set; } = 20;

    /// <summary>随机停顿概率（每段路径）。</summary>
    public double PauseProbability { get; set; } = 0.3;

    /// <summary>随机停顿最小毫秒。</summary>
    public double PauseMinMs { get; set; } = 200;

    /// <summary>随机停顿最大毫秒。</summary>
    public double PauseMaxMs { get; set; } = 800;
}
