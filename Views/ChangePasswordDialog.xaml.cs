using System.Windows;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Services.Auth;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for changing password with old password verification
/// </summary>
public partial class ChangePasswordDialog : Window
{
    private readonly UserService _userService;
    private readonly string _userId;
    private readonly string _username;

    public string NewPassword => NewPasswordBox.Password;

    public ChangePasswordDialog(UserService userService, string userId, string username)
    {
        InitializeComponent();
        _userService = userService;
        _userId = userId;
        _username = username;
        UsernameText.Text = $"for user: {username}";

        CurrentPasswordBox.Focus();
    }

    private void Change_Click(object sender, RoutedEventArgs e)
    {
        HideError();

        // Validate current password is not empty
        if (string.IsNullOrEmpty(CurrentPasswordBox.Password))
        {
            ShowError("Please enter your current password");
            CurrentPasswordBox.Focus();
            return;
        }

        // Validate new password strength
        var (isValidPassword, passwordError) = PasswordValidator.Validate(NewPasswordBox.Password);
        if (!isValidPassword)
        {
            ShowError(passwordError);
            NewPasswordBox.Focus();
            return;
        }

        // Check passwords match
        if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
        {
            ShowError("New passwords do not match");
            ConfirmPasswordBox.Focus();
            return;
        }

        // Check new password is different from old
        if (CurrentPasswordBox.Password == NewPasswordBox.Password)
        {
            ShowError("New password must be different from current password");
            NewPasswordBox.Focus();
            return;
        }

        // Verify old password and change
        var success = _userService.ChangePassword(_userId, CurrentPasswordBox.Password, NewPasswordBox.Password);

        if (success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("Current password is incorrect");
            CurrentPasswordBox.Clear();
            CurrentPasswordBox.Focus();
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
