using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Macros;

public static class MacroDuration
{
    /// <summary>
    /// About how long one round takes at the macro's saved speed: the recorded waits, wait steps and drags.
    /// Waiting for windows and the clipboard comes on top, so the real time is a little longer.
    /// </summary>
    public static TimeSpan EstimateRound(MacroDefinition macro)
    {
        double totalMs = macro.Steps
            .Where(step => step.IsEnabled)
            .Sum(step => step.DelayBeforeMs + step switch
            {
                WaitStep { IsPasswordEntry: false } wait => wait.DurationMs,
                MouseDragStep drag => drag.DurationMs,
                _ => 0,
            });
        return TimeSpan.FromMilliseconds(totalMs / macro.SpeedMultiplier);
    }
}
