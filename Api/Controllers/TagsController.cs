using Microsoft.AspNetCore.Mvc;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using Serilog;

namespace OpcUaCommunicationEngine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly IPlcManager _plcManager;
    private readonly ILogger _logger;

    public TagsController(IPlcManager plcManager, ILogger logger)
    {
        _plcManager = plcManager;
        _logger = logger;
    }

    /// <summary>
    /// Get all tags from all PLCs
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<TagDto>>> GetAll()
    {
        try
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
                        Timestamp = tag.LastUpdate,
                        DataType = tag.DataType,
                        IsWritable = tag.IsWritable
                    });
                }
            }

            return Ok(ApiResponse<List<TagDto>>.Ok(tags));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting all tags");
            return StatusCode(500, ApiResponse<List<TagDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get tags for a specific PLC
    /// </summary>
    [HttpGet("plc/{plcId}")]
    public ActionResult<ApiResponse<List<TagDto>>> GetByPlc(string plcId)
    {
        try
        {
            var connection = _plcManager.GetConnection(plcId);
            if (connection == null)
                return NotFound(ApiResponse<List<TagDto>>.Fail($"PLC '{plcId}' not found"));

            var tags = connection.Device.Tags.Select(tag => new TagDto
            {
                Id = tag.Id,
                Name = tag.Name,
                NodeId = tag.NodeId,
                PlcId = connection.Device.Id,
                PlcName = connection.Device.Name,
                Value = tag.Value,
                Quality = tag.Quality.ToString(),
                Timestamp = tag.LastUpdate,
                DataType = tag.DataType,
                IsWritable = tag.IsWritable
            }).ToList();

            return Ok(ApiResponse<List<TagDto>>.Ok(tags));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting tags for PLC {PlcId}", plcId);
            return StatusCode(500, ApiResponse<List<TagDto>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get a specific tag by ID
    /// </summary>
    [HttpGet("{tagId}")]
    public ActionResult<ApiResponse<TagDto>> GetById(string tagId)
    {
        try
        {
            foreach (var connection in _plcManager.Connections)
            {
                var tag = connection.Device.Tags.FirstOrDefault(t => t.Id == tagId);
                if (tag != null)
                {
                    return Ok(ApiResponse<TagDto>.Ok(new TagDto
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        NodeId = tag.NodeId,
                        PlcId = connection.Device.Id,
                        PlcName = connection.Device.Name,
                        Value = tag.Value,
                        Quality = tag.Quality.ToString(),
                        Timestamp = tag.LastUpdate,
                        DataType = tag.DataType,
                        IsWritable = tag.IsWritable
                    }));
                }
            }

            return NotFound(ApiResponse<TagDto>.Fail($"Tag '{tagId}' not found"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting tag {TagId}", tagId);
            return StatusCode(500, ApiResponse<TagDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Read tag value from PLC
    /// </summary>
    [HttpGet("{plcId}/{nodeId}/read")]
    public async Task<ActionResult<ApiResponse<TagDto>>> ReadTag(
        string plcId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = _plcManager.GetConnection(plcId);
            if (connection == null)
                return NotFound(ApiResponse<TagDto>.Fail($"PLC '{plcId}' not found"));

            if (connection.ConnectionState != PlcConnectionState.Connected)
                return BadRequest(ApiResponse<TagDto>.Fail("PLC is not connected"));

            // URL decode the nodeId (it may contain special chars like = or ;)
            var decodedNodeId = Uri.UnescapeDataString(nodeId);

            var tagValue = await _plcManager.ReadTagAsync(plcId, decodedNodeId, cancellationToken);
            if (tagValue == null)
                return BadRequest(ApiResponse<TagDto>.Fail("Failed to read tag value"));

            var dto = new TagDto
            {
                NodeId = decodedNodeId,
                PlcId = plcId,
                Value = tagValue.Value,
                Quality = tagValue.Quality.ToString(),
                Timestamp = tagValue.SourceTimestamp
            };

            return Ok(ApiResponse<TagDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading tag {NodeId} from PLC {PlcId}", nodeId, plcId);
            return StatusCode(500, ApiResponse<TagDto>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Write value to a tag
    /// </summary>
    [HttpPost("{plcId}/{nodeId}/write")]
    public async Task<ActionResult<ApiResponse<bool>>> WriteTag(
        string plcId,
        string nodeId,
        [FromBody] WriteTagRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = _plcManager.GetConnection(plcId);
            if (connection == null)
                return NotFound(ApiResponse<bool>.Fail($"PLC '{plcId}' not found"));

            if (connection.ConnectionState != PlcConnectionState.Connected)
                return BadRequest(ApiResponse<bool>.Fail("PLC is not connected"));

            // URL decode the nodeId
            var decodedNodeId = Uri.UnescapeDataString(nodeId);

            var result = await _plcManager.WriteTagAsync(plcId, decodedNodeId, request.Value, cancellationToken);

            if (result)
            {
                _logger.Information("Tag {NodeId} written with value {Value}", decodedNodeId, request.Value);
                return Ok(ApiResponse<bool>.Ok(true));
            }
            else
            {
                return BadRequest(ApiResponse<bool>.Fail("Failed to write tag value"));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing tag {NodeId} on PLC {PlcId}", nodeId, plcId);
            return StatusCode(500, ApiResponse<bool>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Write multiple tags at once
    /// </summary>
    [HttpPost("{plcId}/write-multiple")]
    public async Task<ActionResult<ApiResponse<List<bool>>>> WriteMultipleTags(
        string plcId,
        [FromBody] WriteTagsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = _plcManager.GetConnection(plcId);
            if (connection == null)
                return NotFound(ApiResponse<List<bool>>.Fail($"PLC '{plcId}' not found"));

            if (connection.ConnectionState != PlcConnectionState.Connected)
                return BadRequest(ApiResponse<List<bool>>.Fail("PLC is not connected"));

            var items = request.Tags.Select(t => (t.NodeId, t.Value)).ToList();
            var results = await _plcManager.WriteTagsAsync(plcId, items, cancellationToken);

            return Ok(ApiResponse<List<bool>>.Ok(results.ToList()));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing multiple tags on PLC {PlcId}", plcId);
            return StatusCode(500, ApiResponse<List<bool>>.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get tag values with subscription (real-time updates available via SignalR)
    /// </summary>
    [HttpGet("subscribed")]
    public ActionResult<ApiResponse<List<TagDto>>> GetSubscribedTags()
    {
        try
        {
            var tags = new List<TagDto>();

            foreach (var connection in _plcManager.Connections)
            {
                // Only include tags from connected PLCs
                if (connection.ConnectionState == PlcConnectionState.Connected)
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
                            Timestamp = tag.LastUpdate,
                            DataType = tag.DataType,
                            IsWritable = tag.IsWritable
                        });
                    }
                }
            }

            return Ok(ApiResponse<List<TagDto>>.Ok(tags));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting subscribed tags");
            return StatusCode(500, ApiResponse<List<TagDto>>.Fail(ex.Message));
        }
    }
}
