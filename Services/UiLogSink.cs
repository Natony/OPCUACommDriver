using System.Collections.ObjectModel;
using OpcUaCommunicationEngine.Models;
using Serilog.Core;
using Serilog.Events;

namespace OpcUaCommunicationEngine.Services;

/// <summary>
/// Serilog sink để hiển thị log trên UI
/// </summary>
public class UiLogSink : ILogEventSink
{
    private readonly ObservableCollection<LogEntry> _logEntries;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly int _maxEntries;

    public UiLogSink(ObservableCollection<LogEntry> logEntries, int maxEntries = 1000)
    {
        _logEntries = logEntries;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _maxEntries = maxEntries;
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp.LocalDateTime,
            Level = GetLevelShortName(logEvent.Level),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString()
        };

        // Dispatch to UI thread
        _dispatcher.InvokeAsync(() =>
        {
            _logEntries.Add(entry);

            // Keep only last N entries to avoid memory issues
            while (_logEntries.Count > _maxEntries)
            {
                _logEntries.RemoveAt(0);
            }
        });
    }

    private static string GetLevelShortName(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "???"
        };
    }
}

/// <summary>
/// Extension method để dễ dàng thêm UiLogSink vào Serilog configuration
/// </summary>
public static class UiLogSinkExtensions
{
    public static Serilog.LoggerConfiguration UiSink(
        this Serilog.Configuration.LoggerSinkConfiguration sinkConfig,
        ObservableCollection<LogEntry> logEntries,
        int maxEntries = 1000)
    {
        return sinkConfig.Sink(new UiLogSink(logEntries, maxEntries));
    }
}
