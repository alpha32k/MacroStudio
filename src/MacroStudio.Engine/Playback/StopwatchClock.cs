using System.Diagnostics;
using MacroStudio.Core.Playback;

namespace MacroStudio.Engine.Playback;

/// <summary>
/// Waits to the millisecond. It sleeps through most of the time and spins only for the last millisecond,
/// and it wakes up at once when playback is stopped.
/// </summary>
public sealed class StopwatchClock : IPlaybackClock
{
    private static readonly long SpinThresholdTicks = Stopwatch.Frequency / 1000;

    public long NowMs => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

    public void Wait(TimeSpan duration, CancellationToken cancellation)
    {
        long end = Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * Stopwatch.Frequency);
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            long remaining = end - Stopwatch.GetTimestamp();
            if (remaining <= 0)
            {
                return;
            }

            if (remaining > SpinThresholdTicks)
            {
                int sleepMs = (int)((remaining - SpinThresholdTicks) * 1000 / Stopwatch.Frequency);
                cancellation.WaitHandle.WaitOne(Math.Max(1, sleepMs));
            }
            else
            {
                Thread.Yield();
            }
        }
    }
}
