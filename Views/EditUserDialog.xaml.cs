using System.Windows;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for adding or editing user details
/// </summary>
public partial class EditUserDialog : Window
{
    private readonly bool _isEditMode;
    private readonly User? _existingUser;

    public string Username => UsernameTextBox.Text.Trim();
    public string Password => PasswordBox.Password;
    public string DisplayName => DisplayNameTextBox.Text.Trim();
    public UserRole SelectedRole => (UserRole)RoleComboBox.SelectedItem;
    public bool IsActive => IsActiveCheckBox.IsChecked == true;

    /// <summary>
    /// Constructor for adding a new user
    /// </summary>
    public EditUserDialog()
    {
        InitializeComponent();
        _isEditMode = false;
        HeaderText.Text = "Add New User";
        RoleComboBox.SelectedItem = UserRole.Operator;
    }

    /// <summary>
    /// Constructor for editing an existing user
    /// </summary>
    public EditUserDialog(User user)
    {
        InitializeComponent();
        _isEditMode = true;
        _existingUser = user;

        HeaderText.Text = "Edit User";

        // Populate fields
        UsernameTextBox.Text = user.Username;
        UsernameTextBox.IsEnabled = false; // Can't change username
        DisplayNameTextBox.Text = user.DisplayName;
        RoleComboBox.SelectedItem = user.Role;
        IsActiveCheckBox.IsChecked = user.IsActive;

        // Hide password field for edit mode
        PasswordLabel.Visibility = Visibility.Collapsed;
        PasswordBox.Visibility = Visibility.Collapsed;

        // Can't change admin role
        if (user.Username == "admin")
        {
            RoleComboBox.IsEnabled = false;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (!_isEditMode)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Username is required", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Password is required", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (Password.Length < 6)
            {
                MessageBox.Show("Password must be at least 6 characters", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
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
