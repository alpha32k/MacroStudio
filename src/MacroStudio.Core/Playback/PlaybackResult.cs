using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Playback;

/// <param name="CompletedRounds">Rounds played to the end.</param>
/// <param name="FailedStepNumber">1-based step number when <paramref name="Outcome"/> is Failed.</param>
/// <param name="Window">The window the failed step needed, if any.</param>
/// <param name="SkippedSteps">Steps left out after failing (see ErrorPolicy).</param>
/// <param name="SkippedRounds">Rounds left unfinished after a step failed in them.</param>
public sealed record PlaybackResult(
    PlaybackOutcome Outcome,
    int CompletedRounds,
    int? FailedStepNumber = null,
    PlaybackFailure? Failure = null,
    WindowTarget? Window = null,
    int SkippedSteps = 0,
    int SkippedRounds = 0);
