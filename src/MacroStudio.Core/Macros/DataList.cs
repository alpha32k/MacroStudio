using System.Text;

namespace MacroStudio.Core.Macros;

/// <summary>
/// A list the macro works through, one row per round: a CSV file (for example saved from Excel with
/// "Save as > CSV") or a text file with one value per line. Text steps type a column with {1}, {2}...
/// </summary>
/// <param name="HasHeader">The first row holds column names and is not worked through.</param>
public sealed record DataList(string Path, bool HasHeader);

/// <summary>Reads a list file into rows of values.</summary>
public static class DataListReader
{
    public const int MaxRows = 100_000;

    /// <returns>The rows to work through (without the header) and the column names (the header, or "1", "2"...).</returns>
    public static (IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string> Columns) Read(DataList list)
    {
        IReadOnlyList<IReadOnlyList<string>> all = Parse(ReadText(list.Path));
        if (all.Count > MaxRows + 1)
        {
            throw new InvalidDataException($"A list can have at most {MaxRows} rows.");
        }

        int width = all.Count == 0 ? 0 : all.Max(row => row.Count);
        IReadOnlyList<string> columns = list.HasHeader && all.Count > 0
            ? [.. Enumerable.Range(0, width).Select(i => i < all[0].Count && all[0][i].Trim().Length > 0 ? all[0][i].Trim() : (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))]
            : [.. Enumerable.Range(1, width).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        return (list.HasHeader ? [.. all.Skip(1)] : all, columns);
    }

    /// <summary>
    /// Excel saves CSV in UTF-8 (with a mark) or in the Windows code page of the computer: 1254 in Turkey.
    /// Text that is not valid UTF-8 is read in that code page, so "ğ" and "ş" survive both.
    /// </summary>
    public static string ReadText(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254).GetString(bytes);
        }
    }

    /// <summary>
    /// CSV with quotes ("a;b" stays one value, "" is a quote). The separator is the one the first line uses most:
    /// Turkish Excel writes ";", English Excel ",", and copied tables tab. Empty lines at the end are left out.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        string firstLine = text.Split('\n', 2)[0];
        char separator = new[] { ';', ',', '\t' }.OrderByDescending(c => firstLine.Count(x => x == c)).First();
        if (!firstLine.Contains(separator, StringComparison.Ordinal))
        {
            separator = '\0';
        }

        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var value = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    value.Append(c);
                }
            }
            else if (c == '"' && value.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == separator)
            {
                row.Add(value.ToString());
                value.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(value.ToString());
                value.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                value.Append(c);
            }
        }

        if (value.Length > 0 || row.Count > 0)
        {
            row.Add(value.ToString());
            rows.Add(row);
        }

        while (rows.Count > 0 && rows[^1].All(v => v.Length == 0))
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return rows;
    }
}
