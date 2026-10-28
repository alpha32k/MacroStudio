using MacroStudio.Core.Input;
using MacroStudio.Core.Settings;

namespace MacroStudio.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_folder, "settings.json");

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsDefaults()
    {
        var store = new SettingsStore(FilePath);

        AppSettings settings = store.Load(() => AppSettings.CreateDefault("tr"));

        Assert.Equal(AppLanguage.Turkish, settings.Language);
        Assert.Equal(AppSettings.DefaultNotchBarOpenDelayMs, settings.NotchBarOpenDelayMs);
    }

    [Fact]
    public void Save_ThenLoad_GivesTheSameSettings()
    {
        var store = new SettingsStore(FilePath);
        var saved = new AppSettings
        {
            Language = AppLanguage.Turkish,
            Theme = ThemePreference.Dark,
            AccentColor = "#107C10",
            ShowNotchBar = false,
            NotchBarMonitor = @"\\.\DISPLAY2",
            NotchBarPosition = NotchBarPosition.Right,
            NotchBarOpenDelayMs = 400,
            NotchBarPinned = true,
            OpenMainWindowShortcut = new KeyShortcut(KeyModifiers.Control | KeyModifiers.Shift, 0x4D),
            PlayPauseShortcut = new KeyShortcut(KeyModifiers.Windows, 0x77),
        };

        store.Save(saved);
        AppSettings loaded = store.Load(() => new AppSettings());

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void Load_ABrokenFileThatCannotBeKept_StillStartsWithDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ not json");

        // A folder where the backup copy should go makes the copy fail.
        Directory.CreateDirectory(FilePath + ".broken");
        AppSettings settings = new SettingsStore(FilePath).Load(() => AppSettings.CreateDefault("tr"));

        Assert.Equal(AppLanguage.Turkish, settings.Language);
    }

    [Fact]
    public void Save_LeavesNoTemporaryFile()
    {
        var store = new SettingsStore(FilePath);

        store.Save(new AppSettings());

        Assert.True(File.Exists(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_WhenFileIsBroken_ReturnsDefaultsAndKeepsACopy()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ this is not json");
        var store = new SettingsStore(FilePath);

        AppSettings settings = store.Load(() => AppSettings.CreateDefault("en"));

        Assert.Equal(new AppSettings(), settings);
        Assert.Equal("{ this is not json", File.ReadAllText(FilePath + ".broken"));
    }

    [Fact]
    public void Load_WhenValuesAreOutOfRange_FixesThem()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """
            {
              "theme": 7,
              "accentColor": "blue",
              "notchBarPosition": 42,
              "notchBarOpenDelayMs": 99999
            }
            """);
        var store = new SettingsStore(FilePath);

        AppSettings settings = store.Load(() => new AppSettings());

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Null(settings.AccentColor);
        Assert.Equal(NotchBarPosition.Center, settings.NotchBarPosition);
        Assert.Equal(AppSettings.MaxNotchBarOpenDelayMs, settings.NotchBarOpenDelayMs);
    }

    [Fact]
    public void Load_FileFromBeforeThePinButton_LeavesTheBarUnpinned()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """{ "schemaVersion": 1, "language": "Turkish" }""");
        var store = new SettingsStore(FilePath);

        AppSettings settings = store.Load(() => new AppSettings());

        Assert.False(settings.NotchBarPinned);
        Assert.Equal(AppLanguage.Turkish, settings.Language);
    }

    [Fact]
    public void Save_WritesEnumsAsReadableText()
    {
        var store = new SettingsStore(FilePath);

        store.Save(new AppSettings { Theme = ThemePreference.Dark });

        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(FilePath), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
