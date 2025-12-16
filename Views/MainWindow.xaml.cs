using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using OpcUaCommunicationEngine.ViewModels;

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
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Setup auto-scroll for log list
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }
    }

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Auto-scroll to bottom when new items are added
        if (e.Action == NotifyCollectionChangedAction.Add && LogListBox.Items.Count > 0)
        {
            LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Cleanup event handler
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LogEntries.CollectionChanged -= LogEntries_CollectionChanged;
        }
        base.OnClosing(e);
    }
}
