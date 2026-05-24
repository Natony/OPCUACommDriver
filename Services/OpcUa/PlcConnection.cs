using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OpcUaCommunicationEngine.Services.OpcUa;

/// <summary>
/// OPC UA Connection cho 1 PLC
/// Sử dụng OPC Foundation library
/// </summary>
public class PlcConnection : IPlcConnection
{
    private readonly PlcDevice _device;
    private readonly object _lock = new();

    private Session? _session;
    private ApplicationConfiguration? _appConfig;
    private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;
    private bool _isDisconnecting; // Flag to prevent keep-alive from triggering reconnect during disconnect
    private bool _isReconnecting;  // Flag to prevent multiple reconnect loops

    // SessionReconnectHandler for proper OPC UA reconnection handling
    // This handler manages the two-layer reconnection: Secure Channel + Session
    private SessionReconnectHandler? _reconnectHandler;

    // Timestamp to track when reconnection completed - used to ignore stale KeepAlive events
    private DateTime _lastReconnectCompleteTime = DateTime.MinValue;
    private const int KeepAliveSettlingPeriodMs = 2000; // Ignore KeepAlive failures for 2s after reconnection

    // Subscriptions management
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
    private readonly ConcurrentDictionary<uint, (string TagId, string NodeId)> _monitoredItemMapping = new();

    // Use Log.Logger directly to ensure UI sink gets the logs after reconfiguration
    private static ILogger Logger => Log.Logger;

    #region Properties

    public PlcDevice Device => _device;

    public PlcConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                var oldState = _connectionState;
                _connectionState = value;
                _device.ConnectionState = value;
                
                try
                {
                    ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                    {
                        PlcId = _device.Id,
                        PlcName = _device.Name,
                        OldState = oldState,
                        NewState = value
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error in ConnectionStateChanged handler for {PlcName}: {Error}", _device.Name, ex.Message);
                }
            }
        }
    }

    public bool IsConnected => _session?.Connected == true && ConnectionState == PlcConnectionState.Connected;

    public string? SessionId => _session?.SessionId?.ToString();

    /// <summary>
    /// Get the underlying OPC UA Session for advanced operations (e.g., browsing)
    /// </summary>
    public Session? Session => _session;

    public DateTime? LastConnectedTime { get; private set; }

    public DateTime? LastDisconnectedTime { get; private set; }

    public string? LastError { get; private set; }

    #endregion

    #region Events

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<Interfaces.TagValueChangedEventArgs>? TagValueChanged;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    #endregion

    #region Constructor

    public PlcConnection(PlcDevice device, Microsoft.Extensions.Logging.ILogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        // Note: We use static Logger property instead of injected logger
        // to ensure logs go to UI sink after it's configured

        Logger.Debug("PlcConnection created for {PlcName} ({Endpoint})", device.Name, device.EndpointUrl);
    }

    #endregion

    #region Connection Methods

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlcConnection));

        if (IsConnected)
        {
            Logger.Warning("Already connected to {PlcName}", _device.Name);
            return true;
        }

        try
        {
            ConnectionState = PlcConnectionState.Connecting;
            Logger.Information("═══════════════════════════════════════════════════════════");
            Logger.Information("Connecting to PLC: {PlcName}", _device.Name);
            Logger.Information("Target Endpoint: {Endpoint}", _device.EndpointUrl);
            Logger.Information("Security Policy: {Policy}, Mode: {Mode}", _device.SecurityPolicy, _device.SecurityMode);

            // Initialize OPC UA Application Configuration
            _appConfig = await CreateApplicationConfigurationAsync();

            // Discover and select endpoint with proper error handling
            var selectedEndpoint = await SelectEndpointAsync(_device.EndpointUrl, cancellationToken);

            Logger.Information("Selected Endpoint: {Endpoint}", selectedEndpoint.EndpointUrl);
            Logger.Information("  → Security Policy: {Policy}", selectedEndpoint.SecurityPolicyUri);
            Logger.Information("  → Security Mode: {Mode}", selectedEndpoint.SecurityMode);
            Logger.Information("  → Transport Profile: {Profile}", selectedEndpoint.TransportProfileUri);

            // Create endpoint configuration
            var endpointConfig = EndpointConfiguration.Create(_appConfig);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            // Create session
            Logger.Information("Creating OPC UA Session...");
            _session = await Session.Create(
                _appConfig,
                endpoint,
                false,
                _appConfig.ApplicationName,
                (uint)_device.SessionTimeout,
                GetUserIdentity(),
                null,
                cancellationToken);

            // Setup session events
            _session.KeepAlive += Session_KeepAlive;
            _session.Notification += Session_Notification;
            _session.PublishError += Session_PublishError;

            ConnectionState = PlcConnectionState.Connected;
            LastConnectedTime = DateTime.Now;
            LastError = null;

            Logger.Information("✓ Connected successfully to {PlcName}", _device.Name);
            Logger.Information("  → Session ID: {SessionId}", _session.SessionId);
            Logger.Information("  → Session Name: {SessionName}", _session.SessionName);
            Logger.Information("  → Server URI: {ServerUri}", _session.Endpoint.Server.ApplicationUri);
            Logger.Information("═══════════════════════════════════════════════════════════");

            // Create default subscriptions if configured
            if (_device.SubscriptionGroups.Any())
            {
                foreach (var group in _device.SubscriptionGroups.Where(g => g.IsEnabled))
                {
                    await CreateSubscriptionAsync(group, cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            ConnectionState = PlcConnectionState.Error;

            Logger.Error("✗ Failed to connect to {PlcName} ({Endpoint})", _device.Name, _device.EndpointUrl);
            Logger.Error("  → Exception type: {Type}", ex.GetType().FullName);
            Logger.Error("  → Message: {Message}", ex.Message);

            if (ex is ServiceResultException sre)
            {
                Logger.Error("  → OPC UA StatusCode: 0x{Code:X8} ({Symbol})",
                    sre.StatusCode, StatusCodes.GetBrowseName(sre.StatusCode));
            }

            var inner = ex.InnerException;
            int depth = 1;
            while (inner != null && depth <= 5)
            {
                Logger.Error("  → Inner [{Depth}] {Type}: {Message}", depth, inner.GetType().FullName, inner.Message);
                inner = inner.InnerException;
                depth++;
            }

            Logger.Debug(ex, "Full connection failure stack trace for {PlcName}", _device.Name);
            Logger.Information("═══════════════════════════════════════════════════════════");

            RaiseError(ex.Message, ex, isCritical: false);

            // Only start auto-reconnect if not already in reconnect loop
            // This prevents nested reconnect loops
            if (_device.AutoReconnect && !_isReconnecting && !_isDisconnecting)
            {
                StartAutoReconnect();
            }

            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed) return;
        if (_isDisconnecting) return; // Prevent re-entry

        _isDisconnecting = true;

        try
        {
            // Stop reconnect handler FIRST
            lock (_lock)
            {
                if (_reconnectHandler != null)
                {
                    try
                    {
                        _reconnectHandler.Dispose();
                    }
                    catch { /* Ignore */ }
                    _reconnectHandler = null;
                }
            }

            // Stop auto-reconnect
            StopAutoReconnect();

            if (_session != null)
            {
                ConnectionState = PlcConnectionState.Disconnecting;
                Logger.Information("═══════════════════════════════════════════════════════════");
                Logger.Information("Disconnecting from PLC: {PlcName}", _device.Name);
                Logger.Information("  → Session ID: {SessionId}", _session.SessionId);

                // Unsubscribe events FIRST to prevent keep-alive from triggering reconnect
                try
                {
                    _session.KeepAlive -= Session_KeepAlive;
                    _session.Notification -= Session_Notification;
                    _session.PublishError -= Session_PublishError;
                }
                catch { /* Ignore */ }

                // Remove subscriptions
                foreach (var subscription in _subscriptions.Values)
                {
                    try
                    {
                        subscription.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug("Error removing subscription during disconnect: {Error}", ex.Message);
                    }
                }
                _subscriptions.Clear();
                _monitoredItemMapping.Clear();

                // Close session with timeout
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _session.CloseAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.Debug("Error closing session: {Error}", ex.Message);
                }

                try
                {
                    _session.Dispose();
                }
                catch { /* Ignore */ }

                _session = null;

                Logger.Information("✓ Disconnected from {PlcName}", _device.Name);
                Logger.Information("═══════════════════════════════════════════════════════════");
            }

            ConnectionState = PlcConnectionState.Disconnected;
            LastDisconnectedTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disconnecting from {PlcName}", _device.Name);
            ConnectionState = PlcConnectionState.Disconnected;
            _session = null;
        }
        finally
        {
            _isDisconnecting = false;
        }
    }

    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        await Task.Delay(1000, cancellationToken);
        return await ConnectAsync(cancellationToken);
    }

    #endregion

    #region Read/Write Methods

    public async Task<TagValue?> ReadTagAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warning("Cannot read tag - not connected to {PlcName}", _device.Name);
            return null;
        }

        try
        {
            var nodesToRead = new ReadValueIdCollection
            {
                new ReadValueId
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value
                }
            };

            _session.Read(
                null,
                0,
                TimestampsToReturn.Both,
                nodesToRead,
                out DataValueCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            if (results.Count > 0)
            {
                var dataValue = results[0];
                return new TagValue
                {
                    NodeId = nodeId,
                    Value = dataValue.Value,
                    Quality = MapStatusCode(dataValue.StatusCode),
                    SourceTimestamp = dataValue.SourceTimestamp,
                    ServerTimestamp = dataValue.ServerTimestamp
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reading tag {NodeId} from {PlcName}", nodeId, _device.Name);
            return null;
        }
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var results = new List<TagValue>();
        
        if (!IsConnected || _session == null)
        {
            Logger.Warning("Cannot read tags - not connected to {PlcName}", _device.Name);
            return results;
        }

        try
        {
            var nodeIdList = nodeIds.ToList();
            var nodesToRead = new ReadValueIdCollection();
            
            foreach (var nodeId in nodeIdList)
            {
                nodesToRead.Add(new ReadValueId
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value
                });
            }

            _session.Read(
                null,
                0,
                TimestampsToReturn.Both,
                nodesToRead,
                out DataValueCollection dataValues,
                out DiagnosticInfoCollection diagnosticInfos);

            for (int i = 0; i < dataValues.Count; i++)
            {
                var dataValue = dataValues[i];
                results.Add(new TagValue
                {
                    NodeId = nodeIdList[i],
                    Value = dataValue.Value,
                    Quality = MapStatusCode(dataValue.StatusCode),
                    SourceTimestamp = dataValue.SourceTimestamp,
                    ServerTimestamp = dataValue.ServerTimestamp
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reading tags from {PlcName}", _device.Name);
        }

        return results;
    }

    public async Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warning("Cannot write tag - not connected to {PlcName}", _device.Name);
            return false;
        }

        try
        {
            // Convert JsonElement to native type if needed
            var convertedValue = ConvertJsonElement(value);

            // Read the node's data type to ensure proper type conversion
            var nodeToRead = new ReadValueId
            {
                NodeId = new NodeId(nodeId),
                AttributeId = Attributes.DataType
            };

            _session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                new ReadValueIdCollection { nodeToRead },
                out DataValueCollection dataTypeResults,
                out DiagnosticInfoCollection _);

            if (dataTypeResults.Count > 0 && StatusCode.IsGood(dataTypeResults[0].StatusCode))
            {
                var dataTypeNodeId = dataTypeResults[0].Value as NodeId;
                convertedValue = ConvertToExpectedType(convertedValue, dataTypeNodeId);
            }

            var nodesToWrite = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(convertedValue))
                }
            };

            _session.Write(
                null,
                nodesToWrite,
                out StatusCodeCollection results,
                out DiagnosticInfoCollection diagnosticInfos);

            var success = StatusCode.IsGood(results[0]);

            if (!success)
            {
                Logger.Warning("Write to {NodeId} failed: {StatusCode}", nodeId, results[0]);
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error writing to tag {NodeId} on {PlcName}", nodeId, _device.Name);
            return false;
        }
    }

    /// <summary>
    /// Convert value to the expected OPC UA data type
    /// </summary>
    private object ConvertToExpectedType(object value, NodeId? dataTypeNodeId)
    {
        if (dataTypeNodeId == null) return value;

        // OPC UA built-in type identifiers
        var typeId = dataTypeNodeId.Identifier as uint? ?? 0;

        try
        {
            return typeId switch
            {
                1 => Convert.ToBoolean(value),      // Boolean
                2 => Convert.ToSByte(value),        // SByte
                3 => Convert.ToByte(value),         // Byte
                4 => Convert.ToInt16(value),        // Int16
                5 => Convert.ToUInt16(value),       // UInt16
                6 => Convert.ToInt32(value),        // Int32
                7 => Convert.ToUInt32(value),       // UInt32
                8 => Convert.ToInt64(value),        // Int64
                9 => Convert.ToUInt64(value),       // UInt64
                10 => Convert.ToSingle(value),      // Float
                11 => Convert.ToDouble(value),      // Double
                12 => Convert.ToString(value) ?? string.Empty, // String
                13 => Convert.ToDateTime(value),    // DateTime
                _ => value
            };
        }
        catch (Exception ex)
        {
            Logger.Warning("Failed to convert value to type {TypeId}: {Error}", typeId, ex.Message);
            return value;
        }
    }

    /// <summary>
    /// Convert JsonElement to native .NET type for OPC UA Variant
    /// </summary>
    private object ConvertJsonElement(object value)
    {
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt64(out var longVal)
                    ? (jsonElement.TryGetInt32(out var intVal) ? intVal : longVal)
                    : jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Null => null!,
                _ => value
            };
        }
        return value;
    }

    public async Task<IReadOnlyList<bool>> WriteTagsAsync(IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        var results = new List<bool>();
        
        if (!IsConnected || _session == null)
        {
            Logger.Warning("Cannot write tags - not connected to {PlcName}", _device.Name);
            return items.Select(_ => false).ToList();
        }

        try
        {
            var nodesToWrite = new WriteValueCollection();
            
            foreach (var (nodeId, value) in items)
            {
                var convertedValue = ConvertJsonElement(value);
                nodesToWrite.Add(new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(convertedValue))
                });
            }

            _session.Write(
                null,
                nodesToWrite,
                out StatusCodeCollection statusCodes,
                out DiagnosticInfoCollection diagnosticInfos);

            foreach (var statusCode in statusCodes)
            {
                results.Add(StatusCode.IsGood(statusCode));
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error writing tags to {PlcName}", _device.Name);
            return items.Select(_ => false).ToList();
        }

        return results;
    }

    #endregion

    #region Subscription Methods

    public async Task<bool> CreateSubscriptionAsync(SubscriptionGroup group, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warning("Cannot create subscription - not connected to {PlcName}", _device.Name);
            return false;
        }

        try
        {
            if (_subscriptions.ContainsKey(group.Id))
            {
                Logger.Warning("Subscription {GroupName} already exists", group.Name);
                return true;
            }

            var subscription = new Subscription(_session.DefaultSubscription)
            {
                DisplayName = group.Name,
                PublishingEnabled = true,
                PublishingInterval = group.PublishingInterval,
                KeepAliveCount = group.KeepAliveCount,
                LifetimeCount = group.LifetimeCount,
                MaxNotificationsPerPublish = group.MaxNotificationsPerPublish,
                Priority = (byte)group.Priority
            };

            var tags = _device.Tags.Where(t => t.SubscriptionGroupId == group.Id && t.IsEnabled).ToList();

            Logger.Information("Creating subscription group: {GroupName} (Interval: {Interval}ms)",
                group.Name, group.PublishingInterval);

            if (!tags.Any())
            {
                Logger.Warning("  → No enabled tags for subscription {GroupName}", group.Name);
            }
            else
            {
                Logger.Information("  → Subscribing to {Count} tag(s):", tags.Count);
            }

            foreach (var tag in tags)
            {
                // Validate NodeId before creating monitored item
                if (string.IsNullOrWhiteSpace(tag.NodeId))
                {
                    Logger.Warning("     ✗ {TagName}: Skipped - NodeId is empty", tag.Name);
                    continue;
                }

                // Trim whitespace from NodeId
                var nodeIdStr = tag.NodeId.Trim();

                try
                {
                    var monitoredItem = new MonitoredItem(subscription.DefaultItem)
                    {
                        DisplayName = tag.Name,
                        StartNodeId = new NodeId(nodeIdStr),
                        AttributeId = Attributes.Value,
                        SamplingInterval = tag.ScanRate,
                        QueueSize = 1,
                        DiscardOldest = true
                    };

                    monitoredItem.Notification += MonitoredItem_Notification;
                    subscription.AddItem(monitoredItem);

                    _monitoredItemMapping[monitoredItem.ClientHandle] = (tag.Id, tag.NodeId);

                    Logger.Information("     • {TagName} → NodeId: {NodeId} (ScanRate: {Rate}ms)",
                        tag.Name, nodeIdStr, tag.ScanRate);
                }
                catch (Exception ex)
                {
                    Logger.Error("     ✗ {TagName}: Invalid NodeId '{NodeId}' - {Error}",
                        tag.Name, nodeIdStr, ex.Message);
                }
            }

            _session.AddSubscription(subscription);
            await subscription.CreateAsync(cancellationToken);

            if (subscription.MonitoredItemCount > 0)
            {
                await subscription.ApplyChangesAsync(cancellationToken);

                // Log status of each monitored item
                Logger.Information("  → Monitored items status:");
                foreach (var item in subscription.MonitoredItems)
                {
                    var status = item.Status?.Error?.StatusCode.ToString() ?? "Good";
                    var statusSymbol = StatusCode.IsGood(item.Status?.Error?.StatusCode ?? StatusCodes.Good) ? "✓" : "✗";
                    Logger.Information("     {Symbol} {Name}: {Status}", statusSymbol, item.DisplayName, status);
                }
            }

            _subscriptions[group.Id] = subscription;

            Logger.Information("✓ Subscription {GroupName} created with {Count} monitored items",
                group.Name, subscription.MonitoredItemCount);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error creating subscription {GroupName} on {PlcName}", group.Name, _device.Name);
            return false;
        }
    }

    public async Task<bool> RemoveSubscriptionAsync(string groupId, CancellationToken cancellationToken = default)
    {
        if (_session == null) return false;

        try
        {
            if (_subscriptions.TryRemove(groupId, out var subscription))
            {
                foreach (var item in subscription.MonitoredItems)
                {
                    _monitoredItemMapping.TryRemove(item.ClientHandle, out _);
                }

                _session.RemoveSubscription(subscription);
                subscription.Dispose();

                Logger.Information("Removed subscription {GroupId}", groupId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing subscription {GroupId}", groupId);
            return false;
        }
    }

    /// <summary>
    /// Recreate all subscriptions after reconnection
    /// Old subscriptions become invalid when session changes, so we need to create new ones
    /// </summary>
    private async Task RecreateSubscriptionsAsync()
    {
        if (_session == null || !IsConnected)
        {
            Logger.Warning("Cannot recreate subscriptions - session not connected");
            return;
        }

        Logger.Information("Recreating subscriptions after reconnection...");

        // Clear old subscriptions (they're invalid now)
        foreach (var kvp in _subscriptions.ToList())
        {
            try
            {
                // Try to clean up old subscription
                kvp.Value.Dispose();
            }
            catch { /* Ignore - old session is gone */ }
        }
        _subscriptions.Clear();
        _monitoredItemMapping.Clear();

        // Recreate subscriptions from device configuration
        var enabledGroups = _device.SubscriptionGroups.Where(g => g.IsEnabled).ToList();

        if (!enabledGroups.Any())
        {
            Logger.Information("No enabled subscription groups to recreate");
            return;
        }

        var successCount = 0;
        foreach (var group in enabledGroups)
        {
            try
            {
                var success = await CreateSubscriptionAsync(group);
                if (success) successCount++;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to recreate subscription {GroupName}", group.Name);
            }
        }

        Logger.Information("Recreated {Success}/{Total} subscriptions after reconnection",
            successCount, enabledGroups.Count);
    }

    public async Task<bool> AddTagToSubscriptionAsync(string groupId, TagItem tag, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return false;

        try
        {
            if (!_subscriptions.TryGetValue(groupId, out var subscription))
            {
                Logger.Warning("Subscription {GroupId} not found", groupId);
                return false;
            }

            var monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = tag.Name,
                StartNodeId = new NodeId(tag.NodeId),
                AttributeId = Attributes.Value,
                SamplingInterval = tag.ScanRate,
                QueueSize = 1,
                DiscardOldest = true
            };

            monitoredItem.Notification += MonitoredItem_Notification;
            subscription.AddItem(monitoredItem);
            await subscription.ApplyChangesAsync(cancellationToken);

            _monitoredItemMapping[monitoredItem.ClientHandle] = (tag.Id, tag.NodeId);
            
            Logger.Debug("Added tag {TagName} to subscription {GroupId}", tag.Name, groupId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding tag {TagName} to subscription {GroupId}", tag.Name, groupId);
            return false;
        }
    }

    public async Task<bool> RemoveTagFromSubscriptionAsync(string groupId, string tagId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return false;

        try
        {
            if (!_subscriptions.TryGetValue(groupId, out var subscription))
            {
                return false;
            }

            var mapping = _monitoredItemMapping.FirstOrDefault(m => m.Value.TagId == tagId);
            if (mapping.Value.TagId == null) return false;

            var monitoredItem = subscription.MonitoredItems.FirstOrDefault(m => m.ClientHandle == mapping.Key);
            if (monitoredItem != null)
            {
                subscription.RemoveItem(monitoredItem);
                await subscription.ApplyChangesAsync(cancellationToken);
                _monitoredItemMapping.TryRemove(mapping.Key, out _);
                
                Logger.Debug("Removed tag {TagId} from subscription {GroupId}", tagId, groupId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error removing tag {TagId} from subscription {GroupId}", tagId, groupId);
            return false;
        }
    }

    #endregion

    #region Browse Methods

    public async Task<IReadOnlyList<BrowseNode>> BrowseAsync(string? nodeId = null, CancellationToken cancellationToken = default)
    {
        var results = new List<BrowseNode>();
        
        if (!IsConnected || _session == null)
        {
            return results;
        }

        try
        {
            var startNode = string.IsNullOrEmpty(nodeId) 
                ? ObjectIds.ObjectsFolder 
                : new NodeId(nodeId);

            _session.Browse(
                null,
                null,
                startNode,
                0,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Object | (uint)NodeClass.Variable | (uint)NodeClass.Method,
                out byte[] continuationPoint,
                out ReferenceDescriptionCollection references);

            foreach (var reference in references)
            {
                var browseNode = new BrowseNode
                {
                    NodeId = reference.NodeId.ToString(),
                    DisplayName = reference.DisplayName.Text,
                    BrowseName = reference.BrowseName.ToString(),
                    NodeClass = reference.NodeClass.ToString(),
                    HasChildren = !reference.IsForward || reference.ReferenceTypeId != ReferenceTypeIds.HasComponent
                };

                if (reference.NodeClass == NodeClass.Variable)
                {
                    try
                    {
                        var node = _session.ReadNode((NodeId)reference.NodeId);
                        if (node is VariableNode varNode)
                        {
                            browseNode = browseNode with { DataType = varNode.DataType?.ToString() };
                        }
                    }
                    catch { }
                }

                results.Add(browseNode);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error browsing node {NodeId}", nodeId);
        }

        return results;
    }

    public async Task<NodeInfo?> GetNodeInfoAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return null;

        try
        {
            var node = _session.ReadNode(new NodeId(nodeId));
            
            var nodeInfo = new NodeInfo
            {
                NodeId = nodeId,
                DisplayName = node.DisplayName.Text,
                BrowseName = node.BrowseName.ToString(),
                NodeClass = node.NodeClass.ToString(),
                Description = node.Description?.Text
            };

            if (node is VariableNode varNode)
            {
                nodeInfo = nodeInfo with
                {
                    DataType = varNode.DataType?.ToString(),
                    IsReadable = (varNode.AccessLevel & AccessLevels.CurrentRead) != 0,
                    IsWritable = (varNode.AccessLevel & AccessLevels.CurrentWrite) != 0
                };

                var value = await ReadTagAsync(nodeId, cancellationToken);
                if (value != null)
                {
                    nodeInfo = nodeInfo with { CurrentValue = value.Value };
                }
            }

            return nodeInfo;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting node info for {NodeId}", nodeId);
            return null;
        }
    }

    #endregion

    #region Session Event Handlers

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // Ignore keep-alive events during disconnect
        if (_isDisconnecting || _disposed)
        {
            return;
        }

        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            // Don't spam logs if reconnect handler is already active
            if (_reconnectHandler != null)
            {
                // Only log at Debug level when handler is active
                Logger.Debug("Keep-alive check during reconnection: {Status}", e.Status);
                return;
            }

            Logger.Warning("Keep-alive failed for {PlcName}: {Status} (CurrentState: {State})",
                _device.Name, e.Status, e.CurrentState);

            // Log additional details for debugging
            if (e.Status.InnerResult != null)
            {
                Logger.Debug("  → Inner status: {InnerStatus}", e.Status.InnerResult);
            }

            // Only trigger reconnect if we're actually connected (not already reconnecting or disconnecting)
            // Also check settling period to avoid race condition with stale KeepAlive events after reconnection
            var timeSinceLastReconnect = (DateTime.Now - _lastReconnectCompleteTime).TotalMilliseconds;
            if (timeSinceLastReconnect < KeepAliveSettlingPeriodMs)
            {
                Logger.Debug("Ignoring KeepAlive failure during settling period ({ElapsedMs}ms < {SettlingMs}ms)",
                    (int)timeSinceLastReconnect, KeepAliveSettlingPeriodMs);
                return;
            }

            if (ConnectionState == PlcConnectionState.Connected && _device.AutoReconnect && !_isReconnecting)
            {
                ConnectionState = PlcConnectionState.Reconnecting;

                // Use SessionReconnectHandler for proper OPC UA reconnection
                // This handles both Secure Channel and Session layer reconnection
                lock (_lock)
                {
                    if (_reconnectHandler == null)
                    {
                        // Set _isReconnecting to prevent race conditions
                        _isReconnecting = true;

                        // reconnectPeriod = 10000ms (10 seconds) - time between reconnect attempts
                        _reconnectHandler = new SessionReconnectHandler(true);
                        _reconnectHandler.BeginReconnect(
                            _session,
                            10000, // 10 seconds between reconnect attempts
                            SessionReconnectHandler_Complete);

                        Logger.Information("Started SessionReconnectHandler for {PlcName}", _device.Name);
                    }
                }
            }
        }
        else
        {
            // Keep-alive succeeded - session is alive!
            if (ConnectionState == PlcConnectionState.Reconnecting)
            {
                // Session recovered while reconnect handler was running
                // Cancel the handler and set state back to Connected
                lock (_lock)
                {
                    if (_reconnectHandler != null)
                    {
                        Logger.Information("Keep-alive succeeded - session recovered, cancelling reconnect handler");
                        try
                        {
                            _reconnectHandler.Dispose();
                        }
                        catch { /* Ignore */ }
                        _reconnectHandler = null;
                    }

                    // Reset flags and set settling time
                    _isReconnecting = false;
                    _lastReconnectCompleteTime = DateTime.Now;
                }

                ConnectionState = PlcConnectionState.Connected;
                Logger.Information("✓ Connection recovered for {PlcName}", _device.Name);
            }
        }
    }

    /// <summary>
    /// Callback for SessionReconnectHandler when reconnection completes
    /// </summary>
    private void SessionReconnectHandler_Complete(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            // Ignore callback if disposing or disconnecting
            if (_disposed || _isDisconnecting)
            {
                _reconnectHandler?.Dispose();
                _reconnectHandler = null;
                _isReconnecting = false;
                return;
            }

            if (_reconnectHandler?.Session != null)
            {
                // Reconnection successful - update session reference
                var oldSession = _session;
                _session = (Session)_reconnectHandler.Session;

                Logger.Information("✓ SessionReconnectHandler completed for {PlcName}", _device.Name);
                Logger.Information("  → New Session ID: {SessionId}", _session?.SessionId);

                // Dispose old session if different - IMPORTANT: Remove event handlers FIRST
                // to prevent stale KeepAlive events from firing
                if (oldSession != null && oldSession != _session)
                {
                    try
                    {
                        oldSession.KeepAlive -= Session_KeepAlive;
                        oldSession.Notification -= Session_Notification;
                        oldSession.PublishError -= Session_PublishError;
                        oldSession.Dispose();
                    }
                    catch { /* Ignore */ }
                }

                // Re-attach event handlers if new session
                if (_session != null && _session != oldSession)
                {
                    _session.KeepAlive += Session_KeepAlive;
                    _session.Notification += Session_Notification;
                    _session.PublishError += Session_PublishError;
                }

                // Set settling timestamp BEFORE changing state to prevent race condition
                _lastReconnectCompleteTime = DateTime.Now;

                ConnectionState = PlcConnectionState.Connected;
                LastConnectedTime = DateTime.Now;

                // Recreate subscriptions on new session (fire and forget)
                _ = RecreateSubscriptionsAsync();
            }
            else
            {
                Logger.Warning("SessionReconnectHandler failed for {PlcName}", _device.Name);

                // Fall back to manual auto-reconnect (don't set _isReconnecting = false here,
                // StartAutoReconnect will manage it)
                if (_device.AutoReconnect)
                {
                    // Dispose the handler first
                    _reconnectHandler?.Dispose();
                    _reconnectHandler = null;
                    _isReconnecting = false; // Reset before calling StartAutoReconnect

                    StartAutoReconnect();
                    return; // Early return - StartAutoReconnect manages state
                }
                else
                {
                    ConnectionState = PlcConnectionState.Error;
                }
            }

            // Dispose the handler and reset reconnecting flag
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;
            _isReconnecting = false;
        }
    }

    private void Session_Notification(ISession session, NotificationEventArgs e)
    {
        // Handled by individual MonitoredItem notifications
    }

    private void Session_PublishError(ISession session, PublishErrorEventArgs e)
    {
        Logger.Warning("Publish error for {PlcName}: {Status}", _device.Name, e.Status);
    }

    private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is MonitoredItemNotification notification)
            {
                if (_monitoredItemMapping.TryGetValue(monitoredItem.ClientHandle, out var mapping))
                {
                    var quality = MapStatusCode(notification.Value.StatusCode);
                    var tagValue = new TagValue
                    {
                        TagId = mapping.TagId,
                        NodeId = mapping.NodeId,
                        Value = notification.Value.Value,
                        Quality = quality,
                        SourceTimestamp = notification.Value.SourceTimestamp,
                        ServerTimestamp = notification.Value.ServerTimestamp
                    };

                    var tag = _device.Tags.FirstOrDefault(t => t.Id == mapping.TagId);
                    if (tag != null)
                    {
                        tag.UpdateValue(tagValue.Value, tagValue.Quality, tagValue.SourceTimestamp);
                    }

                    TagValueChanged?.Invoke(this, new Interfaces.TagValueChangedEventArgs
                    {
                        PlcId = _device.Id,
                        TagId = mapping.TagId,
                        NodeId = mapping.NodeId,
                        Value = tagValue
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing monitored item notification");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Select endpoint with proper error handling and security matching
    /// </summary>
    private async Task<EndpointDescription> SelectEndpointAsync(string endpointUrl, CancellationToken cancellationToken)
    {
        EndpointDescriptionCollection? endpoints = null;
        Exception? lastException = null;

        // Retry discovery up to 3 times with increasing timeout
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                Logger.Information("Discovering endpoints from {Endpoint}... (attempt {Attempt}/3)", endpointUrl, attempt);

                // Create endpoint configuration with timeout
                var endpointConfig = EndpointConfiguration.Create();
                endpointConfig.OperationTimeout = 5000 * attempt; // 5s, 10s, 15s

                // Use discovery client to get available endpoints
                using var discoveryClient = DiscoveryClient.Create(new Uri(endpointUrl), endpointConfig);

                // Run discovery with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5 * attempt));

                endpoints = await Task.Run(() =>    discoveryClient.GetEndpoints(null), timeoutCts.Token);

                if (endpoints != null && endpoints.Count > 0)
                {
                    Logger.Information("Found {Count} available endpoint(s):", endpoints.Count);
                    var index = 1;
                    foreach (var ep in endpoints)
                    {
                        Logger.Information("  [{Index}] {Url}", index, ep.EndpointUrl);
                        Logger.Information("      Security: {Policy} / {Mode}",
                            ep.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None",
                            ep.SecurityMode);
                        index++;
                    }
                    break; // Success, exit retry loop
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // User cancelled, don't retry
            }
            catch (ServiceResultException sre)
            {
                lastException = sre;
                var statusCode = sre.StatusCode;

                // Log specific OPC UA error
                Logger.Warning("OPC UA Error (attempt {Attempt}): {Status} - {Message}",
                    attempt, StatusCodes.GetBrowseName(statusCode), sre.Message);

                // Handle specific error codes
                if (statusCode == StatusCodes.BadNotConnected ||
                    statusCode == StatusCodes.BadServerNotConnected ||
                    statusCode == StatusCodes.BadConnectionClosed)
                {
                    Logger.Error("Cannot connect to OPC UA server at {Endpoint}", endpointUrl);
                    Logger.Error("  → Please check:");
                    Logger.Error("    1. PLC is powered on and running");
                    Logger.Error("    2. Network connection to PLC is available");
                    Logger.Error("    3. OPC UA Server is enabled on PLC");
                    Logger.Error("    4. Firewall is not blocking port 4840");

                    if (attempt < 3)
                    {
                        Logger.Information("Retrying in {Delay} seconds...", attempt * 2);
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                        continue;
                    }
                }
                else if (statusCode == StatusCodes.BadTimeout)
                {
                    Logger.Warning("Connection timeout - PLC may be slow or network congested");
                    if (attempt < 3)
                    {
                        Logger.Information("Retrying with longer timeout...");
                        continue;
                    }
                }
                else if (statusCode == StatusCodes.BadSecureChannelClosed)
                {
                    Logger.Warning("Secure channel closed - will try without security");
                    break; // Try fallback
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                Logger.Warning("Discovery error (attempt {Attempt}): {Error}", attempt, ex.Message);

                if (attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                }
            }
        }

        // If discovery failed, try fallback method
        if (endpoints == null || endpoints.Count == 0)
        {
            Logger.Warning("Discovery client failed, trying CoreClientUtils fallback...");

            try
            {
                var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
                Logger.Information("Selected endpoint using fallback: {Endpoint}", endpoint.EndpointUrl);
                return endpoint;
            }
            catch (Exception fallbackEx)
            {
                Logger.Error("All connection attempts failed to {Endpoint}", endpointUrl);

                // Throw user-friendly exception
                var errorMessage = GetConnectionErrorMessage(endpointUrl, lastException ?? fallbackEx);
                throw new InvalidOperationException(errorMessage, lastException ?? fallbackEx);
            }
        }

        // Select the best matching endpoint based on security settings
        return SelectBestEndpoint(endpoints);
    }

    /// <summary>
    /// Get user-friendly error message for connection failures
    /// </summary>
    private string GetConnectionErrorMessage(string endpointUrl, Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Cannot connect to OPC UA server at {endpointUrl}");
        sb.AppendLine();
        sb.AppendLine("Possible causes:");
        sb.AppendLine("  1. PLC is not powered on or not running");
        sb.AppendLine("  2. Network connection is unavailable");
        sb.AppendLine("  3. OPC UA Server is not enabled on PLC");
        sb.AppendLine("  4. Firewall blocking OPC UA port (default: 4840)");
        sb.AppendLine("  5. Incorrect endpoint URL");
        sb.AppendLine();
        sb.AppendLine($"Technical details: {ex.Message}");

        return sb.ToString();
    }

    /// <summary>
    /// Select the best endpoint based on device security configuration
    /// NOTE: For S7-1200/1500, prefer No Security to avoid slow Secure Channel renewal issues
    /// (S7-1200 takes 6-14 seconds for renewal with Basic256Sha256, causing BadTimeout errors)
    /// </summary>
    private EndpointDescription SelectBestEndpoint(EndpointDescriptionCollection endpoints)
    {
        var useSecurity = _device.SecurityPolicy != OpcUaSecurityPolicy.None;
        var targetSecurityPolicy = GetSecurityPolicyUri(_device.SecurityPolicy);
        var targetSecurityMode = (MessageSecurityMode)(int)_device.SecurityMode;

        // PRIORITY 1: If no security required OR for better S7-1200 stability, prefer None security endpoint
        // This avoids the slow Secure Channel renewal problem with Siemens PLCs
        if (!useSecurity)
        {
            var noneEndpoint = endpoints
                .Where(e => e.SecurityMode == MessageSecurityMode.None)
                .OrderBy(e => e.SecurityLevel)
                .FirstOrDefault();

            if (noneEndpoint != null)
            {
                Logger.Information("Selected No Security endpoint (recommended for S7-1200/1500 stability)");
                Logger.Debug("  → Endpoint: {Endpoint}", noneEndpoint.EndpointUrl);
                return noneEndpoint;
            }

            // If no "None" security endpoint, return lowest security level
            Logger.Warning("No endpoint with SecurityMode.None found, using lowest security endpoint");
            Logger.Warning("⚠ For S7-1200/1500, this may cause connection stability issues");
            return endpoints.OrderBy(e => e.SecurityLevel).First();
        }

        // PRIORITY 2: Try to match exact security policy and mode
        var exactMatch = endpoints
            .Where(e => e.SecurityPolicyUri == targetSecurityPolicy &&
                        e.SecurityMode == targetSecurityMode)
            .OrderByDescending(e => e.SecurityLevel)
            .FirstOrDefault();

        if (exactMatch != null)
        {
            Logger.Information("Selected exact match endpoint: Policy={Policy}, Mode={Mode}",
                exactMatch.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None",
                exactMatch.SecurityMode);

            // Warn about potential S7-1200 issues with security
            if (targetSecurityPolicy.Contains("Basic256Sha256"))
            {
                Logger.Warning("⚠ Using Basic256Sha256 with S7-1200 may cause slow Secure Channel renewal (6-14s)");
                Logger.Warning("  Consider using No Security for better stability");
            }

            return exactMatch;
        }

        // PRIORITY 3: Try to match security policy only
        var policyMatch = endpoints
            .Where(e => e.SecurityPolicyUri == targetSecurityPolicy &&
                        e.SecurityMode != MessageSecurityMode.None)
            .OrderByDescending(e => e.SecurityLevel)
            .FirstOrDefault();

        if (policyMatch != null)
        {
            Logger.Information("Selected policy match endpoint: Policy={Policy}, Mode={Mode}",
                policyMatch.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None",
                policyMatch.SecurityMode);
            return policyMatch;
        }

        // PRIORITY 4: Fallback - prefer lighter security for S7-1200 compatibility
        // Basic128Rsa15 and Basic256 are faster than Basic256Sha256
        var lighterSecurity = endpoints
            .Where(e => e.SecurityMode != MessageSecurityMode.None &&
                        (e.SecurityPolicyUri?.Contains("Basic128") == true ||
                         e.SecurityPolicyUri?.Contains("Basic256") == true &&
                         !e.SecurityPolicyUri.Contains("Sha256")))
            .OrderBy(e => e.SecurityLevel)
            .FirstOrDefault();

        if (lighterSecurity != null)
        {
            Logger.Information("Selected lighter security endpoint for S7-1200 compatibility: {Policy}",
                lighterSecurity.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None");
            return lighterSecurity;
        }

        // PRIORITY 5: Select any secure endpoint (may have slow renewal on S7-1200)
        var anySecure = endpoints
            .Where(e => e.SecurityMode != MessageSecurityMode.None)
            .OrderBy(e => e.SecurityLevel) // Prefer lower security for speed
            .FirstOrDefault();

        if (anySecure != null)
        {
            Logger.Warning("Using secure endpoint: {Policy} (may be slow on S7-1200)",
                anySecure.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None");
            return anySecure;
        }

        // Last resort: return first available endpoint
        Logger.Warning("No matching endpoint found, using first available");
        return endpoints.First();
    }

    /// <summary>
    /// Convert OpcUaSecurityPolicy enum to security policy URI
    /// </summary>
    private static string GetSecurityPolicyUri(OpcUaSecurityPolicy policy)
    {
        return policy switch
        {
            OpcUaSecurityPolicy.None => SecurityPolicies.None,
            OpcUaSecurityPolicy.Basic128Rsa15 => SecurityPolicies.Basic128Rsa15,
            OpcUaSecurityPolicy.Basic256 => SecurityPolicies.Basic256,
            OpcUaSecurityPolicy.Basic256Sha256 => SecurityPolicies.Basic256Sha256,
            OpcUaSecurityPolicy.Aes128Sha256RsaOaep => SecurityPolicies.Aes128_Sha256_RsaOaep,
            OpcUaSecurityPolicy.Aes256Sha256RsaPss => SecurityPolicies.Aes256_Sha256_RsaPss,
            _ => SecurityPolicies.None
        };
    }

    private async Task<ApplicationConfiguration> CreateApplicationConfigurationAsync()
    {
        var config = new ApplicationConfiguration
        {
            ApplicationName = "OPC UA Communication Engine",
            ApplicationUri = Utils.Format(@"urn:{0}:OpcUaCommunicationEngine", System.Net.Dns.GetHostName()),
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = @"Directory",
                    StorePath = @"./Certificates/own",
                    SubjectName = "CN=OPC UA Communication Engine, C=VN, S=HCMC, O=MyCompany"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = @"Directory",
                    StorePath = @"./Certificates/issuers"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = @"Directory",
                    StorePath = @"./Certificates/trusted"
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = @"Directory",
                    StorePath = @"./Certificates/rejected"
                },
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = true
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            // OperationTimeout = 60000ms: S7-1200 takes 6-14 seconds for Secure Channel renewal with Basic256Sha256
            // This timeout must be longer than the renewal time to avoid BadTimeout errors
            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 60000,           // 60 seconds for S7-1200 slow operations
                SecurityTokenLifetime = 3600000     // 1 hour Secure Channel lifetime
            },
            ClientConfiguration = new ClientConfiguration
            {
                DefaultSessionTimeout = 120000,     // 2 minutes session timeout
                MinSubscriptionLifetime = 10000     // 10 seconds minimum subscription lifetime
            }
        };

        await config.Validate(ApplicationType.Client);

        var haveAppCertificate = await config.SecurityConfiguration.ApplicationCertificate.Find(true);
        if (haveAppCertificate == null)
        {
            Logger.Information("Creating new application certificate...");
            var cert = CertificateFactory.CreateCertificate(
                config.SecurityConfiguration.ApplicationCertificate.StoreType,
                config.SecurityConfiguration.ApplicationCertificate.StorePath,
                null,
                config.ApplicationUri,
                config.ApplicationName,
                config.SecurityConfiguration.ApplicationCertificate.SubjectName,
                null,
                CertificateFactory.DefaultKeySize,
                DateTime.UtcNow - TimeSpan.FromDays(1),
                CertificateFactory.DefaultLifeTime,
                CertificateFactory.DefaultHashSize,
                false,
                null,
                null);
        }

        config.CertificateValidator = new CertificateValidator();
        config.CertificateValidator.CertificateValidation += (validator, e) =>
        {
            e.Accept = true;
        };

        return config;
    }

    private UserIdentity GetUserIdentity()
    {
        if (!string.IsNullOrEmpty(_device.UserName))
        {
            return new UserIdentity(_device.UserName, _device.Password ?? string.Empty);
        }
        return new UserIdentity(new AnonymousIdentityToken());
    }

    private TagQuality MapStatusCode(StatusCode statusCode)
    {
        if (StatusCode.IsGood(statusCode)) return TagQuality.Good;
        if (StatusCode.IsBad(statusCode)) return TagQuality.Bad;
        return TagQuality.Uncertain;
    }

    private void StartAutoReconnect()
    {
        lock (_lock)
        {
            // Prevent multiple reconnect loops
            if (_isReconnecting || _reconnectCts != null || _isDisconnecting || _disposed)
            {
                return;
            }

            _isReconnecting = true;
            _reconnectCts = new CancellationTokenSource();
        }

        _ = AutoReconnectLoopAsync(_reconnectCts.Token);
    }

    private void StopAutoReconnect()
    {
        lock (_lock)
        {
            _isReconnecting = false;

            if (_reconnectCts != null)
            {
                try
                {
                    _reconnectCts.Cancel();
                    _reconnectCts.Dispose();
                }
                catch { /* Ignore */ }
                _reconnectCts = null;
            }
        }
    }

    private async Task AutoReconnectLoopAsync(CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = _device.MaxReconnectAttempts > 0 ? _device.MaxReconnectAttempts : 10;
        var baseDelay = Math.Max(_device.ReconnectInterval, 5000); // Minimum 5 seconds

        Logger.Information("Starting auto-reconnect for {PlcName} (max {MaxRetries} attempts, interval {Interval}ms)",
            _device.Name, maxRetries, baseDelay);

        try
        {
            while (!cancellationToken.IsCancellationRequested && retryCount < maxRetries && _isReconnecting)
            {
                try
                {
                    // Exponential backoff with max 30 seconds
                    var delay = Math.Min(baseDelay * (int)Math.Pow(1.5, retryCount), 30000);
                    Logger.Debug("Waiting {Delay}ms before reconnect attempt...", delay);

                    await Task.Delay(delay, cancellationToken);

                    if (cancellationToken.IsCancellationRequested || !_isReconnecting) break;

                    retryCount++;
                    Logger.Information("Auto-reconnect attempt {Attempt}/{MaxAttempts} for {PlcName}...",
                        retryCount, maxRetries, _device.Name);

                    ConnectionState = PlcConnectionState.Reconnecting;

                    // Disconnect cleanly first (without triggering new reconnect)
                    var wasReconnecting = _isReconnecting;
                    _isReconnecting = false; // Temporarily disable to prevent nested loops

                    if (_session != null)
                    {
                        try
                        {
                            _session.KeepAlive -= Session_KeepAlive;
                            _session.Notification -= Session_Notification;
                            _session.PublishError -= Session_PublishError;
                            _session.Dispose();
                        }
                        catch { /* Ignore */ }
                        _session = null;
                    }

                    // Clear old subscriptions - they're invalid after session close
                    foreach (var kvp in _subscriptions.ToList())
                    {
                        try { kvp.Value.Dispose(); } catch { }
                    }
                    _subscriptions.Clear();
                    _monitoredItemMapping.Clear();

                    _isReconnecting = wasReconnecting;

                    // Try to connect
                    var connected = await ConnectAsync(cancellationToken);
                    if (connected)
                    {
                        Logger.Information("✓ Auto-reconnect successful for {PlcName}", _device.Name);
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Warning("Auto-reconnect error for {PlcName}: {Error}", _device.Name, ex.Message);
                }
            }

            if (retryCount >= maxRetries)
            {
                Logger.Error("✗ Max reconnect attempts ({MaxRetries}) reached for {PlcName}", maxRetries, _device.Name);
                ConnectionState = PlcConnectionState.Error;
            }
        }
        finally
        {
            lock (_lock)
            {
                _isReconnecting = false;
                _reconnectCts = null;
            }
        }
    }

    private void RaiseError(string message, Exception? ex = null, bool isCritical = false)
    {
        try
        {
            ErrorOccurred?.Invoke(this, new PlcErrorEventArgs
            {
                PlcId = _device.Id,
                PlcName = _device.Name,
                ErrorMessage = message,
                Exception = ex,
                IsCritical = isCritical
            });
        }
        catch (Exception handlerEx)
        {
            Logger.Error(handlerEx, "Error in ErrorOccurred handler for {PlcName}: {Error}", _device.Name, handlerEx.Message);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose reconnect handler
        lock (_lock)
        {
            _reconnectHandler?.Dispose();
            _reconnectHandler = null;
        }

        StopAutoReconnect();
        DisconnectAsync().GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }

    #endregion
}
