using System.Collections.ObjectModel;
using System.Windows.Input;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.OpcUa;
using Serilog;

namespace OpcUaCommunicationEngine.ViewModels;

public class BrowseServerViewModel : ObservableObject
{
    private readonly PlcConnection _connection;
    private readonly PlcDevice _device;
    private readonly ILogger _logger = Log.Logger;

    private BrowseNodeItem? _selectedNode;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private string _selectedNodeDetails = string.Empty;

    public BrowseServerViewModel(PlcConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _device = connection.Device;

        RefreshCommand         = new AsyncRelayCommand(RefreshAsync);
        AddSelectedTagCommand  = new RelayCommand(AddSelectedTag, () => CanAddSelectedTag);
        AddAllVariablesCommand = new AsyncRelayCommand(AddAllVariablesAsync, () => SelectedNode != null);
        CopyNodeIdCommand      = new RelayCommand(CopyNodeId, () => SelectedNode != null);
        ReadValueCommand       = new AsyncRelayCommand(ReadSelectedValueAsync, () => SelectedNode?.IsVariable == true);
        ExpandAllCommand       = new RelayCommand(ExpandAll);
        CollapseAllCommand     = new RelayCommand(CollapseAll);

        _ = LoadRootNodesAsync();
    }

    #region Properties

    public ObservableCollection<BrowseNodeItem> RootNodes { get; } = new();
    public ObservableCollection<TagItem> SelectedTags { get; } = new();

    public BrowseNodeItem? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                OnPropertyChanged(nameof(CanAddSelectedTag));
                UpdateSelectedNodeDetails();
                (AddSelectedTagCommand  as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyNodeIdCommand      as RelayCommand)?.RaiseCanExecuteChanged();
                (ReadValueCommand       as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string SelectedNodeDetails
    {
        get => _selectedNodeDetails;
        set => SetProperty(ref _selectedNodeDetails, value);
    }

    public bool CanAddSelectedTag => SelectedNode?.CanAddAsTag == true;
    public string WindowTitle => $"Browse Server — {_device.Name}  ({_device.EndpointUrl})";

    #endregion

    #region Commands

    public ICommand RefreshCommand         { get; }
    public ICommand AddSelectedTagCommand  { get; }
    public ICommand AddAllVariablesCommand { get; }
    public ICommand CopyNodeIdCommand      { get; }
    public ICommand ReadValueCommand       { get; }
    public ICommand ExpandAllCommand       { get; }
    public ICommand CollapseAllCommand     { get; }

    #endregion

    #region Loading

    private async Task LoadRootNodesAsync()
    {
        IsBusy = true;
        StatusMessage = "Đang tải cây node OPC UA...";

        try
        {
            RootNodes.Clear();

            // Standard OPC UA root folders (well-known numeric NodeIds)
            var rootFolders = new[]
            {
                ("i=85", "Objects"),
                ("i=86", "Types"),
                ("i=87", "Views")
            };

            foreach (var (nodeIdStr, fallbackName) in rootFolders)
            {
                var info = await _connection.GetNodeInfoAsync(nodeIdStr);
                var folderNode = new BrowseNodeItem
                {
                    NodeId      = nodeIdStr,
                    DisplayName = info?.DisplayName ?? fallbackName,
                    BrowseName  = info?.BrowseName  ?? fallbackName,
                    NodeClass   = info?.NodeClass   ?? "Object",
                    Description = info?.Description ?? "",
                    HasChildren = true
                };
                folderNode.OnExpandRequested += OnNodeExpandRequested;
                folderNode.AddLoadingPlaceholder();

                // Eager-load the Objects folder; the rest load on demand
                if (nodeIdStr == "i=85")
                {
                    await LoadChildrenAsync(folderNode);
                    folderNode.IsExpanded = true;
                }

                RootNodes.Add(folderNode);
            }

            StatusMessage = $"Đã tải {RootNodes.Count} thư mục gốc";
            _logger.Information("BrowseServer: loaded {Count} root folders for {PlcName}", RootNodes.Count, _device.Name);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi tải node: {ex.Message}";
            _logger.Error(ex, "BrowseServer: error loading root nodes for {PlcName}", _device.Name);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnNodeExpandRequested(BrowseNodeItem node)
    {
        if (node.ChildrenLoaded) return;
        await LoadChildrenAsync(node);
    }

    private async Task LoadChildrenAsync(BrowseNodeItem parentNode)
    {
        if (parentNode.ChildrenLoaded) return;
        parentNode.IsLoading = true;

        try
        {
            // Use PlcConnection.BrowseAsync — keeps all OPC UA logic in the service layer
            var children = await _connection.BrowseAsync(parentNode.NodeId);
            parentNode.ClearLoadingPlaceholder();

            foreach (var child in children)
            {
                var childNode = new BrowseNodeItem
                {
                    NodeId      = child.NodeId,
                    DisplayName = child.DisplayName,
                    BrowseName  = child.BrowseName,
                    NodeClass   = child.NodeClass,
                    Description = child.Description ?? "",
                    HasChildren = child.HasChildren,
                    Parent      = parentNode
                };
                childNode.OnExpandRequested += OnNodeExpandRequested;

                if (child.NodeClass == "Variable")
                    await LoadVariableDetailsAsync(childNode);

                if (childNode.HasChildren)
                    childNode.AddLoadingPlaceholder();

                parentNode.Children.Add(childNode);
            }

            parentNode.HasChildren = parentNode.Children.Count > 0;
            _logger.Debug("BrowseServer: loaded {Count} children for {NodeName}", parentNode.Children.Count, parentNode.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.Warning("BrowseServer: error loading children for {NodeId}: {Error}", parentNode.NodeId, ex.Message);
            parentNode.ClearLoadingPlaceholder();
            parentNode.HasChildren = false;
        }
        finally
        {
            parentNode.IsLoading = false;
        }
    }

    // Use PlcConnection.GetNodeInfoAsync — reads DataType, access level, current value
    private async Task LoadVariableDetailsAsync(BrowseNodeItem node)
    {
        try
        {
            var info = await _connection.GetNodeInfoAsync(node.NodeId);
            if (info == null) return;

            node.DataType = info.DataType ?? "";

            var access = new List<string>();
            if (info.IsReadable) access.Add("Read");
            if (info.IsWritable) access.Add("Write");
            node.AccessLevel = string.Join("/", access);

            node.CurrentValue = info.CurrentValue;
        }
        catch (Exception ex)
        {
            _logger.Debug("BrowseServer: error loading variable details for {NodeId}: {Error}", node.NodeId, ex.Message);
        }
    }

    #endregion

    #region Commands Implementation

    private void UpdateSelectedNodeDetails()
    {
        if (SelectedNode == null) { SelectedNodeDetails = string.Empty; return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"NodeId:      {SelectedNode.NodeId}");
        sb.AppendLine($"DisplayName: {SelectedNode.DisplayName}");
        sb.AppendLine($"BrowseName:  {SelectedNode.BrowseName}");
        sb.AppendLine($"NodeClass:   {SelectedNode.NodeClass}");

        if (SelectedNode.IsVariable)
        {
            sb.AppendLine($"DataType:    {SelectedNode.DataType}");
            sb.AppendLine($"Access:      {SelectedNode.AccessLevel}");
            sb.AppendLine($"Value:       {SelectedNode.ValueDisplay}");
        }
        if (!string.IsNullOrEmpty(SelectedNode.Description))
            sb.AppendLine($"Description: {SelectedNode.Description}");

        SelectedNodeDetails = sb.ToString();
    }

    private async Task RefreshAsync()
    {
        await LoadRootNodesAsync();
    }

    private void AddSelectedTag()
    {
        if (SelectedNode == null || !SelectedNode.CanAddAsTag) return;

        var subscriptionGroup = _device.SubscriptionGroups.FirstOrDefault(g => g.IsEnabled);
        if (subscriptionGroup == null)
        {
            StatusMessage = "Không có subscription group nào khả dụng";
            return;
        }

        if (_device.Tags.Any(t => t.NodeId == SelectedNode.NodeId))
        {
            StatusMessage = $"Tag '{SelectedNode.DisplayName}' đã tồn tại";
            return;
        }

        var tag = SelectedNode.ToTagItem(_device.Id, subscriptionGroup.Id);
        _device.Tags.Add(tag);
        SelectedTags.Add(tag);

        StatusMessage = $"Đã thêm tag: {tag.Name}";
        _logger.Information("BrowseServer: added tag {TagName} (NodeId={NodeId})", tag.Name, tag.NodeId);
    }

    private async Task AddAllVariablesAsync()
    {
        if (SelectedNode == null) return;

        IsBusy = true;
        StatusMessage = "Đang thêm tất cả biến...";

        try
        {
            var subscriptionGroup = _device.SubscriptionGroups.FirstOrDefault(g => g.IsEnabled);
            if (subscriptionGroup == null) { StatusMessage = "Không có subscription group nào"; return; }

            var count = await AddVariablesRecursiveAsync(SelectedNode, subscriptionGroup.Id);
            StatusMessage = $"Đã thêm {count} biến từ '{SelectedNode.DisplayName}'";
            _logger.Information("BrowseServer: bulk-added {Count} variables from {NodeName}", count, SelectedNode.DisplayName);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.Message}";
            _logger.Error(ex, "BrowseServer: error adding all variables");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<int> AddVariablesRecursiveAsync(BrowseNodeItem node, string subscriptionGroupId)
    {
        var count = 0;

        if (!node.ChildrenLoaded && node.HasChildren)
            await LoadChildrenAsync(node);

        if (node.IsVariable && !_device.Tags.Any(t => t.NodeId == node.NodeId))
        {
            var tag = node.ToTagItem(_device.Id, subscriptionGroupId);
            _device.Tags.Add(tag);
            SelectedTags.Add(tag);
            count++;
        }

        foreach (var child in node.Children.ToList())
            count += await AddVariablesRecursiveAsync(child, subscriptionGroupId);

        return count;
    }

    private void CopyNodeId()
    {
        if (SelectedNode == null) return;
        try
        {
            System.Windows.Clipboard.SetText(SelectedNode.NodeId);
            StatusMessage = $"Đã sao chép: {SelectedNode.NodeId}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi sao chép: {ex.Message}";
        }
    }

    // Use PlcConnection.ReadTagAsync — keeps read logic in the service layer
    private async Task ReadSelectedValueAsync()
    {
        if (SelectedNode == null || !SelectedNode.IsVariable) return;
        try
        {
            var tagValue = await _connection.ReadTagAsync(SelectedNode.NodeId);
            SelectedNode.CurrentValue = tagValue?.Value;
            UpdateSelectedNodeDetails();
            StatusMessage = $"Giá trị: {SelectedNode.ValueDisplay}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi đọc giá trị: {ex.Message}";
        }
    }

    private void ExpandAll()
    {
        foreach (var node in RootNodes)
            ExpandNodeRecursive(node);
    }

    private void ExpandNodeRecursive(BrowseNodeItem node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
            if (child.NodeClass != "Loading") ExpandNodeRecursive(child);
    }

    private void CollapseAll()
    {
        foreach (var node in RootNodes)
            CollapseNodeRecursive(node);
    }

    private void CollapseNodeRecursive(BrowseNodeItem node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            CollapseNodeRecursive(child);
    }

    #endregion
}
