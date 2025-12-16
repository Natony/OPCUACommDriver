using System.Windows;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// MainWindow code-behind
/// Trong MVVM, code-behind nên được giữ tối thiểu
/// Logic chính nằm trong ViewModel
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Có thể thêm logic xác nhận trước khi đóng
        base.OnClosing(e);
    }
}
