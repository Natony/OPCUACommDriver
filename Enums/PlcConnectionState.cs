namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// Trạng thái kết nối của PLC theo State Machine Pattern
/// [DISABLED] → [CONNECTING] → [CONNECTED] ↔ [ERROR] → [RECONNECTING]
/// </summary>
public enum PlcConnectionState
{
    /// <summary>
    /// PLC bị vô hiệu hóa, không kết nối
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Đang trong quá trình kết nối
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Đã kết nối thành công
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Lỗi kết nối
    /// </summary>
    Error = 3,

    /// <summary>
    /// Đang thử kết nối lại
    /// </summary>
    Reconnecting = 4,

    /// <summary>
    /// Đang ngắt kết nối
    /// </summary>
    Disconnecting = 5,

    /// <summary>
    /// Đã ngắt kết nối
    /// </summary>
    Disconnected = 6
}
