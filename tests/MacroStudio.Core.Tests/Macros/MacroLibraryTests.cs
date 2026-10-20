using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Tests.Macros;

public sealed class MacroLibraryTests : IDisposable
{
    private static readonly WindowTarget Excel = new("EXCEL", "Kitap1 - Excel", "XLMAIN", new ScreenRect(0, 0, 1200, 800));

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveNew_ThenLoad_KeepsEveryStepType()
    {
        var library = new MacroLibrary(_folder);
        MacroDefinition macro = CreateMacro("Excel'den SAP'a", AllStepTypes());

        string path = library.SaveNew(macro);
        MacroDefinition loaded = MacroLibrary.Load(path);

        Assert.Equal(macro.Steps.Select(step => step.GetType()), loaded.Steps.Select(step => step.GetType()));
        Assert.Equal(macro.Steps[0], loaded.Steps[0]);
        Assert.Equal("Çağrı İş öğü", Assert.IsType<TypeTextStep>(loaded.Steps[5]).Text);
        Assert.Equal(macro.Name, loaded.Name);
    }

    [Fact]
    public void SaveNew_WritesReadableStepTypes()
    {
        string path = new MacroLibrary(_folder).SaveNew(CreateMacro("Test", [new CopyStep { Target = Excel }]));

        string json = File.ReadAllText(path);
        Assert.Contains("\"type\": \"copy\"", json, StringComparison.Ordinal);
        Assert.Contains("\"processName\": \"EXCEL\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Rapor: Ocak/Şubat", "Rapor Ocak Şubat.msmacro")]
    [InlineData("CON", "CON_.msmacro")]
    [InlineData("???", "Macro.msmacro")]
    public void SaveNew_MakesASafeFileName(string name, string expectedFileName)
    {
        string path = new MacroLibrary(_folder).SaveNew(CreateMacro(name, [new CopyStep()]));

        Assert.Equal(expectedFileName, Path.GetFileName(path));
    }

    [Fact]
    public void SaveNew_WithAnExistingName_AddsANumber()
    {
        var library = new MacroLibrary(_folder);

        library.SaveNew(CreateMacro("Günlük", [new CopyStep()]));
        string second = library.SaveNew(CreateMacro("Günlük", [new CopyStep()]));

        Assert.Equal("Günlük (2).msmacro", Path.GetFileName(second));
    }

    [Fact]
    public void Save_KeepsThePreviousVersion_AndOnlyTheNewestTen()
    {
        var library = new MacroLibrary(_folder);
        string path = library.SaveNew(CreateMacro("Günlük", [new CopyStep()]));

        for (int i = 1; i <= 12; i++)
        {
            library.Save(path, CreateMacro("Günlük", [new WaitStep { DurationMs = i }]));
            Thread.Sleep(2);
        }

        Assert.Equal(11, Assert.IsType<WaitStep>(Assert.Single(MacroLibrary.Load(library.VersionsOf(path)[0]).Steps)).DurationMs);
        Assert.Equal(MacroLibrary.KeptVersions, library.VersionsOf(path).Count);
        Assert.Equal(12, Assert.IsType<WaitStep>(Assert.Single(MacroLibrary.Load(path).Steps)).DurationMs);
        Assert.Single(library.LoadAll().Macros);
    }

    [Fact]
    public void Rename_MovesTheFileAndItsVersions()
    {
        var library = new MacroLibrary(_folder);
        string path = library.SaveNew(CreateMacro("Günlük", [new CopyStep()]));
        library.Save(path, CreateMacro("Günlük", [new PasteStep()]));

        string renamed = library.Rename(path, "Haftalık");

        Assert.Equal("Haftalık.msmacro", Path.GetFileName(renamed));
        Assert.False(File.Exists(path));
        Assert.Equal("Haftalık", MacroLibrary.Load(renamed).Name);
        Assert.Single(library.VersionsOf(renamed));
        Assert.Single(library.LoadAll().Macros);
    }

    [Fact]
    public void Rename_ToItsOwnFileName_KeepsTheFile()
    {
        var library = new MacroLibrary(_folder);
        string path = library.SaveNew(CreateMacro("Günlük", [new CopyStep()]));

        string renamed = library.Rename(path, "Günlük ");

        Assert.Equal(path, renamed);
        Assert.Equal("Günlük ", MacroLibrary.Load(path).Name);
    }

    [Fact]
    public void MoveToTrash_ThenRestore_BringsBackTheMacroAndItsVersions()
    {
        var library = new MacroLibrary(_folder);
        string path = library.SaveNew(CreateMacro("Günlük", [new CopyStep()]));
        library.Save(path, CreateMacro("Günlük", [new PasteStep()]));

        library.MoveToTrash(path);

        Assert.Empty(library.LoadAll().Macros);
        TrashEntry entry = Assert.Single(library.ListTrash());
        Assert.Equal("Günlük", entry.Name);

        string restored = library.Restore(entry);

        Assert.Equal(path, restored);
        Assert.IsType<PasteStep>(Assert.Single(MacroLibrary.Load(restored).Steps));
        Assert.Single(library.VersionsOf(restored));
        Assert.Empty(library.ListTrash());
    }

    [Fact]
    public void EmptyOldTrash_DeletesOnlyWhatIsOlderThanThirtyDays()
    {
        var library = new MacroLibrary(_folder);
        library.MoveToTrash(library.SaveNew(CreateMacro("Eski", [new CopyStep()])));
        DateTime deletedAt = Assert.Single(library.ListTrash()).DeletedAt;

        library.EmptyOldTrash(deletedAt + TimeSpan.FromDays(29));
        Assert.Single(library.ListTrash());

        library.EmptyOldTrash(deletedAt + TimeSpan.FromDays(31));
        Assert.Empty(library.ListTrash());
    }

    [Fact]
    public void LoadAll_ListsBrokenFilesSeparately()
    {
        var library = new MacroLibrary(_folder);
        library.SaveNew(CreateMacro("Good", [new CopyStep()]));
        File.WriteAllText(Path.Combine(_folder, "Broken" + MacroLibrary.FileExtension), "{ not json");

        (IReadOnlyList<MacroFile> macros, IReadOnlyList<string> unreadable) = library.LoadAll();

        Assert.Equal("Good", Assert.Single(macros).Macro.Name);
        Assert.EndsWith("Broken.msmacro", Assert.Single(unreadable), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsAMacroOutsideTheLimits()
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "Huge.msmacro");
        File.WriteAllText(path, """
            { "name": "Huge", "steps": [ { "type": "wait", "durationMs": 99999999 } ] }
            """);

        Assert.Throws<InvalidMacroFileException>(() => MacroLibrary.Load(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static MacroDefinition CreateMacro(string name, IReadOnlyList<MacroStep> steps) => new()
    {
        Name = name,
        CreatedAt = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.FromHours(3)),
        RecordedOn = new ScreenInfo(1920, 1080, 1.0),
        Steps = steps,
    };

    private static MacroStep[] AllStepTypes() =>
    [
        new MouseClickStep { Button = MouseButton.Left, ClickCount = 2, Modifiers = KeyModifiers.Control, Target = Excel, ScreenPosition = new ScreenPoint(300, 200), DelayBeforeMs = 120 },
        new MouseDragStep { Button = MouseButton.Left, Target = Excel, ScreenPath = [new ScreenPoint(1, 2), new ScreenPoint(30, 40)], DurationMs = 400 },
        new MouseWheelStep { Delta = -240, Target = Excel, ScreenPosition = new ScreenPoint(500, 400) },
        new KeyPressStep { VirtualKey = VirtualKeys.Down, ScanCode = 0x50, IsExtended = true, PressCount = 3, Target = Excel },
        new HotkeyStep { Modifiers = KeyModifiers.Control | KeyModifiers.Shift, VirtualKey = 0x53 },
        new TypeTextStep { Text = "Çağrı İş öğü", Target = Excel },
        new FocusWindowStep { Target = Excel },
        new CopyStep { Target = Excel },
        new PasteStep { Target = Excel, CleanText = false },
        new WaitStep { IsPasswordEntry = true, Note = "Parola" },
    ];
}
