using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;

namespace MacroStudio.Core.Tests.Playback;

public sealed class ListResumeStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CheckpointPersistsAcrossInstances_AndIsSpecificToTheMacroAndList()
    {
        Guid macroId = Guid.NewGuid();
        string path = WriteList("list.csv", "Name\nA\nB\nC\n");
        var dataList = new DataList(path, HasHeader: true);
        PlaybackList list = DataListReader.ReadForPlayback(dataList);
        Assert.Equal(3, list.Rows.Count);

        new ListResumeStore(_folder).Save(macroId, list.Source, 2);
        var reloaded = new ListResumeStore(_folder);

        Assert.Equal(2, reloaded.SuggestedRow(macroId, DataListReader.ReadForPlayback(dataList)));
        Assert.Equal(1, reloaded.SuggestedRow(Guid.NewGuid(), list));
        Assert.Equal(1, reloaded.SuggestedRow(macroId, DataListReader.ReadForPlayback(dataList with { HasHeader = false })));
        string otherPath = WriteList("other.csv", "Name\nA\nB\nC\n");
        Assert.Equal(1, reloaded.SuggestedRow(macroId, DataListReader.ReadForPlayback(dataList with { Path = otherPath })));
        File.WriteAllText(path, "Name\nC\nB\nA\n");
        Assert.Equal(1, reloaded.SuggestedRow(macroId, DataListReader.ReadForPlayback(dataList)));
    }

    [Fact]
    public void CompletingTheList_OffersTheFirstRowAgain()
    {
        Guid macroId = Guid.NewGuid();
        var store = new ListResumeStore(_folder);
        PlaybackList list = DataListReader.ReadForPlayback(new DataList(WriteList("list.csv", "A\nB\n"), HasHeader: false));

        store.Save(macroId, list.Source, 2);
        store.Save(macroId, list.Source, 3);

        Assert.Equal(1, store.SuggestedRow(macroId, list));
    }

    private string WriteList(string name, string contents)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
