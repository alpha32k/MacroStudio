namespace MacroStudio.Core.Playback;

/// <param name="TotalRounds">Null when the macro repeats until the list ends or forever.</param>
/// <param name="StepNumber">1-based number of the step in the macro, disabled steps included.</param>
public sealed record PlaybackProgress(int Round, int? TotalRounds, int StepNumber, int StepCount);
