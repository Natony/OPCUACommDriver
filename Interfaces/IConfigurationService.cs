using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Interfaces;

/// <summary>
/// Interface cho Configuration Service
/// Quản lý việc load/save cấu hình PLC và Tags
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Event khi configuration thay đổi
    /// </summary>
    event EventHandler<AppConfiguration>? ConfigurationChanged;

    /// <summary>
    /// Configuration hiện tại
    /// </summary>
    AppConfiguration CurrentConfiguration { get; }

    /// <summary>
    /// Load configuration từ file
    /// </summary>
    Task<AppConfiguration> LoadConfigurationAsync(string? filePath = null);

    /// <summary>
    /// Save configuration ra file
    /// </summary>
    Task<bool> SaveConfigurationAsync(string? filePath = null);

    /// <summary>
    /// Save configuration ra file khác (Save As)
    /// </summary>
    Task<bool> SaveConfigurationAsAsync(string filePath);

    /// <summary>
    /// Kiểm tra có thay đổi chưa save không
    /// </summary>
    bool HasUnsavedChanges { get; }

    /// <summary>
    /// Đường dẫn file configuration hiện tại
    /// </summary>
    string? CurrentFilePath { get; }

    /// <summary>
    /// Đánh dấu đã có thay đổi
    /// </summary>
    void MarkAsModified();

    /// <summary>
    /// Reset về configuration mặc định
    /// </summary>
    void ResetToDefault();

    /// <summary>
    /// Validate configuration
    /// </summary>
    ValidationResult ValidateConfiguration(AppConfiguration config);

    /// <summary>
    /// Export configuration (để backup)
    /// </summary>
    Task<string> ExportConfigurationAsync();

    /// <summary>
    /// Import configuration (từ backup)
    /// </summary>
    Task<AppConfiguration> ImportConfigurationAsync(string json);
}

/// <summary>
/// Kết quả validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult 
        { 
            IsValid = false, 
            Errors = errors.ToList() 
        };
    }
}
