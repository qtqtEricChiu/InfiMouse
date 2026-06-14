namespace InfiMouse.Model;

/// <summary>
/// 缓动函数类型枚举。
/// </summary>
public enum EasingType
{
    /// <summary>线性（无缓动）</summary>
    Linear,
    /// <summary>Ease-Out 二次方：t*(2-t)</summary>
    EaseOutQuad,
    /// <summary>Ease-Out 三次方：1-(1-t)³</summary>
    EaseOutCubic,
    /// <summary>Ease-Out 正弦：sin(t·π/2)</summary>
    EaseOutSine,
    /// <summary>Ease-Out 圆形：√(1-(t-1)²)</summary>
    EaseOutCircular,
}
