using System.Windows;
using Microsoft.Win32;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for editing PLC properties
/// </summary>
public partial class EditPlcDialog : Window
{
    private readonly PlcDevice _plcDevice;
    private readonly PlcDevice _originalDevice;

    public PlcDevice PlcDevice => _plcDevice;

    public EditPlcDialog(PlcDevice plcDevice)
    {
        InitializeComponent();

        _originalDevice = plcDevice;
        _plcDevice = plcDevice.Clone();

        // Set DataContext to the cloned device for binding
        DataContext = _plcDevice;

        // Load enum values for ComboBoxes
        CmbSecurityPolicy.ItemsSource = Enum.GetValues<OpcUaSecurityPolicy>();
        CmbSecurityPolicy.SelectedItem = _plcDevice.SecurityPolicy;

        CmbSecurityMode.ItemsSource = Enum.GetValues<OpcUaSecurityMode>();
        CmbSecurityMode.SelectedItem = _plcDevice.SecurityMode;

        // Load password (not bound for security reasons)
        TxtPassword.Password = _plcDevice.Password;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(_plcDevice.Name))
        {
            MessageBox.Show("Name is required.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_plcDevice.EndpointUrl))
        {
            MessageBox.Show("Endpoint URL is required.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtEndpointUrl.Focus();
            return;
        }

        // Validate endpoint URL format
        if (!_plcDevice.EndpointUrl.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Endpoint URL must start with 'opc.tcp://'", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtEndpointUrl.Focus();
            return;
        }

        // Get password from PasswordBox
        _plcDevice.Password = TxtPassword.Password;

        // Get selected enum values from ComboBoxes
        if (CmbSecurityPolicy.SelectedItem is OpcUaSecurityPolicy secPolicy)
        {
            _plcDevice.SecurityPolicy = secPolicy;
        }
        if (CmbSecurityMode.SelectedItem is OpcUaSecurityMode secMode)
        {
            _plcDevice.SecurityMode = secMode;
        }

        // Copy values back to original device
        _originalDevice.Name = _plcDevice.Name;
        _originalDevice.Description = _plcDevice.Description;
        _originalDevice.EndpointUrl = _plcDevice.EndpointUrl;
        _originalDevice.IsEnabled = _plcDevice.IsEnabled;
        _originalDevice.SecurityPolicy = _plcDevice.SecurityPolicy;
        _originalDevice.SecurityMode = _plcDevice.SecurityMode;
        _originalDevice.UserName = _plcDevice.UserName;
        _originalDevice.Password = _plcDevice.Password;
        _originalDevice.CertificatePath = _plcDevice.CertificatePath;
        _originalDevice.PrivateKeyPath = _plcDevice.PrivateKeyPath;
        _originalDevice.SessionTimeout = _plcDevice.SessionTimeout;
        _originalDevice.KeepAliveInterval = _plcDevice.KeepAliveInterval;
        _originalDevice.AutoReconnect = _plcDevice.AutoReconnect;
        _originalDevice.ReconnectInterval = _plcDevice.ReconnectInterval;
        _originalDevice.MaxReconnectAttempts = _plcDevice.MaxReconnectAttempts;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BrowseCertificate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Certificate files (*.der;*.pem;*.crt)|*.der;*.pem;*.crt|All files (*.*)|*.*",
            Title = "Select Certificate File"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtCertificatePath.Text = dialog.FileName;
            _plcDevice.CertificatePath = dialog.FileName;
        }
    }

    private void BrowsePrivateKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Key files (*.pem;*.key)|*.pem;*.key|All files (*.*)|*.*",
            Title = "Select Private Key File"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtPrivateKeyPath.Text = dialog.FileName;
            _plcDevice.PrivateKeyPath = dialog.FileName;
        }
    }
}
