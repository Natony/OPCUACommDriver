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

public class PlcsControllerTests
{
    private readonly MockPlcManager _mockPlcManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly PlcsController _controller;

    public PlcsControllerTests()
    {
        _mockPlcManager = new MockPlcManager();
        _mockLogger = new Mock<ILogger>();
        _controller = new PlcsController(_mockPlcManager, _mockLogger.Object);
    }

    #region GetAll Tests

    [Fact]
    public void GetAll_WithNoPlcs_ReturnsEmptyList()
    {
        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<PlcDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_WithMultiplePlcs_ReturnsAllPlcs()
    {
        // Arrange
        _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        _mockPlcManager.AddMockPlc("plc2", "PLC 2", "opc.tcp://localhost:4841");

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<PlcDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);
        response.Data![0].Id.Should().Be("plc1");
        response.Data![1].Id.Should().Be("plc2");
    }

    [Fact]
    public void GetAll_ReturnsCorrectConnectionState()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;

        // Act
        var result = _controller.GetAll();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<PlcDto>>>().Subject;
        response.Data![0].ConnectionState.Should().Be("Connected");
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_WithExistingPlc_ReturnsPlc()
    {
        // Arrange
        _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");

        // Act
        var result = _controller.GetById("plc1");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<PlcDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Id.Should().Be("plc1");
        response.Data!.Name.Should().Be("PLC 1");
    }

    [Fact]
    public void GetById_WithNonExistingPlc_ReturnsNotFound()
    {
        // Act
        var result = _controller.GetById("nonexistent");

        // Assert
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var response = notFoundResult.Value.Should().BeOfType<ApiResponse<PlcDto>>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("not found");
    }

    #endregion

    #region GetStatus Tests

    [Fact]
    public void GetStatus_ReturnsAllPlcStatuses()
    {
        // Arrange
        var connection1 = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        var connection2 = _mockPlcManager.AddMockPlc("plc2", "PLC 2", "opc.tcp://localhost:4841");
        connection1.ConnectionState = PlcConnectionState.Connected;
        connection2.ConnectionState = PlcConnectionState.Disconnected;

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<ConnectionStatusDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(2);
        response.Data![0].IsConnected.Should().BeTrue();
        response.Data![1].IsConnected.Should().BeFalse();
    }

    #endregion

    #region Connect Tests

    [Fact]
    public async Task Connect_WithExistingPlc_ConnectsSuccessfully()
    {
        // Arrange
        _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");

        // Act
        var result = await _controller.Connect("plc1", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_WithNonExistingPlc_ReturnsFalse()
    {
        // Act
        var result = await _controller.Connect("nonexistent", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        response.Data.Should().BeFalse();
    }

    #endregion

    #region Disconnect Tests

    [Fact]
    public async Task Disconnect_WithConnectedPlc_DisconnectsSuccessfully()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        await connection.ConnectAsync();

        // Act
        var result = await _controller.Disconnect("plc1", CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeTrue();
    }

    #endregion

    #region ConnectAll Tests

    [Fact]
    public async Task ConnectAll_ConnectsAllPlcs()
    {
        // Arrange
        _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        _mockPlcManager.AddMockPlc("plc2", "PLC 2", "opc.tcp://localhost:4841");

        // Act
        var result = await _controller.ConnectAll(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<int>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().Be(2);
    }

    #endregion

    #region DisconnectAll Tests

    [Fact]
    public async Task DisconnectAll_DisconnectsAllPlcs()
    {
        // Arrange
        var conn1 = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        var conn2 = _mockPlcManager.AddMockPlc("plc2", "PLC 2", "opc.tcp://localhost:4841");
        await conn1.ConnectAsync();
        await conn2.ConnectAsync();

        // Act
        var result = await _controller.DisconnectAll(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<bool>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeTrue();
    }

    #endregion

    #region Browse Tests

    [Fact]
    public async Task Browse_WithConnectedPlc_ReturnsBrowseNodes()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;

        // Act
        var result = await _controller.Browse("plc1", null, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<List<BrowseNodeDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Browse_WithDisconnectedPlc_ReturnsBadRequest()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Disconnected;

        // Act
        var result = await _controller.Browse("plc1", null, CancellationToken.None);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<ApiResponse<List<BrowseNodeDto>>>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("not connected");
    }

    [Fact]
    public async Task Browse_WithNonExistingPlc_ReturnsNotFound()
    {
        // Act
        var result = await _controller.Browse("nonexistent", null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion
}
