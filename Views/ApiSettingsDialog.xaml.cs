using System.Collections.ObjectModel;
using System.Windows;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for configuring API server settings
/// </summary>
public partial class ApiSettingsDialog : Window
{
    private readonly ApiBindSuggestion _suggestion;
    private readonly string _suggestedSubnet;

    /// <summary>
    /// The resulting API settings after user configuration
    /// </summary>
    public ApiSettings? Result { get; private set; }

    public ApiSettingsDialog(
        ApiSettings currentSettings,
        ObservableCollection<PlcDevice> plcDevices)
    {
        InitializeComponent();

        // Get suggestion based on PLCs
        var endpoints = plcDevices.Select(p => p.EndpointUrl);
        _suggestion = ApiSettings.SuggestBindAddressFromMultiplePlcs(endpoints);
        _suggestedSubnet = _suggestion.SubnetPrefix ?? "";

        // Initialize UI with current settings
        EnabledCheckBox.IsChecked = currentSettings.Enabled;
        BindAddressTextBox.Text = currentSettings.BindAddress;
        PortTextBox.Text = currentSettings.Port.ToString();

        // Show PLC list
        PlcListBox.ItemsSource = plcDevices;

        // Update suggestion text
        UpdateSuggestionText();

        // Update button state based on PLC count
        UseSuggestionButton.IsEnabled = _suggestion.CanConfigure && !string.IsNullOrEmpty(_suggestion.SubnetPrefix);
    }

    private void UpdateSuggestionText()
    {
        if (!_suggestion.CanConfigure)
        {
            SuggestionTextBlock.Text = _suggestion.Reason;
            SuggestionTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
        }
        else if (_suggestion.IsSameSubnet)
        {
            SuggestionTextBlock.Text = $"{_suggestion.Reason}\nGợi ý: {_suggestion.SubnetPrefix}xxx (nhập 3 số cuối của IP máy tính)";
            SuggestionTextBlock.Foreground = System.Windows.Media.Brushes.Green;
        }
        else
        {
            SuggestionTextBlock.Text = _suggestion.Reason;
            SuggestionTextBlock.Foreground = System.Windows.Media.Brushes.Blue;
        }
    }

    private void UseSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_suggestedSubnet))
        {
            // Set the subnet prefix and let user complete it
            BindAddressTextBox.Text = _suggestedSubnet;
            BindAddressTextBox.Focus();
            BindAddressTextBox.CaretIndex = BindAddressTextBox.Text.Length;
        }
        else
        {
            BindAddressTextBox.Text = _suggestion.SuggestedAddress;
        }
    }

    private void SetAllInterfaces_Click(object sender, RoutedEventArgs e)
    {
        BindAddressTextBox.Text = "0.0.0.0";
    }

    private void SetLocalhost_Click(object sender, RoutedEventArgs e)
    {
        BindAddressTextBox.Text = "127.0.0.1";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Validate port
        if (!int.TryParse(PortTextBox.Text, out int port) || port < 1024 || port > 65535)
        {
            MessageBox.Show("Port must be a number between 1024 and 65535",
                "Invalid Port", MessageBoxButton.OK, MessageBoxImage.Warning);
            PortTextBox.Focus();
            return;
        }

        // Validate bind address
        var bindAddress = BindAddressTextBox.Text.Trim();
        if (!ApiSettings.IsValidBindAddress(bindAddress))
        {
            MessageBox.Show("Invalid bind address. Use format: xxx.xxx.xxx.xxx\nExamples: 0.0.0.0, 127.0.0.1, 192.168.1.100",
                "Invalid Address", MessageBoxButton.OK, MessageBoxImage.Warning);
            BindAddressTextBox.Focus();
            return;
        }

        // Create result
        Result = new ApiSettings
        {
            Enabled = EnabledCheckBox.IsChecked ?? true,
            BindAddress = bindAddress,
            Port = port
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
