using System.Windows;
using OpcUaCommunicationEngine.Services.Auth;
using OpcUaCommunicationEngine.ViewModels;
using Serilog;

namespace OpcUaCommunicationEngine.Views;

public partial class UserManagementWindow : Window
{
    private readonly UserService _userService;
    private readonly UserManagementViewModel _vm;
    private static ILogger Logger => Log.Logger;

    public UserManagementWindow(UserService userService)
    {
        InitializeComponent();
        _userService = userService;
        _vm = new UserManagementViewModel(userService);
        DataContext = _vm;
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EditUserDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _userService.CreateUser(dlg.Username, dlg.Password, dlg.DisplayName, dlg.SelectedRole, dlg.LockDurationMinutes);
            _vm.Reload();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to add user");
            MessageBox.Show(this, $"Không thể tạo người dùng: {ex.Message}", "Lỗi",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedUser == null)
        {
            MessageBox.Show(this, "Vui lòng chọn người dùng để sửa.", "Sửa người dùng",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var user = _vm.SelectedUser.User;
        var dlg = new EditUserDialog(user) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _userService.UpdateUser(user.Id,
                displayName: dlg.DisplayName,
                role: dlg.SelectedRole,
                isActive: dlg.IsActive,
                lockDurationMinutes: dlg.LockDurationMinutes,
                updateLockDuration: true);
            _vm.Reload();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update user");
            MessageBox.Show(this, $"Không thể cập nhật người dùng: {ex.Message}", "Lỗi",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedUser == null)
        {
            MessageBox.Show(this, "Vui lòng chọn người dùng để đặt lại mật khẩu.", "Đặt lại mật khẩu",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var user = _vm.SelectedUser.User;
        var dlg = new ResetPasswordDialog(user.Username) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (_userService.ResetPassword(user.Id, dlg.NewPassword))
            {
                MessageBox.Show(this, $"Đã đặt lại mật khẩu cho {user.Username}.", "Thành công",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(this, "Đặt lại mật khẩu thất bại.", "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to reset password");
            MessageBox.Show(this, $"Lỗi: {ex.Message}", "Lỗi",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LockUser_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedUser == null)
        {
            MessageBox.Show(this, "Vui lòng chọn người dùng để khóa/mở khóa.", "Khóa người dùng",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var user = _vm.SelectedUser.User;
        var newActive = !user.IsActive;
        var action = newActive ? "Mở khóa" : "Khóa";

        var result = MessageBox.Show(this,
            $"{action} người dùng {user.Username}?",
            $"{action} người dùng",
            MessageBoxButton.YesNo,
            newActive ? MessageBoxImage.Question : MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _userService.UpdateUser(user.Id, isActive: newActive);
            _vm.Reload();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to lock/unlock user");
            MessageBox.Show(this, $"Lỗi: {ex.Message}", "Lỗi",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
