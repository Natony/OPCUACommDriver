using System.Collections.Concurrent;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.OpcUa;

/// <summary>
/// Manager quản lý tất cả các PLC connections
/// Implement IPlcManager interface
/// </summary>
public class PlcManager : IPlcManager
{
    private readonly IConfigurationService _configService;
    private readonly IDataCache _dataCache;
    private readonly ConcurrentDictionary<string, IPlcConnection> _connections = new();
    private bool _disposed;

    // Use Log.Logger directly to ensure UI sink gets the logs after reconfiguration
    private static ILogger Logger => Log.Logger;

    #region Events

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<Interfaces.TagValueChangedEventArgs>? TagValueChanged;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    #endregion

    #region Properties

    public int PlcCount => _connections.Count;
    public int ConnectedCount => _connections.Values.Count(c => c.IsConnected);
    public IReadOnlyList<IPlcConnection> Connections => _connections.Values.ToList();

    #endregion

    #region Constructor

    public PlcManager(
        ILogger logger,
        IConfigurationService configService,
        IDataCache dataCache)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        // Note: We use static Logger property instead of injected logger

        Logger.Information("PlcManager initialized");
    }

    #endregion

    #region Connection Management

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.Information("Initializing PlcManager with {Count} PLCs...", 
            _configService.CurrentConfiguration.PlcDevices.Count);

        foreach (var device in _configService.CurrentConfiguration.PlcDevices.Where(d => d.IsEnabled))
        {
            await AddPlcAsync(device, cancellationToken);
        }

        Logger.Information("PlcManager initialized with {Count} connections", _connections.Count);
    }

    public async Task<IPlcConnection?> AddPlcAsync(PlcDevice device, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlcManager));

        if (_connections.ContainsKey(device.Id))
        {
            Logger.Warning("PLC {Name} already exists", device.Name);
            return _connections[device.Id];
        }

        try
        {
            var connection = new PlcConnection(device, Log.Logger);
            
            connection.ConnectionStateChanged += OnConnectionStateChanged;
            connection.TagValueChanged += OnTagValueChanged;
            connection.ErrorOccurred += OnErrorOccurred;

            _connections[device.Id] = connection;
            
            Logger.Information("Added PLC {Name} ({Id})", device.Name, device.Id);
            
            return connection;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding PLC {Name}", device.Name);
            return null;
        }
    }

    public async Task<bool> RemovePlcAsync(string plcId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryRemove(plcId, out var connection))
        {
            connection.ConnectionStateChanged -= OnConnectionStateChanged;
            connection.TagValueChanged -= OnTagValueChanged;
            connection.ErrorOccurred -= OnErrorOccurred;

            await connection.DisconnectAsync();
            connection.Dispose();
            
            Logger.Information("Removed PLC {Id}", plcId);
            return true;
        }

        return false;
    }

    public IPlcConnection? GetConnection(string plcId)
    {
        _connections.TryGetValue(plcId, out var connection);
        return connection;
    }

    public bool HasPlc(string plcId) => _connections.ContainsKey(plcId);

    #endregion

    #region Connect/Disconnect Operations

    public async Task<bool> ConnectAsync(string plcId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return await connection.ConnectAsync(cancellationToken);
        }
        
        Logger.Warning("PLC {Id} not found", plcId);
        return false;
    }

    public async Task DisconnectAsync(string plcId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            await connection.DisconnectAsync();
        }
    }

    public async Task<int> ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        Logger.Information("Connecting to all PLCs...");
        
        var tasks = _connections.Values
            .Where(c => !c.IsConnected && c.Device.IsEnabled)
            .Select(c => ConnectWithLoggingAsync(c, cancellationToken));

        var results = await Task.WhenAll(tasks);
        var connectedCount = results.Count(r => r);
        
        Logger.Information("Connected to {Count}/{Total} PLCs", 
            connectedCount, _connections.Count);
        
        return connectedCount;
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        Logger.Information("Disconnecting from all PLCs...");
        
        var tasks = _connections.Values
            .Where(c => c.IsConnected)
            .Select(c => c.DisconnectAsync());

        await Task.WhenAll(tasks);
        
        Logger.Information("Disconnected from all PLCs");
    }

    public async Task<bool> ReconnectAsync(string plcId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return await connection.ReconnectAsync(cancellationToken);
        }
        return false;
    }

    public async Task<int> ReconnectAllAsync(CancellationToken cancellationToken = default)
    {
        Logger.Information("Reconnecting all PLCs...");
        
        await DisconnectAllAsync(cancellationToken);
        return await ConnectAllAsync(cancellationToken);
    }

    private async Task<bool> ConnectWithLoggingAsync(IPlcConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error connecting to {PlcName}", connection.Device.Name);
            return false;
        }
    }

    #endregion

    #region Read/Write Operations

    public async Task<TagValue?> ReadTagAsync(string plcId, string nodeId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            var value = await connection.ReadTagAsync(nodeId, cancellationToken);
            
            if (value != null)
            {
                _dataCache.UpdateTag(plcId, nodeId, value);
            }
            
            return value;
        }
        return null;
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(string plcId, IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            var values = await connection.ReadTagsAsync(nodeIds, cancellationToken);
            
            foreach (var value in values)
            {
                _dataCache.UpdateTag(plcId, value.NodeId, value);
            }
            
            return values;
        }
        return Array.Empty<TagValue>();
    }

    public async Task<bool> WriteTagAsync(string plcId, string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return await connection.WriteTagAsync(nodeId, value, cancellationToken);
        }
        return false;
    }

    public async Task<IReadOnlyList<bool>> WriteTagsAsync(string plcId, IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return await connection.WriteTagsAsync(items, cancellationToken);
        }
        return items.Select(_ => false).ToList();
    }

    public async Task<IDictionary<string, IReadOnlyList<TagValue>>> ReadAllTagsAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, IReadOnlyList<TagValue>>();

        var tasks = _connections.Values
            .Where(c => c.IsConnected)
            .Select(async c =>
            {
                var nodeIds = c.Device.Tags.Where(t => t.IsEnabled).Select(t => t.NodeId);
                var values = await c.ReadTagsAsync(nodeIds, cancellationToken);
                return (c.Device.Id, values);
            });

        var taskResults = await Task.WhenAll(tasks);
        
        foreach (var (plcId, values) in taskResults)
        {
            results[plcId] = values;
        }

        return results;
    }

    #endregion

    #region Browse Operations

    public async Task<IReadOnlyList<BrowseNode>> BrowseAsync(string plcId, string? nodeId = null, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return await connection.BrowseAsync(nodeId, cancellationToken);
        }
        return Array.Empty<BrowseNode>();
    }

    public async Task<NodeInfo?> GetNodeInfoAsync(string plcId, string nodeId, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return await connection.GetNodeInfoAsync(nodeId, cancellationToken);
        }
        return null;
    }

    #endregion

    #region Status Methods

    public IReadOnlyList<PlcStatus> GetAllStatus()
    {
        return _connections.Values.Select(c => new PlcStatus
        {
            PlcId = c.Device.Id,
            PlcName = c.Device.Name,
            EndpointUrl = c.Device.EndpointUrl,
            ConnectionState = c.ConnectionState,
            IsConnected = c.IsConnected,
            SessionId = c.SessionId,
            LastConnectedTime = c.LastConnectedTime,
            LastDisconnectedTime = c.LastDisconnectedTime,
            LastError = c.LastError,
            TagCount = c.Device.Tags.Count,
            EnabledTagCount = c.Device.Tags.Count(t => t.IsEnabled)
        }).ToList();
    }

    public PlcStatus? GetStatus(string plcId)
    {
        if (_connections.TryGetValue(plcId, out var connection))
        {
            return new PlcStatus
            {
                PlcId = connection.Device.Id,
                PlcName = connection.Device.Name,
                EndpointUrl = connection.Device.EndpointUrl,
                ConnectionState = connection.ConnectionState,
                IsConnected = connection.IsConnected,
                SessionId = connection.SessionId,
                LastConnectedTime = connection.LastConnectedTime,
                LastDisconnectedTime = connection.LastDisconnectedTime,
                LastError = connection.LastError,
                TagCount = connection.Device.Tags.Count,
                EnabledTagCount = connection.Device.Tags.Count(t => t.IsEnabled)
            };
        }
        return null;
    }

    #endregion

    #region Event Handlers

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        Logger.Information("PLC {PlcName} state changed: {OldState} -> {NewState}", 
            e.PlcName, e.OldState, e.NewState);
        
        ConnectionStateChanged?.Invoke(this, e);
    }

    private void OnTagValueChanged(object? sender, Interfaces.TagValueChangedEventArgs e)
    {
        // Update cache
        _dataCache.UpdateTag(e.PlcId, e.NodeId, e.Value);
        
        // Forward event
        TagValueChanged?.Invoke(this, e);
    }

    private void OnErrorOccurred(object? sender, PlcErrorEventArgs e)
    {
        Logger.Warning("Error on PLC {PlcName}: {Error}", e.PlcName, e.ErrorMessage);
        
        ErrorOccurred?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Logger.Information("Disposing PlcManager...");

        foreach (var connection in _connections.Values)
        {
            connection.ConnectionStateChanged -= OnConnectionStateChanged;
            connection.TagValueChanged -= OnTagValueChanged;
            connection.ErrorOccurred -= OnErrorOccurred;
            
            connection.Dispose();
        }

        _connections.Clear();

        Logger.Information("PlcManager disposed");
        
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Trạng thái của 1 PLC
/// </summary>
public class PlcStatus
{
    public string PlcId { get; init; } = string.Empty;
    public string PlcName { get; init; } = string.Empty;
    public string EndpointUrl { get; init; } = string.Empty;
    public PlcConnectionState ConnectionState { get; init; }
    public bool IsConnected { get; init; }
    public string? SessionId { get; init; }
    public DateTime? LastConnectedTime { get; init; }
    public DateTime? LastDisconnectedTime { get; init; }
    public string? LastError { get; init; }
    public int TagCount { get; init; }
    public int EnabledTagCount { get; init; }
}
