using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model đại diện cho một Subscription Group
/// Các Tag cùng scan rate sẽ được nhóm vào cùng một Subscription để tối ưu performance
/// </summary>
public class SubscriptionGroup : ObservableObject
{
    private string _id = string.Empty;
    private string _plcId = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private int _publishingInterval = 1000;
    private uint _lifetimeCount = 100;
    private uint _keepAliveCount = 10;
    private uint _maxNotificationsPerPublish = 1000;
    private bool _publishingEnabled = true;
    private bool _isEnabled = true;
    private byte _priority = 0;
    private bool _isActive;
    private int _activeMonitoredItemCount;
    private DateTime? _lastPublishTime;

    #region Identity Properties

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string PlcId
    {
        get => _plcId;
        set => SetProperty(ref _plcId, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    #endregion

    #region Subscription Configuration

    public int PublishingInterval
    {
        get => _publishingInterval;
        set => SetProperty(ref _publishingInterval, value);
    }

    public uint LifetimeCount
    {
        get => _lifetimeCount;
        set => SetProperty(ref _lifetimeCount, value);
    }

    public uint KeepAliveCount
    {
        get => _keepAliveCount;
        set => SetProperty(ref _keepAliveCount, value);
    }

    public uint MaxNotificationsPerPublish
    {
        get => _maxNotificationsPerPublish;
        set => SetProperty(ref _maxNotificationsPerPublish, value);
    }

    public bool PublishingEnabled
    {
        get => _publishingEnabled;
        set => SetProperty(ref _publishingEnabled, value);
    }

    /// <summary>
    /// Cho phép subscription hay không
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public byte Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    #endregion

    #region Runtime State

    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    [JsonIgnore]
    public int ActiveMonitoredItemCount
    {
        get => _activeMonitoredItemCount;
        set => SetProperty(ref _activeMonitoredItemCount, value);
    }

    [JsonIgnore]
    public DateTime? LastPublishTime
    {
        get => _lastPublishTime;
        set
        {
            if (SetProperty(ref _lastPublishTime, value))
            {
                OnPropertyChanged(nameof(LastPublishTimeText));
            }
        }
    }

    public ObservableCollection<string> TagIds { get; set; } = new();

    #endregion

    #region Computed Properties

    [JsonIgnore]
    public string LastPublishTimeText => LastPublishTime?.ToString("HH:mm:ss.fff") ?? "N/A";

    [JsonIgnore]
    public string IntervalText => PublishingInterval >= 1000 
        ? $"{PublishingInterval / 1000.0:F1}s" 
        : $"{PublishingInterval}ms";

    [JsonIgnore]
    public int TagCount => TagIds.Count;

    #endregion

    #region Factory Methods

    public static SubscriptionGroup Create(string plcId, string name, int publishingInterval)
    {
        return new SubscriptionGroup
        {
            Id = Guid.NewGuid().ToString(),
            PlcId = plcId,
            Name = name,
            PublishingInterval = publishingInterval,
            IsEnabled = true
        };
    }

    public static List<SubscriptionGroup> CreateDefaultGroups(string plcId)
    {
        return new List<SubscriptionGroup>
        {
            Create(plcId, "Fast (100ms)", 100),
            Create(plcId, "Normal (500ms)", 500),
            Create(plcId, "Slow (1000ms)", 1000),
            Create(plcId, "VerySlow (5000ms)", 5000)
        };
    }

    #endregion

    public override string ToString()
    {
        return $"{Name} ({IntervalText}) - {TagCount} tags";
    }
}
