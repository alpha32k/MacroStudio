using System.Globalization;
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

    /// <summary>What to do when a step fails; by default the macro stops at once.</summary>
    public ErrorPolicy OnError { get; init; } = ErrorPolicy.StopAtOnce;

    /// <summary>The rows of the macro's list file, one per round; null when it has none. The list ending ends the macro.</summary>
    public IReadOnlyList<IReadOnlyList<string>>? ListRows { get; init; }

    /// <summary>The list's column names, for {Müşteri}-style fields in text steps.</summary>
    public IReadOnlyList<string> ListColumns { get; init; } = [];

    /// <summary>The date and time format of {tarih} and {saat}.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;
}
