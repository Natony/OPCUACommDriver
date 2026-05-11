using System.Text.RegularExpressions;

namespace OpcUaCommunicationEngine.Helpers;

/// <summary>
/// Password validation helper
/// </summary>
public static class PasswordValidator
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    /// <summary>
    /// Validate password strength
    /// </summary>
    /// <param name="password">Password to validate</param>
    /// <returns>Tuple of (isValid, errorMessage)</returns>
    public static (bool isValid, string? error) Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password is required");
        }

        if (password.Length < MinLength)
        {
            return (false, $"Password must be at least {MinLength} characters");
        }

        if (password.Length > MaxLength)
        {
            return (false, $"Password must not exceed {MaxLength} characters");
        }

        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            return (false, "Password must contain at least one uppercase letter");
        }

        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            return (false, "Password must contain at least one lowercase letter");
        }

        if (!Regex.IsMatch(password, @"[0-9]"))
        {
            return (false, "Password must contain at least one digit");
        }

        return (true, null);
    }

    /// <summary>
    /// Get password requirements as a user-friendly string
    /// </summary>
    public static string GetRequirements()
    {
        return $"Password must be {MinLength}-{MaxLength} characters with at least one uppercase, one lowercase, and one digit";
    }
}
