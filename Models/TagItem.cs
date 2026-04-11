using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho một OPC UA Tag/Node hoặc Modbus Register
/// Mỗi Tag thuộc về một PLC và một Subscription Group
/// </summary>
public class TagItem : ObservableObject
{
    private string _id = string.Empty;
    private string _plcId = string.Empty;
    private string _subscriptionGroupId = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _nodeId = string.Empty;
    private string _displayName = string.Empty;
    private string _browsePath = string.Empty;
    private TagDataType _dataType = TagDataType.Unknown;
    private TagAccessMode _accessMode = TagAccessMode.ReadWrite;
    private bool _isEnabled = true;
    private int _scanRate = 1000;
    private double _deadband = 0;
    private object? _value;
    private object? _previousValue;
    private TagQuality _quality = TagQuality.Unknown;
    private DateTime? _timestamp;
    private DateTime? _serverTimestamp;
    private string _lastError = string.Empty;
    private bool _isSubscribed;
    private string _engineeringUnit = string.Empty;
    private double? _minValue;
    private double? _maxValue;
    private int _arraySize;
    private ModbusRegisterType _registerType = ModbusRegisterType.HoldingRegister;

    #region Identity Properties

    /// <summary>
    /// ID duy nhất của Tag (GUID)
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// ID của PLC chứa Tag này
    /// </summary>
    public string PlcId
    {
        get => _plcId;
        set => SetProperty(ref _plcId, value);
    }

    /// <summary>
    /// ID của Subscription Group chứa Tag này
    /// </summary>
    public string SubscriptionGroupId
    {
        get => _subscriptionGroupId;
        set => SetProperty(ref _subscriptionGroupId, value);
    }

    /// <summary>
    /// Tên của Tag (user-defined)
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Mô tả về Tag
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    #endregion

    #region OPC UA Properties

    /// <summary>
    /// OPC UA NodeId (vd: ns=3;s=MyVariable hoặc ns=2;i=1001)
    /// </summary>
    public string NodeId
    {
        get => _nodeId;
        set => SetProperty(ref _nodeId, value);
    }

    /// <summary>
    /// Display Name từ OPC UA Server
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// Browse Path trong cây OPC UA (vd: /Objects/MyDevice/Temperature)
    /// </summary>
    public string BrowsePath
    {
        get => _browsePath;
        set => SetProperty(ref _browsePath, value);
    }

    /// <summary>
    /// Kiểu dữ liệu của Tag
    /// </summary>
    public TagDataType DataType
    {
        get => _dataType;
        set => SetProperty(ref _dataType, value);
    }

    /// <summary>
    /// Chế độ truy cập (Read/Write/ReadWrite)
    /// </summary>
    public TagAccessMode AccessMode
    {
        get => _accessMode;
        set => SetProperty(ref _accessMode, value);
    }

    /// <summary>
    /// Cho phép đọc/ghi Tag này không
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    #endregion

    #region Subscription Configuration

    /// <summary>
    /// Tốc độ quét (milliseconds) - mặc định 1000ms
    /// </summary>
    public int ScanRate
    {
        get => _scanRate;
        set => SetProperty(ref _scanRate, value);
    }

    /// <summary>
    /// Deadband - ngưỡng thay đổi tối thiểu để publish (cho analog values)
    /// </summary>
    public double Deadband
    {
        get => _deadband;
        set => SetProperty(ref _deadband, value);
    }

    #endregion

    #region Runtime Values (không lưu vào config)

    /// <summary>
    /// Giá trị hiện tại
    /// </summary>
    [JsonIgnore]
    public object? Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(HasValueChanged));
            }
        }
    }

    /// <summary>
    /// Giá trị trước đó (để so sánh thay đổi)
    /// </summary>
    [JsonIgnore]
    public object? PreviousValue
    {
        get => _previousValue;
        set => SetProperty(ref _previousValue, value);
    }

    /// <summary>
    /// Chất lượng dữ liệu
    /// </summary>
    [JsonIgnore]
    public TagQuality Quality
    {
        get => _quality;
        set
        {
            if (SetProperty(ref _quality, value))
            {
                OnPropertyChanged(nameof(QualityText));
                OnPropertyChanged(nameof(IsGoodQuality));
            }
        }
    }

    /// <summary>
    /// Timestamp từ client (khi nhận được data)
    /// </summary>
    [JsonIgnore]
    public DateTime? Timestamp
    {
        get => _timestamp;
        set
        {
            if (SetProperty(ref _timestamp, value))
            {
                OnPropertyChanged(nameof(TimestampText));
            }
        }
    }

    /// <summary>
    /// Timestamp từ OPC UA Server
    /// </summary>
    [JsonIgnore]
    public DateTime? ServerTimestamp
    {
        get => _serverTimestamp;
        set => SetProperty(ref _serverTimestamp, value);
    }

    /// <summary>
    /// Lỗi gần nhất
    /// </summary>
    [JsonIgnore]
    public string LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }

    /// <summary>
    /// Tag đã được subscribe chưa
    /// </summary>
    [JsonIgnore]
    public bool IsSubscribed
    {
        get => _isSubscribed;
        set => SetProperty(ref _isSubscribed, value);
    }

    #endregion

    #region Engineering Properties

    /// <summary>
    /// Đơn vị kỹ thuật (vd: °C, bar, kg)
    /// </summary>
    public string EngineeringUnit
    {
        get => _engineeringUnit;
        set => SetProperty(ref _engineeringUnit, value);
    }

    /// <summary>
    /// Giá trị tối thiểu (cho scaling/validation)
    /// </summary>
    public double? MinValue
    {
        get => _minValue;
        set => SetProperty(ref _minValue, value);
    }

    /// <summary>
    /// Giá trị tối đa (cho scaling/validation)
    /// </summary>
    public double? MaxValue
    {
        get => _maxValue;
        set => SetProperty(ref _maxValue, value);
    }

    /// <summary>
    /// Kích thước mảng (0 = scalar, >0 = array)
    /// </summary>
    public int ArraySize
    {
        get => _arraySize;
        set => SetProperty(ref _arraySize, value);
    }

    #endregion

    #region Modbus Properties

    /// <summary>
    /// Loại thanh ghi Modbus (HoldingRegister, InputRegister, Coil, DiscreteInput)
    /// Chỉ sử dụng khi PLC dùng giao thức Modbus
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public ModbusRegisterType RegisterType
    {
        get => _registerType;
        set => SetProperty(ref _registerType, value);
    }

    /// <summary>
    /// Danh sách BitMapping - ánh xạ các bit trong thanh ghi sang giá trị boolean
    /// Chỉ sử dụng khi cần trích xuất nhiều boolean từ 1 thanh ghi
    /// </summary>
    public ObservableCollection<BitMapping>? BitMapping { get; set; }

    /// <summary>
    /// Kiểm tra tag có BitMapping không
    /// </summary>
    [JsonIgnore]
    public bool HasBitMapping => BitMapping != null && BitMapping.Count > 0;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Giá trị hiển thị dạng string
    /// </summary>
    [JsonIgnore]
    public string DisplayValue
    {
        get
        {
            if (Value == null) return "N/A";
            if (Value is Array arr)
            {
                return $"[{arr.Length} items]";
            }
            return Value.ToString() ?? "N/A";
        }
    }

    /// <summary>
    /// Timestamp dạng text
    /// </summary>
    [JsonIgnore]
    public string TimestampText => Timestamp?.ToString("HH:mm:ss.fff") ?? "N/A";

    /// <summary>
    /// Quality dạng text
    /// </summary>
    [JsonIgnore]
    public string QualityText => Quality.ToString();

    /// <summary>
    /// Kiểm tra quality có tốt không
    /// </summary>
    [JsonIgnore]
    public bool IsGoodQuality => Quality == TagQuality.Good;

    /// <summary>
    /// Kiểm tra giá trị có thay đổi so với trước không
    /// </summary>
    [JsonIgnore]
    public bool HasValueChanged => !Equals(Value, PreviousValue);

    /// <summary>
    /// Tag có thể đọc không
    /// </summary>
    [JsonIgnore]
    public bool CanRead => AccessMode == TagAccessMode.Read || AccessMode == TagAccessMode.ReadWrite;

    /// <summary>
    /// Tag có thể ghi không
    /// </summary>
    [JsonIgnore]
    public bool CanWrite => AccessMode == TagAccessMode.Write || AccessMode == TagAccessMode.ReadWrite;

    /// <summary>
    /// Full path để hiển thị (PLC/BrowsePath/Name)
    /// </summary>
    [JsonIgnore]
    public string FullPath => string.IsNullOrEmpty(BrowsePath) ? Name : $"{BrowsePath}/{Name}";

    #endregion

    #region Factory Methods

    /// <summary>
    /// Tạo Tag mới với ID tự động
    /// </summary>
    public static TagItem Create(string plcId, string name, string nodeId)
    {
        return new TagItem
        {
            Id = Guid.NewGuid().ToString(),
            PlcId = plcId,
            Name = name,
            NodeId = nodeId,
            Quality = TagQuality.Unknown
        };
    }

    /// <summary>
    /// Clone Tag (không copy runtime state)
    /// </summary>
    public TagItem Clone()
    {
        var clone = new TagItem
        {
            Id = this.Id,
            PlcId = this.PlcId,
            SubscriptionGroupId = this.SubscriptionGroupId,
            Name = this.Name,
            Description = this.Description,
            NodeId = this.NodeId,
            DisplayName = this.DisplayName,
            BrowsePath = this.BrowsePath,
            DataType = this.DataType,
            AccessMode = this.AccessMode,
            IsEnabled = this.IsEnabled,
            ScanRate = this.ScanRate,
            Deadband = this.Deadband,
            EngineeringUnit = this.EngineeringUnit,
            MinValue = this.MinValue,
            MaxValue = this.MaxValue,
            ArraySize = this.ArraySize,
            RegisterType = this.RegisterType
        };

        if (this.BitMapping != null)
        {
            clone.BitMapping = new ObservableCollection<BitMapping>(
                this.BitMapping.Select(b => new BitMapping
                {
                    Bit = b.Bit,
                    Name = b.Name,
                    Description = b.Description
                }));
        }

        return clone;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Cập nhật giá trị mới
    /// </summary>
    public void UpdateValue(object? newValue, TagQuality quality, DateTime timestamp, DateTime? serverTimestamp = null)
    {
        PreviousValue = Value;
        Value = newValue;
        Quality = quality;
        Timestamp = timestamp;
        ServerTimestamp = serverTimestamp;
        LastError = string.Empty;
    }

    /// <summary>
    /// Đánh dấu lỗi
    /// </summary>
    public void SetError(string error)
    {
        Quality = TagQuality.Bad;
        LastError = error;
        Timestamp = DateTime.Now;
    }

    #endregion

    public override string ToString()
    {
        return $"{Name} ({NodeId}) = {DisplayValue} [{QualityText}]";
    }
}
