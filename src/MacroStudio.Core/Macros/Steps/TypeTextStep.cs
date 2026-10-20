namespace MacroStudio.Core.Macros.Steps;

/// <summary>
/// Typed characters. They are replayed as text, not as keys, so Turkish letters (ğ, ü, ş, ı, İ, ö, ç)
/// come out right whatever keyboard layout the computer has.
/// </summary>
public sealed record TypeTextStep : MacroStep
{
    public required string Text { get; set; }

    public WindowTarget? Target { get; set; }
}
