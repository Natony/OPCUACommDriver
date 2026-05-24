using System.Windows;
using System.Windows.Input;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Sign-in surface. Minimal code-behind — replace the placeholder
/// auth call with your real IAuthService.SignInAsync(...) when wiring DI.
/// </summary>
public partial class LoginWindow : Window
{
    public bool IsAuthenticated { get; private set; }
    public string? SignedInUser { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        Loaded += (_, _) => PasswordBox.Focus();
    }

    private void SignIn_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var user = UserBox.Text?.Trim() ?? string.Empty;
        var pass = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            ErrorText.Text = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        // TODO: replace with real IAuthService call:
        //   var result = await _authService.SignInAsync(user, pass);
        //   if (!result.Success) { ErrorText.Text = result.Error; … return; }
        // For now we accept any non-empty credentials.

        SignedInUser = user;
        IsAuthenticated = true;
        DialogResult = true;
        Close();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
