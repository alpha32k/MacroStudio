namespace MacroStudio.Core.Playback;

public enum ErrorAction
{
    /// <summary>Stop the macro and say why.</summary>
    Stop,

    /// <summary>Leave the step out and go on with the next one.</summary>
    SkipStep,

    /// <summary>Leave the rest of this round out and start the next round (the next row of a list).</summary>
    SkipRound,
}

/// <summary>
/// What playback does when a step fails because a window did not come or the clipboard did not change: try again a
/// few times, a second apart, and then stop or skip. A program running as administrator or a locked screen always
/// stops the macro: trying again cannot help there.
/// </summary>
public sealed record ErrorPolicy(ErrorAction Action, int Retries)
{
    public const int MaxRetries = 10;

    public static ErrorPolicy StopAtOnce { get; } = new(ErrorAction.Stop, 0);

    public static bool CanRetry(PlaybackFailure failure) =>
        failure is not (PlaybackFailure.TargetIsElevated or PlaybackFailure.InputBlocked);
}
