using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Playback;

/// <param name="CompletedRounds">Rounds played to the end.</param>
/// <param name="FailedStepNumber">1-based step number when <paramref name="Outcome"/> is Failed.</param>
/// <param name="Window">The window the failed step needed, if any.</param>
public sealed record PlaybackResult(
    PlaybackOutcome Outcome,
    int CompletedRounds,
    int? FailedStepNumber = null,
    PlaybackFailure? Failure = null,
    WindowTarget? Window = null);
