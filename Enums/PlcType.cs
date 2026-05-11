namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// Loại/Hãng PLC được hỗ trợ
/// </summary>
public enum PlcType
{
    /// <summary>
    /// Generic/Unknown PLC
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Siemens S7-300 series
    /// </summary>
    SiemensS7300 = 1,

    /// <summary>
    /// Siemens S7-400 series
    /// </summary>
    SiemensS7400 = 2,

    /// <summary>
    /// Siemens S7-1200 series
    /// </summary>
    SiemensS7_1200 = 3,

    /// <summary>
    /// Siemens S7-1500 series
    /// </summary>
    SiemensS7_1500 = 4,

    /// <summary>
    /// Mitsubishi FX3U series
    /// </summary>
    MitsubishiFX3U = 10,

    /// <summary>
    /// Mitsubishi FX5U series
    /// </summary>
    MitsubishiFX5U = 11,

    /// <summary>
    /// Mitsubishi Q Series
    /// </summary>
    MitsubishiQ = 12,

    /// <summary>
    /// Mitsubishi iQ-R Series
    /// </summary>
    MitsubishiIQR = 13
}
