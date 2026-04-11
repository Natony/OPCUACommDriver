namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Mapping bit từ một thanh ghi Modbus (16-bit)
/// Dùng để trích xuất các giá trị boolean từ 1 thanh ghi Word
/// </summary>
public class BitMapping
{
    /// <summary>
    /// Vị trí bit trong thanh ghi (0-15)
    /// </summary>
    public int Bit { get; set; }

    /// <summary>
    /// Tên của bit (để hiển thị)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Mô tả
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Trích xuất giá trị boolean từ thanh ghi 16-bit
    /// </summary>
    public bool ExtractBit(ushort registerValue)
    {
        if (Bit < 0 || Bit > 15)
            return false;

        return (registerValue & (1 << Bit)) != 0;
    }

    /// <summary>
    /// Đặt bit trong thanh ghi 16-bit
    /// </summary>
    public ushort SetBit(ushort registerValue, bool bitValue)
    {
        if (Bit < 0 || Bit > 15)
            return registerValue;

        if (bitValue)
            return (ushort)(registerValue | (1 << Bit));
        else
            return (ushort)(registerValue & ~(1 << Bit));
    }

    public override string ToString()
    {
        return $"Bit{Bit}: {Name}";
    }
}
