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
    /// Suggest a default API bind address based on single PLC subnet
    /// If PLC is 192.168.1.100, suggests 192.168.1. prefix
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
    /// Suggest API bind address based on multiple PLCs
    /// - If all PLCs are on same subnet -> suggest that subnet
    /// - If PLCs are on different subnets -> use 0.0.0.0 (all interfaces)
    /// - If no PLCs -> return default
    /// </summary>
    public static ApiBindSuggestion SuggestBindAddressFromMultiplePlcs(IEnumerable<string?> plcEndpointUrls, string defaultAddress = "0.0.0.0")
    {
        var endpoints = plcEndpointUrls?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();

        if (endpoints == null || endpoints.Count == 0)
        {
            return new ApiBindSuggestion
            {
                SuggestedAddress = defaultAddress,
                Reason = "Chưa có PLC nào được cấu hình",
                CanConfigure = false
            };
        }

        // Extract all subnets
        var subnets = endpoints
            .Select(ExtractSubnetFromPlcEndpoint)
            .Where(s => s != null)
            .Distinct()
            .ToList();

        if (subnets.Count == 0)
        {
            return new ApiBindSuggestion
            {
                SuggestedAddress = defaultAddress,
                Reason = "Không thể xác định subnet từ địa chỉ PLC",
                CanConfigure = true
            };
        }

        if (subnets.Count == 1)
        {
            // All PLCs on same subnet
            return new ApiBindSuggestion
            {
                SuggestedAddress = subnets[0]!,
                Reason = $"Tất cả {endpoints.Count} PLC đều trên subnet {subnets[0]}",
                SubnetPrefix = subnets[0],
                CanConfigure = true,
                IsSameSubnet = true
            };
        }

        // Multiple subnets - need to bind to all interfaces
        return new ApiBindSuggestion
        {
            SuggestedAddress = "0.0.0.0",
            Reason = $"Có {subnets.Count} subnet khác nhau ({string.Join(", ", subnets.Select(s => s + "x"))}). Sử dụng 0.0.0.0 để lắng nghe tất cả.",
            CanConfigure = true,
            IsSameSubnet = false,
            DetectedSubnets = subnets!
        };
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

/// <summary>
/// Result of API bind address suggestion
/// </summary>
public class ApiBindSuggestion
{
    /// <summary>
    /// Suggested bind address (e.g., "192.168.1." or "0.0.0.0")
    /// </summary>
    public string SuggestedAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Human-readable reason for the suggestion
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Subnet prefix if all PLCs are on same subnet (e.g., "192.168.1.")
    /// </summary>
    public string? SubnetPrefix { get; set; }

    /// <summary>
    /// Whether the API address can be configured (requires at least one PLC)
    /// </summary>
    public bool CanConfigure { get; set; }

    /// <summary>
    /// Whether all PLCs are on the same subnet
    /// </summary>
    public bool IsSameSubnet { get; set; }

    /// <summary>
    /// List of detected subnets when PLCs are on different networks
    /// </summary>
    public List<string> DetectedSubnets { get; set; } = new();
}
