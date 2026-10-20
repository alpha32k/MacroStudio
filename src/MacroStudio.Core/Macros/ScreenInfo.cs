namespace MacroStudio.Core.Macros;

/// <summary>Primary monitor at recording time, to warn when a macro is played on a different screen setup.</summary>
/// <param name="Scale">Display scale: 1.0 = 100 %, 1.5 = 150 %.</param>
public sealed record ScreenInfo(int Width, int Height, double Scale)
{
    private const double ScaleTolerance = 0.01;

    /// <summary>Same size and scale: recorded positions land on the same spots.</summary>
    public bool Matches(ScreenInfo other) =>
        Width == other.Width && Height == other.Height && Math.Abs(Scale - other.Scale) < ScaleTolerance;
}
