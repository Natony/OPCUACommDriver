using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Services.Protocols;

/// <summary>
/// Kết nối đến Mitsubishi PLC qua giao thức MC Protocol (MELSEC Communication)
/// Hỗ trợ: FX5U, FX3U, Q Series, iQ-R Series
/// Port mặc định: 5000, 5001
/// </summary>
public class MitsubishiMcConnection : BaseProtocolConnection
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    // MC Protocol Frame types
    private enum McFrameType
    {
        Frame3E,    // Binary, 3E Frame (QnA compatible)
        Frame4E,    // Binary, 4E Frame (iQ-R)
        FrameAscii  // ASCII mode
    }

    // MC Protocol Device codes
    private static readonly Dictionary<string, (byte BinaryCode, string AsciiCode)> DeviceCodes = new()
    {
        { "D", (0xA8, "D*") },    // Data Register
        { "W", (0xB4, "W*") },    // Link Register
        { "R", (0xAF, "R*") },    // File Register
        { "M", (0x90, "M*") },    // Internal Relay
        { "X", (0x9C, "X*") },    // Input
        { "Y", (0x9D, "Y*") },    // Output
        { "L", (0x92, "L*") },    // Latch Relay
        { "F", (0x93, "F*") },    // Annunciator
        { "B", (0xA0, "B*") },    // Link Relay
        { "S", (0x98, "S*") },    // Step Relay
        { "SD", (0xA9, "SD") },   // Special Register
        { "SM", (0x91, "SM") },   // Special Relay
        { "TN", (0xC2, "TN") },   // Timer Current Value
        { "CN", (0xC5, "CN") },   // Counter Current Value
    };

    private readonly McFrameType _frameType;

    protected override string ProtocolName => "Mitsubishi MC";

    public MitsubishiMcConnection(PlcDevice device, ILogger logger) : base(device, logger)
    {
        // Default to 3E Frame for FX5U compatibility
        _frameType = device.PlcType switch
        {
            PlcType.MitsubishiIQR => McFrameType.Frame4E,
            _ => McFrameType.Frame3E
        };
    }

    #region MC Protocol Implementation

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

            await _client.ConnectAsync(_device.IpAddress, _device.Port, cts.Token);
            _stream = _client.GetStream();

            _logger.LogInformation("[Mitsubishi MC] Connected to {Name} ({IP}:{Port}) using {Frame}",
                _device.Name, _device.IpAddress, _device.Port, _frameType);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mitsubishi MC] Connection failed to {IP}:{Port}", _device.IpAddress, _device.Port);
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
            _logger.LogWarning(ex, "[Mitsubishi MC] Error during cleanup");
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
            var (deviceCode, startAddress, pointCount) = ParseMcAddress(address);

            // Build MC Read Request (3E Frame)
            var request = BuildReadRequest(deviceCode, startAddress, pointCount);

            // Send request
            await _stream.WriteAsync(request, cancellationToken);

            // Read response
            var response = await ReadResponseAsync(cancellationToken);

            // Parse response
            var value = ParseReadResponse(response, pointCount);

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
            _logger.LogError(ex, "[Mitsubishi MC] Read failed for {Address}", address);
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
            var (deviceCode, startAddress, pointCount) = ParseMcAddress(address);

            // Build MC Write Request (3E Frame)
            var request = BuildWriteRequest(deviceCode, startAddress, value, pointCount);

            // Send request
            await _stream.WriteAsync(request, cancellationToken);

            // Read response
            var response = await ReadResponseAsync(cancellationToken);

            // Check for success
            return ValidateWriteResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Mitsubishi MC] Write failed for {Address}", address);
            return false;
        }
    }

    #endregion

    #region MC Protocol Helpers

    private (string DeviceCode, int StartAddress, int PointCount) ParseMcAddress(string address)
    {
        address = address.ToUpperInvariant().Trim();

        // Parse format: D100, D100:10, W0, M0, X0, Y0, etc.
        var pointCount = 1;
        if (address.Contains(':'))
        {
            var parts = address.Split(':');
            address = parts[0];
            pointCount = int.Parse(parts[1]);
        }

        // Extract device code and address
        string deviceCode;
        int startAddress;

        // Check for two-character device codes first (SD, SM, TN, CN)
        if (address.Length >= 2 && DeviceCodes.ContainsKey(address.Substring(0, 2)))
        {
            deviceCode = address.Substring(0, 2);
            startAddress = int.Parse(address.Substring(2));
        }
        else if (DeviceCodes.ContainsKey(address.Substring(0, 1)))
        {
            deviceCode = address.Substring(0, 1);
            startAddress = int.Parse(address.Substring(1));
        }
        else
        {
            throw new ArgumentException($"Unknown device code in address: {address}");
        }

        return (deviceCode, startAddress, pointCount);
    }

    private byte[] BuildReadRequest(string deviceCode, int startAddress, int pointCount)
    {
        // MC Protocol 3E Frame (Binary) - Read Request
        var request = new List<byte>();

        // Subheader (2 bytes)
        request.Add(0x50); // Request subheader
        request.Add(0x00);

        // Network number (1 byte)
        request.Add(0x00);

        // PC number (1 byte)
        request.Add(0xFF);

        // Request destination module I/O number (2 bytes)
        request.Add(0xFF);
        request.Add(0x03);

        // Request destination module station number (1 byte)
        request.Add(0x00);

        // Request data length (2 bytes) - will be filled later
        var lengthPosition = request.Count;
        request.Add(0x00);
        request.Add(0x00);

        // Monitoring timer (2 bytes) - 10 x 250ms = 2.5 seconds
        request.Add(0x0A);
        request.Add(0x00);

        // Command (2 bytes) - Batch read
        var isWordDevice = IsWordDevice(deviceCode);
        if (isWordDevice)
        {
            request.Add(0x01); // Word device read
            request.Add(0x04);
        }
        else
        {
            request.Add(0x01); // Bit device read
            request.Add(0x04);
        }

        // Subcommand (2 bytes)
        request.Add(0x00);
        request.Add(0x00);

        // Device code (binary)
        var (binaryCode, _) = DeviceCodes[deviceCode];

        // Start device (4 bytes - 3 bytes address + 1 byte device code)
        request.Add((byte)(startAddress & 0xFF));
        request.Add((byte)((startAddress >> 8) & 0xFF));
        request.Add((byte)((startAddress >> 16) & 0xFF));
        request.Add(binaryCode);

        // Number of device points (2 bytes)
        request.Add((byte)(pointCount & 0xFF));
        request.Add((byte)((pointCount >> 8) & 0xFF));

        // Fill in request data length
        var dataLength = request.Count - lengthPosition - 2;
        request[lengthPosition] = (byte)(dataLength & 0xFF);
        request[lengthPosition + 1] = (byte)((dataLength >> 8) & 0xFF);

        return request.ToArray();
    }

    private byte[] BuildWriteRequest(string deviceCode, int startAddress, object value, int pointCount)
    {
        var request = new List<byte>();

        // Subheader
        request.Add(0x50);
        request.Add(0x00);

        // Network number
        request.Add(0x00);

        // PC number
        request.Add(0xFF);

        // Request destination module I/O number
        request.Add(0xFF);
        request.Add(0x03);

        // Request destination module station number
        request.Add(0x00);

        // Request data length - placeholder
        var lengthPosition = request.Count;
        request.Add(0x00);
        request.Add(0x00);

        // Monitoring timer
        request.Add(0x0A);
        request.Add(0x00);

        // Command - Batch write
        var isWordDevice = IsWordDevice(deviceCode);
        if (isWordDevice)
        {
            request.Add(0x01); // Word device write
            request.Add(0x14);
        }
        else
        {
            request.Add(0x01); // Bit device write
            request.Add(0x14);
        }

        // Subcommand
        request.Add(0x00);
        request.Add(0x00);

        // Device code
        var (binaryCode, _) = DeviceCodes[deviceCode];

        // Start device
        request.Add((byte)(startAddress & 0xFF));
        request.Add((byte)((startAddress >> 8) & 0xFF));
        request.Add((byte)((startAddress >> 16) & 0xFF));
        request.Add(binaryCode);

        // Number of device points
        request.Add((byte)(pointCount & 0xFF));
        request.Add((byte)((pointCount >> 8) & 0xFF));

        // Data to write
        var dataBytes = ConvertValueToBytes(value, isWordDevice);
        request.AddRange(dataBytes);

        // Fill in request data length
        var dataLength = request.Count - lengthPosition - 2;
        request[lengthPosition] = (byte)(dataLength & 0xFF);
        request[lengthPosition + 1] = (byte)((dataLength >> 8) & 0xFF);

        return request.ToArray();
    }

    private async Task<byte[]> ReadResponseAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

        var response = new byte[bytesRead];
        Array.Copy(buffer, response, bytesRead);
        return response;
    }

    private object? ParseReadResponse(byte[] response, int pointCount)
    {
        // Check minimum response length for 3E frame
        // Subheader(2) + Network(1) + PC(1) + IO(2) + Station(1) + Length(2) + EndCode(2) = 11 bytes minimum
        if (response.Length < 11)
        {
            throw new Exception($"Invalid response length: {response.Length}");
        }

        // Check end code (bytes 9-10, little endian)
        var endCode = response[9] | (response[10] << 8);
        if (endCode != 0x0000)
        {
            throw new Exception($"MC Protocol error code: 0x{endCode:X4}");
        }

        // Data starts at offset 11
        var dataOffset = 11;
        var availableData = response.Length - dataOffset;

        if (availableData < 2)
        {
            return null;
        }

        // Read word value (2 bytes, little endian)
        if (pointCount == 1)
        {
            return (short)(response[dataOffset] | (response[dataOffset + 1] << 8));
        }

        // Read multiple words
        var values = new short[pointCount];
        for (int i = 0; i < pointCount && (dataOffset + i * 2 + 1) < response.Length; i++)
        {
            values[i] = (short)(response[dataOffset + i * 2] | (response[dataOffset + i * 2 + 1] << 8));
        }
        return values;
    }

    private bool ValidateWriteResponse(byte[] response)
    {
        // Check minimum response length
        if (response.Length < 11)
        {
            return false;
        }

        // Check end code
        var endCode = response[9] | (response[10] << 8);
        return endCode == 0x0000;
    }

    private bool IsWordDevice(string deviceCode)
    {
        // Word devices: D, W, R, SD, TN, CN
        // Bit devices: M, X, Y, L, F, B, S, SM
        return deviceCode switch
        {
            "D" or "W" or "R" or "SD" or "TN" or "CN" => true,
            _ => false
        };
    }

    private byte[] ConvertValueToBytes(object value, bool isWordDevice)
    {
        if (isWordDevice)
        {
            var wordValue = Convert.ToInt16(value);
            return new[]
            {
                (byte)(wordValue & 0xFF),
                (byte)((wordValue >> 8) & 0xFF)
            };
        }
        else
        {
            // Bit device - 0 or 1
            var bitValue = Convert.ToBoolean(value) ? (byte)0x01 : (byte)0x00;
            return new[] { bitValue };
        }
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
