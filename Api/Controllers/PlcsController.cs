using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using Serilog;

namespace OpcUaCommunicationEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlcsController : ControllerBase
{
    private readonly IPlcManager _plcManager;
    private readonly ILogger _logger;

    public PlcsController(IPlcManager plcManager, ILogger logger)
    {
        _plcManager = plcManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all PLCs
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<PlcDto>>> GetAll()
    {
        try
        {
            var plcs = _plcManager.Connections.Select(c => new PlcDto
            {
                Id = c.Device.Id,
                Name = c.Device.Name,
                EndpointUrl = c.Device.EndpointUrl,
                ConnectionState = c.ConnectionState.ToString(),
                TagCount = c.Device.Tags.Count
            }).ToList();

            return Ok(ApiResponse<List<PlcDto>>.Ok(plcs));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting PLCs");
            return StatusCode(500, ApiResponse<List<PlcDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get PLC by ID
    /// </summary>
    [HttpGet("{plcId}")]
    public ActionResult<ApiResponse<PlcDto>> GetById(string plcId)
    {
        try
        {
            var connection = _plcManager.GetConnection(plcId);
            if (connection == null)
                return NotFound(ApiResponse<PlcDto>.Fail($"PLC '{plcId}' not found"));

            var plc = new PlcDto
            {
                Id = connection.Device.Id,
                Name = connection.Device.Name,
                EndpointUrl = connection.Device.EndpointUrl,
                ConnectionState = connection.ConnectionState.ToString(),
                TagCount = connection.Device.Tags.Count
            };

            return Ok(ApiResponse<PlcDto>.Ok(plc));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting PLC {PlcId}", plcId);
            return StatusCode(500, ApiResponse<PlcDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get connection status of all PLCs
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<List<ConnectionStatusDto>>> GetStatus()
    {
        try
        {
            var statuses = _plcManager.Connections.Select(c => new ConnectionStatusDto
            {
                PlcId = c.Device.Id,
                PlcName = c.Device.Name,
                State = c.ConnectionState.ToString(),
                IsConnected = c.ConnectionState == PlcConnectionState.Connected
            }).ToList();

            return Ok(ApiResponse<List<ConnectionStatusDto>>.Ok(statuses));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting PLC status");
            return StatusCode(500, ApiResponse<List<ConnectionStatusDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Connect to a PLC
    /// </summary>
    [HttpPost("{plcId}/connect")]
    public async Task<ActionResult<ApiResponse<bool>>> Connect(string plcId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _plcManager.ConnectAsync(plcId, cancellationToken);
            return Ok(ApiResponse<bool>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error connecting to PLC {PlcId}", plcId);
            return StatusCode(500, ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Disconnect from a PLC
    /// </summary>
    [HttpPost("{plcId}/disconnect")]
    public async Task<ActionResult<ApiResponse<bool>>> Disconnect(string plcId, CancellationToken cancellationToken)
    {
        try
        {
            await _plcManager.DisconnectAsync(plcId, cancellationToken);
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from PLC {PlcId}", plcId);
            return StatusCode(500, ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Connect to all PLCs
    /// </summary>
    [HttpPost("connect-all")]
    public async Task<ActionResult<ApiResponse<int>>> ConnectAll(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _plcManager.ConnectAllAsync(cancellationToken);
            return Ok(ApiResponse<int>.Ok(count));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error connecting to all PLCs");
            return StatusCode(500, ApiResponse<int>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Disconnect from all PLCs
    /// </summary>
    [HttpPost("disconnect-all")]
    public async Task<ActionResult<ApiResponse<bool>>> DisconnectAll(CancellationToken cancellationToken)
    {
        try
        {
            await _plcManager.DisconnectAllAsync(cancellationToken);
            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from all PLCs");
            return StatusCode(500, ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Browse OPC UA server nodes
    /// </summary>
    [HttpGet("{plcId}/browse")]
    public async Task<ActionResult<ApiResponse<List<BrowseNodeDto>>>> Browse(
        string plcId,
        [FromQuery] string? nodeId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = _plcManager.GetConnection(plcId);
            if (connection == null)
                return NotFound(ApiResponse<List<BrowseNodeDto>>.Fail($"PLC '{plcId}' not found"));

            if (connection.ConnectionState != PlcConnectionState.Connected)
                return BadRequest(ApiResponse<List<BrowseNodeDto>>.Fail("PLC is not connected"));

            var nodes = await _plcManager.BrowseAsync(plcId, nodeId, cancellationToken);
            var dtos = nodes.Select(n => new BrowseNodeDto
            {
                NodeId = n.NodeId,
                DisplayName = n.DisplayName,
                NodeClass = n.NodeClass,
                DataType = n.DataType,
                HasChildren = n.HasChildren
            }).ToList();

            return Ok(ApiResponse<List<BrowseNodeDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error browsing PLC {PlcId}", plcId);
            return StatusCode(500, ApiResponse<List<BrowseNodeDto>>.Fail(ex.Message));
        }
    }
}
