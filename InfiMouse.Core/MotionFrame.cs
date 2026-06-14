using System.Drawing;

namespace InfiMouse.Core;

/// <summary>
/// 单帧运动数据：包含目标位置和到达时间戳。
/// </summary>
public readonly struct MotionFrame
{
    /// <summary>目标位置（物理像素坐标）。</summary>
    public PointF Position { get; }

    /// <summary>从播放开始到该帧的累计时间。</summary>
    public TimeSpan Timestamp { get; }

    public MotionFrame(PointF position, TimeSpan timestamp)
    {
        Position = position;
        Timestamp = timestamp;
    }

    public override string ToString() => $"Pos=({Position.X:F1}, {Position.Y:F1}) @{Timestamp.TotalMilliseconds:F0}ms";
}
