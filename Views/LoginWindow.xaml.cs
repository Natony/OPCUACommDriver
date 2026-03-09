using System.IO;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.Auth;
using Serilog;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Login window for desktop application
/// </summary>
public partial class LoginWindow : Window
{
    private readonly UserService _userService;
    private readonly string _credentialsPath = "Configurations/saved_credentials.json";
    private static ILogger Logger => Log.Logger;

    public User? LoggedInUser { get; private set; }

    public LoginWindow(UserService userService)
    {
        InitializeComponent();
        _userService = userService;

        // Load saved credentials
        LoadSavedCredentials();

        // Focus username field
        Loaded += (s, e) =>
        {
            if (string.IsNullOrEmpty(UsernameTextBox.Text))
                UsernameTextBox.Focus();
            else
                PasswordBox.Focus();
        };
    }

    private void LoadSavedCredentials()
    {
        try
        {
            if (File.Exists(_credentialsPath))
            {
                var json = File.ReadAllText(_credentialsPath);
                var saved = JsonConvert.DeserializeObject<SavedCredentials>(json);
                if (saved != null && !string.IsNullOrEmpty(saved.Username))
                {
                    UsernameTextBox.Text = saved.Username;
                    RememberMeCheckBox.IsChecked = true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load saved credentials");
        }
    }

    private void SaveCredentials(string username)
    {
        try
        {
            var directory = Path.GetDirectoryName(_credentialsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var saved = new SavedCredentials { Username = username };
            var json = JsonConvert.SerializeObject(saved, Formatting.Indented);
            File.WriteAllText(_credentialsPath, json);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to save credentials");
        }
    }

    private void ClearSavedCredentials()
    {
        try
        {
            if (File.Exists(_credentialsPath))
            {
                File.Delete(_credentialsPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to clear saved credentials");
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformLoginAsync();
    }

    private async Task PerformLoginAsync()
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        // Validation
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter username");
            UsernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter password");
            PasswordBox.Focus();
            return;
        }

        // Show loading
        SetLoading(true);
        HideError();

        try
        {
            // Validate credentials
            await Task.Run(() =>
            {
                LoggedInUser = _userService.ValidateCredentials(username, password);
            });

            if (LoggedInUser != null)
            {
                Logger.Information("User {Username} logged in successfully", username);

                // Save or clear credentials based on checkbox
                if (RememberMeCheckBox.IsChecked == true)
                {
                    SaveCredentials(username);
                }
                else
                {
                    ClearSavedCredentials();
                }

                // Close login window with success
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("Invalid username or password");
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Login error");
            ShowError($"Login failed: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void SetLoading(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        UsernameTextBox.IsEnabled = !isLoading;
        PasswordBox.IsEnabled = !isLoading;
        RememberMeCheckBox.IsEnabled = !isLoading;
        LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PasswordBox.Focus();
        }
    }

    private async void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PerformLoginAsync();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // If user closes without logging in, exit app
        if (DialogResult != true)
        {
            Application.Current.Shutdown();
        }
        base.OnClosing(e);
    }
}

/// <summary>
/// Saved credentials model (only username, never password)
/// </summary>
public class SavedCredentials
{
    public string Username { get; set; } = string.Empty;
}
