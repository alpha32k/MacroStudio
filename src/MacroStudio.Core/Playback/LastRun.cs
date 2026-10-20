namespace MacroStudio.Core.Playback;

/// <summary>How the last full playback of a macro ended; shown in the macro lists.</summary>
public sealed record LastRun(DateTimeOffset EndedAt, PlaybackOutcome Outcome, int CompletedRounds);
