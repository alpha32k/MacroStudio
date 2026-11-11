using MacroStudio.Core.Playback;

namespace MacroStudio.Core.Tests.Playback;

public sealed class RunHistoryStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_folder, "runs.json");

    [Fact]
    public void SaveThenLoad_KeepsTheLastRunOfEachMacro()
    {
        var store = new RunHistoryStore(FilePath);
        Guid id = Guid.NewGuid();
        var run = new LastRun(new DateTimeOffset(2026, 10, 15, 14, 58, 0, TimeSpan.FromHours(3)), PlaybackOutcome.ListEnded, 50);

        store.Save(new Dictionary<Guid, LastRun> { [id] = run });

        Assert.Equal(run, store.Load()[id]);
    }

    [Fact]
    public void Load_WithoutAFile_OrWithABrokenOne_IsEmpty()
    {
        var store = new RunHistoryStore(FilePath);
        Assert.Empty(store.Load());

        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ not json");
        Assert.Empty(store.Load());
    }

    [Fact]
    public void LastErrorSurvivesASuccessfulRunAndCanBeLoadedFromDisk()
    {
        var store = new RunHistoryStore(FilePath);
        Guid macroId = Guid.NewGuid();
        Guid stepId = Guid.NewGuid();
        var endedAt = new DateTimeOffset(2026, 10, 15, 14, 58, 0, TimeSpan.FromHours(3));
        LastRun failed = LastRun.FromResult(endedAt,
            new PlaybackResult(PlaybackOutcome.Failed, 1, 4, PlaybackFailure.WindowNotFound, FailedRound: 2, FailedStepId: stepId), null);
        LastRun completed = LastRun.FromResult(endedAt.AddMinutes(1), new PlaybackResult(PlaybackOutcome.Completed, 2), failed);

        store.Save(new Dictionary<Guid, LastRun> { [macroId] = completed });

        LastRun reloaded = store.Load()[macroId];
        Assert.Equal(PlaybackOutcome.Completed, reloaded.Outcome);
        Assert.Equal(new RunFailure(endedAt, 2, 4, stepId, PlaybackFailure.WindowNotFound, null), reloaded.LastFailure);
    }

    [Fact]
    public void Load_PreviousHistoryWithoutFailureDetails_KeepsTheSummary()
    {
        Guid id = Guid.NewGuid();
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, $$"""
            { "{{id}}": { "endedAt": "2026-10-15T14:58:00+03:00", "outcome": "Failed", "completedRounds": 1 } }
            """);

        LastRun run = new RunHistoryStore(FilePath).Load()[id];

        Assert.Equal((PlaybackOutcome.Failed, 1), (run.Outcome, run.CompletedRounds));
        Assert.Null(run.LastFailure);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
