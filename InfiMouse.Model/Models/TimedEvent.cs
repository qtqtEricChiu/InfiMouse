using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiMouse.Model;

/// <summary>
/// 时间轴事件：时间戳 + 事件类型 + 数据载荷。
/// 使用多态子类处理不同类型的数据。
/// </summary>
public class TimedEvent
{
    /// <summary>事件触发时间戳（毫秒，从 0 开始）。</summary>
    public int TimestampMs { get; set; }

    /// <summary>事件类型。</summary>
    public TimedEventType Type { get; set; }

    /// <summary>事件数据载荷（多态：MouseMoveData / KeyEventData / GamepadEventData）。</summary>
    [JsonConverter(typeof(TimedEventDataConverter))]
    public object? Data { get; set; }

    public override string ToString() => $"[{TimestampMs}ms] {Type}";
}

/// <summary>鼠标移动事件数据。</summary>
public class MouseMoveData
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>键盘事件数据。</summary>
public class KeyEventData
{
    /// <summary>虚拟键码。</summary>
    public ushort VkCode { get; set; }

    /// <summary>true=按下，false=释放。</summary>
    public bool IsPress { get; set; }

    /// <summary>持续时间（毫秒），仅 Tap 操作有效。</summary>
    public int DurationMs { get; set; }
}

/// <summary>手柄事件数据。</summary>
public class GamepadEventData
{
    /// <summary>按键位掩码（XINPUT_GAMEPAD_*）。</summary>
    public ushort ButtonFlags { get; set; }

    /// <summary>左摇杆 X（-32768 ~ 32767）。</summary>
    public short LeftThumbX { get; set; }

    /// <summary>左摇杆 Y（-32768 ~ 32767）。</summary>
    public short LeftThumbY { get; set; }

    /// <summary>右摇杆 X（-32768 ~ 32767）。</summary>
    public short RightThumbX { get; set; }

    /// <summary>右摇杆 Y（-32768 ~ 32767）。</summary>
    public short RightThumbY { get; set; }

    /// <summary>左扳机（0 ~ 255）。</summary>
    public byte LeftTrigger { get; set; }

    /// <summary>右扳机（0 ~ 255）。</summary>
    public byte RightTrigger { get; set; }
}

/// <summary>
/// TimedEvent.Data 多态序列化转换器。
/// 根据 TimedEvent.Type 确定 Data 的具体类型。
/// </summary>
public class TimedEventDataConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var element = doc.RootElement;

        // 读取父级 Type（在序列化时通过写 TypeDiscriminator 实现）
        // 简化方案：尝试按属性名判断类型
        if (element.TryGetProperty("VkCode", out _) || element.TryGetProperty("IsPress", out _))
            return JsonSerializer.Deserialize<KeyEventData>(element.GetRawText(), options);
        if (element.TryGetProperty("ButtonFlags", out _) || element.TryGetProperty("LeftThumbX", out _))
            return JsonSerializer.Deserialize<GamepadEventData>(element.GetRawText(), options);
        if (element.TryGetProperty("X", out _) && element.TryGetProperty("Y", out _))
            return JsonSerializer.Deserialize<MouseMoveData>(element.GetRawText(), options);

        return null;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
