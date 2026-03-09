using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.Auth;
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

    // Lock related fields
    private OperatorLockService? _lockService;
    private bool _hasLock;
    private string _lockStatusText = "No lock service";
    private string _lockHolderName = string.Empty;
    private bool _canAcquireLock;
    private string _currentUserId = "local-user";

    // Logged in user
    private User? _loggedInUser;

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

    public string WindowTitle
    {
        get
        {
            var fileName = !string.IsNullOrEmpty(_configService.CurrentFilePath)
                ? Path.GetFileName(_configService.CurrentFilePath)
                : "New Configuration";
            var modified = HasUnsavedChanges ? " *" : "";
            var userInfo = _loggedInUser != null ? $" [{_loggedInUser.DisplayName}]" : "";
            return $"OPC UA Communication Engine - {fileName}{modified}{userInfo}";
        }
    }

    #region Lock Properties

    /// <summary>
    /// Whether current user holds the operator lock
    /// </summary>
    public bool HasLock
    {
        get => _hasLock;
        private set
        {
            if (SetProperty(ref _hasLock, value))
            {
                OnPropertyChanged(nameof(LockIcon));
                OnPropertyChanged(nameof(LockStatusText));
                OnPropertyChanged(nameof(LockStatusColor));
                OnPropertyChanged(nameof(CanAcquireLock));
            }
        }
    }

    /// <summary>
    /// Whether user can acquire the lock (no one else holds it)
    /// </summary>
    public bool CanAcquireLock
    {
        get => _canAcquireLock && !_hasLock;
        private set => SetProperty(ref _canAcquireLock, value);
    }

    /// <summary>
    /// Lock status display text
    /// </summary>
    public string LockStatusText
    {
        get
        {
            if (_lockService == null)
                return "No lock service";

            if (_hasLock)
                return "You have control";

            if (!string.IsNullOrEmpty(_lockHolderName))
                return $"Locked by {_lockHolderName}";

            return "Available";
        }
    }

    /// <summary>
    /// Lock icon based on current state
    /// </summary>
    public string LockIcon
    {
        get
        {
            if (_lockService == null)
                return "⚠️";

            if (_hasLock)
                return "🔒";

            if (!string.IsNullOrEmpty(_lockHolderName))
                return "🔐";

            return "🔓";
        }
    }

    /// <summary>
    /// Lock status color as Brush
    /// </summary>
    public Brush LockStatusColor
    {
        get
        {
            if (_lockService == null)
                return Brushes.Gray;

            if (_hasLock)
                return Brushes.Green;

            if (!string.IsNullOrEmpty(_lockHolderName))
                return Brushes.Orange;

            return Brushes.DodgerBlue;
        }
    }

    #endregion

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

    // Lock commands
    public ICommand AcquireLockCommand { get; }
    public ICommand ReleaseLockCommand { get; }

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

        // Lock commands
        AcquireLockCommand = new AsyncRelayCommand(AcquireLockAsync, () => CanAcquireLock);
        ReleaseLockCommand = new AsyncRelayCommand(ReleaseLockAsync, () => HasLock);

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
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json",
            Title = "Open Configuration File"
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        BusyMessage = "Loading configuration...";
        StatusMessage = "Loading configuration...";

        try
        {
            await _plcManager.DisconnectAllAsync();
            await Task.Run(async () => await _configService.LoadConfigurationAsync(dialog.FileName));

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
            StatusMessage = $"Loaded {PlcDevices.Count} PLCs from {Path.GetFileName(dialog.FileName)}";
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ConnectionStatusText));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading configuration");
            StatusMessage = "Error loading configuration";
            System.Windows.MessageBox.Show(
                $"Error loading configuration:\n{ex.Message}",
                "Load Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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

        var dialog = new Views.EditPlcDialog(SelectedPlc);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            _configService.MarkAsModified();
            StatusMessage = $"PLC '{SelectedPlc.Name}' updated";
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
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

        var dialog = new Views.EditTagDialog(SelectedTag);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            _configService.MarkAsModified();
            StatusMessage = $"Tag '{SelectedTag.Name}' updated";
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
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

    /// <summary>
    /// Delete multiple selected tags
    /// </summary>
    public void DeleteSelectedTags(IEnumerable<TagItem> tagsToDelete)
    {
        if (SelectedPlc == null) return;

        var tagsList = tagsToDelete.ToList();
        var count = tagsList.Count;

        foreach (var tag in tagsList)
        {
            SelectedPlc.Tags.Remove(tag);
        }

        _configService.MarkAsModified();
        SelectedTag = null;

        StatusMessage = $"Deleted {count} tag(s)";
        _logger.Information("Deleted {Count} tags from PLC {PlcName}", count, SelectedPlc.Name);

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

        // Check if PLC is connected
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

        // Check if tag is writable
        if (!SelectedTag.CanWrite)
        {
            System.Windows.MessageBox.Show(
                "This tag is read-only and cannot be written.",
                "Read-Only Tag",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var dialog = new Views.WriteValueDialog(SelectedTag);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true && dialog.NewValue != null)
        {
            try
            {
                StatusMessage = $"Writing to {SelectedTag.Name}...";

                var success = await _plcManager.WriteTagAsync(SelectedPlc.Id, SelectedTag.NodeId, dialog.NewValue);

                if (success)
                {
                    StatusMessage = $"Successfully wrote '{dialog.NewValue}' to {SelectedTag.Name}";

                    // Refresh the tag value
                    var value = await _plcManager.ReadTagAsync(SelectedPlc.Id, SelectedTag.NodeId);
                    if (value != null)
                    {
                        SelectedTag.UpdateValue(value.Value, value.Quality, value.SourceTimestamp);
                    }
                }
                else
                {
                    StatusMessage = $"Failed to write to {SelectedTag.Name}";
                    System.Windows.MessageBox.Show(
                        $"Failed to write value to {SelectedTag.Name}",
                        "Write Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error writing to tag {TagName}", SelectedTag.Name);
                StatusMessage = $"Error writing to {SelectedTag.Name}";
                System.Windows.MessageBox.Show(
                    $"Error writing to {SelectedTag.Name}: {ex.Message}",
                    "Write Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Write a string value to a tag (for inline editing)
    /// </summary>
    public async Task WriteTagValueAsync(TagItem tag, string valueString)
    {
        if (SelectedPlc == null || tag == null) return;

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

        if (!tag.CanWrite)
        {
            System.Windows.MessageBox.Show(
                "This tag is read-only and cannot be written.",
                "Read-Only Tag",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Parse the value based on tag data type
            object? parsedValue = ParseValue(valueString, tag.DataType);

            if (parsedValue == null)
            {
                System.Windows.MessageBox.Show(
                    $"Could not parse '{valueString}' as {tag.DataType}",
                    "Parse Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            StatusMessage = $"Writing to {tag.Name}...";

            var success = await _plcManager.WriteTagAsync(SelectedPlc.Id, tag.NodeId, parsedValue);

            if (success)
            {
                StatusMessage = $"Successfully wrote '{parsedValue}' to {tag.Name}";

                // Refresh the tag value
                var value = await _plcManager.ReadTagAsync(SelectedPlc.Id, tag.NodeId);
                if (value != null)
                {
                    tag.UpdateValue(value.Value, value.Quality, value.SourceTimestamp);
                }
            }
            else
            {
                StatusMessage = $"Failed to write to {tag.Name}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing to tag {TagName}", tag.Name);
            StatusMessage = $"Error writing to {tag.Name}: {ex.Message}";
        }
    }

    private object? ParseValue(string input, Enums.TagDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        try
        {
            return dataType switch
            {
                Enums.TagDataType.Boolean => input.Equals("true", StringComparison.OrdinalIgnoreCase) || input == "1",
                Enums.TagDataType.SByte => sbyte.Parse(input),
                Enums.TagDataType.Byte => byte.Parse(input),
                Enums.TagDataType.Int16 => short.Parse(input),
                Enums.TagDataType.UInt16 => ushort.Parse(input),
                Enums.TagDataType.Int32 => int.Parse(input),
                Enums.TagDataType.UInt32 => uint.Parse(input),
                Enums.TagDataType.Int64 => long.Parse(input),
                Enums.TagDataType.UInt64 => ulong.Parse(input),
                Enums.TagDataType.Float => float.Parse(input),
                Enums.TagDataType.Double => double.Parse(input),
                Enums.TagDataType.String => input,
                Enums.TagDataType.DateTime => DateTime.Parse(input),
                _ => TryParseUnknown(input)
            };
        }
        catch
        {
            return null;
        }
    }

    private object TryParseUnknown(string input)
    {
        if (bool.TryParse(input, out var boolVal)) return boolVal;
        if (int.TryParse(input, out var intVal)) return intVal;
        if (double.TryParse(input, out var dblVal)) return dblVal;
        return input;
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
            // Get the underlying PlcConnection to access Session
            if (connection is not Services.OpcUa.PlcConnection plcConnection || plcConnection.Session == null)
            {
                System.Windows.MessageBox.Show(
                    "Cannot access OPC UA session.",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            StatusMessage = "Opening Browse Server window...";

            // Open Browse Server window
            var browseWindow = new Views.BrowseServerWindow(plcConnection.Session, SelectedPlc);
            browseWindow.Owner = System.Windows.Application.Current.MainWindow;

            var result = browseWindow.ShowDialog();

            if (result == true && browseWindow.AddedTags.Any())
            {
                // Refresh the tag list
                OnPropertyChanged(nameof(SelectedPlcTags));

                StatusMessage = $"Added {browseWindow.AddedTags.Count} tags from server browser";
                _logger.Information("Added {Count} tags from server browser", browseWindow.AddedTags.Count);

                // Mark as unsaved
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            else
            {
                StatusMessage = "Browse server closed";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error browsing server");
            StatusMessage = "Error browsing server";
            System.Windows.MessageBox.Show(
                $"Error opening server browser: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
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

    #region Lock Methods

    /// <summary>
    /// Set the logged in user for the desktop application
    /// </summary>
    public void SetLoggedInUser(User user)
    {
        _loggedInUser = user;
        _currentUserId = user.Id;
        _logger.Information("Logged in user set: {Username} (Role: {Role})", user.Username, user.Role);

        // Update window title to show logged in user
        OnPropertyChanged(nameof(WindowTitle));
    }

    /// <summary>
    /// Set the lock service and subscribe to events
    /// </summary>
    public void SetLockService(OperatorLockService lockService)
    {
        if (_lockService != null)
        {
            // Unsubscribe from old service
            _lockService.LockAcquired -= OnLockAcquired;
            _lockService.LockReleased -= OnLockReleased;
            _lockService.LockExtended -= OnLockExtended;
        }

        _lockService = lockService;

        if (_lockService != null)
        {
            // Subscribe to events
            _lockService.LockAcquired += OnLockAcquired;
            _lockService.LockReleased += OnLockReleased;
            _lockService.LockExtended += OnLockExtended;

            // Update initial state
            UpdateLockState();
        }

        // Notify UI
        OnPropertyChanged(nameof(LockIcon));
        OnPropertyChanged(nameof(LockStatusText));
        OnPropertyChanged(nameof(LockStatusColor));
        OnPropertyChanged(nameof(CanAcquireLock));
        OnPropertyChanged(nameof(HasLock));

        _logger.Information("Lock service configured for UI");
    }

    private void UpdateLockState()
    {
        if (_lockService == null) return;

        var status = _lockService.GetLockStatus();

        if (status.IsLocked)
        {
            _lockHolderName = status.DisplayName ?? status.Username ?? "Unknown";
            HasLock = status.UserId == _currentUserId;
            CanAcquireLock = false;
        }
        else
        {
            _lockHolderName = string.Empty;
            HasLock = false;
            CanAcquireLock = true;
        }
    }

    private void OnLockAcquired(object? sender, LockEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            _lockHolderName = e.Lock.DisplayName ?? e.Lock.Username ?? "Unknown";
            HasLock = e.Lock.UserId == _currentUserId;
            CanAcquireLock = false;

            if (HasLock)
            {
                StatusMessage = "You have acquired operator control";
                _logger.Information("Lock acquired by current user");
            }
            else
            {
                StatusMessage = $"Control acquired by {_lockHolderName}";
                _logger.Information("Lock acquired by {User}", _lockHolderName);
            }
        });
    }

    private void OnLockReleased(object? sender, LockEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var wasOurs = HasLock;
            _lockHolderName = string.Empty;
            HasLock = false;
            CanAcquireLock = true;

            if (wasOurs)
            {
                StatusMessage = "You have released operator control";
            }
            else
            {
                StatusMessage = "Operator control is now available";
            }

            _logger.Information("Lock released");
        });
    }

    private void OnLockExtended(object? sender, LockEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (HasLock)
            {
                StatusMessage = $"Lock extended until {e.Lock.ExpiresAt:HH:mm:ss}";
            }
        });
    }

    private async Task AcquireLockAsync()
    {
        if (_lockService == null)
        {
            StatusMessage = "Lock service not available";
            return;
        }

        try
        {
            StatusMessage = "Acquiring operator control...";

            // For local UI, use Operator role
            var (success, operatorLock, error, _, _) = _lockService.TryAcquireLock(
                _currentUserId, "Local User", "Local User", Enums.UserRole.Operator);

            if (success && operatorLock != null)
            {
                _lockHolderName = "You";
                HasLock = true;
                CanAcquireLock = false;
                StatusMessage = "Operator control acquired";
                _logger.Information("Successfully acquired operator lock");
            }
            else
            {
                StatusMessage = $"Failed to acquire lock: {error}";
                _logger.Warning("Failed to acquire lock: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error acquiring lock: {ex.Message}";
            _logger.Error(ex, "Error acquiring lock");
        }
    }

    private async Task ReleaseLockAsync()
    {
        if (_lockService == null)
        {
            StatusMessage = "Lock service not available";
            return;
        }

        try
        {
            StatusMessage = "Releasing operator control...";

            var (success, error) = _lockService.ReleaseLock(_currentUserId);

            if (success)
            {
                _lockHolderName = string.Empty;
                HasLock = false;
                CanAcquireLock = true;
                StatusMessage = "Operator control released";
                _logger.Information("Successfully released operator lock");
            }
            else
            {
                StatusMessage = $"Failed to release lock: {error}";
                _logger.Warning("Failed to release lock: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error releasing lock: {ex.Message}";
            _logger.Error(ex, "Error releasing lock");
        }
    }

    #endregion
}
