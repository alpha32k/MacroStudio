using System.Globalization;
using System.Text;

namespace MacroStudio.Core.Macros;

/// <summary>What a text step's {…} fields stand for in the current round.</summary>
/// <param name="Row">The list row of this round; null when the macro has no list.</param>
/// <param name="Columns">The list's column names (its header), for {Müşteri}-style fields.</param>
/// <param name="Round">1 for the first round.</param>
public sealed record TemplateValues(IReadOnlyList<string>? Row, IReadOnlyList<string> Columns, int Round, DateTime Now, CultureInfo Culture);

/// <summary>
/// Fills the fields of a text step: {1}, {2}… or a column name for the list row of the round, {tur} for the round
/// number, {tarih} and {saat} for the date and time (English names work too). Anything else in braces is typed as
/// it is, so ordinary text with braces is safe.
/// </summary>
public static class TextTemplate
{
    public static bool HasFields(string text) => text.Contains('{', StringComparison.Ordinal);

    public static string Fill(string text, TemplateValues values)
    {
        if (!HasFields(text))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            int close = text[i] == '{' ? text.IndexOf('}', i + 1) : -1;
            if (close > i && Resolve(text[(i + 1)..close].Trim(), values) is { } value)
            {
                result.Append(value);
                i = close + 1;
            }
            else
            {
                result.Append(text[i]);
                i++;
            }
        }

        return result.ToString();
    }

    private static string? Resolve(string field, TemplateValues values)
    {
        if (values.Row is { } row)
        {
            if (int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out int column) && column >= 1)
            {
                return column <= row.Count ? row[column - 1] : "";
            }

            for (int c = 0; c < values.Columns.Count; c++)
            {
                if (string.Equals(values.Columns[c], field, StringComparison.CurrentCultureIgnoreCase))
                {
                    return c < row.Count ? row[c] : "";
                }
            }
        }

        return field.ToLower(CultureInfo.GetCultureInfo("tr-TR")) switch
        {
            "tur" or "round" or "sayaç" or "counter" => values.Round.ToString(CultureInfo.InvariantCulture),
            "tarih" or "date" => values.Now.ToString("d", values.Culture),
            "saat" or "time" => values.Now.ToString("t", values.Culture),
            _ => null,
        };
    }
}
