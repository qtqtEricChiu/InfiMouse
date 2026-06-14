using System.Drawing;
using InfiMouse.Core;
using InfiMouse.Model;

namespace InfiMouse.AntiDetection;

/// <summary>
/// 拟人化处理器：对鼠标路径、时间戳施加随机扰动，模拟人类操作的不精确性。
/// </summary>
public class Humanizer
{
    private readonly Random _rng = new();
    private readonly AntiDetectionConfig _config;

    public Humanizer(AntiDetectionConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 对控制点施加随机偏移。
    /// </summary>
    public (PointF p1, PointF p2) RandomizeControlPoints(PointF baseP1, PointF baseP2)
    {
        double strength = _config.JitterStrength * 5; // 控制点偏移幅度大于轨迹抖动
        return (
            new PointF(
                baseP1.X + (float)(GaussianNoise() * strength),
                baseP1.Y + (float)(GaussianNoise() * strength)
            ),
            new PointF(
                baseP2.X + (float)(GaussianNoise() * strength),
                baseP2.Y + (float)(GaussianNoise() * strength)
            )
        );
    }

    /// <summary>
    /// 对每帧位置施加高频低幅抖动（模拟手颤）。
    /// </summary>
    public List<MotionFrame> JitterFramePositions(List<MotionFrame> frames)
    {
        var result = new List<MotionFrame>(frames.Count);
        foreach (var frame in frames)
        {
            double jx = GaussianNoise() * _config.JitterStrength;
            double jy = GaussianNoise() * _config.JitterStrength;
            var newPos = new PointF(
                frame.Position.X + (float)jx,
                frame.Position.Y + (float)jy
            );
            result.Add(new MotionFrame(newPos, frame.Timestamp));
        }
        return result;
    }

    /// <summary>
    /// 对时间戳施加随机抖动（速度波动）。
    /// </summary>
    public List<MotionFrame> JitterTimestamps(List<MotionFrame> frames)
    {
        if (frames.Count < 3) return frames;

        var result = new List<MotionFrame>(frames.Count);

        // 保持首尾帧时间不变
        result.Add(frames[0]);

        for (int i = 1; i < frames.Count - 1; i++)
        {
            double variance = _config.SpeedVariancePercent * frames[i].Timestamp.TotalMilliseconds;
            double jitter = GaussianNoise() * variance;
            var newTimestamp = TimeSpan.FromMilliseconds(
                Math.Max(0, frames[i].Timestamp.TotalMilliseconds + jitter)
            );
            result.Add(new MotionFrame(frames[i].Position, newTimestamp));
        }

        result.Add(frames[^1]);
        return result;
    }

    /// <summary>
    /// 在事件时间轴中随机插入停顿（模拟操作者思考时间）。
    /// </summary>
    public List<TimedEvent> InsertRandomPauses(List<TimedEvent> timeline)
    {
        if (_config.PauseProbability <= 0) return timeline;

        var result = new List<TimedEvent>();
        var rng = new Random();

        foreach (var evt in timeline)
        {
            result.Add(evt);

            // 每个事件后有概率插入停顿
            if (rng.NextDouble() < _config.PauseProbability)
            {
                int pauseMs = rng.Next((int)_config.PauseMinMs, (int)_config.PauseMaxMs);
                // 在事件之后插入延迟
                result.Add(new TimedEvent
                {
                    TimestampMs = evt.TimestampMs + 1, // 紧跟在后
                    Type = TimedEventType.Delay,
                    Data = new { DurationMs = pauseMs }
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 生成过冲路径：先越过终点再折返。
    /// </summary>
    /// <param name="mainPath">主路径贝塞尔曲线</param>
    /// <returns>包含过冲段的新曲线（过冲 + 回正）</returns>
    public BezierCurve3 GenerateOvershootPath(BezierCurve3 mainPath)
    {
        if (!_config.EnableOvershoot) return mainPath;

        PointF end = mainPath.P3;

        // 计算终点处的切线方向（近似）
        PointF nearEnd = mainPath.Evaluate(0.95f);
        float dx = end.X - nearEnd.X;
        float dy = end.Y - nearEnd.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.1f) return mainPath;

        float dirX = dx / len;
        float dirY = dy / len;

        float overshoot = (float)_config.OvershootPixels;

        // 过冲点（越过终点）
        var overshootPoint = new PointF(
            end.X + dirX * overshoot,
            end.Y + dirY * overshoot
        );

        // 简单的三段构造：主路径终点 → 过冲点 → 回正终点
        // 这里返回一个新的贝塞尔曲线（简化：直接在主路径后追加线段）
        // 更完善的实现：构建新的 BezierCurve3(end → overshoot → end)
        var c1 = new PointF(end.X + dirX * overshoot * 0.33f, end.Y + dirY * overshoot * 0.33f);
        var c2 = new PointF(end.X + dirX * overshoot * 0.66f, end.Y + dirY * overshoot * 0.66f);

        return new BezierCurve3(end, c1, c2, end);
    }

    /// <summary>
    /// 高斯分布随机数（Box-Muller 变换近似）。
    /// </summary>
    private double GaussianNoise()
    {
        double u1 = 1.0 - _rng.NextDouble();
        double u2 = 1.0 - _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
