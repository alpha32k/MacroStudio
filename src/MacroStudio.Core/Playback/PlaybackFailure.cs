namespace MacroStudio.Core.Playback;

public enum PlaybackFailure
{
    /// <summary>No open window matches the recorded program and window kind.</summary>
    WindowNotFound,

    /// <summary>The window exists but Windows did not let it come to the front.</summary>
    WindowNotActivated,

    /// <summary>The program runs as administrator; Windows blocks keys and clicks from normal programs (UIPI).</summary>
    TargetIsElevated,

    /// <summary>After Ctrl+C the clipboard did not change, so the next paste would use old data.</summary>
    ClipboardDidNotChange,

    /// <summary>Another program kept the clipboard locked.</summary>
    ClipboardUnavailable,

    /// <summary>Windows refused the input: the screen is locked or an administrator (UAC) prompt is open.</summary>
    InputBlocked,

    /// <summary>An open step's program, file or web address could not be opened.</summary>
    OpenFailed,

    /// <summary>A wait-until step waited its longest time and the window or color did not come.</summary>
    WaitTimedOut,

    /// <summary>The picture of an image-click step was not on the screen.</summary>
    ImageNotFound,
}
