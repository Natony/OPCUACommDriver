using System.Collections.ObjectModel;
using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Model for displaying OPC UA nodes in a TreeView
/// Supports lazy loading of children for performance
/// </summary>
public class BrowseNodeItem : ObservableObject
{
    private string _nodeId = string.Empty;
    private string _displayName = string.Empty;
    private string _browseName = string.Empty;
    private string _nodeClass = string.Empty;
    private string _dataType = string.Empty;
    private string _description = string.Empty;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isLoading;
    private bool _hasChildren = true;
    private bool _childrenLoaded;
    private object? _currentValue;
    private string _accessLevel = string.Empty;

    public string NodeId
    {
        get => _nodeId;
        set => SetProperty(ref _nodeId, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string BrowseName
    {
        get => _browseName;
        set => SetProperty(ref _browseName, value);
    }

    public string NodeClass
    {
        get => _nodeClass;
        set
        {
            if (SetProperty(ref _nodeClass, value))
            {
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IsVariable));
                OnPropertyChanged(nameof(CanAddAsTag));
            }
        }
    }

    public string DataType
    {
        get => _dataType;
        set => SetProperty(ref _dataType, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                // Trigger lazy loading when expanded
                if (value && !_childrenLoaded && HasChildren)
                {
                    OnExpandRequested?.Invoke(this);
                }
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasChildren
    {
        get => _hasChildren;
        set => SetProperty(ref _hasChildren, value);
    }

    public bool ChildrenLoaded
    {
        get => _childrenLoaded;
        set => SetProperty(ref _childrenLoaded, value);
    }

    public object? CurrentValue
    {
        get => _currentValue;
        set
        {
            if (SetProperty(ref _currentValue, value))
            {
                OnPropertyChanged(nameof(ValueDisplay));
            }
        }
    }

    public string AccessLevel
    {
        get => _accessLevel;
        set => SetProperty(ref _accessLevel, value);
    }

    /// <summary>
    /// Parent node (null for root nodes)
    /// </summary>
    public BrowseNodeItem? Parent { get; set; }

    /// <summary>
    /// Child nodes
    /// </summary>
    public ObservableCollection<BrowseNodeItem> Children { get; } = new();

    /// <summary>
    /// Event fired when node needs to load children (lazy loading)
    /// </summary>
    public event Action<BrowseNodeItem>? OnExpandRequested;

    #region Computed Properties

    /// <summary>
    /// Icon based on node class
    /// </summary>
    public string Icon => NodeClass switch
    {
        "Object" => "📁",
        "Variable" => "📊",
        "Method" => "⚡",
        "ObjectType" => "📦",
        "VariableType" => "📈",
        "ReferenceType" => "🔗",
        "DataType" => "📋",
        "View" => "👁",
        _ => "📄"
    };

    /// <summary>
    /// Is this a Variable node (can be read/written)
    /// </summary>
    public bool IsVariable => NodeClass == "Variable";

    /// <summary>
    /// Can this node be added as a tag
    /// </summary>
    public bool CanAddAsTag => NodeClass == "Variable";

    /// <summary>
    /// Display value as string
    /// </summary>
    public string ValueDisplay => CurrentValue?.ToString() ?? "N/A";

    /// <summary>
    /// Full path display
    /// </summary>
    public string FullPath
    {
        get
        {
            var parts = new List<string>();
            var current = this;
            while (current != null)
            {
                parts.Insert(0, current.DisplayName);
                current = current.Parent;
            }
            return string.Join("/", parts);
        }
    }

    /// <summary>
    /// Tooltip with node details
    /// </summary>
    public string Tooltip => $"NodeId: {NodeId}\n" +
                             $"BrowseName: {BrowseName}\n" +
                             $"NodeClass: {NodeClass}\n" +
                             (IsVariable ? $"DataType: {DataType}\n" : "") +
                             (IsVariable ? $"Value: {ValueDisplay}\n" : "") +
                             (!string.IsNullOrEmpty(Description) ? $"Description: {Description}" : "");

    #endregion

    #region Methods

    /// <summary>
    /// Add a placeholder child for lazy loading UI
    /// </summary>
    public void AddLoadingPlaceholder()
    {
        if (HasChildren && Children.Count == 0)
        {
            Children.Add(new BrowseNodeItem
            {
                DisplayName = "Loading...",
                NodeClass = "Loading",
                HasChildren = false
            });
        }
    }

    /// <summary>
    /// Clear loading placeholder and mark children as loaded
    /// </summary>
    public void ClearLoadingPlaceholder()
    {
        var placeholder = Children.FirstOrDefault(c => c.NodeClass == "Loading");
        if (placeholder != null)
        {
            Children.Remove(placeholder);
        }
        ChildrenLoaded = true;
    }

    /// <summary>
    /// Convert to TagItem for adding to PLC
    /// </summary>
    public TagItem ToTagItem(string plcId, string subscriptionGroupId)
    {
        return new TagItem
        {
            Id = Guid.NewGuid().ToString(),
            PlcId = plcId,
            SubscriptionGroupId = subscriptionGroupId,
            Name = DisplayName,
            NodeId = NodeId,
            DisplayName = DisplayName,
            BrowsePath = FullPath,
            Description = Description,
            DataType = ParseDataType(DataType),
            AccessMode = ParseAccessMode(AccessLevel),
            IsEnabled = true,
            ScanRate = 1000
        };
    }

    private static TagDataType ParseDataType(string dataType)
    {
        if (string.IsNullOrEmpty(dataType)) return TagDataType.Unknown;

        // Extract type name from NodeId format (e.g., "i=6" -> Int32)
        return dataType.ToLowerInvariant() switch
        {
            var s when s.Contains("bool") => TagDataType.Boolean,
            var s when s.Contains("int16") || s.Contains("i=4") => TagDataType.Int16,
            var s when s.Contains("int32") || s.Contains("i=6") => TagDataType.Int32,
            var s when s.Contains("int64") || s.Contains("i=8") => TagDataType.Int64,
            var s when s.Contains("uint16") || s.Contains("i=5") => TagDataType.UInt16,
            var s when s.Contains("uint32") || s.Contains("i=7") => TagDataType.UInt32,
            var s when s.Contains("uint64") || s.Contains("i=9") => TagDataType.UInt64,
            var s when s.Contains("float") || s.Contains("i=10") => TagDataType.Float,
            var s when s.Contains("double") || s.Contains("i=11") => TagDataType.Double,
            var s when s.Contains("string") || s.Contains("i=12") => TagDataType.String,
            var s when s.Contains("datetime") || s.Contains("i=13") => TagDataType.DateTime,
            var s when s.Contains("byte") || s.Contains("i=3") => TagDataType.Byte,
            _ => TagDataType.Unknown
        };
    }

    private static TagAccessMode ParseAccessMode(string accessLevel)
    {
        if (string.IsNullOrEmpty(accessLevel)) return TagAccessMode.ReadWrite;

        var lower = accessLevel.ToLowerInvariant();
        if (lower.Contains("read") && lower.Contains("write")) return TagAccessMode.ReadWrite;
        if (lower.Contains("write")) return TagAccessMode.Write;
        if (lower.Contains("read")) return TagAccessMode.Read;
        return TagAccessMode.ReadWrite;
    }

    #endregion

    public override string ToString() => $"{Icon} {DisplayName} ({NodeClass})";
}
