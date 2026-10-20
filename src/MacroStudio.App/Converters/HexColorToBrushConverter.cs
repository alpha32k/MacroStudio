using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MacroStudio.Core.Settings;

namespace MacroStudio.App.Converters;

/// <summary>"#RRGGBB" to a brush for color swatches; anything else gives no brush.</summary>
public sealed class HexColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && AppSettings.IsHexColor(hex)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
            : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
