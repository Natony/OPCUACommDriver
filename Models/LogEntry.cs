using System.Windows.Media;

namespace OpcUaCommunicationEngine.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string Source { get; set; } = string.Empty;

    public string LevelColor => Level switch
    {
        "VRB" or "VERBOSE" => "#808080",
        "DBG" or "DEBUG" => "#0066CC",
        "INF" or "INFORMATION" => "#008000",
        "WRN" or "WARNING" => "#FF8C00",
        "ERR" or "ERROR" => "#FF0000",
        "FTL" or "FATAL" => "#8B0000",
        _ => "#000000"
    };

    public Brush LevelBrush => Level switch
    {
        "VRB" or "VERBOSE" => new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
        "DBG" or "DEBUG" => new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
        "INF" or "INFORMATION" => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
        "WRN" or "WARNING" => new SolidColorBrush(Color.FromRgb(0xF5, 0xC6, 0x1C)),
        "ERR" or "ERROR" => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        "FTL" or "FATAL" => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
        _ => new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB))
    };

    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");
    public string TimeText => TimestampText;
    public string LevelText => Level;

    public string DisplayText => Exception != null
        ? $"{TimestampText} [{Level}] {Message}\n{Exception}"
        : $"{TimestampText} [{Level}] {Message}";
}
