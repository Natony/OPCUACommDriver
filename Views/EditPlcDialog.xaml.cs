using System.Windows;
using System.Windows.Controls;
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

        // Load Protocol Type enum values
        CmbProtocolType.ItemsSource = Enum.GetValues<ProtocolType>();
        CmbProtocolType.SelectedItem = _plcDevice.ProtocolType;

        // Load PLC Type enum values
        CmbPlcType.ItemsSource = Enum.GetValues<PlcType>();
        CmbPlcType.SelectedItem = _plcDevice.PlcType;

        // Load enum values for ComboBoxes
        CmbSecurityPolicy.ItemsSource = Enum.GetValues<OpcUaSecurityPolicy>();
        CmbSecurityPolicy.SelectedItem = _plcDevice.SecurityPolicy;

        CmbSecurityMode.ItemsSource = Enum.GetValues<OpcUaSecurityMode>();
        CmbSecurityMode.SelectedItem = _plcDevice.SecurityMode;

        // Load password (not bound for security reasons)
        TxtPassword.Password = _plcDevice.Password;

        // Update UI based on current protocol type
        UpdateProtocolUI(_plcDevice.ProtocolType);
    }

    private void CmbProtocolType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProtocolType.SelectedItem is ProtocolType protocolType)
        {
            _plcDevice.ProtocolType = protocolType;
            UpdateProtocolUI(protocolType);
            SetDefaultValuesForProtocol(protocolType);
        }
    }

    private void UpdateProtocolUI(ProtocolType protocolType)
    {
        // Show/hide panels based on protocol type
        bool isOpcUa = protocolType == ProtocolType.OpcUa;
        bool isTcpIp = !isOpcUa;
        bool isSiemensS7 = protocolType == ProtocolType.SiemensS7;

        // OPC UA panels
        PanelOpcUa.Visibility = isOpcUa ? Visibility.Visible : Visibility.Collapsed;
        PanelSecurity.Visibility = isOpcUa ? Visibility.Visible : Visibility.Collapsed;
        PanelSessionConfig.Visibility = isOpcUa ? Visibility.Visible : Visibility.Collapsed;

        // TCP/IP panels
        PanelTcpIp.Visibility = isTcpIp ? Visibility.Visible : Visibility.Collapsed;

        // Siemens S7 specific (Rack/Slot)
        PanelSiemensS7.Visibility = isSiemensS7 ? Visibility.Visible : Visibility.Collapsed;

        // PLC Type selector (for TCP/IP protocols)
        LblPlcType.Visibility = isTcpIp ? Visibility.Visible : Visibility.Collapsed;
        CmbPlcType.Visibility = isTcpIp ? Visibility.Visible : Visibility.Collapsed;

        // Filter PLC types based on protocol
        UpdatePlcTypeOptions(protocolType);
    }

    private void UpdatePlcTypeOptions(ProtocolType protocolType)
    {
        var plcTypes = protocolType switch
        {
            ProtocolType.SiemensS7 => new[]
            {
                PlcType.SiemensS7300,
                PlcType.SiemensS7400,
                PlcType.SiemensS7_1200,
                PlcType.SiemensS7_1500
            },
            ProtocolType.MitsubishiMc => new[]
            {
                PlcType.MitsubishiFX3U,
                PlcType.MitsubishiFX5U,
                PlcType.MitsubishiQ,
                PlcType.MitsubishiIQR
            },
            ProtocolType.ModbusTcp => new[] { PlcType.Generic },
            _ => Enum.GetValues<PlcType>()
        };

        CmbPlcType.ItemsSource = plcTypes;
        if (plcTypes.Length > 0)
        {
            CmbPlcType.SelectedItem = plcTypes[0];
        }
    }

    private void SetDefaultValuesForProtocol(ProtocolType protocolType)
    {
        switch (protocolType)
        {
            case ProtocolType.SiemensS7:
                _plcDevice.Port = 102;
                _plcDevice.Rack = 0;
                _plcDevice.Slot = 1;
                break;
            case ProtocolType.MitsubishiMc:
                _plcDevice.Port = 5000;
                break;
            case ProtocolType.ModbusTcp:
                _plcDevice.Port = 502;
                break;
        }
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

        // Get selected protocol and PLC type
        if (CmbProtocolType.SelectedItem is ProtocolType protocolType)
        {
            _plcDevice.ProtocolType = protocolType;
        }
        if (CmbPlcType.SelectedItem is PlcType plcType)
        {
            _plcDevice.PlcType = plcType;
        }

        // Validate based on protocol type
        if (_plcDevice.ProtocolType == ProtocolType.OpcUa)
        {
            // OPC UA validation
            if (string.IsNullOrWhiteSpace(_plcDevice.EndpointUrl))
            {
                MessageBox.Show("Endpoint URL is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtEndpointUrl.Focus();
                return;
            }

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
        }
        else
        {
            // TCP/IP validation
            if (string.IsNullOrWhiteSpace(_plcDevice.IpAddress))
            {
                MessageBox.Show("IP Address is required.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtIpAddress.Focus();
                return;
            }

            // Validate IP address format
            if (!System.Net.IPAddress.TryParse(_plcDevice.IpAddress, out _))
            {
                MessageBox.Show("Please enter a valid IP address.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtIpAddress.Focus();
                return;
            }

            if (_plcDevice.Port <= 0 || _plcDevice.Port > 65535)
            {
                MessageBox.Show("Port must be between 1 and 65535.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPort.Focus();
                return;
            }
        }

        // Copy values back to original device
        _originalDevice.Name = _plcDevice.Name;
        _originalDevice.Description = _plcDevice.Description;
        _originalDevice.IsEnabled = _plcDevice.IsEnabled;
        _originalDevice.AutoReconnect = _plcDevice.AutoReconnect;
        _originalDevice.ReconnectInterval = _plcDevice.ReconnectInterval;
        _originalDevice.MaxReconnectAttempts = _plcDevice.MaxReconnectAttempts;

        // Protocol specific values
        _originalDevice.ProtocolType = _plcDevice.ProtocolType;
        _originalDevice.PlcType = _plcDevice.PlcType;

        if (_plcDevice.ProtocolType == ProtocolType.OpcUa)
        {
            // OPC UA values
            _originalDevice.EndpointUrl = _plcDevice.EndpointUrl;
            _originalDevice.SecurityPolicy = _plcDevice.SecurityPolicy;
            _originalDevice.SecurityMode = _plcDevice.SecurityMode;
            _originalDevice.UserName = _plcDevice.UserName;
            _originalDevice.Password = _plcDevice.Password;
            _originalDevice.CertificatePath = _plcDevice.CertificatePath;
            _originalDevice.PrivateKeyPath = _plcDevice.PrivateKeyPath;
            _originalDevice.SessionTimeout = _plcDevice.SessionTimeout;
            _originalDevice.KeepAliveInterval = _plcDevice.KeepAliveInterval;
        }
        else
        {
            // TCP/IP values
            _originalDevice.IpAddress = _plcDevice.IpAddress;
            _originalDevice.Port = _plcDevice.Port;
            _originalDevice.Rack = _plcDevice.Rack;
            _originalDevice.Slot = _plcDevice.Slot;
            _originalDevice.ConnectionTimeout = _plcDevice.ConnectionTimeout;
            _originalDevice.ReadTimeout = _plcDevice.ReadTimeout;
            _originalDevice.WriteTimeout = _plcDevice.WriteTimeout;
        }

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
