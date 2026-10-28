using System.Globalization;
using MacroStudio.App.Localization;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

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

    public static IReadOnlyList<LocalizedOption<ErrorPolicy>> ErrorPolicies { get; } =
    [
        LocalizedOption<ErrorPolicy>.FromKey(ErrorPolicy.StopAtOnce, "OnError_Stop"),
        LocalizedOption<ErrorPolicy>.FromKey(new ErrorPolicy(ErrorAction.Stop, 2), "OnError_RetryThenStop"),
        LocalizedOption<ErrorPolicy>.FromKey(new ErrorPolicy(ErrorAction.SkipStep, 2), "OnError_RetryThenSkipStep"),
        LocalizedOption<ErrorPolicy>.FromKey(new ErrorPolicy(ErrorAction.SkipRound, 2), "OnError_RetryThenSkipRound"),
    ];

    /// <summary>The choice for a saved policy; one edited by hand to something not offered shows as the nearest kind.</summary>
    public static LocalizedOption<ErrorPolicy> ErrorPolicyFor(ErrorPolicy policy) =>
        ErrorPolicies.FirstOrDefault(choice => choice.Value == policy)
        ?? ErrorPolicies.FirstOrDefault(choice => choice.Value.Action == policy.Action && choice.Value.Retries > 0)
        ?? ErrorPolicies[0];

    /// <summary>The choice for a saved loop; a count that is not offered (for example 7) keeps the given fallback.</summary>
    public static LocalizedOption<LoopSettings> LoopFor(LoopSettings loop, LocalizedOption<LoopSettings> fallback) =>
        Loops.FirstOrDefault(choice => choice.Value == loop) ?? fallback;

    public static double SpeedFor(double speed, double fallback) => Speeds.Contains(speed) ? speed : fallback;
}
