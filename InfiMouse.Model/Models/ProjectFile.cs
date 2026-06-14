namespace InfiMouse.Model;

/// <summary>
/// 项目文件模型：聚合路径点、配置、反检测参数和事件列表。
/// 支持 .infimouse 格式的保存和加载。
/// </summary>
public class ProjectFile
{
    /// <summary>文件格式版本号。</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最后修改时间。</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>路径控制点列表（含起点、终点、控制点 P1/P2）。</summary>
    public List<PathPoint> PathPoints { get; set; } = new();

    /// <summary>贝塞尔曲线控制点（P1, P2）。</summary>
    public ControlPoint? ControlPoint1 { get; set; }
    public ControlPoint? ControlPoint2 { get; set; }

    /// <summary>动画参数配置。</summary>
    public AnimationConfig Animation { get; set; } = new();

    /// <summary>反检测 / 拟人化参数。</summary>
    public AntiDetectionConfig AntiDetection { get; set; } = new();

    /// <summary>时间轴事件列表。</summary>
    public List<TimedEvent> Events { get; set; } = new();

    /// <summary>项目名称（用于窗口标题）。</summary>
    public string ProjectName { get; set; } = "未命名项目";
}
