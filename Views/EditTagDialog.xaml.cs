using System.Windows;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for editing Tag properties
/// </summary>
public partial class EditTagDialog : Window
{
    private readonly TagItem _tagItem;
    private readonly TagItem _originalTag;

    public TagItem TagItem => _tagItem;

    public EditTagDialog(TagItem tagItem)
    {
        InitializeComponent();

        _originalTag = tagItem;
        _tagItem = tagItem.Clone();

        // Set DataContext to the cloned tag for binding
        DataContext = _tagItem;

        // Load enum values for ComboBoxes
        CmbDataType.ItemsSource = Enum.GetValues<TagDataType>();
        CmbDataType.SelectedItem = _tagItem.DataType;

        CmbAccessMode.ItemsSource = Enum.GetValues<TagAccessMode>();
        CmbAccessMode.SelectedItem = _tagItem.AccessMode;

        // Load nullable values
        TxtMinValue.Text = _tagItem.MinValue?.ToString() ?? "";
        TxtMaxValue.Text = _tagItem.MaxValue?.ToString() ?? "";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(_tagItem.Name))
        {
            MessageBox.Show("Name is required.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_tagItem.NodeId))
        {
            MessageBox.Show("Node ID is required.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNodeId.Focus();
            return;
        }

        // Get selected enum values from ComboBoxes
        if (CmbDataType.SelectedItem is TagDataType dataType)
        {
            _tagItem.DataType = dataType;
        }
        if (CmbAccessMode.SelectedItem is TagAccessMode accessMode)
        {
            _tagItem.AccessMode = accessMode;
        }

        // Parse nullable values
        if (double.TryParse(TxtMinValue.Text, out var minVal))
        {
            _tagItem.MinValue = minVal;
        }
        else
        {
            _tagItem.MinValue = null;
        }

        if (double.TryParse(TxtMaxValue.Text, out var maxVal))
        {
            _tagItem.MaxValue = maxVal;
        }
        else
        {
            _tagItem.MaxValue = null;
        }

        // Copy values back to original tag
        _originalTag.Name = _tagItem.Name;
        _originalTag.Description = _tagItem.Description;
        _originalTag.NodeId = _tagItem.NodeId;
        _originalTag.DisplayName = _tagItem.DisplayName;
        _originalTag.BrowsePath = _tagItem.BrowsePath;
        _originalTag.DataType = _tagItem.DataType;
        _originalTag.AccessMode = _tagItem.AccessMode;
        _originalTag.IsEnabled = _tagItem.IsEnabled;
        _originalTag.ScanRate = _tagItem.ScanRate;
        _originalTag.Deadband = _tagItem.Deadband;
        _originalTag.EngineeringUnit = _tagItem.EngineeringUnit;
        _originalTag.MinValue = _tagItem.MinValue;
        _originalTag.MaxValue = _tagItem.MaxValue;
        _originalTag.ArraySize = _tagItem.ArraySize;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
