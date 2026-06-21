using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MT5GoldScalper.V2.Converters;

public sealed class BooleanToThemeBrushConverter : IValueConverter
{
    public Brush DarkBrush { get; set; } = Brushes.Black;
    public Brush LightBrush { get; set; } = Brushes.White;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? DarkBrush : LightBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

<<<<<<< HEAD
public sealed class BooleanToConnectionBrushConverter : IValueConverter
{
    public Brush ConnectedBrush { get; set; } = Brushes.LimeGreen;
    public Brush DisconnectedBrush { get; set; } = Brushes.IndianRed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? ConnectedBrush : DisconnectedBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

=======
>>>>>>> 51e2d5bcb0b5db4084d8598cb42f117288d2e977
public sealed class SeverityToBrushConverter : IValueConverter
{
    public Brush GoodBrush { get; set; } = Brushes.LimeGreen;
    public Brush WatchBrush { get; set; } = Brushes.Goldenrod;
    public Brush BlockedBrush { get; set; } = Brushes.IndianRed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Watch" => WatchBrush,
            "Blocked" => BlockedBrush,
            _ => GoodBrush
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class PercentageToValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim().TrimEnd('%');
        return double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentage)
            ? Math.Clamp(percentage, 0, 100)
            : 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class PercentageToBrushConverter : IValueConverter
{
    public Brush GoodBrush { get; set; } = Brushes.LimeGreen;
    public Brush WatchBrush { get; set; } = Brushes.Goldenrod;
    public Brush BadBrush { get; set; } = Brushes.IndianRed;
    public Brush NeutralBrush { get; set; } = Brushes.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim().TrimEnd('%');
        if (!double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentage))
        {
            return NeutralBrush;
        }

        return percentage >= 75 ? GoodBrush
            : percentage >= 50 ? WatchBrush
            : BadBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class SourceToBrushConverter : IValueConverter
{
    public Brush AutoBrush { get; set; } = Brushes.SteelBlue;
    public Brush SemiBrush { get; set; } = Brushes.MediumPurple;
    public Brush ApiBrush { get; set; } = Brushes.DarkCyan;
    public Brush ManBrush { get; set; } = Brushes.SlateGray;
    public Brush CfgBrush { get; set; } = Brushes.DarkSlateBlue;
    public Brush DefaultBrush { get; set; } = Brushes.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString()?.ToUpperInvariant() switch
        {
            "AUTO" => AutoBrush,
            "SEMI" => SemiBrush,
            "API" => ApiBrush,
            "MAN" => ManBrush,
            "CFG" => CfgBrush,
            "SEL" => ManBrush,
            _ => DefaultBrush
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
