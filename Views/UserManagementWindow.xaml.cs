using System.Collections.ObjectModel;
using System.Windows;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.Auth;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// User Management Window for administering user accounts
/// </summary>
public partial class UserManagementWindow : Window
{
    private readonly UserService _userService;
    private ObservableCollection<UserViewModel> _users = new();

    public UserManagementWindow(UserService userService)
    {
        InitializeComponent();
        _userService = userService;
        LoadUsers();
    }

    private void LoadUsers()
    {
        try
        {
            _users.Clear();
            var users = _userService.GetAllUsers();

            foreach (var user in users)
            {
                _users.Add(new UserViewModel
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    CanDelete = user.Username != "admin" // Can't delete admin
                });
            }

            UsersDataGrid.ItemsSource = _users;
            StatusText.Text = $"{_users.Count} users loaded";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading users: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EditUserDialog();
        dialog.Owner = this;

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var user = _userService.CreateUser(
                    dialog.Username,
                    dialog.Password,
                    dialog.DisplayName,
                    dialog.SelectedRole);

                LoadUsers();
                StatusText.Text = $"User '{dialog.Username}' created successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating user: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is UserViewModel userVm)
        {
            var user = _userService.GetUserById(userVm.Id);
            if (user == null)
            {
                MessageBox.Show("User not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new EditUserDialog(user);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var updatedUser = _userService.UpdateUser(
                        userVm.Id,
                        dialog.DisplayName,
                        dialog.SelectedRole,
                        dialog.IsActive);

                    if (updatedUser != null)
                    {
                        LoadUsers();
                        StatusText.Text = $"User '{userVm.Username}' updated successfully";
                    }
                    else
                    {
                        MessageBox.Show("Failed to update user", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating user: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is UserViewModel userVm)
        {
            var dialog = new ResetPasswordDialog(userVm.Username);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var success = _userService.ResetPassword(userVm.Id, dialog.NewPassword);

                    if (success)
                    {
                        StatusText.Text = $"Password reset for '{userVm.Username}'";
                        MessageBox.Show($"Password has been reset for {userVm.Username}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to reset password", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error resetting password: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is UserViewModel userVm)
        {
            if (userVm.Username == "admin")
            {
                MessageBox.Show("Cannot delete the admin user", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete user '{userVm.Username}'?\nThis action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = _userService.DeleteUser(userVm.Id);

                    if (success)
                    {
                        LoadUsers();
                        StatusText.Text = $"User '{userVm.Username}' deleted";
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete user", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting user: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadUsers();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// ViewModel for displaying users in the DataGrid
/// </summary>
public class UserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool CanDelete { get; set; }
}
