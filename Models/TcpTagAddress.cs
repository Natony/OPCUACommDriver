using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho địa chỉ Tag khi sử dụng TCP/IP protocol
/// Hỗ trợ parse và format địa chỉ cho Siemens S7 và Mitsubishi MC
/// </summary>
public class TcpTagAddress
{
    /// <summary>
    /// Địa chỉ gốc (raw address string)
    /// VD: "DB1.DBW0", "D100", "MW0", "M100"
    /// </summary>
    public string RawAddress { get; set; } = string.Empty;

    /// <summary>
    /// Loại vùng nhớ cho Siemens S7
    /// </summary>
    public S7MemoryArea S7Area { get; set; }

    /// <summary>
    /// Số DB (chỉ cho Siemens Data Block)
    /// </summary>
    public int DbNumber { get; set; }

    /// <summary>
    /// Địa chỉ bắt đầu
    /// </summary>
    public int StartAddress { get; set; }

    /// <summary>
    /// Bit offset (cho bit access)
    /// </summary>
    public int BitOffset { get; set; }

    /// <summary>
    /// Độ dài dữ liệu (bytes)
    /// </summary>
    public int DataLength { get; set; }

    /// <summary>
    /// Loại device cho Mitsubishi MC
    /// </summary>
    public string McDeviceCode { get; set; } = string.Empty;

    /// <summary>
    /// Số điểm đọc/ghi (cho batch operations)
    /// </summary>
    public int PointCount { get; set; } = 1;

    /// <summary>
    /// Parse địa chỉ Siemens S7
    /// Hỗ trợ: DB1.DBW0, DB10.DBD100, MW0, MD100, IW0, QW0
    /// </summary>
    public static TcpTagAddress ParseS7Address(string address)
    {
        var result = new TcpTagAddress { RawAddress = address };
        address = address.ToUpperInvariant().Trim();

        // Data Block: DB1.DBW0, DB1.DBD100, DB10.DBX0.0
        if (address.StartsWith("DB"))
        {
            var parts = address.Split('.');
            result.DbNumber = int.Parse(parts[0].Substring(2));
            result.S7Area = S7MemoryArea.DataBlock;

            var offsetPart = parts[1];
            if (offsetPart.StartsWith("DBX"))
            {
                result.StartAddress = int.Parse(offsetPart.Substring(3).Split('.')[0]);
                result.BitOffset = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                result.DataLength = 1;
            }
            else if (offsetPart.StartsWith("DBB"))
            {
                result.StartAddress = int.Parse(offsetPart.Substring(3));
                result.DataLength = 1;
            }
            else if (offsetPart.StartsWith("DBW"))
            {
                result.StartAddress = int.Parse(offsetPart.Substring(3));
                result.DataLength = 2;
            }
            else if (offsetPart.StartsWith("DBD"))
            {
                result.StartAddress = int.Parse(offsetPart.Substring(3));
                result.DataLength = 4;
            }
            else
            {
                throw new ArgumentException($"Invalid DB address format: {address}");
            }
        }
        // Memory (Merker): MW0, MD100, MB50, MX0.0
        else if (address.StartsWith("M"))
        {
            result.S7Area = S7MemoryArea.Merker;
            ParseS7AreaAddress(address.Substring(1), result);
        }
        // Input: IW0, ID100, IB50
        else if (address.StartsWith("I") || address.StartsWith("E"))
        {
            result.S7Area = S7MemoryArea.Input;
            ParseS7AreaAddress(address.Substring(1), result);
        }
        // Output: QW0, QD100, QB50
        else if (address.StartsWith("Q") || address.StartsWith("A"))
        {
            result.S7Area = S7MemoryArea.Output;
            ParseS7AreaAddress(address.Substring(1), result);
        }
        else
        {
            throw new ArgumentException($"Unsupported S7 address format: {address}");
        }

        return result;
    }

    private static void ParseS7AreaAddress(string addressPart, TcpTagAddress result)
    {
        if (addressPart.StartsWith("X"))
        {
            var parts = addressPart.Substring(1).Split('.');
            result.StartAddress = int.Parse(parts[0]);
            result.BitOffset = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            result.DataLength = 1;
        }
        else if (addressPart.StartsWith("B"))
        {
            result.StartAddress = int.Parse(addressPart.Substring(1));
            result.DataLength = 1;
        }
        else if (addressPart.StartsWith("W"))
        {
            result.StartAddress = int.Parse(addressPart.Substring(1));
            result.DataLength = 2;
        }
        else if (addressPart.StartsWith("D"))
        {
            result.StartAddress = int.Parse(addressPart.Substring(1));
            result.DataLength = 4;
        }
        else
        {
            // Default to word
            result.StartAddress = int.Parse(addressPart);
            result.DataLength = 2;
        }
    }

    /// <summary>
    /// Parse địa chỉ Mitsubishi MC Protocol
    /// Hỗ trợ: D100, D100:10, W0, M0, X0, Y0
    /// </summary>
    public static TcpTagAddress ParseMcAddress(string address)
    {
        var result = new TcpTagAddress { RawAddress = address };
        address = address.ToUpperInvariant().Trim();

        // Check for point count: D100:10
        if (address.Contains(':'))
        {
            var parts = address.Split(':');
            address = parts[0];
            result.PointCount = int.Parse(parts[1]);
        }

        // Extract device code and address
        var twoCharCodes = new[] { "SD", "SM", "TN", "CN" };
        foreach (var code in twoCharCodes)
        {
            if (address.StartsWith(code))
            {
                result.McDeviceCode = code;
                result.StartAddress = int.Parse(address.Substring(2));
                return result;
            }
        }

        // Single character device codes
        var singleCharCodes = new[] { "D", "W", "R", "M", "X", "Y", "L", "F", "B", "S" };
        foreach (var code in singleCharCodes)
        {
            if (address.StartsWith(code))
            {
                result.McDeviceCode = code;
                result.StartAddress = int.Parse(address.Substring(1));
                return result;
            }
        }

        throw new ArgumentException($"Unsupported MC address format: {address}");
    }

    /// <summary>
    /// Parse dia chi Modbus TCP
    /// Ho tro: HR0, HR0.5, IR100, C0, DI10
    /// </summary>
    public static TcpTagAddress ParseModbusAddress(string address)
    {
        var result = new TcpTagAddress { RawAddress = address };
        address = address.ToUpperInvariant().Trim();

        // Check for point count: HR0:10
        if (address.Contains(':'))
        {
            var colonParts = address.Split(':');
            address = colonParts[0];
            result.PointCount = int.Parse(colonParts[1]);
        }

        // Holding Register: HR0, HR0.5
        if (address.StartsWith("HR"))
        {
            result.ModbusDataType = ModbusRegisterType.HoldingRegister;
            var rest = address.Substring(2);
            if (rest.Contains('.'))
            {
                var parts = rest.Split('.');
                result.StartAddress = int.Parse(parts[0]);
                result.BitOffset = int.Parse(parts[1]);
                result.DataLength = 1;
            }
            else
            {
                result.StartAddress = int.Parse(rest);
                result.DataLength = 2;
            }
            return result;
        }

        // Input Register: IR0
        if (address.StartsWith("IR"))
        {
            result.ModbusDataType = ModbusRegisterType.InputRegister;
            result.StartAddress = int.Parse(address.Substring(2));
            result.DataLength = 2;
            return result;
        }

        // Coil: C0
        if (address.StartsWith("C") && !address.StartsWith("CO"))
        {
            result.ModbusDataType = ModbusRegisterType.Coil;
            result.StartAddress = int.Parse(address.Substring(1));
            result.DataLength = 1;
            return result;
        }

        // Discrete Input: DI0
        if (address.StartsWith("DI"))
        {
            result.ModbusDataType = ModbusRegisterType.DiscreteInput;
            result.StartAddress = int.Parse(address.Substring(2));
            result.DataLength = 1;
            return result;
        }

        throw new ArgumentException($"Unsupported Modbus address format: {address}");
    }

    public override string ToString()
    {
        return RawAddress;
    }

    /// <summary>
    /// Loai Modbus register
    /// </summary>
    public ModbusRegisterType ModbusDataType { get; set; }
}

/// <summary>
/// Vùng nhớ Siemens S7
/// </summary>
public enum S7MemoryArea : byte
{
    /// <summary>
    /// Data Block
    /// </summary>
    DataBlock = 0x84,

    /// <summary>
    /// Input (I/E)
    /// </summary>
    Input = 0x81,

    /// <summary>
    /// Output (Q/A)
    /// </summary>
    Output = 0x82,

    /// <summary>
    /// Memory/Merker (M)
    /// </summary>
    Merker = 0x83,

    /// <summary>
    /// Timer
    /// </summary>
    Timer = 0x1D,

    /// <summary>
    /// Counter
    /// </summary>
    Counter = 0x1C
}

/// <summary>
/// Loai register Modbus
/// </summary>
public enum ModbusRegisterType
{
    /// <summary>
    /// Coil (Read/Write) - Function 01/05/15
    /// </summary>
    Coil = 0,

    /// <summary>
    /// Discrete Input (Read Only) - Function 02
    /// </summary>
    DiscreteInput = 1,

    /// <summary>
    /// Holding Register (Read/Write) - Function 03/06/16
    /// </summary>
    HoldingRegister = 3,

    /// <summary>
    /// Input Register (Read Only) - Function 04
    /// </summary>
    InputRegister = 4
}
