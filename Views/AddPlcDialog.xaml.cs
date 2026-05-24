using System.Windows;
using System.Windows.Input;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

public partial class AddPlcDialog : Window
{
    /// <summary>Populated when the user clicks "Chỉ thêm" or "Thêm &amp; Kết nối".</summary>
    public PlcDevice? Result { get; private set; }

    /// <summary>True when the user wants to connect immediately after adding.</summary>
    public bool ConnectAfterAdd { get; private set; }

    public AddPlcDialog()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { DialogResult = false; Close(); }
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) AddConnect_Click(this, new RoutedEventArgs());
        };
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Head_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void TestEndpoint_Click(object sender, RoutedEventArgs e)
    {
        // TODO: real probe via OpcUaSession.PingAsync(EndpointBox.Text)
        EndpointStatus.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AddOnly_Click(object sender, RoutedEventArgs e)
    {
        Result = BuildPlc();
        ConnectAfterAdd = false;
        DialogResult = true;
        Close();
    }

    private void AddConnect_Click(object sender, RoutedEventArgs e)
    {
        Result = BuildPlc();
        ConnectAfterAdd = true;
        DialogResult = true;
        Close();
    }

    private PlcDevice BuildPlc()
    {
        var plc = PlcDevice.Create(
            NameBox.Text?.Trim() ?? "New PLC",
            EndpointBox.Text?.Trim() ?? "opc.tcp://localhost:4840");

        plc.SecurityPolicy = PolicyBox.SelectedIndex switch
        {
            1 => OpcUaSecurityPolicy.Basic256Sha256,
            2 => OpcUaSecurityPolicy.Aes256Sha256RsaPss,
            _ => OpcUaSecurityPolicy.None
        };
        plc.SecurityMode = ModeBox.SelectedIndex switch
        {
            1 => OpcUaSecurityMode.Sign,
            2 => OpcUaSecurityMode.SignAndEncrypt,
            _ => OpcUaSecurityMode.None
        };

        if (AuthUsername.IsChecked == true)
        {
            plc.UserName = UserBox.Text?.Trim() ?? "";
            plc.Password = PassBox.Password;
        }

        if (int.TryParse(TimeoutBox.Text,   out var t1)) plc.SessionTimeout    = t1;
        if (int.TryParse(KeepAliveBox.Text, out var t2)) plc.KeepAliveInterval = t2;
        if (int.TryParse(ReconnectBox.Text, out var t3)) plc.ReconnectInterval = t3;
        if (int.TryParse(MaxRetriesBox.Text, out var t4)) plc.MaxReconnectAttempts = t4;

        foreach (var g in SubscriptionGroup.CreateDefaultGroups(plc.Id))
            plc.SubscriptionGroups.Add(g);

        return plc;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Cancel_Click(sender, e);
}
