namespace OpcUaCommunicationEngine.Enums;

/// <summary>
/// User roles for authorization
/// </summary>
public enum UserRole
{
    /// <summary>
    /// View only - can read tags but cannot write or acquire lock
    /// </summary>
    Viewer = 0,

    /// <summary>
    /// Operator - can acquire lock and write tags
    /// </summary>
    Operator = 1,

    /// <summary>
    /// Administrator - full access including user management and force release lock
    /// </summary>
    Admin = 2
}
