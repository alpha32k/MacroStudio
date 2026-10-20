using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Playback;

public sealed record PlaybackOptions
{
    public LoopSettings Loop { get; init; } = LoopSettings.UntilClipboardEmpty;

    /// <summary>2 plays twice as fast: every recorded wait is halved.</summary>
    public double Speed { get; init; } = 1;

    /// <summary>How long to wait for a window (a program starting, a dialog opening) before giving up.</summary>
    public TimeSpan WindowWait { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a copy step waits for the program to fill the clipboard.</summary>
    public TimeSpan ClipboardWait { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>"Play from here": the first round starts at this 1-based step; later rounds start at the top.</summary>
    public int FirstStep { get; init; } = 1;

    /// <summary>"Try this step": no step after this 1-based number is played. Null plays to the end.</summary>
    public int? LastStep { get; init; }
}
