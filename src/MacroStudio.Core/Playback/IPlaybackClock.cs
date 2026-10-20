namespace MacroStudio.Core.Playback;

public interface IPlaybackClock
{
    long NowMs { get; }

    /// <summary>Waits the duration; throws OperationCanceledException as soon as the token is cancelled.</summary>
    void Wait(TimeSpan duration, CancellationToken cancellation);
}
