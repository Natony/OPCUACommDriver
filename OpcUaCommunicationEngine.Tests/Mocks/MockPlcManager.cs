using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.OpcUa;

namespace OpcUaCommunicationEngine.Tests.Mocks;

/// <summary>
/// Mock implementation of IPlcManager for testing
/// </summary>
public class MockPlcManager : IPlcManager
{
    private readonly List<MockPlcConnection> _connections = new();

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<TagValueChangedEventArgs>? TagValueChanged;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    public int PlcCount => _connections.Count;
    public int ConnectedCount => _connections.Count(c => c.IsConnected);
    public IReadOnlyList<IPlcConnection> Connections => _connections;

    public MockPlcManager()
    {
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IPlcConnection?> AddPlcAsync(PlcDevice device, CancellationToken cancellationToken = default)
    {
        var connection = new MockPlcConnection(device);
        _connections.Add(connection);
        return Task.FromResult<IPlcConnection?>(connection);
    }

    public Task<bool> RemovePlcAsync(string plcId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection != null)
        {
            _connections.Remove(connection);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public IPlcConnection? GetConnection(string plcId)
    {
        return _connections.FirstOrDefault(c => c.Device.Id == plcId);
    }

    public bool HasPlc(string plcId)
    {
        return _connections.Any(c => c.Device.Id == plcId);
    }

    public async Task<bool> ConnectAsync(string plcId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return false;

        return await connection.ConnectAsync(cancellationToken);
    }

    public async Task DisconnectAsync(string plcId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection != null)
            await connection.DisconnectAsync();
    }

    public async Task<int> ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        foreach (var connection in _connections)
        {
            if (await connection.ConnectAsync(cancellationToken))
                count++;
        }
        return count;
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var connection in _connections)
        {
            await connection.DisconnectAsync();
        }
    }

    public async Task<bool> ReconnectAsync(string plcId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return false;

        return await connection.ReconnectAsync(cancellationToken);
    }

    public async Task<int> ReconnectAllAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        foreach (var connection in _connections)
        {
            if (await connection.ReconnectAsync(cancellationToken))
                count++;
        }
        return count;
    }

    public async Task<TagValue?> ReadTagAsync(string plcId, string nodeId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return null;

        return await connection.ReadTagAsync(nodeId, cancellationToken);
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(string plcId, IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return new List<TagValue>();

        return await connection.ReadTagsAsync(nodeIds, cancellationToken);
    }

    public async Task<bool> WriteTagAsync(string plcId, string nodeId, object value, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return false;

        return await connection.WriteTagAsync(nodeId, value, cancellationToken);
    }

    public async Task<IReadOnlyList<bool>> WriteTagsAsync(string plcId, IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return items.Select(_ => false).ToList();

        return await connection.WriteTagsAsync(items, cancellationToken);
    }

    public Task<IDictionary<string, IReadOnlyList<TagValue>>> ReadAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, IReadOnlyList<TagValue>>();
        foreach (var connection in _connections)
        {
            var tags = connection.Device.Tags.Select(t => new TagValue
            {
                TagId = t.Id,
                NodeId = t.NodeId,
                Value = t.Value,
                Quality = t.Quality,
                SourceTimestamp = DateTime.UtcNow
            }).ToList();
            result[connection.Device.Id] = tags;
        }
        return Task.FromResult<IDictionary<string, IReadOnlyList<TagValue>>>(result);
    }

    public async Task<IReadOnlyList<BrowseNode>> BrowseAsync(string plcId, string? nodeId = null, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return new List<BrowseNode>();

        return await connection.BrowseAsync(nodeId, cancellationToken);
    }

    public async Task<NodeInfo?> GetNodeInfoAsync(string plcId, string nodeId, CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return null;

        return await connection.GetNodeInfoAsync(nodeId, cancellationToken);
    }

    public IReadOnlyList<PlcStatus> GetAllStatus()
    {
        return _connections.Select(c => new PlcStatus
        {
            PlcId = c.Device.Id,
            PlcName = c.Device.Name,
            State = c.ConnectionState,
            IsConnected = c.IsConnected
        }).ToList();
    }

    public PlcStatus? GetStatus(string plcId)
    {
        var connection = _connections.FirstOrDefault(c => c.Device.Id == plcId);
        if (connection == null)
            return null;

        return new PlcStatus
        {
            PlcId = connection.Device.Id,
            PlcName = connection.Device.Name,
            State = connection.ConnectionState,
            IsConnected = connection.IsConnected
        };
    }

    // Helper methods for testing
    public MockPlcConnection AddMockPlc(string id, string name, string endpointUrl)
    {
        var device = new PlcDevice
        {
            Id = id,
            Name = name,
            EndpointUrl = endpointUrl
        };
        var connection = new MockPlcConnection(device);
        _connections.Add(connection);
        return connection;
    }

    public void RaiseTagValueChanged(string plcId, string tagId, string nodeId, TagValue value)
    {
        TagValueChanged?.Invoke(this, new TagValueChangedEventArgs
        {
            PlcId = plcId,
            TagId = tagId,
            NodeId = nodeId,
            Value = value
        });
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Dispose();
        }
        _connections.Clear();
    }
}

/// <summary>
/// PLC Status for API
/// </summary>
public class PlcStatus
{
    public string PlcId { get; set; } = string.Empty;
    public string PlcName { get; set; } = string.Empty;
    public PlcConnectionState State { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? LastConnected { get; set; }
    public string? LastError { get; set; }
}
