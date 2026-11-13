namespace MacroStudio.Core.Macros.Steps;

/// <summary>Runs a parameterless VBA procedure in an already open Excel workbook.</summary>
public sealed record ExcelMacroStep : MacroStep
{
    public required string WorkbookPath { get; set; }

    public required string Procedure { get; set; }

    public static bool IsWorkbookPath(string? path) =>
        path is { Length: > 0 and <= MacroValidator.MaxOpenPathLength }
        && Path.IsPathFullyQualified(path)
        && !path.Any(char.IsControl)
        && path.IndexOfAny(['*', '?', '"', '<', '>', '|']) < 0
        && (path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".xlsb", StringComparison.OrdinalIgnoreCase));

    public static bool IsProcedureName(string? name)
    {
        if (name is not { Length: > 0 and <= 511 })
        {
            return false;
        }

        string[] parts = name.Split('.');
        return parts.Length <= 2 && parts.All(part => part.Length is > 0 and <= 255
            && char.IsLetter(part[0]) && part.All(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
}
