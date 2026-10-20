using MacroStudio.App.Localization;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.App.Macros;

/// <summary>The short lines under a macro's name on the home page and in My macros.</summary>
public static class MacroSummary
{
    /// <summary>"6 adım · tur başına ~3 sn · 15.10.2026 14:58"</summary>
    public static string Of(MacroFile file)
    {
        Localizer text = Localizer.Instance;
        return text.Format(
            "Macro_Summary",
            file.Macro.Steps.Count,
            Duration(MacroDuration.EstimateRound(file.Macro)),
            file.LastWriteTime.ToString("g", text.Culture));
    }

    /// <summary>"Son çalışma: 15.10.2026 15:20 · liste bitti, 50 tur"; null when the macro never ran.</summary>
    public static string? LastRunOf(LastRun? run)
    {
        Localizer text = Localizer.Instance;
        return run is null
            ? null
            : text.Format("Macro_LastRun", run.EndedAt.LocalDateTime.ToString("g", text.Culture), text["Outcome_" + run.Outcome], run.CompletedRounds);
    }

    private static string Duration(TimeSpan duration) =>
        duration.TotalSeconds < 60
            ? Localizer.Instance.Format("Duration_Seconds", Math.Max(1, (int)Math.Round(duration.TotalSeconds)))
            : Localizer.Instance.Format("Duration_Minutes", (int)duration.TotalMinutes, duration.Seconds);
}
