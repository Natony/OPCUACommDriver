using FluentAssertions;
using OpcUaCommunicationEngine.Api.Models;
using Xunit;

namespace OpcUaCommunicationEngine.Tests.Models;

public class ApiModelsTests
{
    #region ApiResponse Tests

    [Fact]
    public void ApiResponse_Ok_CreatesSuccessResponse()
    {
        // Arrange
        var data = new List<string> { "item1", "item2" };

        // Act
        var response = ApiResponse<List<string>>.Ok(data);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().BeSameAs(data);
        response.Error.Should().BeNull();
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ApiResponse_Fail_CreatesFailureResponse()
    {
        // Arrange
        var errorMessage = "Something went wrong";

        // Act
        var response = ApiResponse<string>.Fail(errorMessage);

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
        response.Error.Should().Be(errorMessage);
    }

    [Fact]
    public void ApiResponse_Ok_WithNullData_CreatesSuccessResponse()
    {
        // Act
        var response = ApiResponse<object?>.Ok(null);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().BeNull();
    }

    #endregion

    #region PlcDto Tests

    [Fact]
    public void PlcDto_DefaultValues_AreEmpty()
    {
        // Act
        var dto = new PlcDto();

        // Assert
        dto.Id.Should().BeEmpty();
        dto.Name.Should().BeEmpty();
        dto.EndpointUrl.Should().BeEmpty();
        dto.ConnectionState.Should().BeEmpty();
        dto.TagCount.Should().Be(0);
        dto.LastConnected.Should().BeNull();
    }

    [Fact]
    public void PlcDto_CanSetAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var dto = new PlcDto
        {
            Id = "plc-123",
            Name = "Test PLC",
            EndpointUrl = "opc.tcp://localhost:4840",
            ConnectionState = "Connected",
            TagCount = 50,
            LastConnected = now
        };

        // Assert
        dto.Id.Should().Be("plc-123");
        dto.Name.Should().Be("Test PLC");
        dto.EndpointUrl.Should().Be("opc.tcp://localhost:4840");
        dto.ConnectionState.Should().Be("Connected");
        dto.TagCount.Should().Be(50);
        dto.LastConnected.Should().Be(now);
    }

    #endregion

    #region TagDto Tests

    [Fact]
    public void TagDto_DefaultValues_AreEmpty()
    {
        // Act
        var dto = new TagDto();

        // Assert
        dto.Id.Should().BeEmpty();
        dto.Name.Should().BeEmpty();
        dto.NodeId.Should().BeEmpty();
        dto.PlcId.Should().BeEmpty();
        dto.PlcName.Should().BeEmpty();
        dto.Value.Should().BeNull();
        dto.Quality.Should().BeEmpty();
        dto.Timestamp.Should().BeNull();
        dto.DataType.Should().BeEmpty();
        dto.IsWritable.Should().BeFalse();
    }

    [Fact]
    public void TagDto_CanStoreVariousValueTypes()
    {
        // Arrange & Act
        var intTag = new TagDto { Value = 42 };
        var boolTag = new TagDto { Value = true };
        var doubleTag = new TagDto { Value = 3.14159 };
        var stringTag = new TagDto { Value = "Hello World" };
        var arrayTag = new TagDto { Value = new int[] { 1, 2, 3 } };

        // Assert
        intTag.Value.Should().Be(42);
        boolTag.Value.Should().Be(true);
        doubleTag.Value.Should().Be(3.14159);
        stringTag.Value.Should().Be("Hello World");
        arrayTag.Value.Should().BeEquivalentTo(new int[] { 1, 2, 3 });
    }

    #endregion

    #region ConnectionStatusDto Tests

    [Fact]
    public void ConnectionStatusDto_DefaultValues_AreCorrect()
    {
        // Act
        var dto = new ConnectionStatusDto();

        // Assert
        dto.PlcId.Should().BeEmpty();
        dto.PlcName.Should().BeEmpty();
        dto.State.Should().BeEmpty();
        dto.IsConnected.Should().BeFalse();
        dto.LastStateChange.Should().BeNull();
    }

    [Fact]
    public void ConnectionStatusDto_CanSetAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var dto = new ConnectionStatusDto
        {
            PlcId = "plc-1",
            PlcName = "Main PLC",
            State = "Connected",
            IsConnected = true,
            LastStateChange = now
        };

        // Assert
        dto.PlcId.Should().Be("plc-1");
        dto.PlcName.Should().Be("Main PLC");
        dto.State.Should().Be("Connected");
        dto.IsConnected.Should().BeTrue();
        dto.LastStateChange.Should().Be(now);
    }

    #endregion

    #region TagValueUpdate Tests

    [Fact]
    public void TagValueUpdate_DefaultValues_AreEmpty()
    {
        // Act
        var update = new TagValueUpdate();

        // Assert
        update.PlcId.Should().BeEmpty();
        update.TagId.Should().BeEmpty();
        update.NodeId.Should().BeEmpty();
        update.Value.Should().BeNull();
        update.Quality.Should().BeEmpty();
    }

    [Fact]
    public void TagValueUpdate_CanSetAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var update = new TagValueUpdate
        {
            PlcId = "plc-1",
            TagId = "tag-1",
            NodeId = "ns=4;i=100",
            Value = 123.45,
            Quality = "Good",
            Timestamp = now
        };

        // Assert
        update.PlcId.Should().Be("plc-1");
        update.TagId.Should().Be("tag-1");
        update.NodeId.Should().Be("ns=4;i=100");
        update.Value.Should().Be(123.45);
        update.Quality.Should().Be("Good");
        update.Timestamp.Should().Be(now);
    }

    #endregion

    #region WriteTagRequest Tests

    [Fact]
    public void WriteTagRequest_CanSetValue()
    {
        // Arrange & Act
        var request = new WriteTagRequest { Value = 42 };

        // Assert
        request.Value.Should().Be(42);
    }

    [Fact]
    public void WriteTagRequest_CanStoreVariousTypes()
    {
        // Arrange & Act
        var intRequest = new WriteTagRequest { Value = 100 };
        var boolRequest = new WriteTagRequest { Value = false };
        var stringRequest = new WriteTagRequest { Value = "test" };

        // Assert
        intRequest.Value.Should().Be(100);
        boolRequest.Value.Should().Be(false);
        stringRequest.Value.Should().Be("test");
    }

    #endregion

    #region WriteTagsRequest Tests

    [Fact]
    public void WriteTagsRequest_DefaultTags_IsEmptyList()
    {
        // Act
        var request = new WriteTagsRequest();

        // Assert
        request.Tags.Should().NotBeNull();
        request.Tags.Should().BeEmpty();
    }

    [Fact]
    public void WriteTagsRequest_CanAddMultipleTags()
    {
        // Arrange & Act
        var request = new WriteTagsRequest
        {
            Tags = new List<WriteTagItem>
            {
                new WriteTagItem { NodeId = "ns=4;i=1", Value = 100 },
                new WriteTagItem { NodeId = "ns=4;i=2", Value = true },
                new WriteTagItem { NodeId = "ns=4;i=3", Value = "text" }
            }
        };

        // Assert
        request.Tags.Should().HaveCount(3);
        request.Tags[0].NodeId.Should().Be("ns=4;i=1");
        request.Tags[0].Value.Should().Be(100);
        request.Tags[1].Value.Should().Be(true);
        request.Tags[2].Value.Should().Be("text");
    }

    #endregion

    #region BrowseNodeDto Tests

    [Fact]
    public void BrowseNodeDto_DefaultValues_AreCorrect()
    {
        // Act
        var dto = new BrowseNodeDto();

        // Assert
        dto.NodeId.Should().BeEmpty();
        dto.DisplayName.Should().BeEmpty();
        dto.NodeClass.Should().BeEmpty();
        dto.DataType.Should().BeNull();
        dto.HasChildren.Should().BeFalse();
        dto.Children.Should().NotBeNull();
        dto.Children.Should().BeEmpty();
    }

    [Fact]
    public void BrowseNodeDto_CanSetHierarchy()
    {
        // Arrange & Act
        var parent = new BrowseNodeDto
        {
            NodeId = "ns=4;i=1",
            DisplayName = "Parent",
            NodeClass = "Object",
            HasChildren = true,
            Children = new List<BrowseNodeDto>
            {
                new BrowseNodeDto
                {
                    NodeId = "ns=4;i=2",
                    DisplayName = "Child 1",
                    NodeClass = "Variable",
                    DataType = "Int32",
                    HasChildren = false
                },
                new BrowseNodeDto
                {
                    NodeId = "ns=4;i=3",
                    DisplayName = "Child 2",
                    NodeClass = "Variable",
                    DataType = "Boolean",
                    HasChildren = false
                }
            }
        };

        // Assert
        parent.Children.Should().HaveCount(2);
        parent.Children[0].DisplayName.Should().Be("Child 1");
        parent.Children[1].DataType.Should().Be("Boolean");
    }

    #endregion

    #region AddTagRequest Tests

    [Fact]
    public void AddTagRequest_DefaultValues_AreCorrect()
    {
        // Act
        var request = new AddTagRequest();

        // Assert
        request.Name.Should().BeEmpty();
        request.NodeId.Should().BeEmpty();
        request.SubscriptionGroupId.Should().BeNull();
        request.SamplingInterval.Should().Be(1000);
    }

    [Fact]
    public void AddTagRequest_CanSetAllProperties()
    {
        // Act
        var request = new AddTagRequest
        {
            Name = "Temperature",
            NodeId = "ns=4;s=PLC.Temperature",
            SubscriptionGroupId = "group-1",
            SamplingInterval = 500
        };

        // Assert
        request.Name.Should().Be("Temperature");
        request.NodeId.Should().Be("ns=4;s=PLC.Temperature");
        request.SubscriptionGroupId.Should().Be("group-1");
        request.SamplingInterval.Should().Be(500);
    }

    #endregion
}
