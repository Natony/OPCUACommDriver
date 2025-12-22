using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OpcUaCommunicationEngine.Api.Hubs;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Tests.Mocks;
using Serilog;
using Xunit;

namespace OpcUaCommunicationEngine.Tests.Hubs;

public class PlcHubTests
{
    private readonly MockPlcManager _mockPlcManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly PlcHub _hub;

    public PlcHubTests()
    {
        _mockPlcManager = new MockPlcManager();
        _mockLogger = new Mock<ILogger>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
        _mockClients.Setup(c => c.Caller).Returns(_mockClientProxy.Object);
        _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);

        _hub = new PlcHub(_mockPlcManager, _mockLogger.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    #region OnConnectedAsync Tests

    [Fact]
    public async Task OnConnectedAsync_SendsCurrentStatusToCaller()
    {
        // Arrange
        _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "PlcStatus",
                It.Is<object[]>(o => o.Length == 1),
                default),
            Times.Once);
    }

    #endregion

    #region SubscribeToPlc Tests

    [Fact]
    public async Task SubscribeToPlc_AddsClientToPlcGroup()
    {
        // Arrange
        var plcId = "plc1";

        // Act
        await _hub.SubscribeToPlc(plcId);

        // Assert
        _mockGroups.Verify(
            x => x.AddToGroupAsync("test-connection-id", $"plc:{plcId}", default),
            Times.Once);
    }

    #endregion

    #region UnsubscribeFromPlc Tests

    [Fact]
    public async Task UnsubscribeFromPlc_RemovesClientFromPlcGroup()
    {
        // Arrange
        var plcId = "plc1";

        // Act
        await _hub.UnsubscribeFromPlc(plcId);

        // Assert
        _mockGroups.Verify(
            x => x.RemoveFromGroupAsync("test-connection-id", $"plc:{plcId}", default),
            Times.Once);
    }

    #endregion

    #region SubscribeToAll Tests

    [Fact]
    public async Task SubscribeToAll_AddsClientToAllGroup()
    {
        // Act
        await _hub.SubscribeToAll();

        // Assert
        _mockGroups.Verify(
            x => x.AddToGroupAsync("test-connection-id", "all", default),
            Times.Once);
    }

    #endregion

    #region GetStatus Tests

    [Fact]
    public async Task GetStatus_SendsPlcStatusesToCaller()
    {
        // Arrange
        var conn1 = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        var conn2 = _mockPlcManager.AddMockPlc("plc2", "PLC 2", "opc.tcp://localhost:4841");
        conn1.ConnectionState = PlcConnectionState.Connected;
        conn2.ConnectionState = PlcConnectionState.Disconnected;

        // Act
        await _hub.GetStatus();

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "PlcStatus",
                It.Is<object[]>(o => o.Length == 1 && ((List<ConnectionStatusDto>)o[0]).Count == 2),
                default),
            Times.Once);
    }

    #endregion

    #region GetAllTagValues Tests

    [Fact]
    public async Task GetAllTagValues_SendsAllTagsToCaller()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.Device.Tags.Add(new TagItem
        {
            Id = "tag1",
            Name = "Tag 1",
            NodeId = "ns=4;i=1",
            Value = 100,
            Quality = TagQuality.Good
        });
        connection.Device.Tags.Add(new TagItem
        {
            Id = "tag2",
            Name = "Tag 2",
            NodeId = "ns=4;i=2",
            Value = true,
            Quality = TagQuality.Good
        });

        // Act
        await _hub.GetAllTagValues();

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "AllTagValues",
                It.Is<object[]>(o => o.Length == 1 && ((List<TagDto>)o[0]).Count == 2),
                default),
            Times.Once);
    }

    #endregion

    #region WriteTag Tests

    [Fact]
    public async Task WriteTag_WithSuccessfulWrite_SendsSuccessResult()
    {
        // Arrange
        var connection = _mockPlcManager.AddMockPlc("plc1", "PLC 1", "opc.tcp://localhost:4840");
        connection.ConnectionState = PlcConnectionState.Connected;

        // Act
        await _hub.WriteTag("plc1", "ns=4;i=1", 999);

        // Assert
        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "WriteResult",
                It.Is<object[]>(o => o.Length == 1),
                default),
            Times.Once);
    }

    #endregion
}

public class PlcHubServiceTests
{
    private readonly Mock<IHubContext<PlcHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IClientProxy> _mockGroupClient;
    private readonly Mock<ILogger> _mockLogger;
    private readonly PlcHubService _service;

    public PlcHubServiceTests()
    {
        _mockHubContext = new Mock<IHubContext<PlcHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockGroupClient = new Mock<IClientProxy>();
        _mockLogger = new Mock<ILogger>();

        _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockGroupClient.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);

        _service = new PlcHubService(_mockHubContext.Object, _mockLogger.Object);
    }

    #region BroadcastTagValueAsync Tests

    [Fact]
    public async Task BroadcastTagValueAsync_SendsToPlcGroupAndAllGroup()
    {
        // Arrange
        var plcId = "plc1";
        var tagId = "tag1";
        var nodeId = "ns=4;i=1";
        var value = 123;
        var quality = "Good";
        var timestamp = DateTime.UtcNow;

        // Act
        await _service.BroadcastTagValueAsync(plcId, tagId, nodeId, value, quality, timestamp);

        // Assert
        _mockClients.Verify(c => c.Group($"plc:{plcId}"), Times.Once);
        _mockClients.Verify(c => c.Group("all"), Times.Once);

        _mockGroupClient.Verify(
            x => x.SendCoreAsync(
                "TagValueChanged",
                It.Is<object[]>(o => o.Length == 1 && ((TagValueUpdate)o[0]).PlcId == plcId),
                default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BroadcastTagValueAsync_SetsCorrectUpdateProperties()
    {
        // Arrange
        var plcId = "plc1";
        var tagId = "tag1";
        var nodeId = "ns=4;i=100";
        var value = 456.78;
        var quality = "Good";
        var timestamp = DateTime.UtcNow;

        TagValueUpdate? capturedUpdate = null;
        _mockGroupClient.Setup(x => x.SendCoreAsync(
            "TagValueChanged",
            It.IsAny<object[]>(),
            default))
            .Callback<string, object[], CancellationToken>((_, args, _) =>
            {
                capturedUpdate = (TagValueUpdate)args[0];
            });

        // Act
        await _service.BroadcastTagValueAsync(plcId, tagId, nodeId, value, quality, timestamp);

        // Assert
        capturedUpdate.Should().NotBeNull();
        capturedUpdate!.PlcId.Should().Be(plcId);
        capturedUpdate.TagId.Should().Be(tagId);
        capturedUpdate.NodeId.Should().Be(nodeId);
        capturedUpdate.Value.Should().Be(value);
        capturedUpdate.Quality.Should().Be(quality);
        capturedUpdate.Timestamp.Should().Be(timestamp);
    }

    #endregion

    #region BroadcastConnectionStateAsync Tests

    [Fact]
    public async Task BroadcastConnectionStateAsync_SendsToAllClients()
    {
        // Arrange
        var plcId = "plc1";
        var plcName = "PLC 1";
        var state = "Connected";
        var isConnected = true;

        // Act
        await _service.BroadcastConnectionStateAsync(plcId, plcName, state, isConnected);

        // Assert
        _mockClients.Verify(c => c.All, Times.Once);

        _mockClientProxy.Verify(
            x => x.SendCoreAsync(
                "ConnectionStateChanged",
                It.Is<object[]>(o => o.Length == 1 && ((ConnectionStatusDto)o[0]).PlcId == plcId),
                default),
            Times.Once);
    }

    [Fact]
    public async Task BroadcastConnectionStateAsync_SetsCorrectStatusProperties()
    {
        // Arrange
        var plcId = "plc1";
        var plcName = "PLC 1";
        var state = "Connected";
        var isConnected = true;

        ConnectionStatusDto? capturedStatus = null;
        _mockClientProxy.Setup(x => x.SendCoreAsync(
            "ConnectionStateChanged",
            It.IsAny<object[]>(),
            default))
            .Callback<string, object[], CancellationToken>((_, args, _) =>
            {
                capturedStatus = (ConnectionStatusDto)args[0];
            });

        // Act
        await _service.BroadcastConnectionStateAsync(plcId, plcName, state, isConnected);

        // Assert
        capturedStatus.Should().NotBeNull();
        capturedStatus!.PlcId.Should().Be(plcId);
        capturedStatus.PlcName.Should().Be(plcName);
        capturedStatus.State.Should().Be(state);
        capturedStatus.IsConnected.Should().BeTrue();
        capturedStatus.LastStateChange.Should().NotBeNull();
    }

    #endregion
}
