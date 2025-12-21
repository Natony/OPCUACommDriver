using Microsoft.AspNetCore.SignalR;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using Serilog;

namespace OpcUaCommunicationEngine.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time PLC data updates
/// </summary>
public class PlcHub : Hub
{
    private readonly IPlcManager _plcManager;
    private readonly ILogger _logger;

    public PlcHub(IPlcManager plcManager, ILogger logger)
    {
        _plcManager = plcManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.Information("Client connected to PlcHub: {ConnectionId}", Context.ConnectionId);

        // Send current status to newly connected client
        await SendCurrentStatus();

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.Information("Client disconnected from PlcHub: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to specific PLC updates
    /// </summary>
    public async Task SubscribeToPlc(string plcId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"plc:{plcId}");
        _logger.Debug("Client {ConnectionId} subscribed to PLC {PlcId}", Context.ConnectionId, plcId);
    }

    /// <summary>
    /// Unsubscribe from specific PLC updates
    /// </summary>
    public async Task UnsubscribeFromPlc(string plcId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"plc:{plcId}");
        _logger.Debug("Client {ConnectionId} unsubscribed from PLC {PlcId}", Context.ConnectionId, plcId);
    }

    /// <summary>
    /// Subscribe to all PLC updates
    /// </summary>
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");
        _logger.Debug("Client {ConnectionId} subscribed to all PLCs", Context.ConnectionId);
    }

    /// <summary>
    /// Get current status of all PLCs
    /// </summary>
    public async Task GetStatus()
    {
        await SendCurrentStatus();
    }

    /// <summary>
    /// Get all current tag values
    /// </summary>
    public async Task GetAllTagValues()
    {
        var tags = new List<TagDto>();

        foreach (var connection in _plcManager.Connections)
        {
            foreach (var tag in connection.Device.Tags)
            {
                tags.Add(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    NodeId = tag.NodeId,
                    PlcId = connection.Device.Id,
                    PlcName = connection.Device.Name,
                    Value = tag.Value,
                    Quality = tag.Quality.ToString(),
                    Timestamp = tag.Timestamp,
                    DataType = tag.DataType.ToString(),
                    IsWritable = tag.CanWrite
                });
            }
        }

        await Clients.Caller.SendAsync("AllTagValues", tags);
    }

    /// <summary>
    /// Write a tag value
    /// </summary>
    public async Task WriteTag(string plcId, string nodeId, object value)
    {
        try
        {
            var result = await _plcManager.WriteTagAsync(plcId, nodeId, value);
            await Clients.Caller.SendAsync("WriteResult", new { plcId, nodeId, success = result });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing tag via SignalR");
            await Clients.Caller.SendAsync("WriteResult", new { plcId, nodeId, success = false, error = ex.Message });
        }
    }

    private async Task SendCurrentStatus()
    {
        var statuses = _plcManager.Connections.Select(c => new ConnectionStatusDto
        {
            PlcId = c.Device.Id,
            PlcName = c.Device.Name,
            State = c.ConnectionState.ToString(),
            IsConnected = c.ConnectionState == PlcConnectionState.Connected
        }).ToList();

        await Clients.Caller.SendAsync("PlcStatus", statuses);
    }
}

/// <summary>
/// Service to broadcast updates to SignalR clients
/// </summary>
public class PlcHubService
{
    private readonly IHubContext<PlcHub> _hubContext;
    private readonly ILogger _logger;

    public PlcHubService(IHubContext<PlcHub> hubContext, ILogger logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast tag value update to all subscribed clients
    /// </summary>
    public async Task BroadcastTagValueAsync(string plcId, string tagId, string nodeId, object? value, string quality, DateTime timestamp)
    {
        var update = new TagValueUpdate
        {
            PlcId = plcId,
            TagId = tagId,
            NodeId = nodeId,
            Value = value,
            Quality = quality,
            Timestamp = timestamp
        };

        // Send to clients subscribed to this specific PLC
        await _hubContext.Clients.Group($"plc:{plcId}").SendAsync("TagValueChanged", update);

        // Send to clients subscribed to all updates
        await _hubContext.Clients.Group("all").SendAsync("TagValueChanged", update);
    }

    /// <summary>
    /// Broadcast connection state change
    /// </summary>
    public async Task BroadcastConnectionStateAsync(string plcId, string plcName, string state, bool isConnected)
    {
        var status = new ConnectionStatusDto
        {
            PlcId = plcId,
            PlcName = plcName,
            State = state,
            IsConnected = isConnected,
            LastStateChange = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ConnectionStateChanged", status);
    }
}
