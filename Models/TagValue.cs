using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho giá trị của một Tag tại một thời điểm
/// Dùng cho Data Cache và lịch sử giá trị
/// </summary>
public class TagValue
{
    /// <summary>
    /// ID của Tag
    /// </summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>
    /// Node ID trong OPC UA
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Giá trị
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Chất lượng dữ liệu
    /// </summary>
    public TagQuality Quality { get; set; }

    /// <summary>
    /// Timestamp từ client
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Timestamp từ source (PLC)
    /// </summary>
    public DateTime SourceTimestamp { get; set; }

    /// <summary>
    /// Timestamp từ server
    /// </summary>
    public DateTime? ServerTimestamp { get; set; }

    /// <summary>
    /// Status code từ OPC UA
    /// </summary>
    public uint StatusCode { get; set; }

    /// <summary>
    /// Tạo TagValue mới
    /// </summary>
    public static TagValue Create(string tagId, object? value, TagQuality quality)
    {
        return new TagValue
        {
            TagId = tagId,
            Value = value,
            Quality = quality,
            Timestamp = DateTime.Now,
            SourceTimestamp = DateTime.Now
        };
    }

    /// <summary>
    /// Tạo TagValue với quality Bad
    /// </summary>
    public static TagValue CreateBad(string tagId, string error)
    {
        return new TagValue
        {
            TagId = tagId,
            Value = null,
            Quality = TagQuality.Bad,
            Timestamp = DateTime.Now,
            SourceTimestamp = DateTime.Now
        };
    }

    public override string ToString()
    {
        return $"[{TagId}] {Value} ({Quality}) @ {Timestamp:HH:mm:ss.fff}";
    }
}

// NOTE: TagValueChangedEventArgs đã được move sang Interfaces/IPlcConnection.cs
// để tránh duplicate
