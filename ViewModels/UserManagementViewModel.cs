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

    public UserDisplayItem(User user)
    {
        User = user;
        PopulatePermissions();
        PopulateRecentAudit();
    }

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
    public string Department => User.Role switch
    {
        UserRole.Admin => "Quản trị hệ thống",
        UserRole.Operator => "Vận hành",
        _ => "Giám sát"
    };
    public string EmployeeId => $"AVS-{(Math.Abs(User.Id.GetHashCode()) % 10000):D4}";

    public bool IsAdmin => User.Role == UserRole.Admin;

    public ObservableCollection<PermissionItem> Permissions { get; } = new();
    public ObservableCollection<AuditItem> RecentAudit { get; } = new();

    private void PopulatePermissions()
    {
        // Permissions matrix per role.
        // IsGranted == true && IsEditable == false → role-locked (rendered as yellow pill in the UI).
        var isAdmin    = User.Role == UserRole.Admin;
        var isOperator = User.Role >= UserRole.Operator;

        Permissions.Add(new PermissionItem(
            "Quản lý PLC",
            "Thêm, sửa, xóa PLC trong hệ thống",
            isAdmin, isEditable: !isAdmin));

        Permissions.Add(new PermissionItem(
            "Sửa Tag (Write)",
            "Ghi giá trị xuống PLC qua OPC UA",
            isOperator, isEditable: User.Role != UserRole.Admin));

        Permissions.Add(new PermissionItem(
            "Kết nối / Ngắt PLC",
            "Connect All, Disconnect, Refresh",
            isOperator, isEditable: User.Role != UserRole.Admin));

        Permissions.Add(new PermissionItem(
            "Quản lý người dùng",
            "Thêm / sửa / phân quyền user",
            isAdmin, isEditable: !isAdmin));

        Permissions.Add(new PermissionItem(
            "Xem Logger",
            "Toàn bộ log + xuất file",
            isGranted: true, isEditable: true));

        Permissions.Add(new PermissionItem(
            "Sửa cấu hình",
            "Load / Save plc_config.json",
            isAdmin, isEditable: !isAdmin));
    }

    private void PopulateRecentAudit()
    {
        // Without a real audit log service we surface a small set of representative entries
        // so the panel is informative rather than empty. These reflect typical actions for the role.
        var now = DateTime.Now;

        if (User.LastLoginAt is { } lastLogin)
        {
            RecentAudit.Add(new AuditItem(
                $"Đăng nhập tài khoản {User.Username}",
                $"{lastLogin:dd MMM HH:mm}",
                FormatRelative(lastLogin),
                LevelColor.Info));
        }

        RecentAudit.Add(new AuditItem(
            $"Tài khoản được tạo",
            $"role: {User.Role}",
            FormatRelative(User.CreatedAt),
            LevelColor.Success));

        if (!User.IsActive)
        {
            RecentAudit.Add(new AuditItem(
                "Tài khoản đang bị khóa",
                "Liên hệ admin để mở khóa",
                "—",
                LevelColor.Error));
        }
    }

    private static string FormatRelative(DateTime ts)
    {
        var local = ts.Kind == DateTimeKind.Utc ? ts.ToLocalTime() : ts;
        var span = DateTime.Now - local;
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} ngày trước";
        return local.ToString("dd MMM yyyy");
    }
}

public enum LevelColor { Info, Success, Warning, Error }

public class PermissionItem
{
    public PermissionItem(string title, string description, bool isGranted, bool isEditable)
    {
        Title = title;
        Description = description;
        IsGranted = isGranted;
        IsEditable = isEditable;
    }

    public string Title { get; }
    public string Description { get; }
    public bool IsGranted { get; set; }
    public bool IsEditable { get; }
}

public class AuditItem
{
    public AuditItem(string message, string detail, string timeText, LevelColor level)
    {
        Message = message;
        Detail = detail;
        TimeText = timeText;
        LevelBrush = level switch
        {
            LevelColor.Success => new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)),
            LevelColor.Warning => new SolidColorBrush(Color.FromRgb(0xF5, 0xC6, 0x1C)),
            LevelColor.Error   => new SolidColorBrush(Color.FromRgb(0xC5, 0x0F, 0x1F)),
            _                  => new SolidColorBrush(Color.FromRgb(0x1B, 0x2C, 0x7E))
        };
    }

    public string Message { get; }
    public string Detail { get; }
    public string TimeText { get; }
    public Brush LevelBrush { get; }
}
