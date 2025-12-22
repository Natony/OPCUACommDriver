using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OpcUaCommunicationEngine.Api.Controllers;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Tests.Mocks;
using Serilog;
using Xunit;

namespace OpcUaCommunicationEngine.Tests.Controllers;

public class TagsControllerTests
{
    private readonly MockPlcManager _mockPlcManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly TagsController _controller;

    public TagsControllerTests()
    {
        _mockPlcManager = new MockPlcManager();
        _mockLogger = new Mock<ILogger>();
        _controller = new TagsController(_mockPlcManager, _mockLogger.Object);
    }

    #region GetAll Tests

    [Fact]
    public void GetAll_WithNoTags_ReturnsEmptyList()
    {
        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<TagDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_WithTags_ReturnsAllTags()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.Device.Tags.Add(new TagItem
        {
            Id = "tag1",
            Name = "Tag 1",
            NodeId = "ns=4;i=1",
            Value = 100,
            Quality = TagQuality.Good,
            DataType = TagDataType.Int32,
            AccessMode = TagAccessMode.ReadWrite
        });
        connection.Device.Tags.Add(new TagItem
        {
            Id = "tag2",
            Name = "Tag 2",
            NodeId = "ns=4;i=2",
            Value = true,
            Quality = TagQuality.Good,
            DataType = TagDataType.Boolean,
            AccessMode = TagAccessMode.ReadWrite
        });

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<TagDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);
    }

    [Fact]
    public void GetAll_ReturnsTagsWithCorrectProperties()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.Device.Tags.Add(new TagItem
        {
            Id = "tag1",
            Name = "Temperature",
            NodeId = "ns=4;i=100",
            Value = 25.5,
            Quality = TagQuality.Good,
            DataType = TagDataType.Double,
            AccessMode = TagAccessMode.ReadWrite,
            Timestamp = DateTime.UtcNow
        });

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<TagDto>>>().Subject;
        var tag = response.Data![0];
        tag.Id.Should().Be("tag1");
        tag.Name.Should().Be("Temperature");
        tag.NodeId.Should().Be("ns=4;i=100");
        tag.Value.Should().Be(25.5);
        tag.Quality.Should().Be("Good");
        tag.IsWritable.Should().BeTrue();
    }

    #endregion

    #region GetByPlc Tests

    [Fact]
    public void GetByPlc_WithExistingPlc_ReturnsPlcTags()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.Device.Tags.Add(new TagItem { Id = "tag1", Name = "Tag 1", NodeId = "ns=4;i=1" });
        connection.Device.Tags.Add(new TagItem { Id = "tag2", Name = "Tag 2", NodeId = "ns=4;i=2" });

        // Act
        var result = _controller.GetByPlc("plc1");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<TagDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);
    }

    [Fact]
    public void GetByPlc_WithNonExistingPlc_ReturnsNotFound()
    {
        // Act
        var result = _controller.GetByPlc("nonexistent");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_WithExistingTag_ReturnsTag()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.Device.Tags.Add(new TagItem
        {
            Id = "tag1",
            Name = "Tag 1",
            NodeId = "ns=4;i=1",
            Value = 42,
            Quality = TagQuality.Good
        });

        // Act
        var result = _controller.GetById("tag1");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<TagDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Id.Should().Be("tag1");
        response.Data!.Value.Should().Be(42);
    }

    [Fact]
    public void GetById_WithNonExistingTag_ReturnsNotFound()
    {
        // Act
        var result = _controller.GetById("nonexistent");

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region ReadTag Tests

    [Fact]
    public async Task ReadTag_WithConnectedPlc_ReturnsTagValue()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;
        connection.SetTagValue("ns=4;i=1", 123);

        // Act
        var result = await _controller.ReadTag("plc1", "ns=4;i=1", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<TagDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Value.Should().Be(123);
    }

    [Fact]
    public async Task ReadTag_WithDisconnectedPlc_ReturnsBadRequest()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Disconnected;

        // Act
        var result = await _controller.ReadTag("plc1", "ns=4;i=1", CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<TagDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("not connected");
    }

    [Fact]
    public async Task ReadTag_WithNonExistingPlc_ReturnsNotFound()
    {
        // Act
        var result = await _controller.ReadTag("nonexistent", "ns=4;i=1", CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReadTag_DecodesUrlEncodedNodeId()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;
        connection.SetTagValue("ns=4;s=Test.Variable", 456);

        // Act - URL encoded nodeId
        var result = await _controller.ReadTag("plc1", "ns%3D4%3Bs%3DTest.Variable", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<TagDto>>().Subject;
        response.Success.Should().BeTrue();
    }

    #endregion

    #region WriteTag Tests

    [Fact]
    public async Task WriteTag_WithConnectedPlc_WritesSuccessfully()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;
        var request = new WriteTagRequest { Value = 999 };

        // Act
        var result = await _controller.WriteTag("plc1", "ns=4;i=1", request, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeTrue();
    }

    [Fact]
    public async Task WriteTag_WithDisconnectedPlc_ReturnsBadRequest()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Disconnected;
        var request = new WriteTagRequest { Value = 999 };

        // Act
        var result = await _controller.WriteTag("plc1", "ns=4;i=1", request, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        response.Success.Should().BeFalse();
    }

    [Fact]
    public async Task WriteTag_WithNonExistingPlc_ReturnsNotFound()
    {
        // Arrange
        var request = new WriteTagRequest { Value = 999 };

        // Act
        var result = await _controller.WriteTag("nonexistent", "ns=4;i=1", request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region WriteMultipleTags Tests

    [Fact]
    public async Task WriteMultipleTags_WithConnectedPlc_WritesAllSuccessfully()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;
        var request = new WriteTagsRequest
        {
            Tags = new List<WriteTagItem>
            {
                new WriteTagItem { NodeId = "ns=4;i=1", Value = 100 },
                new WriteTagItem { NodeId = "ns=4;i=2", Value = 200 },
                new WriteTagItem { NodeId = "ns=4;i=3", Value = 300 }
            }
        };

        // Act
        var result = await _controller.WriteMultipleTags("plc1", request, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<bool>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(3);
        response.Data!.All(r => r).Should().BeTrue();
    }

    [Fact]
    public async Task WriteMultipleTags_WithDisconnectedPlc_ReturnsBadRequest()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Disconnected;
        var request = new WriteTagsRequest
        {
            Tags = new List<WriteTagItem>
            {
                new WriteTagItem { NodeId = "ns=4;i=1", Value = 100 }
            }
        };

        // Act
        var result = await _controller.WriteMultipleTags("plc1", request, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetSubscribedTags Tests

    [Fact]
    public void GetSubscribedTags_ReturnsOnlyConnectedPlcTags()
    {
        // Arrange
        var connectedPlc = _mockPlcManager.AddMockPlc("plc1", "Connected PLC", "opc.tcp://localhost:4840");
        connectedPlc.ConnectionState = PlcConnectionState.Connected;
        connectedPlc.Device.Tags.Add(new TagItem { Id = "tag1", Name = "Tag 1", NodeId = "ns=4;i=1" });

        var disconnectedPlc = _mockPlcManager.AddMockPlc("plc2", "Disconnected PLC", "opc.tcp://localhost:4841");
        disconnectedPlc.ConnectionState = PlcConnectionState.Disconnected;
        disconnectedPlc.Device.Tags.Add(new TagItem { Id = "tag2", Name = "Tag 2", NodeId = "ns=4;i=2" });

        // Act
        var result = _controller.GetSubscribedTags();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<TagDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1);
        response.Data![0].Id.Should().Be("tag1");
    }

    #endregion
}
