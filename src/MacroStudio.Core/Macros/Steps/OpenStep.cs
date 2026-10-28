namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// Opens a program, a file or a web page the way a double click in Explorer would, for example "excel",
/// "C:\Raporlar\Liste.xlsx" or "https://portal.example.com". The next steps wait for its window as usual.
/// </summary>
public sealed record OpenStep : MacroStep
{
    public required string Path { get; set; }

    /// <summary>Extra words for a program, for example a file to open with it; usually empty.</summary>
    public string Arguments { get; set; } = "";
}
