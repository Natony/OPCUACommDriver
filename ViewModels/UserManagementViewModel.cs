using System.Collections.ObjectModel;
using System.Windows.Media;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.Auth;

namespace OpcUaCommunicationEngine.ViewModels;

public class UserManagementViewModel : Models.ObservableObject
{
    private readonly UserService _userService;
    private UserDisplayItem? _selectedUser;

    public ObservableCollection<UserDisplayItem> Users { get; } = new();

    public UserDisplayItem? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public UserManagementViewModel(UserService userService)
    {
        _userService = userService;
        Reload();
    }

    public void Reload()
    {
        var selectedId = SelectedUser?.User.Id;
        Users.Clear();
        foreach (var user in _userService.GetAllUsers())
        {
            Users.Add(new UserDisplayItem(user));
        }
        if (selectedId != null)
        {
            SelectedUser = Users.FirstOrDefault(u => u.User.Id == selectedId) ?? Users.FirstOrDefault();
        }
        else
        {
            SelectedUser = Users.FirstOrDefault();
        }
    }
}

public class UserDisplayItem : Models.ObservableObject
{
    public User User { get; }

    public UserDisplayItem(User user) { User = user; }

    public string DisplayName => string.IsNullOrWhiteSpace(User.DisplayName) ? User.Username : User.DisplayName;
    public string Login => User.Username;
    public string Initials
    {
        get
        {
            var src = DisplayName.Trim();
            if (string.IsNullOrEmpty(src)) return "?";
            var parts = src.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
            return src.Length >= 2 ? src.Substring(0, 2).ToUpper() : src.ToUpper();
        }
    }

    public Brush AvatarBrush => User.Role switch
    {
        UserRole.Admin => new SolidColorBrush(Color.FromRgb(0x1B, 0x2C, 0x7E)),
        UserRole.Operator => new SolidColorBrush(Color.FromRgb(0xC7, 0x77, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
    };

    public string RoleText => User.Role.ToString().ToUpperInvariant();
    public Brush RoleBackBrush => User.Role switch
    {
        UserRole.Admin => new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xF4)),
        UserRole.Operator => new SolidColorBrush(Color.FromRgb(0xFC, 0xEE, 0xD4)),
        _ => new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6))
    };
    public Brush RoleForeBrush => User.Role switch
    {
        UserRole.Admin => new SolidColorBrush(Color.FromRgb(0x1B, 0x2C, 0x7E)),
        UserRole.Operator => new SolidColorBrush(Color.FromRgb(0xC7, 0x77, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63))
    };

    public string StatusText => User.IsActive ? "Đang hoạt động" : "Đã khóa";
    public Brush StatusBrush => User.IsActive
        ? new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10))
        : new SolidColorBrush(Color.FromRgb(0xC5, 0x0F, 0x1F));

    public string LastLoginText => User.LastLoginAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";
    public string CreatedAtText => User.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd");
    public string CreatedBy => "system";
    public string Department => "—";
    public string EmployeeId => "—";

    public bool IsAdmin => User.Role == UserRole.Admin;

    public ObservableCollection<object> Permissions { get; } = new();
    public ObservableCollection<object> RecentAudit { get; } = new();
}
