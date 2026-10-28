using MacroStudio.Core.Input;

namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// Looks for a small picture (a button, an icon) on the screen and clicks it wherever it is now. For programs whose
/// buttons have no names; it needs the same screen scale and theme as when the picture was taken.
/// </summary>
public sealed record ImageClickStep : MacroStep
{
    public const int MaxImageSide = 400;

    public int ImageWidth { get; set; }

    public int ImageHeight { get; set; }

    /// <summary>The picture's pixels, 4 bytes each (blue, green, red, unused), rows top to bottom.</summary>
    public byte[] ImagePixels { get; set; } = [];

    /// <summary>The window searched; null searches the whole screen.</summary>
    public WindowTarget? Target { get; set; }

    public MouseButton Button { get; set; } = MouseButton.Left;

    public int ClickCount { get; set; } = 1;

    /// <summary>How long to keep looking, for a picture that appears a moment later.</summary>
    public int TimeoutMs { get; set; } = 5_000;
}
