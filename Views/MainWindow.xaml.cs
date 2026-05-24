using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.ViewModels;

namespace OpcUaCommunicationEngine.Views;

/// <summary>
/// MainWindow code-behind — keeps MVVM purity except for one piece of pure UI state:
/// the collapse/restore size of the three dockable panels. This is view-only state
/// (no business logic), so it lives here rather than in the view-model.
/// </summary>
public partial class MainWindow : Window
{
    private GridLength _leftRestore   = new(260);
    private GridLength _rightRestore  = new(320);
    private GridLength _bottomRestore = new(200);
    private const double RailSize = 36;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Toggle the left PLC Devices panel between rail (36px) and last restored width.</summary>
    public void ToggleLeftPanel()
    {
        if (LeftCol.Width.Value > RailSize + 1)
        {
            _leftRestore = LeftCol.Width;
            AnimateColumn(LeftCol, _leftRestore.Value, RailSize);
        }
        else
        {
            AnimateColumn(LeftCol, RailSize, _leftRestore.Value);
        }
    }

    /// <summary>Toggle the right Properties panel.</summary>
    public void ToggleRightPanel()
    {
        if (RightCol.Width.Value > RailSize + 1)
        {
            _rightRestore = RightCol.Width;
            AnimateColumn(RightCol, _rightRestore.Value, RailSize);
        }
        else
        {
            AnimateColumn(RightCol, RailSize, _rightRestore.Value);
        }
    }

    /// <summary>Toggle the bottom Logger panel.</summary>
    public void ToggleBottomPanel()
    {
        if (BottomRow.Height.Value > RailSize + 1)
        {
            _bottomRestore = BottomRow.Height;
            AnimateRow(BottomRow, _bottomRestore.Value, RailSize);
        }
        else
        {
            AnimateRow(BottomRow, RailSize, _bottomRestore.Value);
        }
    }

    // 220ms cubic ease — matches the standard motion token.
    private static readonly Duration PanelDuration = new(System.TimeSpan.FromMilliseconds(220));
    private static readonly IEasingFunction PanelEase = new CubicEase { EasingMode = EasingMode.EaseOut };

    private static void AnimateColumn(ColumnDefinition col, double from, double to)
    {
        var anim = new GridLengthAnimation
        {
            From = new GridLength(from),
            To = new GridLength(to),
            Duration = PanelDuration,
            EasingFunction = PanelEase
        };
        col.BeginAnimation(ColumnDefinition.WidthProperty, anim);
    }

    private static void AnimateRow(RowDefinition row, double from, double to)
    {
        var anim = new GridLengthAnimation
        {
            From = new GridLength(from),
            To = new GridLength(to),
            Duration = PanelDuration,
            EasingFunction = PanelEase
        };
        row.BeginAnimation(RowDefinition.HeightProperty, anim);
    }

    // Header-button click handlers (toolbar + menu use commands; these wire the header chevrons).
    private void ToggleLeft_Click(object sender, RoutedEventArgs e)   => ToggleLeftPanel();
    private void ToggleRight_Click(object sender, RoutedEventArgs e)  => ToggleRightPanel();
    private void ToggleBottom_Click(object sender, RoutedEventArgs e) => ToggleBottomPanel();

    private void ApiSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;

        var currentSettings = LoadCurrentApiSettings();
        var dialog = new ApiSettingsDialog(currentSettings, viewModel.PlcDevices) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            SaveApiSettings(dialog.Result);
            MessageBox.Show(
                $"API settings saved.\nNew address: {dialog.Result.BindAddress}:{dialog.Result.Port}\n\nRestart the application to apply changes.",
                "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private static ApiSettings LoadCurrentApiSettings()
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

            doc["Api"] = new Newtonsoft.Json.Linq.JObject
            {
                ["BindAddress"] = settings.BindAddress,
                ["Port"] = settings.Port,
                ["Enabled"] = settings.Enabled
            };

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
        // Persist panel sizes to appsettings here if desired
        base.OnClosing(e);
    }
}

/// <summary>
/// WPF doesn't ship a built-in GridLength animation. Tiny helper:
/// interpolates GridLength.Value between two fixed lengths.
/// </summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public override System.Type TargetPropertyType => typeof(GridLength);

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        double from = From.Value;
        double to   = To.Value;
        double progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is not null) progress = EasingFunction.Ease(progress);
        return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
    }
}
