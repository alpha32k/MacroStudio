namespace MacroStudio.Core.Playback;

public enum PlaybackOutcome
{
    /// <summary>All rounds were played.</summary>
    Completed,

    /// <summary>A copy step copied an empty value: the end of the list was reached.</summary>
    ListEnded,

    /// <summary>The user stopped it (Esc, Ctrl+Alt+End, Stop button).</summary>
    Stopped,

    Failed,
}
