using System.Windows;
using OpcUaCommunicationEngine.Helpers;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for resetting a user's password
/// </summary>
public partial class ResetPasswordDialog : Window
{
    public string NewPassword => NewPasswordBox.Password;

    public ResetPasswordDialog(string username)
    {
        InitializeComponent();
        UsernameText.Text = $"for user: {username}";
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        // Validate password strength
        var (isValidPassword, passwordError) = PasswordValidator.Validate(NewPasswordBox.Password);
        if (!isValidPassword)
        {
            MessageBox.Show(passwordError, "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NewPasswordBox.Focus();
            return;
        }

        if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
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
