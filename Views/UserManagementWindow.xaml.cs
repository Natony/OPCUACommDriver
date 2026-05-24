using System.Windows;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Admin → Users &amp; Permissions surface.
/// Wire the DataContext to a UserManagementViewModel exposing:
///   ObservableCollection&lt;UserViewModel&gt; Users
///   UserViewModel? SelectedUser
/// and have UserViewModel surface: DisplayName / Login / Initials / AvatarBrush /
/// RoleText / RoleBackBrush / RoleForeBrush / StatusText / StatusBrush /
/// LastLoginText / CreatedBy / Department / EmployeeId / CreatedAtText /
/// IsAdmin / Permissions (each: Title, Description, IsGranted, IsEditable) /
/// RecentAudit (each: Message, Detail, TimeText, LevelBrush).
/// </summary>
public partial class UserManagementWindow : Window
{
    public UserManagementWindow()
    {
        InitializeComponent();
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        // TODO: open AddUserDialog
        MessageBox.Show(this, "Thêm người dùng — TODO", "Junction", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Sửa người dùng — TODO", "Junction", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is null) return;
        var result = MessageBox.Show(this,
            "Đặt lại mật khẩu cho người dùng này? Hệ thống sẽ sinh mật khẩu tạm và gửi qua email.",
            "Đặt lại mật khẩu", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            // TODO: viewModel.ResetPasswordCommand.Execute(viewModel.SelectedUser)
        }
    }

    private void LockUser_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this,
            "Khóa người dùng này? Họ sẽ không thể đăng nhập cho đến khi được mở khóa.",
            "Khóa người dùng", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            // TODO: viewModel.LockUserCommand.Execute(viewModel.SelectedUser)
        }
    }
}
