using MacroStudio.Core.Input;
using MacroStudio.Core.Settings;

namespace MacroStudio.Core.Tests.Settings;

public sealed class AppSettingsTests
{
    [Theory]
    [InlineData("tr", AppLanguage.Turkish)]
    [InlineData("TR", AppLanguage.Turkish)]
    [InlineData("en", AppLanguage.English)]
    [InlineData("de", AppLanguage.English)]
    public void CreateDefault_PicksTurkishOnlyForTurkishWindows(string windowsLanguage, AppLanguage expected)
    {
        Assert.Equal(expected, AppSettings.CreateDefault(windowsLanguage).Language);
    }

    [Theory]
    [InlineData("#0078D4", true)]
    [InlineData("#0078d4", true)]
    [InlineData("0078D4", false)]
    [InlineData("#0078D", false)]
    [InlineData("#0078D4F", false)]
    [InlineData("#GG78D4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsHexColor_AcceptsOnlyHashAndSixHexDigits(string? value, bool expected)
    {
        Assert.Equal(expected, AppSettings.IsHexColor(value));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 3)]
    [InlineData(60, AppSettings.MaxRecordingCountdownSeconds)]
    public void Normalize_KeepsTheCountdownInRange(int seconds, int expected)
    {
        var settings = new AppSettings { RecordingCountdownSeconds = seconds };

        Assert.Equal(expected, settings.Normalize().RecordingCountdownSeconds);
    }

    [Theory]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt, 0x52, true)]
    [InlineData(KeyModifiers.Windows, 0x77, true)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift, 0x31, true)]
    [InlineData(KeyModifiers.Shift, 0x41, false)]
    [InlineData(KeyModifiers.None, 0x41, false)]
    [InlineData(KeyModifiers.Control, 0xA2, false)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Alt, 0x14, false)]
    [InlineData(KeyModifiers.Control, 0, false)]
    public void KeyShortcut_IsUsableOnlyWithCtrlAltOrWinAndARealKey(KeyModifiers modifiers, int virtualKey, bool expected)
    {
        Assert.Equal(expected, new KeyShortcut(modifiers, virtualKey).IsUsable);
    }

    [Fact]
    public void Normalize_PutsBackTheDefaultForAnUnusableShortcut()
    {
        var settings = new AppSettings
        {
            ToggleRecordingShortcut = new KeyShortcut(KeyModifiers.Shift, 0x41),
            PlayPauseShortcut = new KeyShortcut(KeyModifiers.Control | KeyModifiers.Shift, 0x50),
        };

        AppSettings normalized = settings.Normalize();

        Assert.Equal(AppSettings.DefaultToggleRecordingShortcut, normalized.ToggleRecordingShortcut);
        Assert.Equal(settings.PlayPauseShortcut, normalized.PlayPauseShortcut);
    }

    [Theory]
    [InlineData(-5, AppSettings.MinNotchBarOpenDelayMs)]
    [InlineData(250, 250)]
    [InlineData(5000, AppSettings.MaxNotchBarOpenDelayMs)]
    public void Normalize_KeepsTheOpenDelayInRange(int delay, int expected)
    {
        var settings = new AppSettings { NotchBarOpenDelayMs = delay };

        Assert.Equal(expected, settings.Normalize().NotchBarOpenDelayMs);
    }
}
