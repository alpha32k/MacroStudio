namespace MacroStudio.App.Playback;

public enum PlaybackState
{
    Idle,

    /// <summary>The countdown before the first step; the user takes the hands off the mouse and keyboard.</summary>
    CountingDown,

    Playing,

    /// <summary>Paused by the user, by touching the mouse or keyboard, or at a password field.</summary>
    Paused,
}
