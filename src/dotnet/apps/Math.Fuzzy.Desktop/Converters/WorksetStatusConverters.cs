using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MathFuzzy.Desktop.ViewModels.Workspace;

namespace MathFuzzy.Desktop.Converters;

/// <summary>Background fill for a status badge (semi-transparent).</summary>
[ValueConversion(typeof(WorksetStatus), typeof(SolidColorBrush))]
public sealed class WorksetStatusToBrushConverter : IValueConverter
{
    public static readonly WorksetStatusToBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is WorksetStatus s ? ToBrush(s) : Brushes.Transparent;

    // Light design-system tints: green 50 / amber 50 / red 50 / gray 50.
    private static SolidColorBrush ToBrush(WorksetStatus s) => s switch
    {
        WorksetStatus.Done    => new SolidColorBrush(Color.FromRgb(0xEA, 0xF3, 0xDE)),
        WorksetStatus.Running => new SolidColorBrush(Color.FromRgb(0xFA, 0xEE, 0xDA)),
        WorksetStatus.Error   => new SolidColorBrush(Color.FromRgb(0xFC, 0xEB, 0xEB)),
        _                     => new SolidColorBrush(Color.FromRgb(0xE4, 0xE1, 0xD7)),   // darker than the selected card
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Foreground/text color for a status badge.</summary>
[ValueConversion(typeof(WorksetStatus), typeof(SolidColorBrush))]
public sealed class WorksetStatusToForegroundConverter : IValueConverter
{
    public static readonly WorksetStatusToForegroundConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is WorksetStatus s ? ToForeground(s) : Brushes.Gray;

    // Dark text from the same ramp as the fill (design-system rule).
    private static SolidColorBrush ToForeground(WorksetStatus s) => s switch
    {
        WorksetStatus.Done    => new SolidColorBrush(Color.FromRgb(0x3B, 0x6D, 0x11)),
        WorksetStatus.Running => new SolidColorBrush(Color.FromRgb(0x85, 0x4F, 0x0B)),
        WorksetStatus.Error   => new SolidColorBrush(Color.FromRgb(0xA3, 0x2D, 0x2D)),
        _                     => new SolidColorBrush(Color.FromRgb(0x88, 0x87, 0x80)),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Returns Visibility.Visible only when WorksetStatus == Running,
/// used to show/hide a spinner icon inside a running badge.
/// </summary>
[ValueConversion(typeof(WorksetStatus), typeof(System.Windows.Visibility))]
public sealed class WorksetStatusToSpinnerVisibilityConverter : IValueConverter
{
    public static readonly WorksetStatusToSpinnerVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is WorksetStatus.Running
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
