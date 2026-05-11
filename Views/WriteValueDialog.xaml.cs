using System.Windows;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// Dialog for writing values to OPC UA tags
/// </summary>
public partial class WriteValueDialog : Window
{
    private readonly TagItem _tagItem;

    public object? NewValue { get; private set; }

    public WriteValueDialog(TagItem tagItem)
    {
        InitializeComponent();
        _tagItem = tagItem;

        // Display tag information
        TxtTagName.Text = tagItem.Name;
        TxtNodeId.Text = tagItem.NodeId;
        TxtDataType.Text = tagItem.DataType.ToString();
        TxtCurrentValue.Text = tagItem.DisplayValue;

        // Set hint based on data type
        TxtHint.Text = GetDataTypeHint(tagItem.DataType);

        // Pre-fill with current value if available
        if (tagItem.Value != null)
        {
            TxtNewValue.Text = tagItem.Value.ToString();
        }

        TxtNewValue.Focus();
        TxtNewValue.SelectAll();
    }

    private string GetDataTypeHint(TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Boolean => "Enter 'true' or 'false' (or '1'/'0')",
            TagDataType.SByte => "Enter a number from -128 to 127",
            TagDataType.Byte => "Enter a number from 0 to 255",
            TagDataType.Int16 => "Enter a number from -32,768 to 32,767",
            TagDataType.UInt16 => "Enter a number from 0 to 65,535",
            TagDataType.Int32 => "Enter a 32-bit integer",
            TagDataType.UInt32 => "Enter a positive 32-bit integer",
            TagDataType.Int64 => "Enter a 64-bit integer",
            TagDataType.UInt64 => "Enter a positive 64-bit integer",
            TagDataType.Float => "Enter a decimal number (single precision)",
            TagDataType.Double => "Enter a decimal number (double precision)",
            TagDataType.String => "Enter text value",
            TagDataType.DateTime => "Enter date/time (yyyy-MM-dd HH:mm:ss)",
            _ => "Enter the value to write"
        };
    }

    private void WriteButton_Click(object sender, RoutedEventArgs e)
    {
        var inputValue = TxtNewValue.Text.Trim();

        if (string.IsNullOrEmpty(inputValue))
        {
            MessageBox.Show("Please enter a value to write.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNewValue.Focus();
            return;
        }

        // Parse value based on data type
        if (!TryParseValue(inputValue, _tagItem.DataType, out var parsedValue, out var errorMessage))
        {
            MessageBox.Show(errorMessage, "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNewValue.Focus();
            TxtNewValue.SelectAll();
            return;
        }

        NewValue = parsedValue;
        DialogResult = true;
        Close();
    }

    private bool TryParseValue(string input, TagDataType dataType, out object? result, out string errorMessage)
    {
        result = null;
        errorMessage = string.Empty;

        try
        {
            switch (dataType)
            {
                case TagDataType.Boolean:
                    if (input.Equals("true", StringComparison.OrdinalIgnoreCase) || input == "1")
                    {
                        result = true;
                        return true;
                    }
                    if (input.Equals("false", StringComparison.OrdinalIgnoreCase) || input == "0")
                    {
                        result = false;
                        return true;
                    }
                    errorMessage = "Invalid boolean value. Use 'true', 'false', '1', or '0'.";
                    return false;

                case TagDataType.SByte:
                    if (sbyte.TryParse(input, out var sbyteVal))
                    {
                        result = sbyteVal;
                        return true;
                    }
                    errorMessage = "Invalid signed byte value. Must be between -128 and 127.";
                    return false;

                case TagDataType.Byte:
                    if (byte.TryParse(input, out var byteVal))
                    {
                        result = byteVal;
                        return true;
                    }
                    errorMessage = "Invalid byte value. Must be between 0 and 255.";
                    return false;

                case TagDataType.Int16:
                    if (short.TryParse(input, out var int16Val))
                    {
                        result = int16Val;
                        return true;
                    }
                    errorMessage = "Invalid Int16 value.";
                    return false;

                case TagDataType.UInt16:
                    if (ushort.TryParse(input, out var uint16Val))
                    {
                        result = uint16Val;
                        return true;
                    }
                    errorMessage = "Invalid UInt16 value.";
                    return false;

                case TagDataType.Int32:
                    if (int.TryParse(input, out var int32Val))
                    {
                        result = int32Val;
                        return true;
                    }
                    errorMessage = "Invalid Int32 value.";
                    return false;

                case TagDataType.UInt32:
                    if (uint.TryParse(input, out var uint32Val))
                    {
                        result = uint32Val;
                        return true;
                    }
                    errorMessage = "Invalid UInt32 value.";
                    return false;

                case TagDataType.Int64:
                    if (long.TryParse(input, out var int64Val))
                    {
                        result = int64Val;
                        return true;
                    }
                    errorMessage = "Invalid Int64 value.";
                    return false;

                case TagDataType.UInt64:
                    if (ulong.TryParse(input, out var uint64Val))
                    {
                        result = uint64Val;
                        return true;
                    }
                    errorMessage = "Invalid UInt64 value.";
                    return false;

                case TagDataType.Float:
                    if (float.TryParse(input, out var floatVal))
                    {
                        result = floatVal;
                        return true;
                    }
                    errorMessage = "Invalid float value.";
                    return false;

                case TagDataType.Double:
                    if (double.TryParse(input, out var doubleVal))
                    {
                        result = doubleVal;
                        return true;
                    }
                    errorMessage = "Invalid double value.";
                    return false;

                case TagDataType.String:
                    result = input;
                    return true;

                case TagDataType.DateTime:
                    if (DateTime.TryParse(input, out var dateTimeVal))
                    {
                        result = dateTimeVal;
                        return true;
                    }
                    errorMessage = "Invalid DateTime value. Use format: yyyy-MM-dd HH:mm:ss";
                    return false;

                case TagDataType.Unknown:
                default:
                    // Try to auto-detect the type
                    if (bool.TryParse(input, out var boolVal))
                    {
                        result = boolVal;
                        return true;
                    }
                    if (int.TryParse(input, out var intVal))
                    {
                        result = intVal;
                        return true;
                    }
                    if (double.TryParse(input, out var dblVal))
                    {
                        result = dblVal;
                        return true;
                    }
                    // Default to string
                    result = input;
                    return true;
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error parsing value: {ex.Message}";
            return false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
