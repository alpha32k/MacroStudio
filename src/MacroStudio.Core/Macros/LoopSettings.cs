namespace MacroStudio.Core.Macros;

/// <param name="Count">Number of runs; only used when <paramref name="Mode"/> is <see cref="LoopMode.Count"/>.</param>
public sealed record LoopSettings(LoopMode Mode, int Count = 1)
{
    public const int MaxCount = 100_000;

    public static LoopSettings UntilClipboardEmpty { get; } = new(LoopMode.UntilClipboardEmpty);

    public static LoopSettings Infinite { get; } = new(LoopMode.Infinite);

    public static LoopSettings Times(int count) => new(LoopMode.Count, Math.Clamp(count, 1, MaxCount));
}
