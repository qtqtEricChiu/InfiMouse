using System.Drawing;
using InfiMouse.Model;

namespace InfiMouse.Core;

/// <summary>
/// 运动规划器：将贝塞尔曲线路径 + 缓动函数 + 时长组合为带时间戳的 MotionFrame 序列。
/// </summary>
public class MotionPlanner
{
    /// <summary>
    /// 根据路径、缓动类型、总时长和帧数生成带时间戳的运动帧序列。
    /// </summary>
    /// <param name="path">贝塞尔曲线</param>
    /// <param name="easing">缓动函数类型</param>
    /// <param name="totalDuration">总时长</param>
    /// <param name="frameCount">生成的帧数（含起点和终点）</param>
    /// <returns>MotionFrame 序列，按时间升序排列</returns>
    public List<MotionFrame> PlanMotion(BezierCurve3 path, EasingType easing, TimeSpan totalDuration, int frameCount)
    {
        if (frameCount < 2)
            frameCount = 2;

        var parameterizer = new ArcLengthParameterizer(path);
        var frames = new List<MotionFrame>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            // 均匀分布的时间进度 t_raw
            double tRaw = (double)i / (frameCount - 1);

            // 应用缓动函数
            double tEased = EasingFunction.Ease(easing, tRaw);

            // 通过弧长参数化获取路径位置
            PointF position = parameterizer.GetPointAtArcLengthFraction((float)tEased);

            // 计算该帧时间戳
            TimeSpan timestamp = totalDuration * tRaw;

            frames.Add(new MotionFrame(position, timestamp));
        }

        return frames;
    }

    /// <summary>
    /// 使用预构建的弧长参数化器规划运动（避免重复计算弧长表）。
    /// </summary>
    public List<MotionFrame> PlanMotion(ArcLengthParameterizer parameterizer, EasingType easing, TimeSpan totalDuration, int frameCount)
    {
        if (frameCount < 2)
            frameCount = 2;

        var frames = new List<MotionFrame>(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            double tRaw = (double)i / (frameCount - 1);
            double tEased = EasingFunction.Ease(easing, tRaw);
            PointF position = parameterizer.GetPointAtArcLengthFraction((float)tEased);
            TimeSpan timestamp = totalDuration * tRaw;
            frames.Add(new MotionFrame(position, timestamp));
        }

        return frames;
    }
}
