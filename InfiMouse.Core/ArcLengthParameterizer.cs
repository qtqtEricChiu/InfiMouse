using System.Drawing;

namespace InfiMouse.Core;

/// <summary>
/// 弧长参数化器：通过对贝塞尔曲线高密度采样，建立弧长→参数t的查找表，
/// 使得按弧长等分时路径点间距均匀，避免等分t造成的速度不均问题。
/// </summary>
public class ArcLengthParameterizer
{
    private readonly BezierCurve3 _curve;
    private readonly float[] _arcLengthTable;  // 累积弧长表，_arcLengthTable[i] 对应 t=i/N 时的累积弧长
    private readonly float _totalLength;
    private readonly int _sampleCount;

    public ArcLengthParameterizer(BezierCurve3 curve, int sampleCount = 1000)
    {
        _curve = curve;
        _sampleCount = sampleCount;
        _arcLengthTable = new float[sampleCount + 1];

        // 计算每个采样点处的累积弧长
        _arcLengthTable[0] = 0;
        PointF prev = curve.P0;
        for (int i = 1; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            PointF curr = curve.Evaluate(t);
            float dx = curr.X - prev.X;
            float dy = curr.Y - prev.Y;
            float segmentLength = MathF.Sqrt(dx * dx + dy * dy);
            _arcLengthTable[i] = _arcLengthTable[i - 1] + segmentLength;
            prev = curr;
        }
        _totalLength = _arcLengthTable[sampleCount];
    }

    /// <summary>
    /// 曲线总弧长。
    /// </summary>
    public float TotalLength => _totalLength;

    /// <summary>
    /// 根据弧长比例（0~1）在曲线上取点。通过二分查找表反查t值。
    /// </summary>
    public PointF GetPointAtArcLengthFraction(float fraction)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        float targetLength = fraction * _totalLength;

        // 二分查找
        int low = 0, high = _sampleCount;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (_arcLengthTable[mid] < targetLength)
                low = mid + 1;
            else
                high = mid;
        }

        // 在 [low-1, low] 区间内线性插值得到精确t
        int i0 = Math.Max(0, low - 1);
        int i1 = Math.Min(_sampleCount, low);
        float len0 = _arcLengthTable[i0];
        float len1 = _arcLengthTable[i1];
        float localFraction = len1 > len0 ? (targetLength - len0) / (len1 - len0) : 0f;

        float t = ((float)i0 + localFraction) / _sampleCount;
        return _curve.Evaluate(t);
    }

    /// <summary>
    /// 按弧长等分生成 numPoints 个均匀间距的路径点。
    /// </summary>
    public List<PointF> GenerateUniformPath(int numPoints)
    {
        var points = new List<PointF>(numPoints);
        for (int i = 0; i < numPoints; i++)
        {
            float fraction = numPoints == 1 ? 1f : (float)i / (numPoints - 1);
            points.Add(GetPointAtArcLengthFraction(fraction));
        }
        return points;
    }
}
