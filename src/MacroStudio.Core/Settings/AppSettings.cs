using MacroStudio.Core.Input;
using MacroStudio.Core.Playback;

namespace MacroStudio.Core.Settings;

/// <summary>
/// User preferences stored in settings.json.
/// Properties use "set", not "init": the JSON source generator resets a missing init-only property to
/// null/0/false, so a settings file from an older version would hide the bar.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const int MinNotchBarOpenDelayMs = 0;
    public const int MaxNotchBarOpenDelayMs = 1000;
    public const int DefaultNotchBarOpenDelayMs = 250;
    public const int MaxRecordingCountdownSeconds = 10;
    public const int MinWindowWaitSeconds = 1;
    public const int MaxWindowWaitSeconds = 60;

    private const KeyModifiers ControlAlt = KeyModifiers.Control | KeyModifiers.Alt;

    public static KeyShortcut DefaultOpenMainWindowShortcut { get; } = new(ControlAlt, 0x4D);

    public static KeyShortcut DefaultToggleRecordingShortcut { get; } = new(ControlAlt, 0x52);

    public static KeyShortcut DefaultPlayPauseShortcut { get; } = new(ControlAlt, 0x50);

    /// <summary>Ctrl+Alt+End; registered only while a macro plays.</summary>
    public static KeyShortcut DefaultEmergencyStopShortcut { get; } = new(ControlAlt, 0x23);

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public AppLanguage Language { get; set; } = AppLanguage.English;

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Custom accent color as "#RRGGBB"; null follows the Windows accent color.</summary>
    public string? AccentColor { get; set; }

    public bool ShowNotchBar { get; set; } = true;

    /// <summary>Monitor device name such as \\.\DISPLAY2; null means the primary monitor.</summary>
    public string? NotchBarMonitor { get; set; }

    public NotchBarPosition NotchBarPosition { get; set; } = NotchBarPosition.Center;

    public int NotchBarOpenDelayMs { get; set; } = DefaultNotchBarOpenDelayMs;

    /// <summary>The user pinned the bar open with the pin button; it then stays open without the pointer on it.</summary>
    public bool NotchBarPinned { get; set; }

    /// <summary>Seconds to switch to the starting window before a recording begins; 0 starts at once.</summary>
    public int RecordingCountdownSeconds { get; set; } = 3;

    public bool RecordingConvertCopyPaste { get; set; } = true;

    public bool RecordingConvertWindowSwitches { get; set; } = true;

    public bool RecordingMergeTyping { get; set; } = true;

    /// <summary>Clicks also note the clicked control's name, so playback finds a button that moved.</summary>
    public bool RecordingFindElements { get; set; } = true;

    /// <summary>Seconds to take the hands off the mouse and keyboard before a macro starts; 0 starts at once.</summary>
    public int PlaybackCountdownSeconds { get; set; } = 3;

    public InterferencePolicy PlaybackOnUserInput { get; set; } = InterferencePolicy.Pause;

    /// <summary>How long playback waits for a program or dialog to open before it stops.</summary>
    public int PlaybackWindowWaitSeconds { get; set; } = 5;

    public KeyShortcut OpenMainWindowShortcut { get; set; } = DefaultOpenMainWindowShortcut;

    public KeyShortcut ToggleRecordingShortcut { get; set; } = DefaultToggleRecordingShortcut;

    public KeyShortcut PlayPauseShortcut { get; set; } = DefaultPlayPauseShortcut;

    public KeyShortcut EmergencyStopShortcut { get; set; } = DefaultEmergencyStopShortcut;

    /// <summary>Macro Studio's own shortcuts, in a fixed order.</summary>
    public IReadOnlyList<KeyShortcut> AppShortcuts() =>
        [OpenMainWindowShortcut, ToggleRecordingShortcut, PlayPauseShortcut, EmergencyStopShortcut];

    /// <summary>First-run settings: Turkish when Windows is in Turkish, otherwise English.</summary>
    public static AppSettings CreateDefault(string windowsLanguageCode) => new()
    {
        Language = string.Equals(windowsLanguageCode, "tr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Turkish
            : AppLanguage.English,
    };

    /// <summary>Returns a copy in which every value is inside its allowed range.</summary>
    public AppSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Language = Enum.IsDefined(Language) ? Language : AppLanguage.English,
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
        AccentColor = IsHexColor(AccentColor) ? AccentColor : null,
        NotchBarPosition = Enum.IsDefined(NotchBarPosition) ? NotchBarPosition : NotchBarPosition.Center,
        NotchBarOpenDelayMs = Math.Clamp(NotchBarOpenDelayMs, MinNotchBarOpenDelayMs, MaxNotchBarOpenDelayMs),
        RecordingCountdownSeconds = Math.Clamp(RecordingCountdownSeconds, 0, MaxRecordingCountdownSeconds),
        PlaybackCountdownSeconds = Math.Clamp(PlaybackCountdownSeconds, 0, MaxRecordingCountdownSeconds),
        PlaybackOnUserInput = Enum.IsDefined(PlaybackOnUserInput) ? PlaybackOnUserInput : InterferencePolicy.Pause,
        PlaybackWindowWaitSeconds = Math.Clamp(PlaybackWindowWaitSeconds, MinWindowWaitSeconds, MaxWindowWaitSeconds),
        OpenMainWindowShortcut = OpenMainWindowShortcut.IsUsable ? OpenMainWindowShortcut : DefaultOpenMainWindowShortcut,
        ToggleRecordingShortcut = ToggleRecordingShortcut.IsUsable ? ToggleRecordingShortcut : DefaultToggleRecordingShortcut,
        PlayPauseShortcut = PlayPauseShortcut.IsUsable ? PlayPauseShortcut : DefaultPlayPauseShortcut,
        EmergencyStopShortcut = EmergencyStopShortcut.IsUsable ? EmergencyStopShortcut : DefaultEmergencyStopShortcut,
    };

    public static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(char.IsAsciiHexDigit);
}
