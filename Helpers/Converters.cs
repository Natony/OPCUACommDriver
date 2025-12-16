using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Helpers;

/// <summary>
/// Convert Boolean to Visibility
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Nếu parameter = "Inverse" thì đảo ngược
            if (parameter?.ToString() == "Inverse")
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var result = visibility == Visibility.Visible;
            if (parameter?.ToString() == "Inverse")
                result = !result;
            return result;
        }
        return false;
    }
}

/// <summary>
/// Convert PlcConnectionState to Brush color
/// </summary>
public class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlcConnectionState state)
        {
            return state switch
            {
                PlcConnectionState.Connected => new SolidColorBrush(Colors.Green),
                PlcConnectionState.Connecting => new SolidColorBrush(Colors.Orange),
                PlcConnectionState.Reconnecting => new SolidColorBrush(Colors.Orange),
                PlcConnectionState.Error => new SolidColorBrush(Colors.Red),
                PlcConnectionState.Disconnected => new SolidColorBrush(Colors.Gray),
                PlcConnectionState.Disabled => new SolidColorBrush(Colors.DarkGray),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convert TagQuality to Brush color
/// </summary>
public class QualityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TagQuality quality)
        {
            return quality switch
            {
                TagQuality.Good => new SolidColorBrush(Colors.Green),
                TagQuality.Uncertain => new SolidColorBrush(Colors.Orange),
                TagQuality.Bad => new SolidColorBrush(Colors.Red),
                TagQuality.Unknown => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convert null to Visibility
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value == null;
        if (parameter?.ToString() == "Inverse")
            isNull = !isNull;

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convert enum to description string
/// </summary>
public class EnumToDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            return enumValue.ToString();
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Multi-value converter cho complex bindings
/// </summary>
public class MultiBooleanToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Nếu parameter = "Or" thì chỉ cần 1 true
        // Ngược lại tất cả phải true
        var useOr = parameter?.ToString() == "Or";

        foreach (var value in values)
        {
            if (value is bool boolValue)
            {
                if (useOr && boolValue)
                    return Visibility.Visible;
                if (!useOr && !boolValue)
                    return Visibility.Collapsed;
            }
        }

        return useOr ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Format number với unit
/// </summary>
public class NumberWithUnitConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2)
        {
            var number = values[0];
            var unit = values[1]?.ToString() ?? string.Empty;

            if (number is double d)
                return $"{d:F2} {unit}".Trim();
            if (number is float f)
                return $"{f:F2} {unit}".Trim();
            if (number is int i)
                return $"{i} {unit}".Trim();

            return $"{number} {unit}".Trim();
        }
        return values.FirstOrDefault()?.ToString() ?? string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convert hex color string to SolidColorBrush
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorString && !string.IsNullOrEmpty(colorString))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorString);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.Black);
            }
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
