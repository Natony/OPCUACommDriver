namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// Loại giao thức kết nối PLC
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// OPC UA (mặc định)
    /// </summary>
    OpcUa = 0,

    /// <summary>
    /// Modbus RTU (Serial)
    /// </summary>
    ModbusRtu = 1,

    /// <summary>
    /// Modbus ASCII (Serial)
    /// </summary>
    ModbusAscii = 2,

    /// <summary>
    /// Modbus TCP/IP
    /// </summary>
    ModbusTcp = 3
}

/// <summary>
/// Loại vùng nhớ Modbus
/// </summary>
public enum ModbusRegisterType
{
    /// <summary>
    /// Coil (DO) - Function 01/05/15 - Đọc/Ghi 1 bit
    /// </summary>
    Coil = 0,

    /// <summary>
    /// Discrete Input (DI) - Function 02 - Chỉ đọc 1 bit
    /// </summary>
    DiscreteInput = 1,

    /// <summary>
    /// Holding Register - Function 03/06/16 - Đọc/Ghi 16-bit
    /// </summary>
    HoldingRegister = 3,

    /// <summary>
    /// Input Register - Function 04 - Chỉ đọc 16-bit
    /// </summary>
    InputRegister = 4
}
