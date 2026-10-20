using System.Text;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Playback;
using MacroStudio.Core.Settings;

namespace MacroStudio.Core.Tests.Macros;

/// <summary>
/// Files written by an older version, or edited by hand, lack fields. A missing field must get its intended
/// default (step enabled, bar shown), not null, 0 or false.
/// </summary>
public sealed class MissingFieldsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "MacroStudio.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void MacroFile_WithOnlyTheRequiredFields_GetsTheDefaults()
    {
        MacroDefinition macro = MacroLibrary.Load(WriteFile("Minimal.msmacro", """
            { "name": "Minimal", "steps": [ { "type": "click", "button": "Left", "screenPosition": { "x": 1, "y": 2 } } ] }
            """));

        MouseClickStep click = Assert.IsType<MouseClickStep>(Assert.Single(macro.Steps));
        Assert.True(click.IsEnabled);
        Assert.Equal(1, click.ClickCount);
        Assert.NotEqual(Guid.Empty, click.Id);
        Assert.Equal(LoopSettings.UntilClipboardEmpty, macro.Loop);
        Assert.Equal(1, macro.SpeedMultiplier);
    }

    [Fact]
    public void SettingsFile_WithOnlySomeFields_KeepsTheOtherDefaults()
    {
        AppSettings settings = new SettingsStore(WriteFile("settings.json", """{ "language": "Turkish" }"""))
            .Load(() => new AppSettings());

        Assert.Equal(AppLanguage.Turkish, settings.Language);
        Assert.True(settings.ShowNotchBar);
        Assert.Equal(AppSettings.DefaultNotchBarOpenDelayMs, settings.NotchBarOpenDelayMs);
        Assert.Equal(3, settings.RecordingCountdownSeconds);
        Assert.True(settings.RecordingConvertCopyPaste && settings.RecordingConvertWindowSwitches && settings.RecordingMergeTyping);
        Assert.Equal((3, InterferencePolicy.Pause, 5), (settings.PlaybackCountdownSeconds, settings.PlaybackOnUserInput, settings.PlaybackWindowWaitSeconds));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string WriteFile(string name, string content)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}
