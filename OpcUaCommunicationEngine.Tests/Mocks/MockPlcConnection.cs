using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Tests.Mocks;

/// <summary>
/// Mock implementation of IPlcConnection for testing
/// </summary>
public class MockPlcConnection : IPlcConnection
{
    private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;

    public PlcDevice Device { get; }
    public PlcConnectionState ConnectionState
    {
        get => _connectionState;
        set => _connectionState = value;
    }
    public bool IsConnected => ConnectionState == PlcConnectionState.Connected;
    public string? SessionId { get; set; }
    public DateTime? LastConnectedTime { get; set; }
    public DateTime? LastDisconnectedTime { get; set; }
    public string? LastError { get; set; }

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<TagValueChangedEventArgs>? TagValueChanged;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    // Mock data
    private readonly Dictionary<string, TagValue> _tagValues = new();
    private readonly List<BrowseNode> _browseNodes = new();

    public MockPlcConnection(PlcDevice device)
    {
        Device = device;
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connectionState = PlcConnectionState.Connected;
        LastConnectedTime = DateTime.UtcNow;
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            PlcId = Device.Id,
            PlcName = Device.Name,
            OldState = PlcConnectionState.Disconnected,
            NewState = PlcConnectionState.Connected
        });
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        _connectionState = PlcConnectionState.Disconnected;
        LastDisconnectedTime = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        return ConnectAsync(cancellationToken);
    }

    public Task<TagValue?> ReadTagAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (_tagValues.TryGetValue(nodeId, out var value))
            return Task.FromResult<TagValue?>(value);

        return Task.FromResult<TagValue?>(new TagValue
        {
            Value = 100,
            Quality = TagQuality.Good,
            SourceTimestamp = DateTime.UtcNow,
            ServerTimestamp = DateTime.UtcNow
        });
    }

    public Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var results = nodeIds.Select(nodeId =>
        {
            if (_tagValues.TryGetValue(nodeId, out var value))
                return value;

            return new TagValue
            {
                Value = 100,
                Quality = TagQuality.Good,
                SourceTimestamp = DateTime.UtcNow,
                ServerTimestamp = DateTime.UtcNow
            };
        }).ToList();

        return Task.FromResult<IReadOnlyList<TagValue>>(results);
    }

    public Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        _tagValues[nodeId] = new TagValue
        {
            Value = value,
            Quality = TagQuality.Good,
            SourceTimestamp = DateTime.UtcNow,
            ServerTimestamp = DateTime.UtcNow
        };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<bool>> WriteTagsAsync(IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            _tagValues[item.NodeId] = new TagValue
            {
                Value = item.Value,
                Quality = TagQuality.Good,
                SourceTimestamp = DateTime.UtcNow,
                ServerTimestamp = DateTime.UtcNow
            };
        }
        return Task.FromResult<IReadOnlyList<bool>>(items.Select(_ => true).ToList());
    }

    public Task<bool> CreateSubscriptionAsync(SubscriptionGroup group, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> RemoveSubscriptionAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> AddTagToSubscriptionAsync(string groupId, TagItem tag, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> RemoveTagFromSubscriptionAsync(string groupId, string tagId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<BrowseNode>> BrowseAsync(string? nodeId = null, CancellationToken cancellationToken = default)
    {
        if (_browseNodes.Count > 0)
            return Task.FromResult<IReadOnlyList<BrowseNode>>(_browseNodes);

        return Task.FromResult<IReadOnlyList<BrowseNode>>(new List<BrowseNode>
        {
            new BrowseNode
            {
                NodeId = "ns=4;i=1",
                DisplayName = "TestNode1",
                NodeClass = "Variable",
                DataType = "Boolean",
                HasChildren = false
            },
            new BrowseNode
            {
                NodeId = "ns=4;i=2",
                DisplayName = "TestNode2",
                NodeClass = "Variable",
                DataType = "Int32",
                HasChildren = false
            }
        });
    }

    public Task<NodeInfo?> GetNodeInfoAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<NodeInfo?>(new NodeInfo
        {
            NodeId = nodeId,
            DisplayName = "TestNode",
            NodeClass = "Variable",
            DataType = "Int32",
            IsReadable = true,
            IsWritable = true
        });
    }

    // Helper methods for testing
    public void SetTagValue(string nodeId, object value)
    {
        _tagValues[nodeId] = new TagValue
        {
            Value = value,
            Quality = TagQuality.Good,
            SourceTimestamp = DateTime.UtcNow,
            ServerTimestamp = DateTime.UtcNow
        };
    }

    public void AddBrowseNode(BrowseNode node)
    {
        _browseNodes.Add(node);
    }

    public void RaiseTagValueChanged(string tagId, string nodeId, TagValue value)
    {
        TagValueChanged?.Invoke(this, new TagValueChangedEventArgs
        {
            PlcId = Device.Id,
            TagId = tagId,
            NodeId = nodeId,
            Value = value
        });
    }

    public void Dispose()
    {
        // Cleanup
    }
}
