using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Macros.Steps;

public enum IfCheck
{
    WindowOpen,
    WindowNotOpen,
    ColorIs,
    ColorIsNot,
}

public enum IfAction
{
    /// <summary>The next <see cref="IfStep.SkipCount"/> steps are left out in this round.</summary>
    SkipSteps,

    /// <summary>The rest of this round is left out; the next round starts.</summary>
    EndRound,

    /// <summary>The macro ends here, as if it had finished.</summary>
    StopMacro,
}

/// <summary>
/// A decision without blocks: "if the warning window is open, skip nothing; if it is not, skip the next step that
/// closes it". It looks once, at once; to wait for something use <see cref="WaitForStep"/>.
/// </summary>
public sealed record IfStep : MacroStep
{
    public const int MaxSkipCount = 100;

    public required IfCheck Check { get; set; }

    /// <summary>The window looked for; for a color, the window the spot is in (null: a fixed place on the screen).</summary>
    public WindowTarget? Target { get; set; }

    /// <summary>The spot for a color check, moved along with its window.</summary>
    public ScreenPoint ScreenPosition { get; set; }

    /// <summary>The color compared, as 0xRRGGBB.</summary>
    public int Color { get; set; }

    public required IfAction Action { get; set; }

    public int SkipCount { get; set; } = 1;
}
