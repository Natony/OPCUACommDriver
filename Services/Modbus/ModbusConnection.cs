using System.Collections.Concurrent;
using System.Net.Sockets;
using NModbus;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.Modbus;

/// <summary>
/// Modbus TCP Connection cho 1 PLC
/// Sử dụng NModbus library để giao tiếp Modbus TCP
/// NodeId của Tag được dùng làm địa chỉ thanh ghi (register address)
/// </summary>
public class ModbusConnection : IPlcConnection
{
    private readonly ILogger _logger;
    private readonly PlcDevice _device;
    private readonly object _lock = new();

    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private PlcConnectionState _connectionState = PlcConnectionState.Disconnected;
    private CancellationTokenSource? _reconnectCts;
    private CancellationTokenSource? _pollingCts;
    private bool _disposed;

    // Cache giá trị tag đã đọc trước đó
    private readonly ConcurrentDictionary<string, object?> _lastValues = new();

    #region Properties

    public PlcDevice Device => _device;

    public PlcConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                var oldState = _connectionState;
                _connectionState = value;
                _device.ConnectionState = value;

                ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                {
                    PlcId = _device.Id,
                    PlcName = _device.Name,
                    OldState = oldState,
                    NewState = value
                });
            }
        }
    }

    public bool IsConnected => _tcpClient?.Connected == true && ConnectionState == PlcConnectionState.Connected;

    public string? SessionId => _tcpClient?.Connected == true ? $"Modbus-{_device.IpAddress}:{_device.Port}" : null;

    public DateTime? LastConnectedTime { get; private set; }

    public DateTime? LastDisconnectedTime { get; private set; }

    public string? LastError { get; private set; }

    #endregion

    #region Events

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<TagValueChangedEventArgs>? TagValueChanged;
    public event EventHandler<PlcErrorEventArgs>? ErrorOccurred;

    #endregion

    #region Constructor

    public ModbusConnection(PlcDevice device, ILogger logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.Debug("ModbusConnection created for {PlcName} ({IpAddress}:{Port})",
            device.Name, device.IpAddress, device.Port);
    }

    #endregion

    #region Connection Methods

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ModbusConnection));

        if (IsConnected)
        {
            _logger.Warning("Already connected to {PlcName}", _device.Name);
            return true;
        }

        try
        {
            ConnectionState = PlcConnectionState.Connecting;
            _logger.Information("Connecting to {PlcName} at {IpAddress}:{Port} via Modbus TCP...",
                _device.Name, _device.IpAddress, _device.Port);

            // Tạo TCP connection
            _tcpClient = new TcpClient();
            _tcpClient.ReceiveTimeout = _device.ReadTimeout;
            _tcpClient.SendTimeout = _device.WriteTimeout;

            // Connect với timeout
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_device.ConnectionTimeout);

            await _tcpClient.ConnectAsync(_device.IpAddress, _device.Port, connectCts.Token);

            // Tạo Modbus Master
            var factory = new ModbusFactory();
            _modbusMaster = factory.CreateMaster(_tcpClient);
            _modbusMaster.Transport.ReadTimeout = _device.ReadTimeout;
            _modbusMaster.Transport.WriteTimeout = _device.WriteTimeout;
            _modbusMaster.Transport.Retries = 3;

            ConnectionState = PlcConnectionState.Connected;
            LastConnectedTime = DateTime.Now;
            LastError = null;

            _logger.Information("Connected to {PlcName} via Modbus TCP. SlaveId: {SlaveId}",
                _device.Name, _device.SlaveId);

            // Bắt đầu polling tags
            StartPolling();

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            ConnectionState = PlcConnectionState.Error;
            _logger.Error(ex, "Failed to connect to {PlcName}: {Error}", _device.Name, ex.Message);

            CleanupConnection();
            RaiseError(ex.Message, ex, isCritical: true);

            if (_device.AutoReconnect)
            {
                StartAutoReconnect();
            }

            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_disposed) return;

        try
        {
            StopAutoReconnect();
            StopPolling();

            if (_tcpClient != null)
            {
                ConnectionState = PlcConnectionState.Disconnecting;
                _logger.Information("Disconnecting from {PlcName}...", _device.Name);

                CleanupConnection();

                _logger.Information("Disconnected from {PlcName}", _device.Name);
            }

            ConnectionState = PlcConnectionState.Disconnected;
            LastDisconnectedTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from {PlcName}", _device.Name);
            ConnectionState = PlcConnectionState.Disconnected;
        }
    }

    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        await Task.Delay(1000, cancellationToken);
        return await ConnectAsync(cancellationToken);
    }

    #endregion

    #region Read/Write Methods

    public async Task<TagValue?> ReadTagAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _modbusMaster == null)
        {
            _logger.Warning("Cannot read tag - not connected to {PlcName}", _device.Name);
            return null;
        }

        try
        {
            var tag = _device.Tags.FirstOrDefault(t => t.NodeId == nodeId);
            if (tag == null)
            {
                _logger.Warning("Tag with NodeId {NodeId} not found in {PlcName}", nodeId, _device.Name);
                return null;
            }

            var address = ParseRegisterAddress(nodeId);
            var registerCount = GetRegisterCount(tag.DataType);
            var value = await ReadModbusValueAsync(tag.RegisterType, address, registerCount, tag.DataType);

            return new TagValue
            {
                TagId = tag.Id,
                NodeId = nodeId,
                Value = value,
                Quality = TagQuality.Good,
                SourceTimestamp = DateTime.Now,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading tag {NodeId} from {PlcName}", nodeId, _device.Name);
            HandleCommunicationError(ex);
            return new TagValue
            {
                NodeId = nodeId,
                Value = null,
                Quality = TagQuality.Bad,
                Timestamp = DateTime.Now
            };
        }
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
    {
        var results = new List<TagValue>();

        if (!IsConnected || _modbusMaster == null)
        {
            _logger.Warning("Cannot read tags - not connected to {PlcName}", _device.Name);
            return results;
        }

        foreach (var nodeId in nodeIds)
        {
            var tagValue = await ReadTagAsync(nodeId, cancellationToken);
            if (tagValue != null)
            {
                results.Add(tagValue);
            }
        }

        return results;
    }

    public async Task<bool> WriteTagAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _modbusMaster == null)
        {
            _logger.Warning("Cannot write tag - not connected to {PlcName}", _device.Name);
            return false;
        }

        try
        {
            var tag = _device.Tags.FirstOrDefault(t => t.NodeId == nodeId);
            var address = ParseRegisterAddress(nodeId);
            var registerType = tag?.RegisterType ?? ModbusRegisterType.HoldingRegister;

            switch (registerType)
            {
                case ModbusRegisterType.Coil:
                    await _modbusMaster.WriteSingleCoilAsync(_device.SlaveId, address, Convert.ToBoolean(value));
                    break;

                case ModbusRegisterType.HoldingRegister:
                    var registerValues = ConvertToRegisters(value, tag?.DataType ?? TagDataType.UInt16);
                    if (registerValues.Length == 1)
                    {
                        await _modbusMaster.WriteSingleRegisterAsync(_device.SlaveId, address, registerValues[0]);
                    }
                    else
                    {
                        await _modbusMaster.WriteMultipleRegistersAsync(_device.SlaveId, address, registerValues);
                    }
                    break;

                default:
                    _logger.Warning("Cannot write to {RegisterType} - read only", registerType);
                    return false;
            }

            _logger.Debug("Written value {Value} to {NodeId} on {PlcName}", value, nodeId, _device.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to tag {NodeId} on {PlcName}", nodeId, _device.Name);
            HandleCommunicationError(ex);
            return false;
        }
    }

    public async Task<IReadOnlyList<bool>> WriteTagsAsync(IReadOnlyList<(string NodeId, object Value)> items, CancellationToken cancellationToken = default)
    {
        var results = new List<bool>();

        foreach (var (nodeId, value) in items)
        {
            var success = await WriteTagAsync(nodeId, value, cancellationToken);
            results.Add(success);
        }

        return results;
    }

    #endregion

    #region Subscription Methods (Modbus dùng polling thay cho subscription)

    public Task<bool> CreateSubscriptionAsync(SubscriptionGroup group, CancellationToken cancellationToken = default)
    {
        // Modbus không có subscription, sử dụng polling
        _logger.Debug("Modbus uses polling instead of subscriptions. Ignoring CreateSubscription for {GroupName}", group.Name);
        return Task.FromResult(true);
    }

    public Task<bool> RemoveSubscriptionAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> AddTagToSubscriptionAsync(string groupId, TagItem tag, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> RemoveTagFromSubscriptionAsync(string groupId, string tagId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    #endregion

    #region Browse Methods (Modbus không hỗ trợ browse)

    public Task<IReadOnlyList<BrowseNode>> BrowseAsync(string? nodeId = null, CancellationToken cancellationToken = default)
    {
        // Modbus không hỗ trợ browse - trả về danh sách tags đã cấu hình
        var nodes = _device.Tags.Select(t => new BrowseNode
        {
            NodeId = t.NodeId,
            DisplayName = t.Name,
            BrowseName = t.Name,
            NodeClass = "Variable",
            DataType = t.DataType.ToString(),
            HasChildren = t.HasBitMapping,
            Description = t.Description
        }).ToList();

        return Task.FromResult<IReadOnlyList<BrowseNode>>(nodes);
    }

    public Task<NodeInfo?> GetNodeInfoAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var tag = _device.Tags.FirstOrDefault(t => t.NodeId == nodeId);
        if (tag == null) return Task.FromResult<NodeInfo?>(null);

        var nodeInfo = new NodeInfo
        {
            NodeId = nodeId,
            DisplayName = tag.Name,
            BrowseName = tag.Name,
            NodeClass = "Variable",
            DataType = tag.DataType.ToString(),
            Description = tag.Description,
            IsReadable = tag.CanRead,
            IsWritable = tag.CanWrite,
            CurrentValue = tag.Value
        };

        return Task.FromResult<NodeInfo?>(nodeInfo);
    }

    #endregion

    #region Polling Logic

    private void StartPolling()
    {
        StopPolling();

        _pollingCts = new CancellationTokenSource();
        _ = PollingLoopAsync(_pollingCts.Token);

        _logger.Information("Started polling for {PlcName} with {TagCount} tags",
            _device.Name, _device.Tags.Count(t => t.IsEnabled));
    }

    private void StopPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    private async Task PollingLoopAsync(CancellationToken cancellationToken)
    {
        // Nhóm tags theo ScanRate
        var tagGroups = _device.Tags
            .Where(t => t.IsEnabled)
            .GroupBy(t => t.ScanRate)
            .ToList();

        // Tạo polling task cho mỗi nhóm scan rate
        var pollingTasks = tagGroups.Select(group =>
            PollTagGroupAsync(group.ToList(), group.Key, cancellationToken));

        try
        {
            await Task.WhenAll(pollingTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Polling error for {PlcName}", _device.Name);
        }
    }

    private async Task PollTagGroupAsync(List<TagItem> tags, int scanRateMs, CancellationToken cancellationToken)
    {
        _logger.Debug("Polling group with {Count} tags at {ScanRate}ms for {PlcName}",
            tags.Count, scanRateMs, _device.Name);

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                foreach (var tag in tags)
                {
                    if (cancellationToken.IsCancellationRequested || !IsConnected) break;

                    await PollSingleTagAsync(tag);
                }

                await Task.Delay(scanRateMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error in polling group for {PlcName}", _device.Name);
                HandleCommunicationError(ex);

                if (!IsConnected) break;

                // Chờ trước khi thử lại
                try
                {
                    await Task.Delay(scanRateMs * 2, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task PollSingleTagAsync(TagItem tag)
    {
        try
        {
            if (_modbusMaster == null || !IsConnected) return;

            var address = ParseRegisterAddress(tag.NodeId);
            var registerCount = GetRegisterCount(tag.DataType);

            // Đọc giá trị thanh ghi
            var value = await ReadModbusValueAsync(tag.RegisterType, address, registerCount, tag.DataType);

            // Xử lý BitMapping nếu có
            if (tag.HasBitMapping && value is ushort registerValue)
            {
                ProcessBitMapping(tag, registerValue);
            }

            // Kiểm tra giá trị có thay đổi không
            var valueChanged = !Equals(value, _lastValues.GetValueOrDefault(tag.Id));
            _lastValues[tag.Id] = value;

            // Cập nhật tag
            var now = DateTime.Now;
            tag.UpdateValue(value, TagQuality.Good, now);

            if (valueChanged)
            {
                var tagValue = new TagValue
                {
                    TagId = tag.Id,
                    NodeId = tag.NodeId,
                    Value = value,
                    Quality = TagQuality.Good,
                    SourceTimestamp = now,
                    Timestamp = now
                };

                TagValueChanged?.Invoke(this, new TagValueChangedEventArgs
                {
                    PlcId = _device.Id,
                    TagId = tag.Id,
                    NodeId = tag.NodeId,
                    Value = tagValue
                });
            }
        }
        catch (Exception ex)
        {
            tag.SetError(ex.Message);
            _logger.Warning("Error polling tag {TagName} ({NodeId}): {Error}",
                tag.Name, tag.NodeId, ex.Message);
            throw;
        }
    }

    private void ProcessBitMapping(TagItem tag, ushort registerValue)
    {
        if (tag.BitMapping == null) return;

        foreach (var mapping in tag.BitMapping)
        {
            var bitValue = mapping.ExtractBit(registerValue);
            var bitTagId = $"{tag.Id}_bit{mapping.Bit}";
            var previousBitValue = _lastValues.GetValueOrDefault(bitTagId);

            if (!Equals(bitValue, previousBitValue))
            {
                _lastValues[bitTagId] = bitValue;

                var bitTagValue = new TagValue
                {
                    TagId = bitTagId,
                    NodeId = $"{tag.NodeId}.{mapping.Bit}",
                    Value = bitValue,
                    Quality = TagQuality.Good,
                    SourceTimestamp = DateTime.Now,
                    Timestamp = DateTime.Now
                };

                TagValueChanged?.Invoke(this, new TagValueChangedEventArgs
                {
                    PlcId = _device.Id,
                    TagId = bitTagId,
                    NodeId = bitTagValue.NodeId,
                    Value = bitTagValue
                });
            }
        }
    }

    #endregion

    #region Modbus Read/Write Helpers

    private async Task<object?> ReadModbusValueAsync(ModbusRegisterType registerType, ushort address, ushort count, TagDataType dataType)
    {
        if (_modbusMaster == null) return null;

        switch (registerType)
        {
            case ModbusRegisterType.Coil:
            {
                var coils = await _modbusMaster.ReadCoilsAsync(_device.SlaveId, address, 1);
                return coils.Length > 0 ? coils[0] : false;
            }

            case ModbusRegisterType.DiscreteInput:
            {
                var inputs = await _modbusMaster.ReadInputsAsync(_device.SlaveId, address, 1);
                return inputs.Length > 0 ? inputs[0] : false;
            }

            case ModbusRegisterType.HoldingRegister:
            {
                var registers = await _modbusMaster.ReadHoldingRegistersAsync(_device.SlaveId, address, count);
                return ConvertFromRegisters(registers, dataType);
            }

            case ModbusRegisterType.InputRegister:
            {
                var registers = await _modbusMaster.ReadInputRegistersAsync(_device.SlaveId, address, count);
                return ConvertFromRegisters(registers, dataType);
            }

            default:
                return null;
        }
    }

    private static object? ConvertFromRegisters(ushort[] registers, TagDataType dataType)
    {
        if (registers.Length == 0) return null;

        switch (dataType)
        {
            case TagDataType.Boolean:
                return registers[0] != 0;

            case TagDataType.Byte:
                return (byte)(registers[0] & 0xFF);

            case TagDataType.SByte:
                return (sbyte)(registers[0] & 0xFF);

            case TagDataType.Int16:
                return (short)registers[0];

            case TagDataType.UInt16:
                return registers[0];

            case TagDataType.Int32:
                if (registers.Length >= 2)
                    return (int)((registers[0] << 16) | registers[1]);
                return (int)(short)registers[0];

            case TagDataType.UInt32:
                if (registers.Length >= 2)
                    return (uint)((registers[0] << 16) | registers[1]);
                return (uint)registers[0];

            case TagDataType.Int64:
                if (registers.Length >= 4)
                    return (long)(((long)registers[0] << 48) | ((long)registers[1] << 32) |
                                  ((long)registers[2] << 16) | registers[3]);
                return (long)(short)registers[0];

            case TagDataType.UInt64:
                if (registers.Length >= 4)
                    return (ulong)(((ulong)registers[0] << 48) | ((ulong)registers[1] << 32) |
                                   ((ulong)registers[2] << 16) | registers[3]);
                return (ulong)registers[0];

            case TagDataType.Float:
                if (registers.Length >= 2)
                {
                    var bytes = new byte[4];
                    bytes[0] = (byte)(registers[1] & 0xFF);
                    bytes[1] = (byte)(registers[1] >> 8);
                    bytes[2] = (byte)(registers[0] & 0xFF);
                    bytes[3] = (byte)(registers[0] >> 8);
                    return BitConverter.ToSingle(bytes, 0);
                }
                return (float)registers[0];

            case TagDataType.Double:
                if (registers.Length >= 4)
                {
                    var bytes = new byte[8];
                    bytes[0] = (byte)(registers[3] & 0xFF);
                    bytes[1] = (byte)(registers[3] >> 8);
                    bytes[2] = (byte)(registers[2] & 0xFF);
                    bytes[3] = (byte)(registers[2] >> 8);
                    bytes[4] = (byte)(registers[1] & 0xFF);
                    bytes[5] = (byte)(registers[1] >> 8);
                    bytes[6] = (byte)(registers[0] & 0xFF);
                    bytes[7] = (byte)(registers[0] >> 8);
                    return BitConverter.ToDouble(bytes, 0);
                }
                return (double)registers[0];

            default:
            case TagDataType.Unknown:
                return registers[0];
        }
    }

    private static ushort[] ConvertToRegisters(object value, TagDataType dataType)
    {
        switch (dataType)
        {
            case TagDataType.Boolean:
                return new[] { (ushort)(Convert.ToBoolean(value) ? 1 : 0) };

            case TagDataType.Byte:
            case TagDataType.SByte:
            case TagDataType.Int16:
            case TagDataType.UInt16:
                return new[] { Convert.ToUInt16(value) };

            case TagDataType.Int32:
            {
                var intVal = Convert.ToInt32(value);
                return new[]
                {
                    (ushort)((intVal >> 16) & 0xFFFF),
                    (ushort)(intVal & 0xFFFF)
                };
            }

            case TagDataType.UInt32:
            {
                var uintVal = Convert.ToUInt32(value);
                return new[]
                {
                    (ushort)((uintVal >> 16) & 0xFFFF),
                    (ushort)(uintVal & 0xFFFF)
                };
            }

            case TagDataType.Float:
            {
                var floatBytes = BitConverter.GetBytes(Convert.ToSingle(value));
                return new[]
                {
                    (ushort)(floatBytes[3] << 8 | floatBytes[2]),
                    (ushort)(floatBytes[1] << 8 | floatBytes[0])
                };
            }

            case TagDataType.Double:
            {
                var doubleBytes = BitConverter.GetBytes(Convert.ToDouble(value));
                return new[]
                {
                    (ushort)(doubleBytes[7] << 8 | doubleBytes[6]),
                    (ushort)(doubleBytes[5] << 8 | doubleBytes[4]),
                    (ushort)(doubleBytes[3] << 8 | doubleBytes[2]),
                    (ushort)(doubleBytes[1] << 8 | doubleBytes[0])
                };
            }

            default:
                return new[] { Convert.ToUInt16(value) };
        }
    }

    private static ushort ParseRegisterAddress(string nodeId)
    {
        if (ushort.TryParse(nodeId, out var address))
            return address;

        // Hỗ trợ format "HR100", "IR50", "C10", "DI20"
        var upper = nodeId.ToUpperInvariant();
        if (upper.StartsWith("HR") && ushort.TryParse(upper[2..], out var hrAddr))
            return hrAddr;
        if (upper.StartsWith("IR") && ushort.TryParse(upper[2..], out var irAddr))
            return irAddr;
        if (upper.StartsWith("C") && ushort.TryParse(upper[1..], out var cAddr))
            return cAddr;
        if (upper.StartsWith("DI") && ushort.TryParse(upper[2..], out var diAddr))
            return diAddr;

        throw new FormatException($"Invalid Modbus register address: {nodeId}");
    }

    private static ushort GetRegisterCount(TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Boolean => 1,
            TagDataType.Byte => 1,
            TagDataType.SByte => 1,
            TagDataType.Int16 => 1,
            TagDataType.UInt16 => 1,
            TagDataType.Int32 => 2,
            TagDataType.UInt32 => 2,
            TagDataType.Float => 2,
            TagDataType.Int64 => 4,
            TagDataType.UInt64 => 4,
            TagDataType.Double => 4,
            _ => 1
        };
    }

    #endregion

    #region Helper Methods

    private void CleanupConnection()
    {
        try
        {
            _modbusMaster?.Dispose();
            _modbusMaster = null;

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error cleaning up Modbus connection");
        }
    }

    private void HandleCommunicationError(Exception ex)
    {
        // Kiểm tra lỗi mất kết nối
        if (ex is SocketException || ex is IOException ||
            ex is TimeoutException || ex is InvalidOperationException)
        {
            if (ConnectionState == PlcConnectionState.Connected)
            {
                _logger.Warning("Communication error on {PlcName}, connection lost: {Error}",
                    _device.Name, ex.Message);

                LastError = ex.Message;
                StopPolling();
                CleanupConnection();
                ConnectionState = PlcConnectionState.Error;
                LastDisconnectedTime = DateTime.Now;

                if (_device.AutoReconnect)
                {
                    StartAutoReconnect();
                }
            }
        }
    }

    private void StartAutoReconnect()
    {
        if (_reconnectCts != null) return;

        _reconnectCts = new CancellationTokenSource();
        _ = AutoReconnectLoopAsync(_reconnectCts.Token);
    }

    private void StopAutoReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    private async Task AutoReconnectLoopAsync(CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var maxRetries = _device.MaxReconnectAttempts > 0 ? _device.MaxReconnectAttempts : int.MaxValue;

        while (!cancellationToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                await Task.Delay(_device.ReconnectInterval, cancellationToken);

                if (cancellationToken.IsCancellationRequested) break;

                _logger.Information("Auto-reconnect attempt {Attempt} for {PlcName}...",
                    retryCount + 1, _device.Name);

                ConnectionState = PlcConnectionState.Reconnecting;

                var connected = await ConnectAsync(cancellationToken);
                if (connected)
                {
                    _logger.Information("Auto-reconnect successful for {PlcName}", _device.Name);
                    _reconnectCts = null;
                    return;
                }

                retryCount++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Auto-reconnect error for {PlcName}", _device.Name);
                retryCount++;
            }
        }

        if (retryCount >= maxRetries)
        {
            _logger.Error("Max reconnect attempts reached for {PlcName}", _device.Name);
            ConnectionState = PlcConnectionState.Error;
        }

        _reconnectCts = null;
    }

    private void RaiseError(string message, Exception? ex = null, bool isCritical = false)
    {
        ErrorOccurred?.Invoke(this, new PlcErrorEventArgs
        {
            PlcId = _device.Id,
            PlcName = _device.Name,
            ErrorMessage = message,
            Exception = ex,
            IsCritical = isCritical
        });
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAutoReconnect();
        StopPolling();
        CleanupConnection();

        GC.SuppressFinalize(this);
    }

    #endregion
}
