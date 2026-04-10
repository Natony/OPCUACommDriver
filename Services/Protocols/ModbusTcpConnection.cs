using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Services.Protocols;

/// <summary>
/// Ket noi Modbus TCP/IP
/// Port: 502 (default)
/// Ho tro: Coils, Discrete Inputs, Holding Registers, Input Registers
///
/// Dia chi format:
/// - HR{address}         : Holding Register (Function 03/06/16)
/// - HR{address}.{bit}   : Holding Register bit (0-15)
/// - IR{address}         : Input Register (Function 04)
/// - C{address}          : Coil (Function 01/05/15)
/// - DI{address}         : Discrete Input (Function 02)
///
/// Vi du: HR0, HR0.0, HR100, IR50, C0, DI10
/// </summary>
public class ModbusTcpConnection : BaseProtocolConnection
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private ushort _transactionId = 0;
    private readonly byte _unitId = 1;
    private readonly object _transactionLock = new();

    protected override string ProtocolName => "Modbus TCP";

    public ModbusTcpConnection(PlcDevice device, ILogger logger) : base(device, logger)
    {
        // Unit ID can be configured in PlcDevice if needed
        _unitId = 1;
    }

    #region Connection Methods

    protected override async Task<bool> ConnectInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            _client = new TcpClient
            {
                ReceiveTimeout = _device.ReadTimeout,
                SendTimeout = _device.WriteTimeout
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_device.ConnectionTimeout);

            var port = _device.Port > 0 ? _device.Port : 502;
            await _client.ConnectAsync(_device.IpAddress, port, cts.Token);
            _stream = _client.GetStream();

            _logger.LogInformation("[Modbus TCP] Connected to {Name} ({IP}:{Port})",
                _device.Name, _device.IpAddress, port);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Modbus TCP] Connection failed to {IP}:{Port}",
                _device.IpAddress, _device.Port);
            CleanupConnection();
            throw;
        }
    }

    protected override Task DisconnectInternalAsync()
    {
        CleanupConnection();
        return Task.CompletedTask;
    }

    protected override bool IsConnectionActive()
    {
        return _client?.Connected == true && _stream != null;
    }

    private void CleanupConnection()
    {
        try
        {
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;

            _client?.Close();
            _client?.Dispose();
            _client = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Modbus TCP] Error during cleanup");
        }
    }

    #endregion

    #region Read/Write Implementation

    protected override async Task<TagValue?> ReadTagInternalAsync(string address, CancellationToken cancellationToken)
    {
        if (_stream == null || !IsConnectionActive())
        {
            return CreateErrorTagValue(address, "Not connected");
        }

        try
        {
            var parsed = ParseModbusAddress(address);
            object? value;

            switch (parsed.Type)
            {
                case ModbusDataType.HoldingRegister:
                    var hrValues = await ReadHoldingRegistersAsync(parsed.Address, parsed.Count, cancellationToken);
                    if (parsed.IsBitAccess)
                    {
                        // Extract bit from register
                        value = (hrValues[0] & (1 << parsed.BitOffset)) != 0;
                    }
                    else if (parsed.Count == 1)
                    {
                        value = (int)hrValues[0];
                    }
                    else if (parsed.Count == 2)
                    {
                        // 32-bit value from 2 registers (Big Endian)
                        value = (hrValues[0] << 16) | hrValues[1];
                    }
                    else
                    {
                        value = hrValues;
                    }
                    break;

                case ModbusDataType.InputRegister:
                    var irValues = await ReadInputRegistersAsync(parsed.Address, parsed.Count, cancellationToken);
                    value = parsed.Count == 1 ? (int)irValues[0] : irValues;
                    break;

                case ModbusDataType.Coil:
                    var coils = await ReadCoilsAsync(parsed.Address, parsed.Count, cancellationToken);
                    value = parsed.Count == 1 ? coils[0] : coils;
                    break;

                case ModbusDataType.DiscreteInput:
                    var di = await ReadDiscreteInputsAsync(parsed.Address, parsed.Count, cancellationToken);
                    value = parsed.Count == 1 ? di[0] : di;
                    break;

                default:
                    return CreateErrorTagValue(address, "Unknown address type");
            }

            return new TagValue
            {
                NodeId = address,
                Value = value,
                Quality = TagQuality.Good,
                Timestamp = DateTime.Now,
                ServerTimestamp = DateTime.Now,
                StatusCode = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Modbus TCP] Read failed for {Address}", address);
            return CreateErrorTagValue(address, ex.Message);
        }
    }

    protected override async Task<bool> WriteTagInternalAsync(string address, object value, CancellationToken cancellationToken)
    {
        if (_stream == null || !IsConnectionActive())
        {
            return false;
        }

        try
        {
            var parsed = ParseModbusAddress(address);

            switch (parsed.Type)
            {
                case ModbusDataType.HoldingRegister:
                    if (parsed.IsBitAccess)
                    {
                        // Read-modify-write for bit access
                        var currentValues = await ReadHoldingRegistersAsync(parsed.Address, 1, cancellationToken);
                        var currentValue = currentValues[0];
                        var boolValue = Convert.ToBoolean(value);

                        if (boolValue)
                            currentValue |= (ushort)(1 << parsed.BitOffset);
                        else
                            currentValue &= (ushort)~(1 << parsed.BitOffset);

                        return await WriteSingleRegisterAsync(parsed.Address, currentValue, cancellationToken);
                    }
                    else
                    {
                        var regValue = Convert.ToUInt16(value);
                        return await WriteSingleRegisterAsync(parsed.Address, regValue, cancellationToken);
                    }

                case ModbusDataType.Coil:
                    var coilValue = Convert.ToBoolean(value);
                    return await WriteSingleCoilAsync(parsed.Address, coilValue, cancellationToken);

                case ModbusDataType.InputRegister:
                case ModbusDataType.DiscreteInput:
                    _logger.LogWarning("[Modbus TCP] Cannot write to read-only address: {Address}", address);
                    return false;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Modbus TCP] Write failed for {Address}", address);
            return false;
        }
    }

    #endregion

    #region Modbus Functions

    /// <summary>
    /// Function 01: Read Coils
    /// </summary>
    private async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort quantity, CancellationToken ct)
    {
        var request = BuildRequest(0x01, startAddress, quantity);
        var response = await SendRequestAsync(request, ct);
        return ParseBooleanResponse(response, quantity);
    }

    /// <summary>
    /// Function 02: Read Discrete Inputs
    /// </summary>
    private async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort quantity, CancellationToken ct)
    {
        var request = BuildRequest(0x02, startAddress, quantity);
        var response = await SendRequestAsync(request, ct);
        return ParseBooleanResponse(response, quantity);
    }

    /// <summary>
    /// Function 03: Read Holding Registers
    /// </summary>
    private async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort quantity, CancellationToken ct)
    {
        var request = BuildRequest(0x03, startAddress, quantity);
        var response = await SendRequestAsync(request, ct);
        return ParseRegisterResponse(response, quantity);
    }

    /// <summary>
    /// Function 04: Read Input Registers
    /// </summary>
    private async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort quantity, CancellationToken ct)
    {
        var request = BuildRequest(0x04, startAddress, quantity);
        var response = await SendRequestAsync(request, ct);
        return ParseRegisterResponse(response, quantity);
    }

    /// <summary>
    /// Function 05: Write Single Coil
    /// </summary>
    private async Task<bool> WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct)
    {
        var valueBytes = value ? (ushort)0xFF00 : (ushort)0x0000;
        var request = BuildRequest(0x05, address, valueBytes);
        var response = await SendRequestAsync(request, ct);
        return ValidateWriteResponse(response, 0x05);
    }

    /// <summary>
    /// Function 06: Write Single Register
    /// </summary>
    private async Task<bool> WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct)
    {
        var request = BuildRequest(0x06, address, value);
        var response = await SendRequestAsync(request, ct);
        return ValidateWriteResponse(response, 0x06);
    }

    /// <summary>
    /// Function 16: Write Multiple Registers
    /// </summary>
    private async Task<bool> WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct)
    {
        var request = BuildWriteMultipleRequest(0x10, startAddress, values);
        var response = await SendRequestAsync(request, ct);
        return ValidateWriteResponse(response, 0x10);
    }

    #endregion

    #region Protocol Helpers

    private ushort GetNextTransactionId()
    {
        lock (_transactionLock)
        {
            return ++_transactionId;
        }
    }

    private byte[] BuildRequest(byte functionCode, ushort address, ushort value)
    {
        var transactionId = GetNextTransactionId();

        // MBAP Header (7 bytes) + PDU (5 bytes)
        var request = new byte[12];

        // Transaction Identifier
        request[0] = (byte)(transactionId >> 8);
        request[1] = (byte)(transactionId & 0xFF);

        // Protocol Identifier (0 = Modbus)
        request[2] = 0x00;
        request[3] = 0x00;

        // Length (remaining bytes after this)
        request[4] = 0x00;
        request[5] = 0x06;

        // Unit Identifier
        request[6] = _unitId;

        // Function Code
        request[7] = functionCode;

        // Starting Address
        request[8] = (byte)(address >> 8);
        request[9] = (byte)(address & 0xFF);

        // Quantity/Value
        request[10] = (byte)(value >> 8);
        request[11] = (byte)(value & 0xFF);

        return request;
    }

    private byte[] BuildWriteMultipleRequest(byte functionCode, ushort startAddress, ushort[] values)
    {
        var quantity = (ushort)values.Length;
        var byteCount = (byte)(quantity * 2);
        var length = 7 + byteCount;

        var request = new byte[9 + byteCount + 4];
        var transactionId = GetNextTransactionId();

        // MBAP Header
        request[0] = (byte)(transactionId >> 8);
        request[1] = (byte)(transactionId & 0xFF);
        request[2] = 0x00;
        request[3] = 0x00;
        request[4] = (byte)(length >> 8);
        request[5] = (byte)(length & 0xFF);
        request[6] = _unitId;

        // PDU
        request[7] = functionCode;
        request[8] = (byte)(startAddress >> 8);
        request[9] = (byte)(startAddress & 0xFF);
        request[10] = (byte)(quantity >> 8);
        request[11] = (byte)(quantity & 0xFF);
        request[12] = byteCount;

        // Data
        for (int i = 0; i < values.Length; i++)
        {
            request[13 + i * 2] = (byte)(values[i] >> 8);
            request[13 + i * 2 + 1] = (byte)(values[i] & 0xFF);
        }

        return request;
    }

    private async Task<byte[]> SendRequestAsync(byte[] request, CancellationToken ct)
    {
        await _stream!.WriteAsync(request, ct);

        // Read MBAP header first (7 bytes)
        var header = new byte[7];
        var headerRead = await ReadExactAsync(header, 7, ct);
        if (headerRead != 7)
        {
            throw new IOException("Failed to read MBAP header");
        }

        // Get PDU length from header
        var pduLength = (header[4] << 8) | header[5];

        // Read PDU (pduLength - 1 because Unit ID is already included in header)
        var pduBytesToRead = pduLength - 1;
        var pdu = new byte[pduBytesToRead];
        var pduRead = await ReadExactAsync(pdu, pduBytesToRead, ct);
        if (pduRead != pduBytesToRead)
        {
            throw new IOException($"Failed to read PDU: expected {pduBytesToRead}, got {pduRead}");
        }

        // Check for exception response
        if ((pdu[0] & 0x80) != 0)
        {
            var exceptionCode = pdu.Length > 1 ? pdu[1] : 0;
            throw new ModbusException(pdu[0] & 0x7F, exceptionCode);
        }

        // Combine header and pdu
        var response = new byte[7 + pduBytesToRead];
        Array.Copy(header, response, 7);
        Array.Copy(pdu, 0, response, 7, pduBytesToRead);

        return response;
    }

    private async Task<int> ReadExactAsync(byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var read = await _stream!.ReadAsync(buffer, totalRead, count - totalRead, ct);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }

    private bool[] ParseBooleanResponse(byte[] response, ushort quantity)
    {
        // Data starts at offset 9 (after MBAP header + function code + byte count)
        var byteCount = response[8];
        var result = new bool[quantity];

        for (int i = 0; i < quantity; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            result[i] = (response[9 + byteIndex] & (1 << bitIndex)) != 0;
        }

        return result;
    }

    private ushort[] ParseRegisterResponse(byte[] response, ushort quantity)
    {
        // Data starts at offset 9 (after MBAP header + function code + byte count)
        var result = new ushort[quantity];

        for (int i = 0; i < quantity; i++)
        {
            var offset = 9 + i * 2;
            result[i] = (ushort)((response[offset] << 8) | response[offset + 1]);
        }

        return result;
    }

    private bool ValidateWriteResponse(byte[] response, byte expectedFunction)
    {
        // Function code at offset 7
        return response.Length >= 8 && response[7] == expectedFunction;
    }

    #endregion

    #region Address Parsing

    private ModbusAddressInfo ParseModbusAddress(string address)
    {
        address = address.ToUpperInvariant().Trim();

        // Holding Register: HR0, HR0.5, HR100
        if (address.StartsWith("HR"))
        {
            var rest = address.Substring(2);
            if (rest.Contains('.'))
            {
                var parts = rest.Split('.');
                return new ModbusAddressInfo
                {
                    Type = ModbusDataType.HoldingRegister,
                    Address = ushort.Parse(parts[0]),
                    BitOffset = int.Parse(parts[1]),
                    IsBitAccess = true,
                    Count = 1
                };
            }

            // Check for count: HR0:10
            if (rest.Contains(':'))
            {
                var parts = rest.Split(':');
                return new ModbusAddressInfo
                {
                    Type = ModbusDataType.HoldingRegister,
                    Address = ushort.Parse(parts[0]),
                    Count = ushort.Parse(parts[1])
                };
            }

            return new ModbusAddressInfo
            {
                Type = ModbusDataType.HoldingRegister,
                Address = ushort.Parse(rest),
                Count = 1
            };
        }

        // Input Register: IR0, IR100
        if (address.StartsWith("IR"))
        {
            var rest = address.Substring(2);
            if (rest.Contains(':'))
            {
                var parts = rest.Split(':');
                return new ModbusAddressInfo
                {
                    Type = ModbusDataType.InputRegister,
                    Address = ushort.Parse(parts[0]),
                    Count = ushort.Parse(parts[1])
                };
            }

            return new ModbusAddressInfo
            {
                Type = ModbusDataType.InputRegister,
                Address = ushort.Parse(rest),
                Count = 1
            };
        }

        // Coil: C0, C100
        if (address.StartsWith("C") && !address.StartsWith("CO"))
        {
            var rest = address.Substring(1);
            if (rest.Contains(':'))
            {
                var parts = rest.Split(':');
                return new ModbusAddressInfo
                {
                    Type = ModbusDataType.Coil,
                    Address = ushort.Parse(parts[0]),
                    Count = ushort.Parse(parts[1])
                };
            }

            return new ModbusAddressInfo
            {
                Type = ModbusDataType.Coil,
                Address = ushort.Parse(rest),
                Count = 1
            };
        }

        // Discrete Input: DI0, DI100
        if (address.StartsWith("DI"))
        {
            var rest = address.Substring(2);
            if (rest.Contains(':'))
            {
                var parts = rest.Split(':');
                return new ModbusAddressInfo
                {
                    Type = ModbusDataType.DiscreteInput,
                    Address = ushort.Parse(parts[0]),
                    Count = ushort.Parse(parts[1])
                };
            }

            return new ModbusAddressInfo
            {
                Type = ModbusDataType.DiscreteInput,
                Address = ushort.Parse(rest),
                Count = 1
            };
        }

        throw new ArgumentException($"Invalid Modbus address format: {address}");
    }

    #endregion

    #region Dispose

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CleanupConnection();
        }
        base.Dispose(disposing);
    }

    #endregion
}

#region Helper Types

internal enum ModbusDataType
{
    Coil,
    DiscreteInput,
    HoldingRegister,
    InputRegister
}

internal class ModbusAddressInfo
{
    public ModbusDataType Type { get; set; }
    public ushort Address { get; set; }
    public ushort Count { get; set; } = 1;
    public bool IsBitAccess { get; set; }
    public int BitOffset { get; set; }
}

public class ModbusException : Exception
{
    public int FunctionCode { get; }
    public int ExceptionCode { get; }

    public ModbusException(int functionCode, int exceptionCode)
        : base($"Modbus exception: Function 0x{functionCode:X2}, Exception 0x{exceptionCode:X2} ({GetExceptionMessage(exceptionCode)})")
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }

    private static string GetExceptionMessage(int code)
    {
        return code switch
        {
            0x01 => "Illegal Function",
            0x02 => "Illegal Data Address",
            0x03 => "Illegal Data Value",
            0x04 => "Slave Device Failure",
            0x05 => "Acknowledge",
            0x06 => "Slave Device Busy",
            0x08 => "Memory Parity Error",
            0x0A => "Gateway Path Unavailable",
            0x0B => "Gateway Target Device Failed to Respond",
            _ => "Unknown"
        };
    }
}

#endregion
