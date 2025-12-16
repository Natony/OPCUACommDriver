using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.OpcUa;

namespace OpcUaCommunicationEngine.Interfaces;

/// <summary>
/// Interface quản lý nhiều PLC connections
/// </summary>
public interface IPlcManager : IDisposable
{
    #region Events

    /// <summary>
    /// Sự kiện khi trạng thái kết nối thay đổi
    /// </summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Sự kiện khi giá trị tag thay đổi
    /// </summary>
    event EventHandler<TagValueChangedEventArgs>? TagValueChanged;

    /// <summary>
    /// Sự kiện khi có lỗi
    /// </summary>
    event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    #endregion

    #region Properties

    /// <summary>
    /// Tổng số PLCs
    /// </summary>
    int PlcCount { get; }

    /// <summary>
    /// Số PLCs đang kết nối
    /// </summary>
    int ConnectedCount { get; }

    /// <summary>
    /// Danh sách connections
    /// </summary>
    IReadOnlyList<IPlcConnection> Connections { get; }

    #endregion

    #region Initialization

    /// <summary>
    /// Khởi tạo manager
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Connection Management

    /// <summary>
    /// Thêm PLC mới
    /// </summary>
    Task<IPlcConnection?> AddPlcAsync(PlcDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa PLC
    /// </summary>
    Task<bool> RemovePlcAsync(string plcId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy connection theo ID
    /// </summary>
    IPlcConnection? GetConnection(string plcId);

    /// <summary>
    /// Kiểm tra PLC có tồn tại
    /// </summary>
    bool HasPlc(string plcId);

    #endregion

    #region Connect/Disconnect

    /// <summary>
    /// Kết nối đến 1 PLC
    /// </summary>
    Task<bool> ConnectAsync(string plcId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ngắt kết nối 1 PLC
    /// </summary>
    Task DisconnectAsync(string plcId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kết nối tất cả PLCs
    /// </summary>
    Task<int> ConnectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ngắt kết nối tất cả PLCs
    /// </summary>
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconnect 1 PLC
    /// </summary>
    Task<bool> ReconnectAsync(string plcId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconnect tất cả PLCs
    /// </summary>
    Task<int> ReconnectAllAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Read/Write

    /// <summary>
    /// Đọc 1 tag
    /// </summary>
    Task<TagValue?> ReadTagAsync(string plcId, string nodeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc nhiều tags
    /// </summary>
    Task<IReadOnlyList<TagValue>> ReadTagsAsync(string plcId, IEnumerable<string> nodeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi 1 tag
    /// </summary>
    Task<bool> WriteTagAsync(string plcId, string nodeId, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhiều tags
    /// </summary>
    Task<IReadOnlyList<bool>> WriteTagsAsync(string plcId, IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đọc tất cả tags từ tất cả PLCs
    /// </summary>
    Task<IDictionary<string, IReadOnlyList<TagValue>>> ReadAllTagsAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Browse

    /// <summary>
    /// Browse OPC UA server
    /// </summary>
    Task<IReadOnlyList<BrowseNode>> BrowseAsync(string plcId, string? nodeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy thông tin node
    /// </summary>
    Task<NodeInfo?> GetNodeInfoAsync(string plcId, string nodeId, CancellationToken cancellationToken = default);

    #endregion

    #region Status

    /// <summary>
    /// Lấy trạng thái tất cả PLCs
    /// </summary>
    IReadOnlyList<PlcStatus> GetAllStatus();

    /// <summary>
    /// Lấy trạng thái 1 PLC
    /// </summary>
    PlcStatus? GetStatus(string plcId);

    #endregion
}
