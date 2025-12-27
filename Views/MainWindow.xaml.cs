using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using OpcUaCommunicationEngine.Models;
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

    /// <summary>
    /// Handle Write button click in DataGrid cell
    /// </summary>
    private void WriteTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TagItem tag)
        {
            if (DataContext is MainViewModel viewModel && viewModel.SelectedPlc != null)
            {
                viewModel.SelectedTag = tag;
                viewModel.WriteTagCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Handle inline value write
    /// </summary>
    private async void WriteValueInline_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TagItem tag)
        {
            if (DataContext is MainViewModel viewModel && viewModel.SelectedPlc != null)
            {
                // Find the TextBox in the same cell
                var parent = button.Parent as Grid;
                var textBox = parent?.Children.OfType<TextBox>().FirstOrDefault();

                if (textBox != null)
                {
                    var newValue = textBox.Text;
                    await viewModel.WriteTagValueAsync(tag, newValue);
                }
            }
        }

        // Exit edit mode
        TagsDataGrid.CommitEdit();
    }

    /// <summary>
    /// Handle delete selected tags
    /// </summary>
    private void DeleteSelectedTags_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedPlc == null)
            return;

        var selectedTags = TagsDataGrid.SelectedItems.Cast<TagItem>().ToList();

        if (selectedTags.Count == 0)
        {
            MessageBox.Show("Please select one or more tags to delete.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete {selectedTags.Count} selected tag(s)?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        viewModel.DeleteSelectedTags(selectedTags);
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
