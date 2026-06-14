using InfiMouse.Model;

namespace InfiMouse.Core;

/// <summary>
/// 缓动函数库。提供多种 Ease-Out 缓动函数，统一接口 Ease(type, t)。
/// 输入 t∈[0,1]，输出 y∈[0,1]，保证 t=0 时 y=0，t=1 时 y=1。
/// </summary>
public static class EasingFunction
{
    public static double Ease(EasingType type, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return type switch
        {
            EasingType.Linear => t,
            EasingType.EaseOutQuad => EaseOutQuad(t),
            EasingType.EaseOutCubic => EaseOutCubic(t),
            EasingType.EaseOutSine => EaseOutSine(t),
            EasingType.EaseOutCircular => EaseOutCircular(t),
            _ => t,
        };
    }

    /// <summary>Ease-Out Quadratic: t*(2-t)</summary>
    public static double EaseOutQuad(double t) => t * (2.0 - t);

    /// <summary>Ease-Out Cubic: 1-(1-t)³</summary>
    public static double EaseOutCubic(double t) => 1.0 - Math.Pow(1.0 - t, 3);

    /// <summary>Ease-Out Sine: sin(t·π/2)</summary>
    public static double EaseOutSine(double t) => Math.Sin(t * Math.PI / 2.0);

    /// <summary>Ease-Out Circular: √(1-(t-1)²)</summary>
    public static double EaseOutCircular(double t) => Math.Sqrt(1.0 - Math.Pow(t - 1.0, 2));
}
