using System.Text.Json;

namespace InfiMouse.Model;

/// <summary>
/// 项目文件序列化器：使用 System.Text.Json 实现 .infimouse 格式的保存与加载。
/// </summary>
public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>默认文件扩展名。</summary>
    public const string FileExtension = ".infimouse";

    /// <summary>文件过滤器（用于 FileSavePicker / FileOpenPicker）。</summary>
    public const string FileFilter = "InfiMouse 项目文件|*.infimouse";

    /// <summary>
    /// 保存项目文件到指定路径。
    /// </summary>
    public static async Task SaveAsync(ProjectFile project, string filePath)
    {
        project.ModifiedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(project, _options);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// 从指定路径加载项目文件。
    /// </summary>
    public static async Task<ProjectFile?> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ProjectFile>(json, _options);
    }

    /// <summary>
    /// 同步保存项目文件。
    /// </summary>
    public static void Save(ProjectFile project, string filePath)
    {
        project.ModifiedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(project, _options);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 同步加载项目文件。
    /// </summary>
    public static ProjectFile? Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ProjectFile>(json, _options);
    }
}
