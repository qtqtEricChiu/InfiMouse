namespace InfiMouse.Model;

/// <summary>
/// 贝塞尔曲线控制点，继承自 PathPoint 以标记语义。
/// </summary>
public class ControlPoint : PathPoint
{
    /// <summary>该控制点属于哪条曲线（P1 或 P2）。</summary>
    public ControlPointRole Role { get; set; }

    public ControlPoint() { }

    public ControlPoint(double x, double y, ControlPointRole role = ControlPointRole.P1)
        : base(x, y)
    {
        Role = role;
    }
}

public enum ControlPointRole
{
    P1,
    P2,
}
