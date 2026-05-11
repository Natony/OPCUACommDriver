using System.Windows;
using System.Windows.Controls;
using OpcUaCommunicationEngine.Helpers;

namespace OpcUaCommunicationEngine.Views;

public partial class ResetPasswordDialog : Window
{
    public string NewPassword => GetValue(NewPasswordBox, NewPasswordTextBox);

    public ResetPasswordDialog(string username)
    {
        InitializeComponent();
        UsernameText.Text = $"for user: {username}";
    }

    private string GetValue(PasswordBox pb, TextBox tb)
        => ShowPasswordCheckBox?.IsChecked == true ? tb.Text : pb.Password;

    private void ShowPassword_Changed(object sender, RoutedEventArgs e)
    {
        bool show = ShowPasswordCheckBox.IsChecked == true;
        TogglePasswordPair(NewPasswordBox, NewPasswordTextBox, show);
        TogglePasswordPair(ConfirmPasswordBox, ConfirmPasswordTextBox, show);
    }

    private static void TogglePasswordPair(PasswordBox pb, TextBox tb, bool show)
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

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var newPwd = GetValue(NewPasswordBox, NewPasswordTextBox);
        var confirmPwd = GetValue(ConfirmPasswordBox, ConfirmPasswordTextBox);

        var (isValidPassword, passwordError) = PasswordValidator.Validate(newPwd);
        if (!isValidPassword)
        {
            MessageBox.Show(passwordError, "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NewPasswordBox.Focus();
            return;
        }

        if (newPwd != confirmPwd)
        {
            MessageBox.Show("Passwords do not match", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            ConfirmPasswordBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
