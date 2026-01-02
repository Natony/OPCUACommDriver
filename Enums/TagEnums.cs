namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// Chế độ truy cập của Tag
/// </summary>
public enum TagAccessMode
{
    /// <summary>
    /// Chỉ đọc
    /// </summary>
    Read = 0,

    /// <summary>
    /// Chỉ ghi
    /// </summary>
    Write = 1,

    /// <summary>
    /// Đọc và ghi
    /// </summary>
    ReadWrite = 2
}

/// <summary>
/// Kiểu dữ liệu của Tag (mapping với OPC UA DataTypes)
/// </summary>
public enum TagDataType
{
    /// <summary>
    /// Boolean (1 bit)
    /// </summary>
    Boolean = 0,

    /// <summary>
    /// Signed Byte (-128 to 127)
    /// </summary>
    SByte = 1,

    /// <summary>
    /// Unsigned Byte (0 to 255)
    /// </summary>
    Byte = 2,

    /// <summary>
    /// Signed 16-bit Integer
    /// </summary>
    Int16 = 3,

    /// <summary>
    /// Unsigned 16-bit Integer
    /// </summary>
    UInt16 = 4,

    /// <summary>
    /// Signed 32-bit Integer
    /// </summary>
    Int32 = 5,

    /// <summary>
    /// Unsigned 32-bit Integer
    /// </summary>
    UInt32 = 6,

    /// <summary>
    /// Signed 64-bit Integer
    /// </summary>
    Int64 = 7,

    /// <summary>
    /// Unsigned 64-bit Integer
    /// </summary>
    UInt64 = 8,

    /// <summary>
    /// 32-bit Floating Point
    /// </summary>
    Float = 9,

    /// <summary>
    /// 64-bit Floating Point
    /// </summary>
    Double = 10,

    /// <summary>
    /// String
    /// </summary>
    String = 11,

    /// <summary>
    /// DateTime
    /// </summary>
    DateTime = 12,

    /// <summary>
    /// ByteString (Array of bytes)
    /// </summary>
    ByteString = 13,

    /// <summary>
    /// Unknown/Auto-detect
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Chất lượng dữ liệu của Tag
/// </summary>
public enum TagQuality
{
    /// <summary>
    /// Dữ liệu tốt
    /// </summary>
    Good = 0,

    /// <summary>
    /// Dữ liệu không chắc chắn
    /// </summary>
    Uncertain = 1,

    /// <summary>
    /// Dữ liệu xấu/lỗi
    /// </summary>
    Bad = 2,

    /// <summary>
    /// Chưa có dữ liệu
    /// </summary>
    Unknown = 3
}
