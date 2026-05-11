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

    private static string GetValue(PasswordBox pb, TextBox tb)
        => pb.Visibility == Visibility.Visible ? pb.Password : tb.Text;

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
