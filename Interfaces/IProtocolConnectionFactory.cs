using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Interfaces;

/// <summary>
/// Factory để tạo các kết nối protocol phù hợp dựa trên cấu hình PLC
/// </summary>
public interface IProtocolConnectionFactory
{
    /// <summary>
    /// Tạo connection dựa trên ProtocolType của PlcDevice
    /// </summary>
    IPlcConnection CreateConnection(PlcDevice device);

    /// <summary>
    /// Kiểm tra xem ProtocolType có được hỗ trợ không
    /// </summary>
    bool IsProtocolSupported(PlcDevice device);
}
