using System.Text.RegularExpressions;

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
    /// Bind address for the API server (default: 0.0.0.0 = all interfaces)
    /// Can be set to specific IP like 192.168.1.100
    /// </summary>
    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Whether the API server should be enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Get the full URL for API server
    /// </summary>
    public string GetBindUrl() => $"http://{BindAddress}:{Port}";

    /// <summary>
    /// Extract subnet prefix from PLC endpoint URL
    /// Example: opc.tcp://192.168.1.100:4840 -> 192.168.1.
    /// </summary>
    public static string? ExtractSubnetFromPlcEndpoint(string? endpointUrl)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
            return null;

        // Match IP address pattern in the URL
        var match = Regex.Match(endpointUrl, @"(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}");
        if (match.Success)
        {
            return match.Groups[1].Value + ".";
        }

        return null;
    }

    /// <summary>
    /// Suggest a default API bind address based on PLC subnet
    /// If PLC is 192.168.1.100, suggests 192.168.1.1 (common gateway/host address)
    /// </summary>
    public static string SuggestBindAddress(string? plcEndpointUrl, string defaultAddress = "0.0.0.0")
    {
        var subnet = ExtractSubnetFromPlcEndpoint(plcEndpointUrl);
        if (subnet != null)
        {
            // Return subnet with placeholder for user to see the pattern
            return subnet;
        }
        return defaultAddress;
    }

    /// <summary>
    /// Check if the bind address is valid
    /// </summary>
    public static bool IsValidBindAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        // Allow 0.0.0.0 or localhost
        if (address == "0.0.0.0" || address == "localhost" || address == "127.0.0.1")
            return true;

        // Validate IP address format
        return Regex.IsMatch(address, @"^(\d{1,3}\.){3}\d{1,3}$") &&
               address.Split('.').All(part => int.TryParse(part, out var num) && num >= 0 && num <= 255);
    }
}
