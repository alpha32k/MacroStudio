namespace MacroStudio.Core.Macros.Steps;

public sealed record PasteStep : MacroStep
{
    public WindowTarget? Target { get; set; }

    /// <summary>
    /// Removes spaces and line breaks at the start and the end before pasting. A cell copied from Excel ends
    /// with a line break, which some programs treat as an extra Enter.
    /// </summary>
    public bool CleanText { get; set; } = true;
}
