using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho một PLC/OPC UA Server hoặc Modbus Device
/// Mỗi PLC sẽ có một Session/Connection riêng biệt
/// </summary>
public class PlcDevice : ObservableObject
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private ProtocolType _protocolType = ProtocolType.OpcUa;
    private string _endpointUrl = string.Empty;
    private bool _isEnabled = true;
    private OpcUaSecurityPolicy _securityPolicy = OpcUaSecurityPolicy.None;
    private OpcUaSecurityMode _securityMode = OpcUaSecurityMode.None;
    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _certificatePath = string.Empty;
    private string _privateKeyPath = string.Empty;
    private int _sessionTimeout = 60000;
    private int _keepAliveInterval = 5000;
    private bool _autoReconnect = true;
    private int _reconnectInterval = 5000;
    private int _maxReconnectAttempts = 10;
    private PlcConnectionState _connectionState = PlcConnectionState.Disabled;
    private string _lastError = string.Empty;
    private DateTime? _lastConnectedTime;
    private DateTime? _lastDisconnectedTime;

    // Modbus-specific fields
    private string _ipAddress = string.Empty;
    private int _port = 502;
    private byte _slaveId = 1;
    private int _connectionTimeout = 5000;
    private int _readTimeout = 3000;
    private int _writeTimeout = 3000;

    #region Identity Properties

    /// <summary>
    /// ID duy nhất của PLC (GUID)
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// Tên hiển thị của PLC
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Mô tả về PLC
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    #endregion

    #region Protocol Properties

    /// <summary>
    /// Loại giao thức kết nối (OPC UA, Modbus TCP, ...)
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ProtocolType ProtocolType
    {
        get => _protocolType;
        set => SetProperty(ref _protocolType, value);
    }

    #endregion

    #region Connection Properties

    /// <summary>
    /// OPC UA Endpoint URL (vd: opc.tcp://192.168.1.100:4840)
    /// Chỉ dùng cho OPC UA
    /// </summary>
    public string EndpointUrl
    {
        get => _endpointUrl;
        set => SetProperty(ref _endpointUrl, value);
    }

    /// <summary>
    /// Cho phép kết nối hay không
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    #endregion

    #region Modbus Connection Properties

    /// <summary>
    /// Địa chỉ IP của PLC (dùng cho Modbus TCP)
    /// </summary>
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    /// <summary>
    /// Port kết nối (mặc định 502 cho Modbus TCP)
    /// </summary>
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    /// <summary>
    /// Slave/Unit ID của Modbus device (mặc định 1)
    /// </summary>
    public byte SlaveId
    {
        get => _slaveId;
        set => SetProperty(ref _slaveId, value);
    }

    /// <summary>
    /// Timeout kết nối (milliseconds)
    /// </summary>
    public int ConnectionTimeout
    {
        get => _connectionTimeout;
        set => SetProperty(ref _connectionTimeout, value);
    }

    /// <summary>
    /// Timeout đọc dữ liệu (milliseconds)
    /// </summary>
    public int ReadTimeout
    {
        get => _readTimeout;
        set => SetProperty(ref _readTimeout, value);
    }

    /// <summary>
    /// Timeout ghi dữ liệu (milliseconds)
    /// </summary>
    public int WriteTimeout
    {
        get => _writeTimeout;
        set => SetProperty(ref _writeTimeout, value);
    }

    #endregion

    #region Security Properties

    /// <summary>
    /// Security Policy cho kết nối OPC UA
    /// </summary>
    public OpcUaSecurityPolicy SecurityPolicy
    {
        get => _securityPolicy;
        set => SetProperty(ref _securityPolicy, value);
    }

    /// <summary>
    /// Security Mode (None, Sign, SignAndEncrypt)
    /// </summary>
    public OpcUaSecurityMode SecurityMode
    {
        get => _securityMode;
        set => SetProperty(ref _securityMode, value);
    }

    /// <summary>
    /// Username cho xác thực (nếu cần)
    /// </summary>
    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    /// <summary>
    /// Password cho xác thực (nếu cần)
    /// Lưu ý: Trong production nên mã hóa password
    /// </summary>
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    /// <summary>
    /// Đường dẫn đến certificate file (.der hoặc .pem)
    /// </summary>
    public string CertificatePath
    {
        get => _certificatePath;
        set => SetProperty(ref _certificatePath, value);
    }

    /// <summary>
    /// Đường dẫn đến private key file
    /// </summary>
    public string PrivateKeyPath
    {
        get => _privateKeyPath;
        set => SetProperty(ref _privateKeyPath, value);
    }

    #endregion

    #region Session Configuration

    /// <summary>
    /// Session Timeout (milliseconds) - mặc định 60 giây
    /// </summary>
    public int SessionTimeout
    {
        get => _sessionTimeout;
        set => SetProperty(ref _sessionTimeout, value);
    }

    /// <summary>
    /// KeepAlive Interval (milliseconds) - mặc định 5 giây
    /// </summary>
    public int KeepAliveInterval
    {
        get => _keepAliveInterval;
        set => SetProperty(ref _keepAliveInterval, value);
    }

    #endregion

    #region Reconnect Configuration

    /// <summary>
    /// Tự động reconnect khi mất kết nối
    /// </summary>
    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => SetProperty(ref _autoReconnect, value);
    }

    /// <summary>
    /// Khoảng thời gian giữa các lần thử reconnect (milliseconds)
    /// </summary>
    public int ReconnectInterval
    {
        get => _reconnectInterval;
        set => SetProperty(ref _reconnectInterval, value);
    }

    /// <summary>
    /// Số lần thử reconnect tối đa (0 = unlimited)
    /// </summary>
    public int MaxReconnectAttempts
    {
        get => _maxReconnectAttempts;
        set => SetProperty(ref _maxReconnectAttempts, value);
    }

    #endregion

    #region Runtime State (không lưu vào config)

    /// <summary>
    /// Trạng thái kết nối hiện tại
    /// </summary>
    [JsonIgnore]
    public PlcConnectionState ConnectionState
    {
        get => _connectionState;
        set => SetProperty(ref _connectionState, value);
    }

    /// <summary>
    /// Thông báo lỗi gần nhất
    /// </summary>
    [JsonIgnore]
    public string LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }

    /// <summary>
    /// Thời điểm kết nối thành công gần nhất
    /// </summary>
    [JsonIgnore]
    public DateTime? LastConnectedTime
    {
        get => _lastConnectedTime;
        set => SetProperty(ref _lastConnectedTime, value);
    }

    /// <summary>
    /// Thời điểm mất kết nối gần nhất
    /// </summary>
    [JsonIgnore]
    public DateTime? LastDisconnectedTime
    {
        get => _lastDisconnectedTime;
        set => SetProperty(ref _lastDisconnectedTime, value);
    }

    /// <summary>
    /// Danh sách các Tag thuộc PLC này
    /// </summary>
    public ObservableCollection<TagItem> Tags { get; set; } = new();

    /// <summary>
    /// Danh sách các Subscription Group
    /// </summary>
    public ObservableCollection<SubscriptionGroup> SubscriptionGroups { get; set; } = new();

    #endregion

    #region Computed Properties

    /// <summary>
    /// Kiểm tra PLC có đang kết nối không
    /// </summary>
    [JsonIgnore]
    public bool IsConnected => ConnectionState == PlcConnectionState.Connected;

    /// <summary>
    /// Hiển thị trạng thái dạng text
    /// </summary>
    [JsonIgnore]
    public string ConnectionStateText => ConnectionState switch
    {
        PlcConnectionState.Disabled => "Disabled",
        PlcConnectionState.Connecting => "Connecting...",
        PlcConnectionState.Connected => "Connected",
        PlcConnectionState.Error => $"Error: {LastError}",
        PlcConnectionState.Reconnecting => "Reconnecting...",
        PlcConnectionState.Disconnecting => "Disconnecting...",
        PlcConnectionState.Disconnected => "Disconnected",
        _ => "Unknown"
    };

    #endregion

    #region Factory Methods

    /// <summary>
    /// Tạo PLC mới với ID tự động
    /// </summary>
    public static PlcDevice Create(string name, string endpointUrl)
    {
        return new PlcDevice
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            EndpointUrl = endpointUrl,
            ConnectionState = PlcConnectionState.Disabled
        };
    }

    /// <summary>
    /// Clone PLC (không copy runtime state)
    /// </summary>
    public PlcDevice Clone()
    {
        return new PlcDevice
        {
            Id = this.Id,
            Name = this.Name,
            Description = this.Description,
            ProtocolType = this.ProtocolType,
            EndpointUrl = this.EndpointUrl,
            IsEnabled = this.IsEnabled,
            SecurityPolicy = this.SecurityPolicy,
            SecurityMode = this.SecurityMode,
            UserName = this.UserName,
            Password = this.Password,
            CertificatePath = this.CertificatePath,
            PrivateKeyPath = this.PrivateKeyPath,
            SessionTimeout = this.SessionTimeout,
            KeepAliveInterval = this.KeepAliveInterval,
            AutoReconnect = this.AutoReconnect,
            ReconnectInterval = this.ReconnectInterval,
            MaxReconnectAttempts = this.MaxReconnectAttempts,
            // Modbus properties
            IpAddress = this.IpAddress,
            Port = this.Port,
            SlaveId = this.SlaveId,
            ConnectionTimeout = this.ConnectionTimeout,
            ReadTimeout = this.ReadTimeout,
            WriteTimeout = this.WriteTimeout
        };
    }

    #endregion

    /// <summary>
    /// Kiểm tra thiết bị có phải Modbus không
    /// </summary>
    [JsonIgnore]
    public bool IsModbus => ProtocolType == ProtocolType.ModbusTcp ||
                            ProtocolType == ProtocolType.ModbusRtu ||
                            ProtocolType == ProtocolType.ModbusAscii;

    /// <summary>
    /// Lấy địa chỉ kết nối hiển thị
    /// </summary>
    [JsonIgnore]
    public string ConnectionAddress => IsModbus
        ? $"{IpAddress}:{Port}"
        : EndpointUrl;

    public override string ToString()
    {
        return $"{Name} ({ConnectionAddress}) [{ProtocolType}] - {ConnectionStateText}";
    }
}
