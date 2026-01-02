namespace OpcUaCommunicationEngine.Api.Models;

/// <summary>
/// PLC information for API response
/// </summary>
public class PlcDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string ConnectionState { get; set; } = string.Empty;
    public int TagCount { get; set; }
    public DateTime? LastConnected { get; set; }
}

/// <summary>
/// Tag information for API response
/// </summary>
public class TagDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string PlcId { get; set; } = string.Empty;
    public string PlcName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string Quality { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string DataType { get; set; } = string.Empty;
    public bool IsWritable { get; set; }
}

/// <summary>
/// Write tag request
/// </summary>
public class WriteTagRequest
{
    public object Value { get; set; } = null!;
}

/// <summary>
/// Write multiple tags request
/// </summary>
public class WriteTagsRequest
{
    public List<WriteTagItem> Tags { get; set; } = new();
}

public class WriteTagItem
{
    public string NodeId { get; set; } = string.Empty;
    public object Value { get; set; } = null!;
}

/// <summary>
/// API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Connection status for API
/// </summary>
public class ConnectionStatusDto
{
    public string PlcId { get; set; } = string.Empty;
    public string PlcName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime? LastStateChange { get; set; }
}

/// <summary>
/// Tag value update (for SignalR)
/// </summary>
public class TagValueUpdate
{
    public string PlcId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string Quality { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Browse node for API
/// </summary>
public class BrowseNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NodeClass { get; set; } = string.Empty;
    public string? DataType { get; set; }
    public bool HasChildren { get; set; }
    public List<BrowseNodeDto> Children { get; set; } = new();
}

/// <summary>
/// Add tag request
/// </summary>
public class AddTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string? SubscriptionGroupId { get; set; }
    public int SamplingInterval { get; set; } = 1000;
}
