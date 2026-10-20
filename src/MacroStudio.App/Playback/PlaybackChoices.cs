using System.Globalization;
using MacroStudio.App.Localization;
using MacroStudio.Core.Macros;

namespace MacroStudio.App.Playback;

/// <summary>The repeat and speed choices offered on the bar and in the editor.</summary>
public static class PlaybackChoices
{
    public static IReadOnlyList<LocalizedOption<LoopSettings>> Loops { get; } =
    [
        new(LoopSettings.UntilClipboardEmpty, l => l["Loop_UntilEmpty"]),
        .. new[] { 1, 5, 10, 50, 100 }.Select(n =>
            new LocalizedOption<LoopSettings>(LoopSettings.Times(n), _ => n.ToString(CultureInfo.InvariantCulture))),
        new(LoopSettings.Infinite, _ => "∞"),
    ];

    public static IReadOnlyList<double> Speeds { get; } = [0.25, 0.5, 1, 1.5, 2, 3, 5];

    /// <summary>The choice for a saved loop; a count that is not offered (for example 7) keeps the given fallback.</summary>
    public static LocalizedOption<LoopSettings> LoopFor(LoopSettings loop, LocalizedOption<LoopSettings> fallback) =>
        Loops.FirstOrDefault(choice => choice.Value == loop) ?? fallback;

    public static double SpeedFor(double speed, double fallback) => Speeds.Contains(speed) ? speed : fallback;
}
