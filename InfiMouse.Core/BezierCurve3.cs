using System.Drawing;

namespace InfiMouse.Core;

/// <summary>
/// 三阶贝塞尔曲线生成器。
/// B(t) = (1-t)³·P₀ + 3(1-t)²·t·P₁ + 3(1-t)·t²·P₂ + t³·P₃
/// </summary>
public class BezierCurve3
{
    public PointF P0 { get; }
    public PointF P1 { get; }
    public PointF P2 { get; }
    public PointF P3 { get; }

    public BezierCurve3(PointF p0, PointF p1, PointF p2, PointF p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
    }

    /// <summary>
    /// 计算贝塞尔曲线在参数 t (0~1) 处的位置。
    /// </summary>
    public PointF Evaluate(float t)
    {
        float u = 1 - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;

        float x = u3 * P0.X + 3 * u2 * t * P1.X + 3 * u * t2 * P2.X + t3 * P3.X;
        float y = u3 * P0.Y + 3 * u2 * t * P1.Y + 3 * u * t2 * P2.Y + t3 * P3.Y;

        return new PointF(x, y);
    }

    /// <summary>
    /// 等分参数 t 采样生成路径点序列。
    /// 注意：等分 t 在曲率变化大时速度不均，推荐使用 ArcLengthParameterizer。
    /// </summary>
    public List<PointF> GeneratePath(int numPoints)
    {
        var points = new List<PointF>(numPoints);
        for (int i = 0; i < numPoints; i++)
        {
            float t = numPoints == 1 ? 1f : (float)i / (numPoints - 1);
            points.Add(Evaluate(t));
        }
        return points;
    }

    /// <summary>
    /// 曲线近似总长度（通过高密度采样计算）。
    /// </summary>
    public float ApproximateLength(int sampleCount = 1000)
    {
        float length = 0;
        PointF prev = P0;
        for (int i = 1; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            PointF curr = Evaluate(t);
            float dx = curr.X - prev.X;
            float dy = curr.Y - prev.Y;
            length += MathF.Sqrt(dx * dx + dy * dy);
            prev = curr;
        }
        return length;
    }
}
