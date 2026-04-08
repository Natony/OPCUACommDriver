using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Services.Protocols;

/// <summary>
/// Kết nối đến Siemens S7 PLC qua giao thức S7comm TCP/IP
/// Hỗ trợ: S7-300, S7-400, S7-1200, S7-1500
/// Port: 102 (ISO-on-TCP)
/// </summary>
public class SiemensS7Connection : BaseProtocolConnection
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly byte[] _pduNegotiationRequest;
    private int _maxPduSize = 240;

    // S7 CPU Types for connection setup
    private enum S7CpuType : byte
    {
        S7300 = 0x02,
        S7400 = 0x03,
        S71200 = 0x00,
        S71500 = 0x00
    }

    protected override string ProtocolName => "Siemens S7";

    public SiemensS7Connection(PlcDevice device, ILogger logger) : base(device, logger)
    {
        _pduNegotiationRequest = BuildPduNegotiationRequest();
    }

    #region S7 Protocol Implementation

    protected override async Task<bool> ConnectInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            _client = new TcpClient
            {
                ReceiveTimeout = _device.ReadTimeout,
                SendTimeout = _device.WriteTimeout
            };

            // Connect TCP
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_device.ConnectionTimeout);

            await _client.ConnectAsync(_device.IpAddress, _device.Port, cts.Token);
            _stream = _client.GetStream();

            _logger.LogDebug("[Siemens S7] TCP connected to {IP}:{Port}", _device.IpAddress, _device.Port);

            // COTP Connection Request
            if (!await SendCotpConnectionRequestAsync(cancellationToken))
            {
                _logger.LogError("[Siemens S7] COTP connection failed");
                return false;
            }

            // S7 Communication Setup (PDU negotiation)
            if (!await SendPduNegotiationAsync(cancellationToken))
            {
                _logger.LogError("[Siemens S7] PDU negotiation failed");
                return false;
            }

            _logger.LogInformation("[Siemens S7] Connected to {Name} ({IP}:{Port}) - PDU Size: {PduSize}",
                _device.Name, _device.IpAddress, _device.Port, _maxPduSize);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Siemens S7] Connection failed to {IP}:{Port}", _device.IpAddress, _device.Port);
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
            _logger.LogWarning(ex, "[Siemens S7] Error during cleanup");
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
            // Parse address: DB1.DBW0, MW100, IW0, QW0, etc.
            var (area, dbNumber, startAddress, dataLength) = ParseS7Address(address);

            // Build S7 Read Request
            var request = BuildReadRequest(area, dbNumber, startAddress, dataLength);

            // Send request
            await _stream.WriteAsync(request, cancellationToken);

            // Read response
            var response = await ReadResponseAsync(cancellationToken);

            // Parse response and extract value
            var value = ParseReadResponse(response, address);

            return new TagValue
            {
                NodeId = address,
                Value = value,
                Quality = TagQuality.Good,
                Timestamp = DateTime.Now,
                ServerTimestamp = DateTime.Now,
                StatusCode = "Good"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Siemens S7] Read failed for {Address}", address);
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
            // Parse address
            var (area, dbNumber, startAddress, dataLength) = ParseS7Address(address);

            // Convert value to bytes
            var dataBytes = ConvertValueToBytes(value, dataLength);

            // Build S7 Write Request
            var request = BuildWriteRequest(area, dbNumber, startAddress, dataBytes);

            // Send request
            await _stream.WriteAsync(request, cancellationToken);

            // Read response
            var response = await ReadResponseAsync(cancellationToken);

            // Check response for success
            return ValidateWriteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Siemens S7] Write failed for {Address}", address);
            return false;
        }
    }

    #endregion

    #region S7 Protocol Helpers

    private async Task<bool> SendCotpConnectionRequestAsync(CancellationToken cancellationToken)
    {
        // TPKT Header + COTP Connection Request
        var rack = (byte)_device.Rack;
        var slot = (byte)_device.Slot;

        var cotpCr = new byte[]
        {
            0x03, 0x00, 0x00, 0x16, // TPKT: Version 3, Reserved, Length = 22
            0x11,                   // COTP: Length = 17
            0xE0,                   // COTP: CR (Connection Request)
            0x00, 0x00,             // Destination reference
            0x00, 0x01,             // Source reference
            0x00,                   // Class 0
            0xC0, 0x01, 0x0A,       // Parameter: TPDU size (1024)
            0xC1, 0x02, 0x01, 0x00, // Parameter: Source TSAP
            0xC2, 0x02, 0x01, (byte)(rack * 32 + slot) // Parameter: Destination TSAP
        };

        await _stream!.WriteAsync(cotpCr, cancellationToken);

        // Read COTP Connection Confirm
        var response = new byte[22];
        var bytesRead = await _stream.ReadAsync(response, 0, response.Length, cancellationToken);

        // Check for COTP CC (Connection Confirm)
        return bytesRead >= 7 && response[5] == 0xD0;
    }

    private async Task<bool> SendPduNegotiationAsync(CancellationToken cancellationToken)
    {
        await _stream!.WriteAsync(_pduNegotiationRequest, cancellationToken);

        var response = new byte[27];
        var bytesRead = await _stream.ReadAsync(response, 0, response.Length, cancellationToken);

        if (bytesRead >= 27)
        {
            // Extract max PDU size from response
            _maxPduSize = (response[25] << 8) | response[26];
            return true;
        }

        return false;
    }

    private byte[] BuildPduNegotiationRequest()
    {
        // TPKT + COTP DT + S7 Setup Communication
        return new byte[]
        {
            0x03, 0x00, 0x00, 0x19,       // TPKT: Version 3, Length = 25
            0x02, 0xF0, 0x80,             // COTP: DT (Data Transfer)
            0x32, 0x01, 0x00, 0x00,       // S7: Protocol ID, Job type
            0x00, 0x00, 0x00, 0x08,       // S7: Parameters length
            0x00, 0x00,                   // S7: Data length
            0xF0,                         // S7: Function: Setup Communication
            0x00,                         // S7: Reserved
            0x00, 0x01,                   // S7: Max AmQ calling
            0x00, 0x01,                   // S7: Max AmQ called
            0x01, 0xE0                    // S7: PDU length (480)
        };
    }

    private (byte Area, int DbNumber, int StartAddress, int DataLength) ParseS7Address(string address)
    {
        address = address.ToUpperInvariant().Trim();

        // Data Block: DB1.DBW0, DB1.DBD100, DB10.DBX0.0
        if (address.StartsWith("DB"))
        {
            var parts = address.Split('.');
            var dbNumber = int.Parse(parts[0].Substring(2));
            var offsetPart = parts[1];

            byte area = 0x84; // Data Block area
            int startAddress;
            int dataLength;

            if (offsetPart.StartsWith("DBX"))
            {
                // Bit: DBX0.0
                startAddress = int.Parse(offsetPart.Substring(3).Split('.')[0]) * 8;
                if (parts.Length > 2) startAddress += int.Parse(parts[2]);
                dataLength = 1;
            }
            else if (offsetPart.StartsWith("DBB"))
            {
                startAddress = int.Parse(offsetPart.Substring(3));
                dataLength = 1;
            }
            else if (offsetPart.StartsWith("DBW"))
            {
                startAddress = int.Parse(offsetPart.Substring(3));
                dataLength = 2;
            }
            else if (offsetPart.StartsWith("DBD"))
            {
                startAddress = int.Parse(offsetPart.Substring(3));
                dataLength = 4;
            }
            else
            {
                throw new ArgumentException($"Invalid DB address format: {address}");
            }

            return (area, dbNumber, startAddress, dataLength);
        }

        // Memory: MW0, MD100, MB50
        if (address.StartsWith("M"))
        {
            byte area = 0x83; // Merker area
            int startAddress;
            int dataLength;

            if (address.StartsWith("MX"))
            {
                startAddress = int.Parse(address.Substring(2).Split('.')[0]) * 8;
                dataLength = 1;
            }
            else if (address.StartsWith("MB"))
            {
                startAddress = int.Parse(address.Substring(2));
                dataLength = 1;
            }
            else if (address.StartsWith("MW"))
            {
                startAddress = int.Parse(address.Substring(2));
                dataLength = 2;
            }
            else if (address.StartsWith("MD"))
            {
                startAddress = int.Parse(address.Substring(2));
                dataLength = 4;
            }
            else
            {
                throw new ArgumentException($"Invalid M address format: {address}");
            }

            return (area, 0, startAddress, dataLength);
        }

        // Inputs: IW0, ID100, IB50
        if (address.StartsWith("I") || address.StartsWith("E"))
        {
            byte area = 0x81; // Input area
            return ParseAreaAddress(address.Substring(1), area);
        }

        // Outputs: QW0, QD100, QB50
        if (address.StartsWith("Q") || address.StartsWith("A"))
        {
            byte area = 0x82; // Output area
            return ParseAreaAddress(address.Substring(1), area);
        }

        throw new ArgumentException($"Unsupported address format: {address}");
    }

    private (byte Area, int DbNumber, int StartAddress, int DataLength) ParseAreaAddress(string addressPart, byte area)
    {
        int startAddress;
        int dataLength;

        if (addressPart.StartsWith("X"))
        {
            startAddress = int.Parse(addressPart.Substring(1).Split('.')[0]) * 8;
            dataLength = 1;
        }
        else if (addressPart.StartsWith("B"))
        {
            startAddress = int.Parse(addressPart.Substring(1));
            dataLength = 1;
        }
        else if (addressPart.StartsWith("W"))
        {
            startAddress = int.Parse(addressPart.Substring(1));
            dataLength = 2;
        }
        else if (addressPart.StartsWith("D"))
        {
            startAddress = int.Parse(addressPart.Substring(1));
            dataLength = 4;
        }
        else
        {
            // Assume word by default
            startAddress = int.Parse(addressPart);
            dataLength = 2;
        }

        return (area, 0, startAddress, dataLength);
    }

    private byte[] BuildReadRequest(byte area, int dbNumber, int startAddress, int dataLength)
    {
        // S7 Read Variable Request
        var request = new byte[31];

        // TPKT Header
        request[0] = 0x03; // Version
        request[1] = 0x00; // Reserved
        request[2] = 0x00; // Length high
        request[3] = 0x1F; // Length low (31 bytes)

        // COTP Data
        request[4] = 0x02; // Length
        request[5] = 0xF0; // PDU type (DT)
        request[6] = 0x80; // Last data unit

        // S7 Header
        request[7] = 0x32;  // Protocol ID
        request[8] = 0x01;  // ROSCTR: Job
        request[9] = 0x00;  // Redundancy
        request[10] = 0x00; // Redundancy
        request[11] = 0x00; // PDU Reference high
        request[12] = 0x01; // PDU Reference low
        request[13] = 0x00; // Parameter length high
        request[14] = 0x0E; // Parameter length low (14)
        request[15] = 0x00; // Data length high
        request[16] = 0x00; // Data length low

        // S7 Read Parameters
        request[17] = 0x04; // Function: Read Var
        request[18] = 0x01; // Item count

        // Item specification
        request[19] = 0x12; // Variable specification
        request[20] = 0x0A; // Length of addressing
        request[21] = 0x10; // Syntax ID: S7ANY

        // Transport size
        request[22] = dataLength switch
        {
            1 => 0x02, // Byte
            2 => 0x04, // Word
            4 => 0x06, // DWord
            _ => 0x02
        };

        // Length
        request[23] = 0x00;
        request[24] = (byte)dataLength;

        // DB number
        request[25] = (byte)(dbNumber >> 8);
        request[26] = (byte)(dbNumber & 0xFF);

        // Area
        request[27] = area;

        // Address (bit address for S7)
        var bitAddress = startAddress * 8;
        request[28] = (byte)((bitAddress >> 16) & 0xFF);
        request[29] = (byte)((bitAddress >> 8) & 0xFF);
        request[30] = (byte)(bitAddress & 0xFF);

        return request;
    }

    private byte[] BuildWriteRequest(byte area, int dbNumber, int startAddress, byte[] data)
    {
        var dataLength = data.Length;
        var requestLength = 35 + dataLength;
        var request = new byte[requestLength];

        // TPKT Header
        request[0] = 0x03;
        request[1] = 0x00;
        request[2] = (byte)((requestLength >> 8) & 0xFF);
        request[3] = (byte)(requestLength & 0xFF);

        // COTP Data
        request[4] = 0x02;
        request[5] = 0xF0;
        request[6] = 0x80;

        // S7 Header
        request[7] = 0x32;
        request[8] = 0x01;
        request[9] = 0x00;
        request[10] = 0x00;
        request[11] = 0x00;
        request[12] = 0x01;
        request[13] = 0x00;
        request[14] = 0x0E; // Parameter length
        request[15] = (byte)(((dataLength + 4) >> 8) & 0xFF);
        request[16] = (byte)((dataLength + 4) & 0xFF);

        // S7 Write Parameters
        request[17] = 0x05; // Function: Write Var
        request[18] = 0x01; // Item count

        // Item specification
        request[19] = 0x12;
        request[20] = 0x0A;
        request[21] = 0x10;

        // Transport size
        request[22] = dataLength switch
        {
            1 => 0x02,
            2 => 0x04,
            4 => 0x06,
            _ => 0x02
        };

        request[23] = 0x00;
        request[24] = (byte)dataLength;

        // DB number
        request[25] = (byte)(dbNumber >> 8);
        request[26] = (byte)(dbNumber & 0xFF);

        // Area
        request[27] = area;

        // Address
        var bitAddress = startAddress * 8;
        request[28] = (byte)((bitAddress >> 16) & 0xFF);
        request[29] = (byte)((bitAddress >> 8) & 0xFF);
        request[30] = (byte)(bitAddress & 0xFF);

        // Data header
        request[31] = 0x00;
        request[32] = 0x04; // Transport size: Byte
        request[33] = (byte)((dataLength * 8 >> 8) & 0xFF);
        request[34] = (byte)((dataLength * 8) & 0xFF);

        // Data
        Array.Copy(data, 0, request, 35, dataLength);

        return request;
    }

    private async Task<byte[]> ReadResponseAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

        var response = new byte[bytesRead];
        Array.Copy(buffer, response, bytesRead);
        return response;
    }

    private object? ParseReadResponse(byte[] response, string address)
    {
        // Check minimum response length
        if (response.Length < 25)
        {
            throw new Exception($"Invalid response length: {response.Length}");
        }

        // Check S7 return code
        var returnCode = response[21];
        if (returnCode != 0xFF)
        {
            throw new Exception($"S7 error code: 0x{returnCode:X2}");
        }

        // Data starts at offset 25
        var dataOffset = 25;
        var transportSize = response[22];
        var dataLength = (response[23] << 8) | response[24];

        // Convert to bits to bytes if needed
        if (transportSize == 0x03 || transportSize == 0x04)
        {
            dataLength /= 8;
        }

        // Extract data based on length
        return dataLength switch
        {
            1 => response[dataOffset],
            2 => (short)((response[dataOffset] << 8) | response[dataOffset + 1]),
            4 => (int)((response[dataOffset] << 24) | (response[dataOffset + 1] << 16) |
                       (response[dataOffset + 2] << 8) | response[dataOffset + 3]),
            _ => null
        };
    }

    private bool ValidateWriteResponse(byte[] response)
    {
        // Check for successful write (return code 0xFF)
        return response.Length >= 22 && response[21] == 0xFF;
    }

    private byte[] ConvertValueToBytes(object value, int length)
    {
        return length switch
        {
            1 => new[] { Convert.ToByte(value) },
            2 => BitConverter.GetBytes(Convert.ToInt16(value)).Reverse().ToArray(),
            4 => BitConverter.GetBytes(Convert.ToInt32(value)).Reverse().ToArray(),
            _ => throw new ArgumentException($"Unsupported data length: {length}")
        };
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
