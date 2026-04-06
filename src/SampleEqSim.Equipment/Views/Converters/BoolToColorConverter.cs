using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SampleEqSim.Equipment.Views.Converters;

/// <summary>bool → 背景色 (true=赤, false=グレー)</summary>
public class BoolToAlarmColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
                     : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>文字列キー → SolidColorBrush (LED色)</summary>
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
        => _map.TryGetValue(value?.ToString() ?? "", out var brush)
            ? brush
            : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
