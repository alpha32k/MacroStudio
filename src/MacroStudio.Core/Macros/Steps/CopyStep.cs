namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// Ctrl+C, then waiting until the clipboard really changed. Programs such as Excel fill the clipboard late,
/// so a fixed wait would sometimes be too short and sometimes needlessly long.
/// </summary>
public sealed record CopyStep : MacroStep
{
    public WindowTarget? Target { get; set; }
}
