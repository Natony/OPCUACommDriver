using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.Auth;
using Serilog;

namespace OpcUaCommunicationEngine.Views;

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

        LoadSavedCredentials();

        // Allow dragging the chromeless window from anywhere not already capturing input
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(UserBox.Text))
                UserBox.Focus();
            else
                PasswordBox.Focus();
        };
    }

    private string GetPassword()
        => PasswordBox.Visibility == Visibility.Visible
            ? PasswordBox.Password
            : PasswordTextBox.Text;

    private void ClearPassword()
    {
        PasswordBox.Clear();
        PasswordTextBox.Text = string.Empty;
    }

    private void ShowPwdBtn_Changed(object sender, RoutedEventArgs e)
    {
        bool show = ShowPwdBtn.IsChecked == true;
        if (show)
        {
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordTextBox.Visibility = Visibility.Visible;
        }
        else
        {
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
        }
    }

    private void UserBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (PasswordBox.Visibility == Visibility.Visible) PasswordBox.Focus();
            else PasswordTextBox.Focus();
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SignIn_Click(sender, new RoutedEventArgs());
    }

    private void SignIn_Click(object sender, RoutedEventArgs e)
    {
        HideError();

        var username = UserBox.Text?.Trim() ?? string.Empty;
        var password = GetPassword();

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Vui lòng nhập tên đăng nhập.");
            UserBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Vui lòng nhập mật khẩu.");
            return;
        }

        LoginButton.IsEnabled = false;

        try
        {
            LoggedInUser = _userService.ValidateCredentials(username, password);

            if (LoggedInUser != null)
            {
                Logger.Information("User {Username} logged in successfully", username);

                if (RememberMeCheckBox.IsChecked == true) SaveCredentials(username);
                else ClearSavedCredentials();

                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("Tên đăng nhập hoặc mật khẩu không đúng.");
                ClearPassword();
                PasswordBox.Focus();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Login error");
            ShowError($"Đăng nhập thất bại: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;

    private void LoadSavedCredentials()
    {
        try
        {
            if (!File.Exists(_credentialsPath)) return;
            var json = File.ReadAllText(_credentialsPath);
            var saved = JsonConvert.DeserializeObject<SavedCredentials>(json);
            if (saved != null && !string.IsNullOrEmpty(saved.Username))
            {
                UserBox.Text = saved.Username;
                RememberMeCheckBox.IsChecked = true;
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
            var dir = Path.GetDirectoryName(_credentialsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_credentialsPath,
                JsonConvert.SerializeObject(new SavedCredentials { Username = username }, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to save credentials");
        }
    }

    private void ClearSavedCredentials()
    {
        try { if (File.Exists(_credentialsPath)) File.Delete(_credentialsPath); }
        catch (Exception ex) { Logger.Warning(ex, "Failed to clear saved credentials"); }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

public class SavedCredentials
{
    public string Username { get; set; } = string.Empty;
}
