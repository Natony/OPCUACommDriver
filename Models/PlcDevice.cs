using System.Collections.ObjectModel;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho một PLC/OPC UA Server
/// Mỗi PLC sẽ có một Session OPC UA riêng biệt hoặc kết nối TCP/IP
/// </summary>
public class PlcDevice : ObservableObject
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
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

    // TCP/IP Protocol fields
    private ProtocolType _protocolType = ProtocolType.OpcUa;
    private PlcType _plcType = PlcType.Generic;
    private string _ipAddress = string.Empty;
    private int _port = 102;
    private int _rack = 0;
    private int _slot = 1;
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

    #region Protocol Selection Properties

    /// <summary>
    /// Loại giao thức truyền thông (OPC UA, Siemens S7, Mitsubishi MC, etc.)
    /// </summary>
    public ProtocolType ProtocolType
    {
        get => _protocolType;
        set => SetProperty(ref _protocolType, value);
    }

    /// <summary>
    /// Loại PLC cụ thể (S7-1200, FX5U, etc.)
    /// </summary>
    public PlcType PlcType
    {
        get => _plcType;
        set => SetProperty(ref _plcType, value);
    }

    #endregion

    #region TCP/IP Connection Properties

    /// <summary>
    /// IP Address của PLC (cho kết nối TCP/IP)
    /// </summary>
    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    /// <summary>
    /// Port kết nối TCP/IP
    /// Siemens S7: 102
    /// Mitsubishi MC: 5000/5001
    /// Modbus: 502
    /// </summary>
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    /// <summary>
    /// Rack number (cho Siemens S7)
    /// S7-300/400: 0
    /// S7-1200/1500: 0
    /// </summary>
    public int Rack
    {
        get => _rack;
        set => SetProperty(ref _rack, value);
    }

    /// <summary>
    /// Slot number (cho Siemens S7)
    /// S7-300: 2
    /// S7-400: 3
    /// S7-1200/1500: 1
    /// </summary>
    public int Slot
    {
        get => _slot;
        set => SetProperty(ref _slot, value);
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

    #region OPC UA Connection Properties

    /// <summary>
    /// OPC UA Endpoint URL (vd: opc.tcp://192.168.1.100:4840)
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

    /// <summary>
    /// Địa chỉ kết nối hiển thị tùy theo giao thức
    /// </summary>
    [JsonIgnore]
    public string DisplayConnectionAddress => ProtocolType switch
    {
        ProtocolType.OpcUa => EndpointUrl,
        ProtocolType.SiemensS7 => $"{IpAddress}:{Port} (Rack:{Rack}, Slot:{Slot})",
        ProtocolType.MitsubishiMc => $"{IpAddress}:{Port}",
        ProtocolType.ModbusTcp => $"{IpAddress}:{Port}",
        _ => EndpointUrl
    };

    /// <summary>
    /// Tên loại giao thức hiển thị
    /// </summary>
    [JsonIgnore]
    public string ProtocolDisplayName => ProtocolType switch
    {
        ProtocolType.OpcUa => "OPC UA",
        ProtocolType.SiemensS7 => "Siemens S7 (TCP/IP)",
        ProtocolType.MitsubishiMc => "Mitsubishi MC Protocol",
        ProtocolType.ModbusTcp => "Modbus TCP",
        _ => "Unknown"
    };

    #endregion

    #region Factory Methods

    /// <summary>
    /// Tạo PLC mới với OPC UA endpoint
    /// </summary>
    public static PlcDevice Create(string name, string endpointUrl)
    {
        return new PlcDevice
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            EndpointUrl = endpointUrl,
            ProtocolType = ProtocolType.OpcUa,
            ConnectionState = PlcConnectionState.Disabled
        };
    }

    /// <summary>
    /// Tạo Siemens PLC với kết nối S7 TCP/IP
    /// </summary>
    public static PlcDevice CreateSiemensS7(string name, string ipAddress, PlcType plcType, int rack = 0, int slot = 1)
    {
        return new PlcDevice
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            IpAddress = ipAddress,
            Port = 102,
            Rack = rack,
            Slot = slot,
            ProtocolType = ProtocolType.SiemensS7,
            PlcType = plcType,
            ConnectionState = PlcConnectionState.Disabled
        };
    }

    /// <summary>
    /// Tạo Mitsubishi PLC với kết nối MC Protocol
    /// </summary>
    public static PlcDevice CreateMitsubishiMc(string name, string ipAddress, PlcType plcType, int port = 5000)
    {
        return new PlcDevice
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            IpAddress = ipAddress,
            Port = port,
            ProtocolType = ProtocolType.MitsubishiMc,
            PlcType = plcType,
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
            // TCP/IP properties
            ProtocolType = this.ProtocolType,
            PlcType = this.PlcType,
            IpAddress = this.IpAddress,
            Port = this.Port,
            Rack = this.Rack,
            Slot = this.Slot,
            ConnectionTimeout = this.ConnectionTimeout,
            ReadTimeout = this.ReadTimeout,
            WriteTimeout = this.WriteTimeout
        };
    }

    #endregion

    public override string ToString()
    {
        return $"{Name} ({DisplayConnectionAddress}) [{ProtocolDisplayName}] - {ConnectionStateText}";
    }
}
