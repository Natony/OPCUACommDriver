using System.Windows;
using System.Windows.Controls;
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

    private void TabManual_Checked(object sender, RoutedEventArgs e)  => ShowPanel(manual: true);
    private void TabScan_Checked(object sender, RoutedEventArgs e)    => ShowPanel(scan: true);
    private void TabImport_Checked(object sender, RoutedEventArgs e)  => ShowPanel(import: true);

    private void ShowPanel(bool manual = false, bool scan = false, bool import = false)
    {
        if (ManualPanel == null) return;
        ManualPanel.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        ScanPanel.Visibility   = scan   ? Visibility.Visible : Visibility.Collapsed;
        ImportPanel.Visibility = import ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ProtocolBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EndpointBox == null) return;

        // Suggest a sensible default endpoint when protocol changes.
        EndpointBox.Text = ProtocolBox.SelectedIndex switch
        {
            1 => "192.168.1.100:102",                  // Siemens S7
            2 => "192.168.1.100:5000",                 // Mitsubishi MC
            3 => "192.168.1.100:502",                  // Modbus TCP
            _ => "opc.tcp://192.168.1.100:4840",       // OPC UA
        };
    }

    private void TestEndpoint_Click(object sender, RoutedEventArgs e)
    {
        // TODO: real probe via OpcUaSession.PingAsync(EndpointBox.Text)
        EndpointStatus.Visibility = Visibility.Visible;
    }

    private void StartScan_Click(object sender, RoutedEventArgs e)
    {
        ScanStatusText.Text = "Tính năng quét mạng sẽ có trong bản cập nhật sắp tới.";
        MessageBox.Show(this,
            "Tính năng quét mạng đang được phát triển và sẽ có trong bản cập nhật sắp tới.",
            "Quét mạng", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BrowseImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON config (*.json)|*.json|All files (*.*)|*.*",
            Title = "Chọn file cấu hình PLC"
        };
        if (dlg.ShowDialog(this) != true) return;

        ImportStatusText.Text = $"Đã chọn: {dlg.FileName}\nTính năng nhập từ file đang được phát triển — vui lòng dùng menu File › Open Configuration để mở cấu hình đầy đủ.";
        MessageBox.Show(this,
            "Nhập từ file PLC riêng lẻ đang được phát triển. Hiện tại bạn có thể dùng menu File › Open Configuration để mở cấu hình đã lưu.",
            "Nhập từ file", MessageBoxButton.OK, MessageBoxImage.Information);
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

        plc.ProtocolType = ProtocolBox.SelectedIndex switch
        {
            1 => ProtocolType.SiemensS7,
            2 => ProtocolType.MitsubishiMc,
            3 => ProtocolType.ModbusTcp,
            _ => ProtocolType.OpcUa
        };

        plc.PlcType = TypeBox.SelectedIndex switch
        {
            1 => PlcType.SiemensS7300,
            2 => PlcType.SiemensS7400,
            3 => PlcType.SiemensS7_1200,
            4 => PlcType.SiemensS7_1500,
            5 => PlcType.MitsubishiFX3U,
            6 => PlcType.MitsubishiFX5U,
            7 => PlcType.MitsubishiQ,
            8 => PlcType.MitsubishiIQR,
            _ => PlcType.Generic
        };

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
