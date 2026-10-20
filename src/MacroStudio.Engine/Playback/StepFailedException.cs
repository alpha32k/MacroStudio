using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.Engine.Playback;

/// <summary>A step could not be played; playback stops and reports why.</summary>
internal sealed class StepFailedException(PlaybackFailure failure, WindowTarget? window)
    : Exception($"Step failed: {failure}.")
{
    public PlaybackFailure Failure { get; } = failure;

    public WindowTarget? Window { get; } = window;
}
