using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>
    /// Toggle boolean value quickly
    /// </summary>
    private async void ToggleBooleanValue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is TagItem tag)
        {
            if (DataContext is MainViewModel viewModel && viewModel.SelectedPlc != null)
            {
                // Toggle the boolean value
                var currentValue = tag.Value as bool? ?? false;
                var newValue = !currentValue;
                await viewModel.WriteTagValueAsync(tag, newValue.ToString());
            }
        }
    }

    /// <summary>
    /// Handle Enter key in value TextBox to submit
    /// </summary>
    private async void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is TextBox textBox && textBox.DataContext is TagItem tag)
            {
                if (DataContext is MainViewModel viewModel && viewModel.SelectedPlc != null)
                {
                    var newValue = textBox.Text;
                    await viewModel.WriteTagValueAsync(tag, newValue);

                    // Exit edit mode
                    TagsDataGrid.CommitEdit();
                    e.Handled = true;
                }
            }
        }
        else if (e.Key == Key.Escape)
        {
            // Cancel edit mode
            TagsDataGrid.CancelEdit();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Open User Management window
    /// </summary>
    private void UserManagement_Click(object sender, RoutedEventArgs e)
    {
        var app = Application.Current as App;
        var userService = app?.ApiHost?.UserService;

        if (userService == null)
        {
            MessageBox.Show("User service is not available. API server may not be running.",
                "Service Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var window = new UserManagementWindow(userService);
        window.Owner = this;
        window.ShowDialog();
    }

    /// <summary>
    /// Open API Settings dialog
    /// </summary>
    private void ApiSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        // Load current settings
        var currentSettings = LoadCurrentApiSettings();

        // Open dialog with current settings and PLC list
        var dialog = new ApiSettingsDialog(currentSettings, viewModel.PlcDevices);
        dialog.Owner = this;

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            // Save new settings
            SaveApiSettings(dialog.Result);

            MessageBox.Show(
                $"API settings saved.\nNew address: {dialog.Result.BindAddress}:{dialog.Result.Port}\n\nRestart the application to apply changes.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private ApiSettings LoadCurrentApiSettings()
    {
        const string appSettingsPath = "Configurations/appsettings.json";
        try
        {
            if (System.IO.File.Exists(appSettingsPath))
            {
                var json = System.IO.File.ReadAllText(appSettingsPath);
                var doc = Newtonsoft.Json.Linq.JObject.Parse(json);
                var apiSection = doc["Api"];
                if (apiSection != null)
                {
                    return new ApiSettings
                    {
                        Port = (int?)apiSection["Port"] ?? 5000,
                        BindAddress = (string?)apiSection["BindAddress"] ?? "0.0.0.0",
                        Enabled = (bool?)apiSection["Enabled"] ?? true
                    };
                }
            }
        }
        catch { }
        return new ApiSettings();
    }

    private void SaveApiSettings(ApiSettings settings)
    {
        const string appSettingsPath = "Configurations/appsettings.json";
        try
        {
            Newtonsoft.Json.Linq.JObject doc;
            if (System.IO.File.Exists(appSettingsPath))
            {
                var json = System.IO.File.ReadAllText(appSettingsPath);
                doc = Newtonsoft.Json.Linq.JObject.Parse(json);
            }
            else
            {
                doc = new Newtonsoft.Json.Linq.JObject();
            }

            // Update Api section
            doc["Api"] = new Newtonsoft.Json.Linq.JObject
            {
                ["BindAddress"] = settings.BindAddress,
                ["Port"] = settings.Port,
                ["Enabled"] = settings.Enabled
            };

            // Save back to file
            System.IO.File.WriteAllText(appSettingsPath, doc.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
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
