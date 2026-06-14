namespace InfiMouse.Model;

/// <summary>
/// 动画参数配置：总时长、缓动类型、帧数等。
/// </summary>
public class AnimationConfig
{
    /// <summary>动画总时长。</summary>
    public TimeSpan TotalDuration { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>缓动函数类型。</summary>
    public EasingType Easing { get; set; } = EasingType.EaseOutCubic;

    /// <summary>生成的帧数（含起点和终点）。默认值在 App 启动时由屏幕刷新率设置。</summary>
    public int FrameCount { get; set; } = 60;

    /// <summary>是否启用随机化（反检测）。默认关闭。</summary>
    public bool EnableRandomization { get; set; } = false;
}
