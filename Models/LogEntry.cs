namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho một log entry hiển thị trên UI
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }

    /// <summary>
    /// Màu hiển thị theo log level
    /// </summary>
    public string LevelColor => Level switch
    {
        "VRB" or "VERBOSE" => "#808080",  // Gray
        "DBG" or "DEBUG" => "#0066CC",     // Blue
        "INF" or "INFORMATION" => "#008000", // Green
        "WRN" or "WARNING" => "#FF8C00",   // Orange
        "ERR" or "ERROR" => "#FF0000",     // Red
        "FTL" or "FATAL" => "#8B0000",     // Dark Red
        _ => "#000000"
    };

    /// <summary>
    /// Format timestamp for display
    /// </summary>
    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    /// <summary>
    /// Full display text
    /// </summary>
    public string DisplayText => Exception != null
        ? $"{TimestampText} [{Level}] {Message}\n{Exception}"
        : $"{TimestampText} [{Level}] {Message}";
}
