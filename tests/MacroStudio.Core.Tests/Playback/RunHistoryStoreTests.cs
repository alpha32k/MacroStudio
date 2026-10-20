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

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
