using System.Drawing;

namespace InfiMouse.Core;

/// <summary>
/// 控制点自动生成器：根据起点、终点自动计算合理的贝塞尔控制点，
/// 在起点-终点连线的垂直方向偏移以产生自然的弧度。
/// </summary>
public static class ControlPointGenerator
{
    private static readonly Random _rng = new();

    /// <summary>
    /// 自动生成两个控制点，使路径呈现自然弧线。
    /// </summary>
    /// <param name="start">起点</param>
    /// <param name="end">终点</param>
    /// <param name="curvatureStrength">曲率强度（0~1），0为直线，1为最强弯曲</param>
    /// <returns>(控制点1, 控制点2)</returns>
    public static (PointF p1, PointF p2) Generate(PointF start, PointF end, float curvatureStrength = 0.3f)
    {
        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);

        if (distance < 1f)
            return (start, end);

        // 垂直方向单位向量
        float perpX = -dy / distance;
        float perpY = dx / distance;

        // 随机偏移方向（左或右）
        float sign = _rng.Next(2) == 0 ? 1f : -1f;
        float offset = distance * Math.Clamp(curvatureStrength, 0f, 1f) * sign;

        float p1x = start.X + dx * 0.33f + perpX * offset;
        float p1y = start.Y + dy * 0.33f + perpY * offset;
        float p2x = start.X + dx * 0.66f + perpX * offset;
        float p2y = start.Y + dy * 0.66f + perpY * offset;

        return (new PointF(p1x, p1y), new PointF(p2x, p2y));
    }
}
