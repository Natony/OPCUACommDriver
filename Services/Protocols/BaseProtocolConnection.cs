using Microsoft.Extensions.Logging;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Services.Protocols;

/// <summary>
/// Base abstract class cho tất cả các protocol connections
/// </summary>
public abstract class BaseProtocolConnection : IPlcConnection
{
    protected readonly PlcDevice _device;
    protected readonly ILogger _logger;
    protected readonly object _lock = new();

    protected PlcConnectionState _connectionState = PlcConnectionState.Disabled;
    protected string? _sessionId;
    protected DateTime? _lastConnectedTime;
    protected DateTime? _lastDisconnectedTime;
    protected string? _lastError;
    protected bool _isDisposed;
    protected bool _isDisconnecting;
    protected CancellationTokenSource? _reconnectCts;

    // Polling mechanism for TCP protocols (Modbus, S7, MC)
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    protected readonly SemaphoreSlim _communicationLock = new(1, 1);

    #region Properties

    public PlcDevice Device => _device;

    public PlcConnectionState ConnectionState
    {
        get => _connectionState;
        protected set
        {
            if (_connectionState != value)
            {
                var oldState = _connectionState;
                _connectionState = value;
                _device.ConnectionState = value;
                OnConnectionStateChanged(oldState, value);
            }
        }
    }

    public bool IsConnected => ConnectionState == PlcConnectionState.Connected;
    public string? SessionId => _sessionId;
    public DateTime? LastConnectedTime => _lastConnectedTime;
    public DateTime? LastDisconnectedTime => _lastDisconnectedTime;
    public string? LastError => _lastError;

    /// <summary>
    /// Tên giao thức để hiển thị trong log
    /// </summary>
    protected abstract string ProtocolName { get; }

    #endregion

    #region Events

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<TagValueChangedEventArgs>? TagValueChanged;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    #endregion

    protected BaseProtocolConnection(PlcDevice device, ILogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Abstract Methods - Must be implemented by each protocol

    protected abstract Task<bool> ConnectInternalAsync(CancellationToken cancellationToken);
    protected abstract Task DisconnectInternalAsync();
    protected abstract Task<TagValue?> ReadTagInternalAsync(string address, CancellationToken cancellationToken);
    protected abstract Task<bool> WriteTagInternalAsync(string address, object value, CancellationToken cancellationToken);
    protected abstract bool IsConnectionActive();

    #endregion

    #region Connection Methods

    public virtual async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            _logger.LogWarning("[{Protocol}] Cannot connect - connection is disposed", ProtocolName);
            return false;
        }

        if (IsConnected)
        {
            _logger.LogDebug("[{Protocol}] Already connected to {Device}", ProtocolName, _device.Name);
            return true;
        }

        try
        {
            ConnectionState = PlcConnectionState.Connecting;
            _logger.LogInformation("[{Protocol}] Connecting to {Device}...", ProtocolName, _device.Name);

            var result = await ConnectInternalAsync(cancellationToken);

            if (result)
            {
                _sessionId = Guid.NewGuid().ToString();
                _lastConnectedTime = DateTime.Now;
                _device.LastConnectedTime = _lastConnectedTime;
                ConnectionState = PlcConnectionState.Connected;
                _logger.LogInformation("[{Protocol}] Connected to {Device} successfully", ProtocolName, _device.Name);

                // Start background polling for tag values
                StartPolling();
            }
            else
            {
                ConnectionState = PlcConnectionState.Error;
                _logger.LogError("[{Protocol}] Failed to connect to {Device}", ProtocolName, _device.Name);
            }

            return result;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _device.LastError = ex.Message;
            ConnectionState = PlcConnectionState.Error;
            OnError($"Connection failed: {ex.Message}", ex, true);
            return false;
        }
    }

    public virtual async Task DisconnectAsync()
    {
        if (_isDisposed || _isDisconnecting)
            return;

        try
        {
            _isDisconnecting = true;
            StopPolling();
            _reconnectCts?.Cancel();
            ConnectionState = PlcConnectionState.Disconnecting;
            _logger.LogInformation("[{Protocol}] Disconnecting from {Device}...", ProtocolName, _device.Name);

            await DisconnectInternalAsync();

            _lastDisconnectedTime = DateTime.Now;
            _device.LastDisconnectedTime = _lastDisconnectedTime;
            ConnectionState = PlcConnectionState.Disconnected;
            _logger.LogInformation("[{Protocol}] Disconnected from {Device}", ProtocolName, _device.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Protocol}] Error disconnecting from {Device}", ProtocolName, _device.Name);
        }
        finally
        {
            _isDisconnecting = false;
        }
    }

    public virtual async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        await Task.Delay(500, cancellationToken);
        return await ConnectAsync(cancellationToken);
    }

    #endregion

    #region Read/Write Methods

    public virtual async Task<TagValue?> ReadTagAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("[{Protocol}] Cannot read - not connected", ProtocolName);
            return null;
        }

        await _communicationLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadTagInternalAsync(nodeId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Protocol}] Error reading tag {NodeId}", ProtocolName, nodeId);
            return CreateErrorTagValue(nodeId, ex.Message);
        }
        finally
        {
            _communicationLock.Release();
        }
    }

    public virtual async Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var results = new List<TagValue>();
        foreach (var nodeId in nodeIds)
        {
            var value = await ReadTagAsync(nodeId, cancellationToken);
            if (value != null)
                results.Add(value);
        }
        return results;
    }

    public virtual async Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("[{Protocol}] Cannot write - not connected", ProtocolName);
            return false;
        }

        await _communicationLock.WaitAsync(cancellationToken);
        try
        {
            return await WriteTagInternalAsync(nodeId, value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Protocol}] Error writing tag {NodeId}", ProtocolName, nodeId);
            return false;
        }
        finally
        {
            _communicationLock.Release();
        }
    }

    public virtual async Task<IReadOnlyList<bool>> WriteTagsAsync(IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        var results = new List<bool>();
        foreach (var (nodeId, value) in items)
        {
            var result = await WriteTagAsync(nodeId, value, cancellationToken);
            results.Add(result);
        }
        return results;
    }

    #endregion

    #region Polling Methods - Background tag scanning for TCP protocols

    private void StartPolling()
    {
        StopPolling();
        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(() => PollTagsAsync(_pollingCts.Token));
        _logger.LogInformation("[{Protocol}] Started background polling for {Device}", ProtocolName, _device.Name);
    }

    private void StopPolling()
    {
        if (_pollingCts != null)
        {
            _pollingCts.Cancel();
            _pollingCts.Dispose();
            _pollingCts = null;
            _logger.LogInformation("[{Protocol}] Stopped background polling for {Device}", ProtocolName, _device.Name);
        }
    }

    private async Task PollTagsAsync(CancellationToken ct)
    {
        // Small delay before first poll to let connection stabilize
        await Task.Delay(200, ct);

        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                var enabledTags = _device.Tags.Where(t => t.IsEnabled).ToList();
                if (enabledTags.Count == 0)
                {
                    await Task.Delay(1000, ct);
                    continue;
                }

                foreach (var tag in enabledTags)
                {
                    if (ct.IsCancellationRequested || !IsConnected) break;

                    try
                    {
                        await _communicationLock.WaitAsync(ct);
                        try
                        {
                            var value = await ReadTagInternalAsync(tag.NodeId, ct);
                            if (value != null)
                            {
                                tag.UpdateValue(value.Value, value.Quality, value.Timestamp, value.ServerTimestamp);
                                OnTagValueChanged(tag.Id, tag.NodeId, value);
                            }
                        }
                        finally
                        {
                            _communicationLock.Release();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[{Protocol}] Error polling tag {NodeId}", ProtocolName, tag.NodeId);
                        tag.SetError(ex.Message);
                    }
                }

                // Use minimum scan rate of enabled tags, with a floor of 100ms
                var minScanRate = enabledTags.Min(t => t.ScanRate);
                await Task.Delay(Math.Max(minScanRate, 100), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Protocol}] Polling loop error for {Device}", ProtocolName, _device.Name);

                if (!ct.IsCancellationRequested && IsConnected)
                {
                    await Task.Delay(2000, ct);
                }
            }
        }

        _logger.LogDebug("[{Protocol}] Polling loop ended for {Device}", ProtocolName, _device.Name);
    }

    #endregion

    #region Subscription Methods - Default implementations (can be overridden)

    public virtual Task<bool> CreateSubscriptionAsync(SubscriptionGroup group, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[{Protocol}] Subscriptions not supported - use polling instead", ProtocolName);
        return Task.FromResult(false);
    }

    public virtual Task<bool> RemoveSubscriptionAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public virtual Task<bool> AddTagToSubscriptionAsync(string groupId, TagItem tag, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public virtual Task<bool> RemoveTagFromSubscriptionAsync(string groupId, string tagId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    #endregion

    #region Browse Methods - Default implementations

    public virtual Task<IReadOnlyList<BrowseNode>> BrowseAsync(string? nodeId = null, CancellationToken cancellationToken = default)
    {
        // TCP/IP protocols typically don't support browsing
        return Task.FromResult<IReadOnlyList<BrowseNode>>(Array.Empty<BrowseNode>());
    }

    public virtual Task<NodeInfo?> GetNodeInfoAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<NodeInfo?>(null);
    }

    #endregion

    #region Auto Reconnect

    protected async Task StartAutoReconnectAsync()
    {
        if (!_device.AutoReconnect || _isDisconnecting || _isDisposed)
            return;

        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        ConnectionState = PlcConnectionState.Reconnecting;
        var attempts = 0;
        var maxAttempts = _device.MaxReconnectAttempts == 0 ? int.MaxValue : _device.MaxReconnectAttempts;

        while (!token.IsCancellationRequested && attempts < maxAttempts)
        {
            attempts++;
            _logger.LogInformation("[{Protocol}] Reconnect attempt {Attempt}/{Max} for {Device}",
                ProtocolName, attempts, _device.MaxReconnectAttempts == 0 ? "∞" : maxAttempts.ToString(), _device.Name);

            try
            {
                await Task.Delay(_device.ReconnectInterval, token);
                if (await ConnectInternalAsync(token))
                {
                    _sessionId = Guid.NewGuid().ToString();
                    _lastConnectedTime = DateTime.Now;
                    _device.LastConnectedTime = _lastConnectedTime;
                    ConnectionState = PlcConnectionState.Connected;
                    _logger.LogInformation("[{Protocol}] Reconnected to {Device} successfully", ProtocolName, _device.Name);

                    // Restart background polling after reconnect
                    StartPolling();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Protocol}] Reconnect attempt {Attempt} failed", ProtocolName, attempts);
            }
        }

        if (!token.IsCancellationRequested)
        {
            _lastError = "Max reconnect attempts reached";
            _device.LastError = _lastError;
            ConnectionState = PlcConnectionState.Error;
            OnError("Max reconnect attempts reached", null, true);
        }
    }

    #endregion

    #region Event Helpers

    protected void OnConnectionStateChanged(PlcConnectionState oldState, PlcConnectionState newState)
    {
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            PlcId = _device.Id,
            PlcName = _device.Name,
            OldState = oldState,
            NewState = newState,
            Timestamp = DateTime.Now,
            Message = $"{ProtocolName}: {oldState} -> {newState}"
        });
    }

    protected void OnTagValueChanged(string tagId, string nodeId, TagValue value)
    {
        TagValueChanged?.Invoke(this, new TagValueChangedEventArgs
        {
            PlcId = _device.Id,
            TagId = tagId,
            NodeId = nodeId,
            Value = value,
            Timestamp = DateTime.Now
        });
    }

    protected void OnError(string message, Exception? ex, bool isCritical)
    {
        _logger.LogError(ex, "[{Protocol}] {Message}", ProtocolName, message);
        ErrorOccurred?.Invoke(this, new PlcErrorEventArgs
        {
            PlcId = _device.Id,
            PlcName = _device.Name,
            ErrorMessage = message,
            Exception = ex,
            Timestamp = DateTime.Now,
            IsCritical = isCritical
        });
    }

    protected TagValue CreateErrorTagValue(string nodeId, string errorMessage)
    {
        return new TagValue
        {
            NodeId = nodeId,
            Value = null,
            Quality = TagQuality.Bad,
            Timestamp = DateTime.Now,
            StatusCode = 0x80000000
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            StopPolling();
            _communicationLock.Dispose();
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();

            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Protocol}] Error during dispose", ProtocolName);
            }
        }

        _isDisposed = true;
    }

    #endregion
}
