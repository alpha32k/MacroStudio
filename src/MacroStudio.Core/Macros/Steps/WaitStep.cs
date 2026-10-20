namespace MacroStudio.Core.Macros.Steps;

public sealed record WaitStep : MacroStep
{
    public int DurationMs { get; set; }

    /// <summary>
    /// The user typed into a password field here. The password is never recorded; playback stops at this
    /// step so the user can type it.
    /// </summary>
    public bool IsPasswordEntry { get; set; }
}
