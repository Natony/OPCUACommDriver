using Microsoft.Extensions.Logging;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.OpcUa;

namespace OpcUaCommunicationEngine.Services.Protocols;

/// <summary>
/// Factory để tạo các kết nối protocol phù hợp dựa trên cấu hình PLC
/// </summary>
public class ProtocolConnectionFactory : IProtocolConnectionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolConnectionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Tạo connection dựa trên ProtocolType của PlcDevice
    /// </summary>
    public IPlcConnection CreateConnection(PlcDevice device)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        var logger = _loggerFactory.CreateLogger($"Connection.{device.Name}");

        return device.ProtocolType switch
        {
            ProtocolType.OpcUa => new PlcConnection(device, logger),
            ProtocolType.SiemensS7 => new SiemensS7Connection(device, logger),
            ProtocolType.MitsubishiMc => new MitsubishiMcConnection(device, logger),
            ProtocolType.ModbusTcp => throw new NotSupportedException($"Modbus TCP protocol is not yet implemented"),
            _ => throw new NotSupportedException($"Protocol type {device.ProtocolType} is not supported")
        };
    }

    /// <summary>
    /// Kiểm tra xem ProtocolType có được hỗ trợ không
    /// </summary>
    public bool IsProtocolSupported(PlcDevice device)
    {
        return device.ProtocolType switch
        {
            ProtocolType.OpcUa => true,
            ProtocolType.SiemensS7 => true,
            ProtocolType.MitsubishiMc => true,
            ProtocolType.ModbusTcp => false,
            _ => false
        };
    }
}
