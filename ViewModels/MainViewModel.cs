using System.Collections.ObjectModel;
using System.Windows.Input;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.OpcUa;
using Serilog;
using Serilog.Events;

namespace OpcUaCommunicationEngine.ViewModels;

/// <summary>
/// ViewModel chính cho MainWindow
/// Tích hợp OPC UA Manager để kết nối thực đến PLCs
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly IDataCache _dataCache;
    private readonly IPlcManager _plcManager;
    private readonly ILogger _logger;

    private PlcDevice? _selectedPlc;
    private TagItem? _selectedTag;
    private string _statusMessage = "Ready";
    private bool _isConnected;
    private int _connectedPlcCount;
    private int _totalPlcCount;
    private bool _isLogPanelVisible = true;

    #region Properties

    public ObservableCollection<PlcDevice> PlcDevices { get; } = new();

    /// <summary>
    /// Collection log entries để hiển thị trên UI
    /// </summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>
    /// Log panel visibility
    /// </summary>
    public bool IsLogPanelVisible
    {
        get => _isLogPanelVisible;
        set => SetProperty(ref _isLogPanelVisible, value);
    }

    public PlcDevice? SelectedPlc
    {
        get => _selectedPlc;
        set
        {
            if (SetProperty(ref _selectedPlc, value))
            {
                OnPropertyChanged(nameof(HasSelectedPlc));
                OnPropertyChanged(nameof(SelectedPlcTags));
                OnPropertyChanged(nameof(SelectedPlcStatus));
                SelectedTag = null;
            }
        }
    }

    public bool HasSelectedPlc => SelectedPlc != null;

    public ObservableCollection<TagItem>? SelectedPlcTags => SelectedPlc?.Tags;

    public TagItem? SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value))
            {
                OnPropertyChanged(nameof(HasSelectedTag));
            }
        }
    }

    public bool HasSelectedTag => SelectedTag != null;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public int ConnectedPlcCount
    {
        get => _connectedPlcCount;
        set => SetProperty(ref _connectedPlcCount, value);
    }

    public int TotalPlcCount
    {
        get => _totalPlcCount;
        set => SetProperty(ref _totalPlcCount, value);
    }

    public string ConnectionStatusText => $"PLCs: {ConnectedPlcCount}/{TotalPlcCount} connected";

    public PlcStatus? SelectedPlcStatus => SelectedPlc != null 
        ? _plcManager.GetStatus(SelectedPlc.Id) 
        : null;

    public bool HasUnsavedChanges => _configService.HasUnsavedChanges;

    public string WindowTitle => HasUnsavedChanges 
        ? "OPC UA Communication Engine *" 
        : "OPC UA Communication Engine";

    #endregion

    #region Commands

    public ICommand LoadConfigCommand { get; }
    public ICommand SaveConfigCommand { get; }
    public ICommand SaveConfigAsCommand { get; }
    public ICommand AddPlcCommand { get; }
    public ICommand EditPlcCommand { get; }
    public ICommand DeletePlcCommand { get; }
    public ICommand AddTagCommand { get; }
    public ICommand EditTagCommand { get; }
    public ICommand DeleteTagCommand { get; }
    public ICommand ConnectAllCommand { get; }
    public ICommand DisconnectAllCommand { get; }
    public ICommand ConnectSelectedCommand { get; }
    public ICommand DisconnectSelectedCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ReadTagCommand { get; }
    public ICommand WriteTagCommand { get; }
    public ICommand BrowseServerCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand ToggleLogPanelCommand { get; }
    public ICommand ClearLogsCommand { get; }

    #endregion

    public MainViewModel(
        IConfigurationService configService,
        IDataCache dataCache,
        IPlcManager plcManager,
        ILogger logger)
    {
        _configService = configService;
        _dataCache = dataCache;
        _plcManager = plcManager;
        _logger = logger;

        _logger.Debug("MainViewModel constructor starting...");

        // Subscribe to PlcManager events
        _plcManager.ConnectionStateChanged += OnPlcConnectionStateChanged;
        _plcManager.TagValueChanged += OnTagValueChanged;
        _plcManager.ErrorOccurred += OnPlcErrorOccurred;

        // Initialize commands
        LoadConfigCommand = new AsyncRelayCommand(LoadConfigurationAsync);
        SaveConfigCommand = new AsyncRelayCommand(SaveConfigurationAsync, () => HasUnsavedChanges);
        SaveConfigAsCommand = new AsyncRelayCommand(SaveConfigurationAsAsync);
        AddPlcCommand = new RelayCommand(AddPlc);
        EditPlcCommand = new RelayCommand(EditPlc, () => HasSelectedPlc);
        DeletePlcCommand = new RelayCommand(DeletePlc);
        AddTagCommand = new RelayCommand(AddTag, () => HasSelectedPlc);
        EditTagCommand = new RelayCommand(EditTag, () => HasSelectedTag);
        DeleteTagCommand = new RelayCommand(DeleteTag);
        ConnectAllCommand = new AsyncRelayCommand(ConnectAllAsync);
        DisconnectAllCommand = new AsyncRelayCommand(DisconnectAllAsync);
        ConnectSelectedCommand = new AsyncRelayCommand(ConnectSelectedAsync, () => HasSelectedPlc);
        DisconnectSelectedCommand = new AsyncRelayCommand(DisconnectSelectedAsync, () => HasSelectedPlc);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ReadTagCommand = new AsyncRelayCommand(ReadSelectedTagAsync, () => HasSelectedTag && HasSelectedPlc);
        WriteTagCommand = new AsyncRelayCommand(WriteSelectedTagAsync, () => HasSelectedTag && HasSelectedPlc);
        BrowseServerCommand = new AsyncRelayCommand(BrowseServerAsync, () => HasSelectedPlc);
        ExitCommand = new RelayCommand(Exit);
        AboutCommand = new RelayCommand(ShowAbout);
        ToggleLogPanelCommand = new RelayCommand(ToggleLogPanel);
        ClearLogsCommand = new RelayCommand(ClearLogs);

        _logger.Debug("MainViewModel constructor completed");
    }

    #region Initialization

    public async Task InitializeAsync()
    {
        _logger.Information("InitializeAsync starting...");
        
        try
        {
            StatusMessage = "Loading configuration...";
            
            // Load configuration
            await _configService.LoadConfigurationAsync();
            
            // Update UI
            PlcDevices.Clear();
            foreach (var plc in _configService.CurrentConfiguration.PlcDevices)
            {
                PlcDevices.Add(plc);
                _logger.Debug("Added PLC: {Name}", plc.Name);
            }
            
            TotalPlcCount = PlcDevices.Count;
            
            // Initialize PlcManager with PLCs from configuration
            await _plcManager.InitializeAsync();
            
            StatusMessage = $"Loaded {PlcDevices.Count} PLCs";
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ConnectionStatusText));
            
            _logger.Information("InitializeAsync completed. PLCs: {Count}", PlcDevices.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in InitializeAsync");
            StatusMessage = "Error loading configuration";
        }
    }

    #endregion

    #region PlcManager Event Handlers

    private void OnPlcConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ConnectedPlcCount = _plcManager.ConnectedCount;
            IsConnected = ConnectedPlcCount > 0;
            
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(SelectedPlcStatus));
            
            StatusMessage = $"{e.PlcName}: {e.NewState}";
            
            var plc = PlcDevices.FirstOrDefault(p => p.Id == e.PlcId);
            if (plc != null)
            {
                plc.ConnectionState = e.NewState;
            }
        });
    }

    private void OnTagValueChanged(object? sender, Interfaces.TagValueChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var plc = PlcDevices.FirstOrDefault(p => p.Id == e.PlcId);
            var tag = plc?.Tags.FirstOrDefault(t => t.Id == e.TagId);
            
            if (tag != null)
            {
                tag.UpdateValue(e.Value.Value, e.Value.Quality, e.Value.SourceTimestamp);
            }
        });
    }

    private void OnPlcErrorOccurred(object? sender, PlcErrorEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            StatusMessage = $"Error on {e.PlcName}: {e.ErrorMessage}";
            
            if (e.IsCritical)
            {
                System.Windows.MessageBox.Show(
                    $"Critical error on {e.PlcName}:\n{e.ErrorMessage}",
                    "PLC Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        });
    }

    #endregion

    #region Configuration Commands

    private async Task LoadConfigurationAsync()
    {
        IsBusy = true;
        BusyMessage = "Loading configuration...";
        StatusMessage = "Loading configuration...";
        
        try
        {
            await _plcManager.DisconnectAllAsync();
            await Task.Run(async () => await _configService.LoadConfigurationAsync());
            
            PlcDevices.Clear();
            foreach (var plc in _configService.CurrentConfiguration.PlcDevices)
            {
                PlcDevices.Add(plc);
                if (!_plcManager.HasPlc(plc.Id))
                {
                    await _plcManager.AddPlcAsync(plc);
                }
            }
            
            TotalPlcCount = PlcDevices.Count;
            StatusMessage = $"Loaded {PlcDevices.Count} PLCs";
            OnPropertyChanged(nameof(ConnectionStatusText));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading configuration");
            StatusMessage = "Error loading configuration";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task SaveConfigurationAsync()
    {
        IsBusy = true;
        BusyMessage = "Saving configuration...";
        
        try
        {
            var result = await Task.Run(async () => await _configService.SaveConfigurationAsync());
            StatusMessage = result ? "Configuration saved" : "Failed to save configuration";
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving configuration");
            StatusMessage = "Error saving configuration";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task SaveConfigurationAsAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "plc_config.json"
        };

        if (dialog.ShowDialog() == true)
        {
            IsBusy = true;
            BusyMessage = "Saving configuration...";
            
            try
            {
                var result = await Task.Run(async () => 
                    await _configService.SaveConfigurationAsAsync(dialog.FileName));
                StatusMessage = result ? "Configuration saved" : "Failed to save configuration";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving configuration");
                StatusMessage = "Error saving configuration";
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }
    }

    #endregion

    #region PLC Commands

    private void AddPlc()
    {
        _logger.Information("AddPlc called");
        
        var newPlc = PlcDevice.Create(
            $"New PLC {PlcDevices.Count + 1}",
            "opc.tcp://localhost:4840");

        foreach (var group in SubscriptionGroup.CreateDefaultGroups(newPlc.Id))
        {
            newPlc.SubscriptionGroups.Add(group);
        }

        PlcDevices.Add(newPlc);
        _configService.CurrentConfiguration.PlcDevices.Add(newPlc);
        _configService.MarkAsModified();
        
        _ = _plcManager.AddPlcAsync(newPlc);
        
        TotalPlcCount = PlcDevices.Count;
        SelectedPlc = newPlc;
        StatusMessage = $"Added new PLC: {newPlc.Name}";
        
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(ConnectionStatusText));
    }

    private void EditPlc()
    {
        if (SelectedPlc == null) return;
        StatusMessage = $"Editing PLC: {SelectedPlc.Name}";
    }

    private void DeletePlc()
    {
        if (SelectedPlc == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete PLC '{SelectedPlc.Name}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        var plcName = SelectedPlc.Name;
        var plcId = SelectedPlc.Id;

        _ = _plcManager.RemovePlcAsync(plcId);

        PlcDevices.Remove(SelectedPlc);
        var plcToRemove = _configService.CurrentConfiguration.PlcDevices
            .FirstOrDefault(p => p.Id == plcId);
        if (plcToRemove != null)
        {
            _configService.CurrentConfiguration.PlcDevices.Remove(plcToRemove);
        }
        
        _configService.MarkAsModified();
        SelectedPlc = null;
        TotalPlcCount = PlcDevices.Count;

        StatusMessage = $"Deleted PLC: {plcName}";
        
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(ConnectionStatusText));
    }

    #endregion

    #region Tag Commands

    private void AddTag()
    {
        if (SelectedPlc == null) return;

        var newTag = TagItem.Create(
            SelectedPlc.Id,
            $"NewTag_{SelectedPlc.Tags.Count + 1}",
            "ns=2;i=1001");

        var firstGroup = SelectedPlc.SubscriptionGroups.FirstOrDefault();
        if (firstGroup != null)
        {
            newTag.SubscriptionGroupId = firstGroup.Id;
        }

        SelectedPlc.Tags.Add(newTag);
        _configService.MarkAsModified();
        
        SelectedTag = newTag;
        StatusMessage = $"Added new Tag: {newTag.Name}";
        
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void EditTag()
    {
        if (SelectedTag == null) return;
        StatusMessage = $"Editing Tag: {SelectedTag.Name}";
    }

    private void DeleteTag()
    {
        if (SelectedTag == null || SelectedPlc == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete Tag '{SelectedTag.Name}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        var tagName = SelectedTag.Name;
        SelectedPlc.Tags.Remove(SelectedTag);
        _configService.MarkAsModified();
        SelectedTag = null;

        StatusMessage = $"Deleted Tag: {tagName}";
        
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    #endregion

    #region Connection Commands

    private async Task ConnectAllAsync()
    {
        IsBusy = true;
        BusyMessage = "Connecting to all PLCs...";
        StatusMessage = "Connecting to all PLCs...";
        
        try
        {
            var connectedCount = await _plcManager.ConnectAllAsync();
            ConnectedPlcCount = connectedCount;
            IsConnected = connectedCount > 0;
            
            StatusMessage = $"Connected to {connectedCount}/{TotalPlcCount} PLCs";
            OnPropertyChanged(nameof(ConnectionStatusText));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error connecting to PLCs");
            StatusMessage = "Error connecting to PLCs";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task DisconnectAllAsync()
    {
        IsBusy = true;
        BusyMessage = "Disconnecting from all PLCs...";
        StatusMessage = "Disconnecting from all PLCs...";
        
        try
        {
            await _plcManager.DisconnectAllAsync();
            ConnectedPlcCount = 0;
            IsConnected = false;
            
            StatusMessage = "Disconnected from all PLCs";
            OnPropertyChanged(nameof(ConnectionStatusText));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from PLCs");
            StatusMessage = "Error disconnecting from PLCs";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task ConnectSelectedAsync()
    {
        if (SelectedPlc == null) return;

        var plcName = SelectedPlc.Name;
        IsBusy = true;
        BusyMessage = $"Connecting to {plcName}...";
        StatusMessage = $"Connecting to {plcName}...";
        
        try
        {
            var success = await _plcManager.ConnectAsync(SelectedPlc.Id);
            
            if (success)
            {
                ConnectedPlcCount = _plcManager.ConnectedCount;
                IsConnected = ConnectedPlcCount > 0;
                StatusMessage = $"Connected to {plcName}";
            }
            else
            {
                StatusMessage = $"Failed to connect to {plcName}";
            }
            
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(SelectedPlcStatus));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error connecting to {PlcName}", plcName);
            StatusMessage = $"Error connecting to {plcName}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task DisconnectSelectedAsync()
    {
        if (SelectedPlc == null) return;

        var plcName = SelectedPlc.Name;
        IsBusy = true;
        BusyMessage = $"Disconnecting from {plcName}...";
        StatusMessage = $"Disconnecting from {plcName}...";
        
        try
        {
            await _plcManager.DisconnectAsync(SelectedPlc.Id);
            
            ConnectedPlcCount = _plcManager.ConnectedCount;
            IsConnected = ConnectedPlcCount > 0;
            StatusMessage = $"Disconnected from {plcName}";
            
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(SelectedPlcStatus));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error disconnecting from {PlcName}", plcName);
            StatusMessage = $"Error disconnecting from {plcName}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    #endregion

    #region Read/Write Commands

    private async Task ReadSelectedTagAsync()
    {
        if (SelectedPlc == null || SelectedTag == null) return;

        try
        {
            StatusMessage = $"Reading {SelectedTag.Name}...";
            
            var value = await _plcManager.ReadTagAsync(SelectedPlc.Id, SelectedTag.NodeId);
            
            if (value != null)
            {
                SelectedTag.UpdateValue(value.Value, value.Quality, value.SourceTimestamp);
                StatusMessage = $"{SelectedTag.Name} = {value.Value}";
            }
            else
            {
                StatusMessage = $"Failed to read {SelectedTag.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading tag {TagName}", SelectedTag.Name);
            StatusMessage = $"Error reading {SelectedTag.Name}";
        }
    }

    private async Task WriteSelectedTagAsync()
    {
        if (SelectedPlc == null || SelectedTag == null) return;

        System.Windows.MessageBox.Show(
            "Write Tag feature - implement value input dialog.",
            "Write Tag",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private async Task BrowseServerAsync()
    {
        if (SelectedPlc == null) return;

        var connection = _plcManager.GetConnection(SelectedPlc.Id);
        if (connection == null || !connection.IsConnected)
        {
            System.Windows.MessageBox.Show(
                "Please connect to the PLC first.",
                "Not Connected",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsBusy = true;
            BusyMessage = "Browsing server...";
            StatusMessage = "Browsing OPC UA server...";

            var nodes = await _plcManager.BrowseAsync(SelectedPlc.Id);
            
            StatusMessage = $"Found {nodes.Count} nodes";
            
            var nodeNames = string.Join("\n", nodes.Take(10).Select(n => $"{n.DisplayName} ({n.NodeClass})"));
            System.Windows.MessageBox.Show(
                $"Found {nodes.Count} nodes:\n\n{nodeNames}\n...",
                "Browse Result",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error browsing server");
            StatusMessage = "Error browsing server";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task RefreshAsync()
    {
        if (SelectedPlc != null && _plcManager.GetConnection(SelectedPlc.Id)?.IsConnected == true)
        {
            var nodeIds = SelectedPlc.Tags.Where(t => t.IsEnabled).Select(t => t.NodeId);
            var values = await _plcManager.ReadTagsAsync(SelectedPlc.Id, nodeIds);
            StatusMessage = $"Refreshed {values.Count} tags";
        }
        else
        {
            await LoadConfigurationAsync();
        }
    }

    #endregion

    #region Other Commands

    private void Exit()
    {
        if (HasUnsavedChanges)
        {
            var result = System.Windows.MessageBox.Show(
                "You have unsaved changes. Do you want to save before exiting?",
                "Unsaved Changes",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            switch (result)
            {
                case System.Windows.MessageBoxResult.Yes:
                    _ = SaveConfigurationAsync();
                    break;
                case System.Windows.MessageBoxResult.Cancel:
                    return;
            }
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "OPC UA Communication Engine\n" +
            "Version 1.0.0\n\n" +
            "A multi-PLC OPC UA communication system\n" +
            "for industrial automation applications.\n\n" +
            "Features:\n" +
            "• Multiple PLC connections\n" +
            "• Real-time tag monitoring\n" +
            "• Subscription-based updates\n" +
            "• Auto-reconnect support\n" +
            "• OPC UA Browse capability",
            "About",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void ToggleLogPanel()
    {
        IsLogPanelVisible = !IsLogPanelVisible;
    }

    private void ClearLogs()
    {
        LogEntries.Clear();
        _logger.Information("Log cleared");
    }

    #endregion
}
