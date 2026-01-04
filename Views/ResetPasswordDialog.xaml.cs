using System.Windows;

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
        // Validate
        if (string.IsNullOrWhiteSpace(NewPasswordBox.Password))
        {
            MessageBox.Show("New password is required", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            NewPasswordBox.Focus();
            return;
        }

        if (NewPasswordBox.Password.Length < 6)
        {
            MessageBox.Show("Password must be at least 6 characters", "Validation Error",
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
