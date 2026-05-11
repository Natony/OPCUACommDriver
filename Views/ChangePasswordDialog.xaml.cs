using System.Windows;
using System.Windows.Controls;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Services.Auth;

namespace OpcUaCommunicationEngine.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly UserService _userService;
    private readonly string _userId;
    private readonly string _username;

    public string NewPassword => GetValue(NewPasswordBox, NewPasswordTextBox);

    public ChangePasswordDialog(UserService userService, string userId, string username)
    {
        InitializeComponent();
        _userService = userService;
        _userId = userId;
        _username = username;
        UsernameText.Text = $"for user: {username}";

        CurrentPasswordBox.Focus();
    }

    private static string GetValue(PasswordBox pb, TextBox tb)
        => pb.Visibility == Visibility.Visible ? pb.Password : tb.Text;

    private void ShowCurrentBtn_Changed(object sender, RoutedEventArgs e)
        => TogglePair(CurrentPasswordBox, CurrentPasswordTextBox, ShowCurrentBtn.IsChecked == true);

    private void ShowNewBtn_Changed(object sender, RoutedEventArgs e)
        => TogglePair(NewPasswordBox, NewPasswordTextBox, ShowNewBtn.IsChecked == true);

    private void ShowConfirmBtn_Changed(object sender, RoutedEventArgs e)
        => TogglePair(ConfirmPasswordBox, ConfirmPasswordTextBox, ShowConfirmBtn.IsChecked == true);

    private static void TogglePair(PasswordBox pb, TextBox tb, bool show)
    {
        if (show)
        {
            tb.Text = pb.Password;
            pb.Visibility = Visibility.Collapsed;
            tb.Visibility = Visibility.Visible;
        }
        else
        {
            pb.Password = tb.Text;
            tb.Visibility = Visibility.Collapsed;
            pb.Visibility = Visibility.Visible;
        }
    }

    private void Change_Click(object sender, RoutedEventArgs e)
    {
        HideError();

        var currentPwd = GetValue(CurrentPasswordBox, CurrentPasswordTextBox);
        var newPwd = GetValue(NewPasswordBox, NewPasswordTextBox);
        var confirmPwd = GetValue(ConfirmPasswordBox, ConfirmPasswordTextBox);

        if (string.IsNullOrEmpty(currentPwd))
        {
            ShowError("Please enter your current password");
            CurrentPasswordBox.Focus();
            return;
        }

        var (isValidPassword, passwordError) = PasswordValidator.Validate(newPwd);
        if (!isValidPassword)
        {
            ShowError(passwordError);
            NewPasswordBox.Focus();
            return;
        }

        if (newPwd != confirmPwd)
        {
            ShowError("New passwords do not match");
            ConfirmPasswordBox.Focus();
            return;
        }

        if (currentPwd == newPwd)
        {
            ShowError("New password must be different from current password");
            NewPasswordBox.Focus();
            return;
        }

        var success = _userService.ChangePassword(_userId, currentPwd, newPwd);

        if (success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("Current password is incorrect");
            CurrentPasswordBox.Clear();
            CurrentPasswordTextBox.Text = "";
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
