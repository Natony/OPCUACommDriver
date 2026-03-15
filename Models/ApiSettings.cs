namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Settings for API server configuration
/// </summary>
public class ApiSettings
{
    /// <summary>
    /// Port number for the API server (default: 5000)
    /// </summary>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Whether the API server should be enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}
