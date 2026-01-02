using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Interfaces;

/// <summary>
/// Interface cho kết nối đến 1 PLC qua OPC UA
/// </summary>
public interface IPlcConnection : IDisposable
{
    #region Properties

    PlcDevice Device { get; }
    PlcConnectionState ConnectionState { get; }
    bool IsConnected { get; }
    string? SessionId { get; }
    DateTime? LastConnectedTime { get; }
    DateTime? LastDisconnectedTime { get; }
    string? LastError { get; }

    #endregion

    #region Events

    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<TagValueChangedEventArgs>? TagValueChanged;
    event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    #endregion

    #region Connection Methods

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<bool> ReconnectAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Read/Write Methods

    Task<TagValue?> ReadTagAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);
    Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<bool>> WriteTagsAsync(IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default);

    #endregion

    #region Subscription Methods

    Task<bool> CreateSubscriptionAsync(SubscriptionGroup group, CancellationToken cancellationToken = default);
    Task<bool> RemoveSubscriptionAsync(string groupId, CancellationToken cancellationToken = default);
    Task<bool> AddTagToSubscriptionAsync(string groupId, TagItem tag, CancellationToken cancellationToken = default);
    Task<bool> RemoveTagFromSubscriptionAsync(string groupId, string tagId, CancellationToken cancellationToken = default);

    #endregion

    #region Browse Methods

    Task<IReadOnlyList<BrowseNode>> BrowseAsync(string? nodeId = null, CancellationToken cancellationToken = default);
    Task<NodeInfo?> GetNodeInfoAsync(string nodeId, CancellationToken cancellationToken = default);

    #endregion
}

#region Event Args

public class ConnectionStateChangedEventArgs : EventArgs
{
    public string PlcId { get; init; } = string.Empty;
    public string PlcName { get; init; } = string.Empty;
    public PlcConnectionState OldState { get; init; }
    public PlcConnectionState NewState { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string? Message { get; init; }
}

public class TagValueChangedEventArgs : EventArgs
{
    public string PlcId { get; init; } = string.Empty;
    public string TagId { get; init; } = string.Empty;
    public string NodeId { get; init; } = string.Empty;
    public TagValue Value { get; init; } = null!;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public class PlcErrorEventArgs : EventArgs
{
    public string PlcId { get; init; } = string.Empty;
    public string PlcName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool IsCritical { get; init; }
}

#endregion

#region Browse Models - Using Records for 'with' expression support

/// <summary>
/// Node khi browse OPC UA server
/// </summary>
public record BrowseNode
{
    public string NodeId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BrowseName { get; init; } = string.Empty;
    public string NodeClass { get; init; } = string.Empty;
    public string? DataType { get; init; }
    public bool HasChildren { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Thông tin chi tiết của 1 node
/// </summary>
public record NodeInfo
{
    public string NodeId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string BrowseName { get; init; } = string.Empty;
    public string NodeClass { get; init; } = string.Empty;
    public string? DataType { get; init; }
    public string? Description { get; init; }
    public bool IsReadable { get; init; }
    public bool IsWritable { get; init; }
    public object? CurrentValue { get; init; }
    public int? ArrayDimensions { get; init; }
    public string? EngineeringUnits { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
}

#endregion
