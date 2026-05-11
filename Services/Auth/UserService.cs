using System.IO;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.Auth;

/// <summary>
/// Service for user management (CRUD operations)
/// </summary>
public class UserService
{
    private readonly string _usersFilePath;
    private readonly object _lock = new();
    private UserStorage _storage;
    private static ILogger Logger => Log.Logger;

    public UserService(AuthSettings settings)
    {
        _usersFilePath = settings.UsersFilePath;
        _storage = LoadOrCreateStorage();
    }

    #region Public Methods

    /// <summary>
    /// Get all users
    /// </summary>
    public IReadOnlyList<User> GetAllUsers()
    {
        lock (_lock)
        {
            return _storage.Users.ToList();
        }
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    public User? GetUserById(string userId)
    {
        lock (_lock)
        {
            return _storage.Users.FirstOrDefault(u => u.Id == userId);
        }
    }

    /// <summary>
    /// Get user by username
    /// </summary>
    public User? GetUserByUsername(string username)
    {
        lock (_lock)
        {
            return _storage.Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Create new user
    /// </summary>
    public User CreateUser(string username, string password, string displayName, UserRole role, int? lockDurationMinutes = null)
    {
        lock (_lock)
        {
            // Check if username already exists
            if (_storage.Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Username '{username}' already exists");
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username.ToLowerInvariant(),
                PasswordHash = HashPassword(password),
                DisplayName = displayName,
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LockDurationMinutes = lockDurationMinutes
            };

            _storage.Users.Add(user);
            SaveStorage();

            Logger.Information("Created user: {Username} with role {Role}, lock duration: {LockDuration}",
                username, role, lockDurationMinutes?.ToString() ?? "default");
            return user;
        }
    }

    /// <summary>
    /// Update user
    /// </summary>
    public User? UpdateUser(string userId, string? displayName = null, UserRole? role = null, bool? isActive = null, int? lockDurationMinutes = null, bool updateLockDuration = false)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return null;

            if (displayName != null)
                user.DisplayName = displayName;

            if (role.HasValue)
                user.Role = role.Value;

            if (isActive.HasValue)
                user.IsActive = isActive.Value;

            if (updateLockDuration)
                user.LockDurationMinutes = lockDurationMinutes;

            SaveStorage();

            Logger.Information("Updated user: {Username}", user.Username);
            return user;
        }
    }

    /// <summary>
    /// Delete user
    /// </summary>
    public bool DeleteUser(string userId)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            // Prevent deleting last admin
            if (user.Role == UserRole.Admin)
            {
                var adminCount = _storage.Users.Count(u => u.Role == UserRole.Admin && u.IsActive);
                if (adminCount <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last admin user");
                }
            }

            _storage.Users.Remove(user);
            SaveStorage();

            Logger.Information("Deleted user: {Username}", user.Username);
            return true;
        }
    }

    /// <summary>
    /// Validate user credentials
    /// </summary>
    public User? ValidateCredentials(string username, string password)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.IsActive);

            if (user == null) return null;

            if (VerifyPassword(password, user.PasswordHash))
            {
                user.LastLoginAt = DateTime.UtcNow;
                SaveStorage();
                return user;
            }

            return null;
        }
    }

    /// <summary>
    /// Reset user password (admin operation)
    /// </summary>
    public bool ResetPassword(string userId, string newPassword)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            user.PasswordHash = HashPassword(newPassword);
            SaveStorage();

            Logger.Information("Password reset for user: {Username}", user.Username);
            return true;
        }
    }

    /// <summary>
    /// Change password (user operation - requires current password)
    /// </summary>
    public bool ChangePassword(string userId, string currentPassword, string newPassword)
    {
        lock (_lock)
        {
            var user = _storage.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            if (!VerifyPassword(currentPassword, user.PasswordHash))
            {
                return false;
            }

            user.PasswordHash = HashPassword(newPassword);
            SaveStorage();

            Logger.Information("Password changed for user: {Username}", user.Username);
            return true;
        }
    }

    #endregion

    #region Private Methods

    private UserStorage LoadOrCreateStorage()
    {
        try
        {
            if (File.Exists(_usersFilePath))
            {
                var json = File.ReadAllText(_usersFilePath);
                var storage = JsonConvert.DeserializeObject<UserStorage>(json);
                if (storage != null && storage.Users.Any())
                {
                    Logger.Information("Loaded {Count} users from storage", storage.Users.Count);
                    return storage;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading users storage, creating default");
        }

        // Create default storage with admin user
        var defaultStorage = CreateDefaultStorage();
        SaveStorageInternal(defaultStorage);
        return defaultStorage;
    }

    private UserStorage CreateDefaultStorage()
    {
        var storage = new UserStorage();

        // Create default admin user
        storage.Users.Add(new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "admin",
            PasswordHash = HashPassword("admin123"),
            DisplayName = "Administrator",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        Logger.Information("Created default admin user (username: admin, password: admin123)");
        return storage;
    }

    private void SaveStorage()
    {
        SaveStorageInternal(_storage);
    }

    private void SaveStorageInternal(UserStorage storage)
    {
        try
        {
            var directory = Path.GetDirectoryName(_usersFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(storage, Formatting.Indented);
            File.WriteAllText(_usersFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving users storage");
        }
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
