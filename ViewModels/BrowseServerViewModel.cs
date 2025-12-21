using System.Collections.ObjectModel;
using System.Windows.Input;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.ViewModels;

/// <summary>
/// ViewModel for the Browse Server window
/// Allows browsing OPC UA server nodes and adding them as tags
/// </summary>
public class BrowseServerViewModel : ObservableObject
{
    private readonly Session _session;
    private readonly PlcDevice _device;
    private readonly ILogger _logger = Log.Logger;

    private BrowseNodeItem? _selectedNode;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private string _selectedNodeDetails = string.Empty;

    public BrowseServerViewModel(Session session, PlcDevice device)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _device = device ?? throw new ArgumentNullException(nameof(device));

        // Initialize commands
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddSelectedTagCommand = new RelayCommand(AddSelectedTag, () => CanAddSelectedTag);
        AddAllVariablesCommand = new AsyncRelayCommand(AddAllVariablesAsync, () => SelectedNode != null);
        CopyNodeIdCommand = new RelayCommand(CopyNodeId, () => SelectedNode != null);
        ReadValueCommand = new AsyncRelayCommand(ReadSelectedValueAsync, () => SelectedNode?.IsVariable == true);
        ExpandAllCommand = new RelayCommand(ExpandAll);
        CollapseAllCommand = new RelayCommand(CollapseAll);

        // Load root nodes
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
                (AddSelectedTagCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyNodeIdCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ReadValueCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string SelectedNodeDetails
    {
        get => _selectedNodeDetails;
        set => SetProperty(ref _selectedNodeDetails, value);
    }

    public bool CanAddSelectedTag => SelectedNode?.CanAddAsTag == true;

    public string WindowTitle => $"Browse Server - {_device.Name} ({_session.Endpoint.EndpointUrl})";

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand AddSelectedTagCommand { get; }
    public ICommand AddAllVariablesCommand { get; }
    public ICommand CopyNodeIdCommand { get; }
    public ICommand ReadValueCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }

    #endregion

    #region Methods

    private async Task LoadRootNodesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading server nodes...";

        try
        {
            RootNodes.Clear();

            // Browse from Objects folder
            var objectsNode = await BrowseNodeAsync(ObjectIds.ObjectsFolder.ToString());
            if (objectsNode != null)
            {
                objectsNode.DisplayName = "Objects";
                objectsNode.IsExpanded = true;
                RootNodes.Add(objectsNode);
            }

            // Also add Types folder for reference
            var typesNode = await BrowseNodeAsync(ObjectIds.TypesFolder.ToString());
            if (typesNode != null)
            {
                typesNode.DisplayName = "Types";
                RootNodes.Add(typesNode);
            }

            // Add Views folder
            var viewsNode = await BrowseNodeAsync(ObjectIds.ViewsFolder.ToString());
            if (viewsNode != null)
            {
                viewsNode.DisplayName = "Views";
                RootNodes.Add(viewsNode);
            }

            StatusMessage = $"Loaded {RootNodes.Count} root folders";
            _logger.Information("Browse server loaded {Count} root folders", RootNodes.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading nodes: {ex.Message}";
            _logger.Error(ex, "Error loading root nodes");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<BrowseNodeItem?> BrowseNodeAsync(string nodeId)
    {
        try
        {
            var node = _session.ReadNode(new NodeId(nodeId));

            var browseNode = new BrowseNodeItem
            {
                NodeId = nodeId,
                DisplayName = node.DisplayName?.Text ?? nodeId,
                BrowseName = node.BrowseName?.ToString() ?? "",
                NodeClass = node.NodeClass.ToString(),
                Description = node.Description?.Text ?? "",
                HasChildren = true
            };

            // Subscribe to expand event for lazy loading
            browseNode.OnExpandRequested += OnNodeExpandRequested;

            // Add placeholder for lazy loading
            browseNode.AddLoadingPlaceholder();

            // Load children for root level
            await LoadChildrenAsync(browseNode);

            return browseNode;
        }
        catch (Exception ex)
        {
            _logger.Warning("Error browsing node {NodeId}: {Error}", nodeId, ex.Message);
            return null;
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
            var nodeId = new NodeId(parentNode.NodeId);

            _session.Browse(
                null,
                null,
                nodeId,
                0,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)Opc.Ua.NodeClass.Object | (uint)Opc.Ua.NodeClass.Variable | (uint)Opc.Ua.NodeClass.Method,
                out byte[] continuationPoint,
                out ReferenceDescriptionCollection references);

            parentNode.ClearLoadingPlaceholder();

            foreach (var reference in references)
            {
                var childNode = new BrowseNodeItem
                {
                    NodeId = reference.NodeId.ToString(),
                    DisplayName = reference.DisplayName?.Text ?? reference.NodeId.ToString(),
                    BrowseName = reference.BrowseName?.ToString() ?? "",
                    NodeClass = reference.NodeClass.ToString(),
                    Parent = parentNode,
                    HasChildren = reference.NodeClass == Opc.Ua.NodeClass.Object ||
                                 (reference.NodeClass == Opc.Ua.NodeClass.Variable && HasVariableChildren(reference.NodeId))
                };

                // Subscribe to expand event
                childNode.OnExpandRequested += OnNodeExpandRequested;

                // Get additional info for variables
                if (reference.NodeClass == Opc.Ua.NodeClass.Variable)
                {
                    await LoadVariableDetailsAsync(childNode, reference.NodeId);
                }

                // Add placeholder for children
                if (childNode.HasChildren)
                {
                    childNode.AddLoadingPlaceholder();
                }

                parentNode.Children.Add(childNode);
            }

            parentNode.HasChildren = parentNode.Children.Count > 0;

            _logger.Debug("Loaded {Count} children for {NodeName}", parentNode.Children.Count, parentNode.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.Warning("Error loading children for {NodeId}: {Error}", parentNode.NodeId, ex.Message);
            parentNode.ClearLoadingPlaceholder();
            parentNode.HasChildren = false;
        }
        finally
        {
            parentNode.IsLoading = false;
        }
    }

    private bool HasVariableChildren(ExpandedNodeId nodeId)
    {
        try
        {
            _session.Browse(
                null, null, (NodeId)nodeId, 1, BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences, true,
                (uint)Opc.Ua.NodeClass.Variable,
                out _, out ReferenceDescriptionCollection refs);
            return refs.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadVariableDetailsAsync(BrowseNodeItem node, ExpandedNodeId nodeId)
    {
        try
        {
            var readNode = _session.ReadNode((NodeId)nodeId);

            if (readNode is VariableNode varNode)
            {
                // Get data type
                if (varNode.DataType != null)
                {
                    try
                    {
                        var dataTypeNode = _session.ReadNode(varNode.DataType);
                        node.DataType = dataTypeNode?.DisplayName?.Text ?? varNode.DataType.ToString();
                    }
                    catch
                    {
                        node.DataType = varNode.DataType.ToString();
                    }
                }

                // Get access level
                var accessLevel = varNode.AccessLevel;
                var accessParts = new List<string>();
                if ((accessLevel & AccessLevels.CurrentRead) != 0) accessParts.Add("Read");
                if ((accessLevel & AccessLevels.CurrentWrite) != 0) accessParts.Add("Write");
                node.AccessLevel = string.Join("/", accessParts);

                // Read current value
                try
                {
                    var value = _session.ReadValue((NodeId)nodeId);
                    node.CurrentValue = value?.Value;
                }
                catch { /* Ignore read errors */ }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug("Error loading variable details for {NodeId}: {Error}", nodeId, ex.Message);
        }
    }

    private void UpdateSelectedNodeDetails()
    {
        if (SelectedNode == null)
        {
            SelectedNodeDetails = string.Empty;
            return;
        }

        var details = new System.Text.StringBuilder();
        details.AppendLine($"NodeId: {SelectedNode.NodeId}");
        details.AppendLine($"DisplayName: {SelectedNode.DisplayName}");
        details.AppendLine($"BrowseName: {SelectedNode.BrowseName}");
        details.AppendLine($"NodeClass: {SelectedNode.NodeClass}");

        if (SelectedNode.IsVariable)
        {
            details.AppendLine($"DataType: {SelectedNode.DataType}");
            details.AppendLine($"AccessLevel: {SelectedNode.AccessLevel}");
            details.AppendLine($"Value: {SelectedNode.ValueDisplay}");
        }

        if (!string.IsNullOrEmpty(SelectedNode.Description))
        {
            details.AppendLine($"Description: {SelectedNode.Description}");
        }

        SelectedNodeDetails = details.ToString();
    }

    private async Task RefreshAsync()
    {
        await LoadRootNodesAsync();
    }

    private void AddSelectedTag()
    {
        if (SelectedNode == null || !SelectedNode.CanAddAsTag) return;

        // Get or create default subscription group
        var subscriptionGroup = _device.SubscriptionGroups.FirstOrDefault(g => g.IsEnabled);
        if (subscriptionGroup == null)
        {
            StatusMessage = "No subscription group available";
            return;
        }

        // Check if tag already exists
        if (_device.Tags.Any(t => t.NodeId == SelectedNode.NodeId))
        {
            StatusMessage = $"Tag '{SelectedNode.DisplayName}' already exists";
            return;
        }

        var tag = SelectedNode.ToTagItem(_device.Id, subscriptionGroup.Id);
        _device.Tags.Add(tag);
        SelectedTags.Add(tag);

        StatusMessage = $"Added tag: {tag.Name}";
        _logger.Information("Added tag {TagName} with NodeId {NodeId}", tag.Name, tag.NodeId);
    }

    private async Task AddAllVariablesAsync()
    {
        if (SelectedNode == null) return;

        IsBusy = true;
        StatusMessage = "Adding all variables...";

        try
        {
            var subscriptionGroup = _device.SubscriptionGroups.FirstOrDefault(g => g.IsEnabled);
            if (subscriptionGroup == null)
            {
                StatusMessage = "No subscription group available";
                return;
            }

            var count = 0;
            await AddVariablesRecursiveAsync(SelectedNode, subscriptionGroup.Id, ref count);

            StatusMessage = $"Added {count} variables";
            _logger.Information("Added {Count} variables from {NodeName}", count, SelectedNode.DisplayName);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.Error(ex, "Error adding all variables");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddVariablesRecursiveAsync(BrowseNodeItem node, string subscriptionGroupId, ref int count)
    {
        // Load children if not loaded
        if (!node.ChildrenLoaded && node.HasChildren)
        {
            await LoadChildrenAsync(node);
        }

        // Add this node if it's a variable
        if (node.IsVariable && !_device.Tags.Any(t => t.NodeId == node.NodeId))
        {
            var tag = node.ToTagItem(_device.Id, subscriptionGroupId);
            _device.Tags.Add(tag);
            SelectedTags.Add(tag);
            count++;
        }

        // Process children
        foreach (var child in node.Children.ToList())
        {
            await AddVariablesRecursiveAsync(child, subscriptionGroupId, ref count);
        }
    }

    private void CopyNodeId()
    {
        if (SelectedNode == null) return;

        try
        {
            System.Windows.Clipboard.SetText(SelectedNode.NodeId);
            StatusMessage = $"Copied: {SelectedNode.NodeId}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error copying: {ex.Message}";
        }
    }

    private async Task ReadSelectedValueAsync()
    {
        if (SelectedNode == null || !SelectedNode.IsVariable) return;

        try
        {
            var value = _session.ReadValue(new NodeId(SelectedNode.NodeId));
            SelectedNode.CurrentValue = value?.Value;
            UpdateSelectedNodeDetails();
            StatusMessage = $"Value: {SelectedNode.ValueDisplay}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reading value: {ex.Message}";
        }
    }

    private void ExpandAll()
    {
        foreach (var node in RootNodes)
        {
            ExpandNodeRecursive(node);
        }
    }

    private void ExpandNodeRecursive(BrowseNodeItem node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
        {
            if (child.NodeClass != "Loading")
            {
                ExpandNodeRecursive(child);
            }
        }
    }

    private void CollapseAll()
    {
        foreach (var node in RootNodes)
        {
            CollapseNodeRecursive(node);
        }
    }

    private void CollapseNodeRecursive(BrowseNodeItem node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseNodeRecursive(child);
        }
    }

    #endregion
}
