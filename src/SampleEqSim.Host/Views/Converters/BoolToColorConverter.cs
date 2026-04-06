using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SampleEqSim.Host.Views.Converters;

public class StringToLedBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> _map = new()
    {
        ["LedGreen"]  = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        ["LedRed"]    = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        ["LedYellow"] = new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)),
        ["LedGray"]   = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
        ["LedBlue"]   = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => _map.TryGetValue(value?.ToString() ?? "", out var b) ? b : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class StringToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value?.ToString() ?? "#E2E8F0";
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Colors.White);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
