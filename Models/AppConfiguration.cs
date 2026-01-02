using System.Collections.ObjectModel;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model chứa toàn bộ cấu hình của ứng dụng
/// Được serialize/deserialize từ file JSON
/// </summary>
public class AppConfiguration : ObservableObject
{
    private string _version = "1.0.0";
    private DateTime _lastModified = DateTime.Now;
    private string _lastModifiedBy = Environment.UserName;
    private ApplicationSettings _settings = new();

    /// <summary>
    /// Phiên bản cấu hình (để migration nếu cần)
    /// </summary>
    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    /// <summary>
    /// Thời gian sửa đổi gần nhất
    /// </summary>
    public DateTime LastModified
    {
        get => _lastModified;
        set => SetProperty(ref _lastModified, value);
    }

    /// <summary>
    /// Người sửa đổi gần nhất
    /// </summary>
    public string LastModifiedBy
    {
        get => _lastModifiedBy;
        set => SetProperty(ref _lastModifiedBy, value);
    }

    /// <summary>
    /// Cài đặt ứng dụng
    /// </summary>
    public ApplicationSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    /// <summary>
    /// Danh sách tất cả các PLC
    /// </summary>
    public ObservableCollection<PlcDevice> PlcDevices { get; set; } = new();

    /// <summary>
    /// Cập nhật thông tin modified
    /// </summary>
    public void MarkModified()
    {
        LastModified = DateTime.Now;
        LastModifiedBy = Environment.UserName;
    }

    /// <summary>
    /// Tạo configuration mặc định
    /// </summary>
    public static AppConfiguration CreateDefault()
    {
        return new AppConfiguration
        {
            Version = "1.0.0",
            LastModified = DateTime.Now,
            Settings = ApplicationSettings.CreateDefault()
        };
    }
}

/// <summary>
/// Cài đặt chung của ứng dụng
/// </summary>
public class ApplicationSettings : ObservableObject
{
    private string _applicationName = "OPC UA Communication Engine";
    private string _certificateStorePath = "./Certificates";
    private string _logPath = "./Logs";
    private string _configPath = "./Configurations";
    private int _defaultSessionTimeout = 60000;
    private int _defaultKeepAliveInterval = 5000;
    private int _defaultReconnectInterval = 5000;
    private int _maxConcurrentConnections = 50;
    private bool _autoConnectOnStartup = false;
    private bool _enableLogging = true;
    private string _logLevel = "Information";
    private int _dataRetentionDays = 7;

    #region Application Settings

    /// <summary>
    /// Tên ứng dụng
    /// </summary>
    public string ApplicationName
    {
        get => _applicationName;
        set => SetProperty(ref _applicationName, value);
    }

    /// <summary>
    /// Đường dẫn lưu certificates
    /// </summary>
    public string CertificateStorePath
    {
        get => _certificateStorePath;
        set => SetProperty(ref _certificateStorePath, value);
    }

    /// <summary>
    /// Đường dẫn lưu log files
    /// </summary>
    public string LogPath
    {
        get => _logPath;
        set => SetProperty(ref _logPath, value);
    }

    /// <summary>
    /// Đường dẫn lưu config files
    /// </summary>
    public string ConfigPath
    {
        get => _configPath;
        set => SetProperty(ref _configPath, value);
    }

    #endregion

    #region Connection Defaults

    /// <summary>
    /// Session timeout mặc định (ms)
    /// </summary>
    public int DefaultSessionTimeout
    {
        get => _defaultSessionTimeout;
        set => SetProperty(ref _defaultSessionTimeout, value);
    }

    /// <summary>
    /// Keep alive interval mặc định (ms)
    /// </summary>
    public int DefaultKeepAliveInterval
    {
        get => _defaultKeepAliveInterval;
        set => SetProperty(ref _defaultKeepAliveInterval, value);
    }

    /// <summary>
    /// Reconnect interval mặc định (ms)
    /// </summary>
    public int DefaultReconnectInterval
    {
        get => _defaultReconnectInterval;
        set => SetProperty(ref _defaultReconnectInterval, value);
    }

    /// <summary>
    /// Số lượng kết nối đồng thời tối đa
    /// </summary>
    public int MaxConcurrentConnections
    {
        get => _maxConcurrentConnections;
        set => SetProperty(ref _maxConcurrentConnections, value);
    }

    /// <summary>
    /// Tự động kết nối khi khởi động ứng dụng
    /// </summary>
    public bool AutoConnectOnStartup
    {
        get => _autoConnectOnStartup;
        set => SetProperty(ref _autoConnectOnStartup, value);
    }

    #endregion

    #region Logging Settings

    /// <summary>
    /// Bật/tắt logging
    /// </summary>
    public bool EnableLogging
    {
        get => _enableLogging;
        set => SetProperty(ref _enableLogging, value);
    }

    /// <summary>
    /// Mức độ log (Verbose, Debug, Information, Warning, Error, Fatal)
    /// </summary>
    public string LogLevel
    {
        get => _logLevel;
        set => SetProperty(ref _logLevel, value);
    }

    /// <summary>
    /// Số ngày giữ log
    /// </summary>
    public int DataRetentionDays
    {
        get => _dataRetentionDays;
        set => SetProperty(ref _dataRetentionDays, value);
    }

    #endregion

    /// <summary>
    /// Tạo settings mặc định
    /// </summary>
    public static ApplicationSettings CreateDefault()
    {
        return new ApplicationSettings();
    }
}
