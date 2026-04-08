namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// Loại giao thức truyền thông với PLC
/// </summary>
public enum ProtocolType
{
    /// <summary>
    /// OPC UA - Universal standard protocol
    /// Supports: Siemens S7-1200/1500, Beckhoff, B&R, etc.
    /// </summary>
    OpcUa = 0,

    /// <summary>
    /// S7comm TCP/IP - Native Siemens protocol
    /// Port: 102
    /// Supports: S7-300, S7-400, S7-1200, S7-1500
    /// </summary>
    SiemensS7 = 1,

    /// <summary>
    /// MC Protocol (MELSEC Communication) - Mitsubishi native protocol
    /// Port: 5000 (default), 5001, etc.
    /// Supports: FX5U, FX3U, Q Series, iQ-R Series
    /// </summary>
    MitsubishiMc = 2,

    /// <summary>
    /// Modbus TCP - Standard industrial protocol
    /// Port: 502
    /// Supports: Many PLCs and devices
    /// </summary>
    ModbusTcp = 3
}
