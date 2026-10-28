using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Macros.Steps;

public enum WaitCondition
{
    /// <summary>The window is open, for example a dialog the program shows when it has finished.</summary>
    WindowAppears,

    /// <summary>The window is gone, for example "Saving…".</summary>
    WindowCloses,

    /// <summary>A spot on the screen has a color, for example a green "Ready" light or a button that is no longer grey.</summary>
    ColorAppears,
}

/// <summary>
/// Waits as long as the program needs instead of a fixed time: until a window opens or closes, or until a spot has
/// a color. Fails when it takes longer than <see cref="TimeoutMs"/>.
/// </summary>
public sealed record WaitForStep : MacroStep
{
    public const int DefaultTimeoutMs = 30_000;

    public required WaitCondition Condition { get; set; }

    /// <summary>The window waited for; for a color, the window the spot is in (null: a fixed place on the screen).</summary>
    public WindowTarget? Target { get; set; }

    /// <summary>The spot for <see cref="WaitCondition.ColorAppears"/>, moved along with its window.</summary>
    public ScreenPoint ScreenPosition { get; set; }

    /// <summary>The color waited for, as 0xRRGGBB.</summary>
    public int Color { get; set; }

    public int TimeoutMs { get; set; } = DefaultTimeoutMs;
}
