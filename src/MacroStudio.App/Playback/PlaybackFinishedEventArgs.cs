using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.App.Playback;

/// <param name="isPartial">Only part of the macro was played ("Play from here", "Try this step").</param>
public sealed class PlaybackFinishedEventArgs(MacroDefinition macro, PlaybackResult result, bool isPartial) : EventArgs
{
    public MacroDefinition Macro { get; } = macro;

    public PlaybackResult Result { get; } = result;

    public bool IsPartial { get; } = isPartial;
}
