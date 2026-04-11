using System.IO;
using System.Net;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services;

/// <summary>
/// Service quản lý Configuration
/// Load/Save configuration từ/ra file JSON
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger _logger;
    private readonly string _defaultConfigPath;
    private AppConfiguration _currentConfiguration;
    private string? _currentFilePath;
    private bool _hasUnsavedChanges;

    public event EventHandler<AppConfiguration>? ConfigurationChanged;

    public AppConfiguration CurrentConfiguration => _currentConfiguration;
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public ConfigurationService(ILogger logger, string? defaultConfigPath = null)
    {
        _logger = logger;
        _defaultConfigPath = defaultConfigPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Configurations", 
            "plc_config.json");
        
        _currentConfiguration = AppConfiguration.CreateDefault();
        _logger.Information("ConfigurationService initialized. Default path: {Path}", _defaultConfigPath);
    }

    /// <summary>
    /// Load configuration từ file
    /// </summary>
    public async Task<AppConfiguration> LoadConfigurationAsync(string? filePath = null)
    {
        var path = filePath ?? _defaultConfigPath;
        
        try
        {
            _logger.Information("Loading configuration from {Path}", path);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _logger.Information("Creating directory: {Dir}", directory);
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(path))
            {
                _logger.Warning("Configuration file not found at {Path}, creating empty config", path);
                _currentConfiguration = AppConfiguration.CreateDefault();
                // Không thêm Sample PLC - để trống
                await SaveConfigurationAsync(path);
                
                ConfigurationChanged?.Invoke(this, _currentConfiguration);
                return _currentConfiguration;
            }

            var json = await File.ReadAllTextAsync(path);
            var config = JsonConvert.DeserializeObject<AppConfiguration>(json, GetJsonSettings());

            if (config == null)
            {
                _logger.Error("Failed to deserialize configuration from {Path}", path);
                _currentConfiguration = AppConfiguration.CreateDefault();
            }
            else
            {
                _currentConfiguration = config;
            }

            _currentFilePath = path;
            _hasUnsavedChanges = false;

            _logger.Information("Loaded configuration with {PlcCount} PLCs", 
                _currentConfiguration.PlcDevices.Count);

            ConfigurationChanged?.Invoke(this, _currentConfiguration);
            return _currentConfiguration;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading configuration from {Path}", path);
            
            // Return default config on error
            _currentConfiguration = AppConfiguration.CreateDefault();
            ConfigurationChanged?.Invoke(this, _currentConfiguration);
            return _currentConfiguration;
        }
    }

    /// <summary>
    /// Save configuration ra file
    /// </summary>
    public async Task<bool> SaveConfigurationAsync(string? filePath = null)
    {
        var path = filePath ?? _currentFilePath ?? _defaultConfigPath;

        try
        {
            _logger.Information("Saving configuration to {Path}", path);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Update metadata
            _currentConfiguration.MarkModified();

            // Serialize with formatting
            var json = JsonConvert.SerializeObject(_currentConfiguration, GetJsonSettings());
            
            // Write to file
            await File.WriteAllTextAsync(path, json);
            
            _currentFilePath = path;
            _hasUnsavedChanges = false;

            _logger.Information("Configuration saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving configuration to {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Save As - lưu ra file khác
    /// </summary>
    public async Task<bool> SaveConfigurationAsAsync(string filePath)
    {
        return await SaveConfigurationAsync(filePath);
    }

    /// <summary>
    /// Đánh dấu đã có thay đổi
    /// </summary>
    public void MarkAsModified()
    {
        _hasUnsavedChanges = true;
        _currentConfiguration.MarkModified();
    }

    /// <summary>
    /// Reset về configuration mặc định
    /// </summary>
    public void ResetToDefault()
    {
        _currentConfiguration = AppConfiguration.CreateDefault();
        _hasUnsavedChanges = true;
        ConfigurationChanged?.Invoke(this, _currentConfiguration);
        _logger.Information("Configuration reset to default");
    }

    /// <summary>
    /// Validate configuration
    /// </summary>
    public ValidationResult ValidateConfiguration(AppConfiguration config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate PLC devices
        foreach (var plc in config.PlcDevices)
        {
            if (string.IsNullOrWhiteSpace(plc.Name))
                errors.Add($"PLC {plc.Id}: Name is required");

            // Validate theo protocol type
            if (plc.IsModbus)
            {
                // Validate Modbus connection
                if (string.IsNullOrWhiteSpace(plc.IpAddress))
                    errors.Add($"PLC {plc.Name}: IP Address is required for Modbus TCP");
                else if (!IPAddress.TryParse(plc.IpAddress, out _))
                    errors.Add($"PLC {plc.Name}: Invalid IP Address format '{plc.IpAddress}'");

                if (plc.Port < 1 || plc.Port > 65535)
                    errors.Add($"PLC {plc.Name}: Port must be between 1 and 65535");
            }
            else
            {
                // Validate OPC UA connection
                if (string.IsNullOrWhiteSpace(plc.EndpointUrl))
                    errors.Add($"PLC {plc.Name}: Endpoint URL is required");
                else if (!IsValidOpcUaUrl(plc.EndpointUrl))
                    errors.Add($"PLC {plc.Name}: Invalid OPC UA endpoint URL format");
            }

            var duplicateNames = config.PlcDevices
                .Where(p => p.Name == plc.Name && p.Id != plc.Id)
                .ToList();
            if (duplicateNames.Any())
                warnings.Add($"PLC '{plc.Name}' has duplicate names");

            foreach (var tag in plc.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag.Name))
                    errors.Add($"PLC {plc.Name}: Tag {tag.Id} has no name");

                if (string.IsNullOrWhiteSpace(tag.NodeId))
                    errors.Add($"PLC {plc.Name}: Tag '{tag.Name}' has no NodeId");

                if (tag.ScanRate < 10)
                    warnings.Add($"PLC {plc.Name}: Tag '{tag.Name}' scan rate is very low ({tag.ScanRate}ms)");

                // Validate Modbus register address
                if (plc.IsModbus && !string.IsNullOrWhiteSpace(tag.NodeId))
                {
                    if (!ushort.TryParse(tag.NodeId, out _))
                        warnings.Add($"PLC {plc.Name}: Tag '{tag.Name}' NodeId '{tag.NodeId}' may not be a valid Modbus register address");
                }
            }

            foreach (var group in plc.SubscriptionGroups)
            {
                if (group.PublishingInterval < 10)
                    warnings.Add($"PLC {plc.Name}: Subscription '{group.Name}' has very low interval ({group.PublishingInterval}ms)");
            }
        }

        if (config.Settings.MaxConcurrentConnections < 1)
            errors.Add("Max concurrent connections must be at least 1");

        if (config.Settings.DefaultSessionTimeout < 1000)
            warnings.Add("Session timeout is very low, may cause connection issues");

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Export configuration thành JSON string
    /// </summary>
    public async Task<string> ExportConfigurationAsync()
    {
        return await Task.FromResult(
            JsonConvert.SerializeObject(_currentConfiguration, GetJsonSettings()));
    }

    /// <summary>
    /// Import configuration từ JSON string
    /// </summary>
    public async Task<AppConfiguration> ImportConfigurationAsync(string json)
    {
        try
        {
            var config = JsonConvert.DeserializeObject<AppConfiguration>(json, GetJsonSettings());
            if (config == null)
                throw new InvalidOperationException("Invalid configuration JSON");

            var validation = ValidateConfiguration(config);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Configuration validation failed: {string.Join(", ", validation.Errors)}");
            }

            _currentConfiguration = config;
            _hasUnsavedChanges = true;
            ConfigurationChanged?.Invoke(this, _currentConfiguration);

            return await Task.FromResult(_currentConfiguration);
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Invalid JSON format for configuration import");
            throw new InvalidOperationException("Invalid JSON format", ex);
        }
    }

    #region Helper Methods

    private static JsonSerializerSettings GetJsonSettings()
    {
        return new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            DateFormatString = "yyyy-MM-dd HH:mm:ss"
        };
    }

    private static bool IsValidOpcUaUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return url.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region PLC Management Methods

    public void AddPlc(PlcDevice plc)
    {
        if (_currentConfiguration.PlcDevices.Any(p => p.Id == plc.Id))
            throw new InvalidOperationException($"PLC with ID {plc.Id} already exists");

        _currentConfiguration.PlcDevices.Add(plc);
        MarkAsModified();
        _logger.Information("Added PLC {Name} ({Id})", plc.Name, plc.Id);
    }

    public void RemovePlc(string plcId)
    {
        var plc = _currentConfiguration.PlcDevices.FirstOrDefault(p => p.Id == plcId);
        if (plc != null)
        {
            _currentConfiguration.PlcDevices.Remove(plc);
            MarkAsModified();
            _logger.Information("Removed PLC {Name} ({Id})", plc.Name, plcId);
        }
    }

    public void UpdatePlc(PlcDevice updatedPlc)
    {
        var index = -1;
        for (int i = 0; i < _currentConfiguration.PlcDevices.Count; i++)
        {
            if (_currentConfiguration.PlcDevices[i].Id == updatedPlc.Id)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            _currentConfiguration.PlcDevices[index] = updatedPlc;
            MarkAsModified();
            _logger.Information("Updated PLC {Name} ({Id})", updatedPlc.Name, updatedPlc.Id);
        }
    }

    public PlcDevice? GetPlc(string plcId)
    {
        return _currentConfiguration.PlcDevices.FirstOrDefault(p => p.Id == plcId);
    }

    #endregion

    #region Tag Management Methods

    public void AddTag(string plcId, TagItem tag)
    {
        var plc = GetPlc(plcId);
        if (plc == null)
            throw new InvalidOperationException($"PLC {plcId} not found");

        if (plc.Tags.Any(t => t.Id == tag.Id))
            throw new InvalidOperationException($"Tag with ID {tag.Id} already exists");

        tag.PlcId = plcId;
        plc.Tags.Add(tag);
        MarkAsModified();
        _logger.Information("Added Tag {Name} to PLC {PlcName}", tag.Name, plc.Name);
    }

    public void RemoveTag(string plcId, string tagId)
    {
        var plc = GetPlc(plcId);
        if (plc == null) return;

        var tag = plc.Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag != null)
        {
            plc.Tags.Remove(tag);
            MarkAsModified();
            _logger.Information("Removed Tag {Name} from PLC {PlcName}", tag.Name, plc.Name);
        }
    }

    #endregion
}
