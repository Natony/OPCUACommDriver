using System.Collections.Concurrent;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.OpcUa;

/// <summary>
/// OPC UA Connection cho 1 PLC
/// Sử dụng OPC Foundation library
/// </summary>
public class PlcConnection : IPlcConnection
{
    private readonly ILogger _logger;
    private readonly PlcDevice _device;
    private readonly object _lock = new();
    
    private Session? _session;
    private ApplicationConfiguration? _appConfig;
    private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    // Subscriptions management
    private readonly ConcurrentDictionary<string, Subscription> _subscriptions = new();
    private readonly ConcurrentDictionary<uint, (string TagId, string NodeId)> _monitoredItemMapping = new();

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
                
                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                {
                    PlcId = _device.Id,
                    PlcName = _device.Name,
                    OldState = oldState,
                    NewState = value
                });
            }
        }
    }

    public bool IsConnected => _session?.Connected == true && ConnectionState == PlcConnectionState.Connected;

    public string? SessionId => _session?.SessionId?.ToString();

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

    public PlcConnection(PlcDevice device, ILogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.Debug("PlcConnection created for {PlcName} ({Endpoint})", device.Name, device.EndpointUrl);
    }

    #endregion

    #region Connection Methods

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlcConnection));

        if (IsConnected)
        {
            _logger.Warning("Already connected to {PlcName}", _device.Name);
            return true;
        }

        try
        {
            ConnectionState = PlcConnectionState.Connecting;
            _logger.Information("═══════════════════════════════════════════════════════════");
            _logger.Information("Connecting to PLC: {PlcName}", _device.Name);
            _logger.Information("Target Endpoint: {Endpoint}", _device.EndpointUrl);
            _logger.Information("Security Policy: {Policy}, Mode: {Mode}", _device.SecurityPolicy, _device.SecurityMode);

            // Initialize OPC UA Application Configuration
            _appConfig = await CreateApplicationConfigurationAsync();

            // Discover and select endpoint with proper error handling
            var selectedEndpoint = await SelectEndpointAsync(_device.EndpointUrl, cancellationToken);

            _logger.Information("Selected Endpoint: {Endpoint}", selectedEndpoint.EndpointUrl);
            _logger.Information("  → Security Policy: {Policy}", selectedEndpoint.SecurityPolicyUri);
            _logger.Information("  → Security Mode: {Mode}", selectedEndpoint.SecurityMode);
            _logger.Information("  → Transport Profile: {Profile}", selectedEndpoint.TransportProfileUri);

            // Create endpoint configuration
            var endpointConfig = EndpointConfiguration.Create(_appConfig);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfig);

            // Create session
            _logger.Information("Creating OPC UA Session...");
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

            _logger.Information("✓ Connected successfully to {PlcName}", _device.Name);
            _logger.Information("  → Session ID: {SessionId}", _session.SessionId);
            _logger.Information("  → Session Name: {SessionName}", _session.SessionName);
            _logger.Information("  → Server URI: {ServerUri}", _session.Endpoint.Server.ApplicationUri);
            _logger.Information("═══════════════════════════════════════════════════════════");

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
            _logger.Error(ex, "Failed to connect to {PlcName}: {Error}", _device.Name, ex.Message);
            
            RaiseError(ex.Message, ex, isCritical: true);

            // Start auto-reconnect if enabled
            if (_device.AutoReconnect)
            {
                StartAutoReconnect();
            }

            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed) return;

        try
        {
            // Stop auto-reconnect
            StopAutoReconnect();

            if (_session != null)
            {
                ConnectionState = PlcConnectionState.Disconnecting;
                _logger.Information("═══════════════════════════════════════════════════════════");
                _logger.Information("Disconnecting from PLC: {PlcName}", _device.Name);
                _logger.Information("  → Session ID: {SessionId}", _session.SessionId);

                // Remove subscriptions
                foreach (var subscription in _subscriptions.Values)
                {
                    try
                    {
                        _session.RemoveSubscription(subscription);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Error removing subscription during disconnect");
                    }
                }
                _subscriptions.Clear();
                _monitoredItemMapping.Clear();

                // Unsubscribe events
                _session.KeepAlive -= Session_KeepAlive;
                _session.Notification -= Session_Notification;
                _session.PublishError -= Session_PublishError;

                // Close session
                await _session.CloseAsync();
                _session.Dispose();
                _session = null;

                _logger.Information("✓ Disconnected from {PlcName}", _device.Name);
                _logger.Information("═══════════════════════════════════════════════════════════");
            }

            ConnectionState = PlcConnectionState.Disconnected;
            LastDisconnectedTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from {PlcName}", _device.Name);
            ConnectionState = PlcConnectionState.Disconnected;
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
            _logger.Warning("Cannot read tag - not connected to {PlcName}", _device.Name);
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
            _logger.Error(ex, "Error reading tag {NodeId} from {PlcName}", nodeId, _device.Name);
            return null;
        }
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var results = new List<TagValue>();
        
        if (!IsConnected || _session == null)
        {
            _logger.Warning("Cannot read tags - not connected to {PlcName}", _device.Name);
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
            _logger.Error(ex, "Error reading tags from {PlcName}", _device.Name);
        }

        return results;
    }

    public async Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            _logger.Warning("Cannot write tag - not connected to {PlcName}", _device.Name);
            return false;
        }

        try
        {
            var nodesToWrite = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
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
                _logger.Warning("Write to {NodeId} failed: {StatusCode}", nodeId, results[0]);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to tag {NodeId} on {PlcName}", nodeId, _device.Name);
            return false;
        }
    }

    public async Task<IReadOnlyList<bool>> WriteTagsAsync(IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        var results = new List<bool>();
        
        if (!IsConnected || _session == null)
        {
            _logger.Warning("Cannot write tags - not connected to {PlcName}", _device.Name);
            return items.Select(_ => false).ToList();
        }

        try
        {
            var nodesToWrite = new WriteValueCollection();
            
            foreach (var (nodeId, value) in items)
            {
                nodesToWrite.Add(new WriteValue
                {
                    NodeId = new NodeId(nodeId),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
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
            _logger.Error(ex, "Error writing tags to {PlcName}", _device.Name);
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
            _logger.Warning("Cannot create subscription - not connected to {PlcName}", _device.Name);
            return false;
        }

        try
        {
            if (_subscriptions.ContainsKey(group.Id))
            {
                _logger.Warning("Subscription {GroupName} already exists", group.Name);
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

            _logger.Information("Creating subscription group: {GroupName} (Interval: {Interval}ms)",
                group.Name, group.PublishingInterval);

            if (!tags.Any())
            {
                _logger.Warning("  → No enabled tags for subscription {GroupName}", group.Name);
            }
            else
            {
                _logger.Information("  → Subscribing to {Count} tag(s):", tags.Count);
            }

            foreach (var tag in tags)
            {
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

                _monitoredItemMapping[monitoredItem.ClientHandle] = (tag.Id, tag.NodeId);

                _logger.Information("     • {TagName} → NodeId: {NodeId} (ScanRate: {Rate}ms)",
                    tag.Name, tag.NodeId, tag.ScanRate);
            }

            _session.AddSubscription(subscription);
            await subscription.CreateAsync(cancellationToken);

            if (subscription.MonitoredItemCount > 0)
            {
                await subscription.ApplyChangesAsync(cancellationToken);

                // Log status of each monitored item
                _logger.Information("  → Monitored items status:");
                foreach (var item in subscription.MonitoredItems)
                {
                    var status = item.Status?.Error?.StatusCode.ToString() ?? "Good";
                    var statusSymbol = StatusCode.IsGood(item.Status?.Error?.StatusCode ?? StatusCodes.Good) ? "✓" : "✗";
                    _logger.Information("     {Symbol} {Name}: {Status}", statusSymbol, item.DisplayName, status);
                }
            }

            _subscriptions[group.Id] = subscription;

            _logger.Information("✓ Subscription {GroupName} created with {Count} monitored items",
                group.Name, subscription.MonitoredItemCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating subscription {GroupName} on {PlcName}", group.Name, _device.Name);
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
                
                _logger.Information("Removed subscription {GroupId}", groupId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing subscription {GroupId}", groupId);
            return false;
        }
    }

    public async Task<bool> AddTagToSubscriptionAsync(string groupId, TagItem tag, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null) return false;

        try
        {
            if (!_subscriptions.TryGetValue(groupId, out var subscription))
            {
                _logger.Warning("Subscription {GroupId} not found", groupId);
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
            
            _logger.Debug("Added tag {TagName} to subscription {GroupId}", tag.Name, groupId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error adding tag {TagName} to subscription {GroupId}", tag.Name, groupId);
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
                
                _logger.Debug("Removed tag {TagId} from subscription {GroupId}", tagId, groupId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing tag {TagId} from subscription {GroupId}", tagId, groupId);
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
            _logger.Error(ex, "Error browsing node {NodeId}", nodeId);
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
            _logger.Error(ex, "Error getting node info for {NodeId}", nodeId);
            return null;
        }
    }

    #endregion

    #region Session Event Handlers

    private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            _logger.Warning("Keep-alive failed for {PlcName}: {Status}", _device.Name, e.Status);
            
            if (ConnectionState == PlcConnectionState.Connected)
            {
                ConnectionState = PlcConnectionState.Reconnecting;
                
                if (_device.AutoReconnect)
                {
                    StartAutoReconnect();
                }
            }
        }
        else
        {
            if (ConnectionState == PlcConnectionState.Reconnecting)
            {
                ConnectionState = PlcConnectionState.Connected;
            }
        }
    }

    private void Session_Notification(ISession session, NotificationEventArgs e)
    {
        // Handled by individual MonitoredItem notifications
    }

    private void Session_PublishError(ISession session, PublishErrorEventArgs e)
    {
        _logger.Warning("Publish error for {PlcName}: {Status}", _device.Name, e.Status);
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
                        // Log value change (Debug level to avoid flooding)
                        var qualitySymbol = quality == TagQuality.Good ? "●" : quality == TagQuality.Bad ? "✗" : "?";
                        _logger.Debug("[{PlcName}] {TagName} ({NodeId}) = {Value} [{Quality}]",
                            _device.Name,
                            tag.Name,
                            mapping.NodeId,
                            notification.Value.Value ?? "null",
                            qualitySymbol);

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
            _logger.Error(ex, "Error processing monitored item notification");
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

        // First, try to discover endpoints without requiring security (useSecurity = false)
        // This avoids BadSecureChannelClosed errors during discovery phase
        try
        {
            _logger.Information("Discovering available endpoints from {Endpoint}...", endpointUrl);

            // Use discovery client to get available endpoints
            using var discoveryClient = DiscoveryClient.Create(new Uri(endpointUrl), EndpointConfiguration.Create());
            endpoints = await Task.Run(() => discoveryClient.GetEndpoints(null), cancellationToken);

            _logger.Information("Found {Count} available endpoint(s):", endpoints.Count);
            var index = 1;
            foreach (var ep in endpoints)
            {
                _logger.Information("  [{Index}] {Url}", index, ep.EndpointUrl);
                _logger.Information("      Security: {Policy} / {Mode}",
                    ep.SecurityPolicyUri?.Split('#').LastOrDefault() ?? "None",
                    ep.SecurityMode);
                index++;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to discover endpoints using DiscoveryClient, trying CoreClientUtils...");

            // Fallback: try CoreClientUtils with useSecurity = false to avoid BadSecureChannelClosed
            try
            {
                var endpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
                _logger.Debug("Selected endpoint using CoreClientUtils fallback: {Endpoint}", endpoint.EndpointUrl);
                return endpoint;
            }
            catch (Exception fallbackEx)
            {
                _logger.Error(fallbackEx, "Failed to select endpoint from {EndpointUrl}", endpointUrl);
                throw new InvalidOperationException(
                    $"Cannot discover endpoints from {endpointUrl}. " +
                    $"Ensure the OPC UA server is running and accessible. " +
                    $"Original error: {ex.Message}", ex);
            }
        }

        if (endpoints == null || endpoints.Count == 0)
        {
            throw new InvalidOperationException($"No endpoints discovered from {endpointUrl}");
        }

        // Select the best matching endpoint based on security settings
        return SelectBestEndpoint(endpoints);
    }

    /// <summary>
    /// Select the best endpoint based on device security configuration
    /// </summary>
    private EndpointDescription SelectBestEndpoint(EndpointDescriptionCollection endpoints)
    {
        var useSecurity = _device.SecurityPolicy != OpcUaSecurityPolicy.None;
        var targetSecurityPolicy = GetSecurityPolicyUri(_device.SecurityPolicy);
        var targetSecurityMode = (MessageSecurityMode)(int)_device.SecurityMode;

        // If no security required, prefer None security endpoint
        if (!useSecurity)
        {
            var noneEndpoint = endpoints
                .Where(e => e.SecurityMode == MessageSecurityMode.None)
                .OrderBy(e => e.SecurityLevel)
                .FirstOrDefault();

            if (noneEndpoint != null)
            {
                _logger.Debug("Selected endpoint with no security: {Endpoint}", noneEndpoint.EndpointUrl);
                return noneEndpoint;
            }

            // If no "None" security endpoint, return lowest security level
            _logger.Warning("No endpoint with SecurityMode.None found, using lowest security endpoint");
            return endpoints.OrderBy(e => e.SecurityLevel).First();
        }

        // Try to match exact security policy and mode
        var exactMatch = endpoints
            .Where(e => e.SecurityPolicyUri == targetSecurityPolicy &&
                        e.SecurityMode == targetSecurityMode)
            .OrderByDescending(e => e.SecurityLevel)
            .FirstOrDefault();

        if (exactMatch != null)
        {
            _logger.Debug("Selected exact match endpoint: Policy={Policy}, Mode={Mode}",
                exactMatch.SecurityPolicyUri, exactMatch.SecurityMode);
            return exactMatch;
        }

        // Try to match security policy only
        var policyMatch = endpoints
            .Where(e => e.SecurityPolicyUri == targetSecurityPolicy &&
                        e.SecurityMode != MessageSecurityMode.None)
            .OrderByDescending(e => e.SecurityLevel)
            .FirstOrDefault();

        if (policyMatch != null)
        {
            _logger.Debug("Selected policy match endpoint: Policy={Policy}, Mode={Mode}",
                policyMatch.SecurityPolicyUri, policyMatch.SecurityMode);
            return policyMatch;
        }

        // Fallback: select highest security endpoint
        var highestSecurity = endpoints
            .Where(e => e.SecurityMode != MessageSecurityMode.None)
            .OrderByDescending(e => e.SecurityLevel)
            .FirstOrDefault();

        if (highestSecurity != null)
        {
            _logger.Warning("No matching security policy found, using highest security endpoint: {Policy}",
                highestSecurity.SecurityPolicyUri);
            return highestSecurity;
        }

        // Last resort: return first available endpoint
        _logger.Warning("No secure endpoint found, using first available endpoint");
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
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        await config.Validate(ApplicationType.Client);

        var haveAppCertificate = await config.SecurityConfiguration.ApplicationCertificate.Find(true);
        if (haveAppCertificate == null)
        {
            _logger.Information("Creating new application certificate...");
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
        if (_reconnectCts != null) return;

        _reconnectCts = new CancellationTokenSource();
        _ = AutoReconnectLoopAsync(_reconnectCts.Token);
    }

    private void StopAutoReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    private async Task AutoReconnectLoopAsync(CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = _device.MaxReconnectAttempts > 0 ? _device.MaxReconnectAttempts : int.MaxValue;

        while (!cancellationToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                await Task.Delay(_device.ReconnectInterval, cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;

                _logger.Information("Auto-reconnect attempt {Attempt} for {PlcName}...", 
                    retryCount + 1, _device.Name);

                ConnectionState = PlcConnectionState.Reconnecting;

                var connected = await ConnectAsync(cancellationToken);
                if (connected)
                {
                    _logger.Information("Auto-reconnect successful for {PlcName}", _device.Name);
                    _reconnectCts = null;
                    return;
                }

                retryCount++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Auto-reconnect error for {PlcName}", _device.Name);
                retryCount++;
            }
        }

        if (retryCount >= maxRetries)
        {
            _logger.Error("Max reconnect attempts reached for {PlcName}", _device.Name);
            ConnectionState = PlcConnectionState.Error;
        }

        _reconnectCts = null;
    }

    private void RaiseError(string message, Exception? ex = null, bool isCritical = false)
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

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAutoReconnect();
        DisconnectAsync().GetAwaiter().GetResult();
        
        GC.SuppressFinalize(this);
    }

    #endregion
}
