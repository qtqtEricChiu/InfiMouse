namespace InfiMouse.Model;

/// <summary>
/// 路径点模型：物理像素坐标。
/// </summary>
public class PathPoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public PathPoint() { }

    public PathPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X:F1}, {Y:F1})";
}
