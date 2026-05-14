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
