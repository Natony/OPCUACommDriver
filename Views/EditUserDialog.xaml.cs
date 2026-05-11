using System.Windows;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

public partial class EditUserDialog : Window
{
    private readonly bool _isEditMode;
    private readonly User? _existingUser;

    public string Username => UsernameTextBox.Text.Trim();

    public string Password => ShowPasswordCheckBox?.IsChecked == true
        ? PasswordTextBox.Text
        : PasswordBox.Password;

    public string ConfirmPassword => ShowPasswordCheckBox?.IsChecked == true
        ? ConfirmPasswordTextBox.Text
        : ConfirmPasswordBox.Password;

    public string DisplayName => DisplayNameTextBox.Text.Trim();
    public UserRole SelectedRole => (UserRole)RoleComboBox.SelectedItem;
    public bool IsActive => IsActiveCheckBox.IsChecked == true;
    public int? LockDurationMinutes
    {
        get
        {
            var text = LockDurationTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            return int.TryParse(text, out var val) ? val : null;
        }
    }

    public EditUserDialog()
    {
        InitializeComponent();
        _isEditMode = false;
        HeaderText.Text = "Add New User";
        RoleComboBox.SelectedItem = UserRole.Operator;
    }

    public EditUserDialog(User user)
    {
        InitializeComponent();
        _isEditMode = true;
        _existingUser = user;

        HeaderText.Text = "Edit User";

        UsernameTextBox.Text = user.Username;
        UsernameTextBox.IsEnabled = false;
        DisplayNameTextBox.Text = user.DisplayName;
        RoleComboBox.SelectedItem = user.Role;
        IsActiveCheckBox.IsChecked = user.IsActive;
        LockDurationTextBox.Text = user.LockDurationMinutes?.ToString() ?? "";

        // Hide password fields in edit mode
        PasswordLabel.Visibility = Visibility.Collapsed;
        PasswordFieldGrid.Visibility = Visibility.Collapsed;
        ConfirmPasswordLabel.Visibility = Visibility.Collapsed;
        ConfirmPasswordFieldGrid.Visibility = Visibility.Collapsed;
        ShowPasswordCheckBox.Visibility = Visibility.Collapsed;

        if (user.Username == "admin")
            RoleComboBox.IsEnabled = false;
    }

    private void ShowPassword_Changed(object sender, RoutedEventArgs e)
    {
        bool show = ShowPasswordCheckBox.IsChecked == true;
        TogglePasswordPair(PasswordBox, PasswordTextBox, show);
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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditMode)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Username is required", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return;
            }

            var (isValidPassword, passwordError) = PasswordValidator.Validate(Password);
            if (!isValidPassword)
            {
                MessageBox.Show(passwordError, "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (Password != ConfirmPassword)
            {
                MessageBox.Show("Passwords do not match", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ConfirmPasswordBox.Focus();
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            MessageBox.Show("Display name is required", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            DisplayNameTextBox.Focus();
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
