using System.Globalization;

namespace MacroStudio.Core.Macros;

/// <summary>
/// Reads a wait typed by a person: "230", "230 ms", "0,23 sn", "0.23 s", "1,5 saniye". A plain number is milliseconds.
/// Both decimal comma and decimal point work, so Turkish and English habits are both understood.
/// </summary>
public static class DelayParser
{
    private static readonly string[] MillisecondUnits = ["ms", "msn", "milisaniye", "millisecond", "milliseconds"];
    private static readonly string[] SecondUnits = ["s", "sn", "sec", "saniye", "second", "seconds"];

    /// <returns>Milliseconds, or null when the text is not a wait between 0 and the allowed maximum.</returns>
    public static int? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string value = text.Trim().ToLowerInvariant();
        double factor = 1;
        foreach ((string[] units, double unitFactor) in new[] { (MillisecondUnits, 1.0), (SecondUnits, 1000.0) })
        {
            string? unit = units.OrderByDescending(u => u.Length).FirstOrDefault(u => value.EndsWith(u, StringComparison.Ordinal));
            if (unit is not null && (value.Length == unit.Length || !char.IsLetter(value[^(unit.Length + 1)])))
            {
                value = value[..^unit.Length].Trim();
                factor = unitFactor;
                break;
            }
        }

        if (!double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return null;
        }

        double milliseconds = Math.Round(number * factor);
        return milliseconds is >= 0 and <= MacroValidator.MaxDelayMs ? (int)milliseconds : null;
    }
}
