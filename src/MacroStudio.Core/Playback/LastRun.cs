using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Playback;

/// <summary>How the last full playback of a macro ended; shown in the macro lists.</summary>
public sealed record LastRun(DateTimeOffset EndedAt, PlaybackOutcome Outcome, int CompletedRounds, RunFailure? LastFailure = null)
{
    public static LastRun FromResult(DateTimeOffset endedAt, PlaybackResult result, LastRun? previous)
    {
        RunFailure? failure = result is { Outcome: PlaybackOutcome.Failed, FailedRound: int round,
            FailedStepNumber: int step, FailedStepId: Guid stepId, Failure: PlaybackFailure reason }
            ? new RunFailure(endedAt, round, step, stepId, reason, result.Window)
            : previous?.LastFailure;
        return new LastRun(endedAt, result.Outcome, result.CompletedRounds, failure);
    }
}

/// <summary>The last failed step, retained even after a later successful run.</summary>
public sealed record RunFailure(DateTimeOffset EndedAt, int Round, int StepNumber, Guid StepId, PlaybackFailure Reason, WindowTarget? Window);
