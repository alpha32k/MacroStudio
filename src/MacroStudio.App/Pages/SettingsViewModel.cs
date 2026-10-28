using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.App.Localization;
using MacroStudio.App.Services;
using MacroStudio.Core.Input;
using MacroStudio.Core.Playback;
using MacroStudio.Core.Settings;
using MacroStudio.Native.Displays;
using MacroStudio.Native.WindowsSettings;

namespace MacroStudio.App.Pages;

/// <summary>Every change is saved at once; there is no Save button.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ShortcutService _shortcuts;
    private readonly string _executablePath = Environment.ProcessPath!;
    private bool _isLoading = true;
    private string? _refusal;

    public SettingsViewModel(SettingsService settings, ShortcutService shortcuts)
    {
        _settings = settings;
        _shortcuts = shortcuts;
        AppSettings current = settings.Current;

        SelectedLanguage = Languages.First(o => o.Value == current.Language);
        SelectedTheme = Themes.First(o => o.Value == current.Theme);
        SelectedAccent = AccentColors.FirstOrDefault(o => string.Equals(o.Value, current.AccentColor, StringComparison.OrdinalIgnoreCase))
            ?? AccentColors[0];
        StartWithWindows = StartupRegistration.IsEnabled(_executablePath);
        ShowNotchBar = current.ShowNotchBar;
        SelectedPosition = Positions.First(o => o.Value == current.NotchBarPosition);
        OpenDelayMs = current.NotchBarOpenDelayMs;
        SelectedCountdown = Closest(Countdowns, current.RecordingCountdownSeconds);
        ConvertCopyPaste = current.RecordingConvertCopyPaste;
        ConvertWindowSwitches = current.RecordingConvertWindowSwitches;
        MergeTyping = current.RecordingMergeTyping;
        FindElements = current.RecordingFindElements;
        SelectedPlaybackCountdown = Closest(Countdowns, current.PlaybackCountdownSeconds);
        SelectedOnUserInput = UserInputPolicies.First(o => o.Value == current.PlaybackOnUserInput);
        SelectedWindowWait = Closest(WindowWaits, current.PlaybackWindowWaitSeconds);
        RefreshMonitors();
        LoadShortcuts();
        shortcuts.Changed += (_, _) => LoadShortcuts();

        _isLoading = false;
    }

    // Language names are written in their own language, so everyone finds theirs.
    public IReadOnlyList<LocalizedOption<AppLanguage>> Languages { get; } =
    [
        new(AppLanguage.English, _ => "English"),
        new(AppLanguage.Turkish, _ => "Türkçe"),
    ];

    public IReadOnlyList<LocalizedOption<ThemePreference>> Themes { get; } =
    [
        LocalizedOption<ThemePreference>.FromKey(ThemePreference.System, "Theme_System"),
        LocalizedOption<ThemePreference>.FromKey(ThemePreference.Light, "Theme_Light"),
        LocalizedOption<ThemePreference>.FromKey(ThemePreference.Dark, "Theme_Dark"),
    ];

    /// <summary>Null is the Windows accent color; the others are Windows' own accent palette.</summary>
    public IReadOnlyList<LocalizedOption<string?>> AccentColors { get; } =
    [
        LocalizedOption<string?>.FromKey(null, "Accent_Windows"),
        LocalizedOption<string?>.FromKey("#0078D4", "Accent_Blue"),
        LocalizedOption<string?>.FromKey("#0063B1", "Accent_Navy"),
        LocalizedOption<string?>.FromKey("#8764B8", "Accent_Purple"),
        LocalizedOption<string?>.FromKey("#E81123", "Accent_Red"),
        LocalizedOption<string?>.FromKey("#CA5010", "Accent_Orange"),
        LocalizedOption<string?>.FromKey("#107C10", "Accent_Green"),
        LocalizedOption<string?>.FromKey("#038387", "Accent_Teal"),
        LocalizedOption<string?>.FromKey("#69797E", "Accent_Graphite"),
    ];

    public IReadOnlyList<LocalizedOption<NotchBarPosition>> Positions { get; } =
    [
        LocalizedOption<NotchBarPosition>.FromKey(NotchBarPosition.Left, "Position_Left"),
        LocalizedOption<NotchBarPosition>.FromKey(NotchBarPosition.Center, "Position_Center"),
        LocalizedOption<NotchBarPosition>.FromKey(NotchBarPosition.Right, "Position_Right"),
    ];

    public IReadOnlyList<LocalizedOption<int>> Countdowns { get; } =
    [
        LocalizedOption<int>.FromKey(0, "Countdown_None"),
        .. new[] { 1, 2, 3, 5, AppSettings.MaxRecordingCountdownSeconds }
            .Select(seconds => new LocalizedOption<int>(seconds, l => l.Format("Countdown_Seconds", seconds))),
    ];

    public IReadOnlyList<LocalizedOption<InterferencePolicy>> UserInputPolicies { get; } =
    [
        LocalizedOption<InterferencePolicy>.FromKey(InterferencePolicy.Pause, "OnUserInput_Pause"),
        LocalizedOption<InterferencePolicy>.FromKey(InterferencePolicy.Stop, "OnUserInput_Stop"),
        LocalizedOption<InterferencePolicy>.FromKey(InterferencePolicy.Ignore, "OnUserInput_Ignore"),
    ];

    public IReadOnlyList<LocalizedOption<int>> WindowWaits { get; } =
        new[] { 2, 5, 10, 20, 30, AppSettings.MaxWindowWaitSeconds }
            .Select(seconds => new LocalizedOption<int>(seconds, l => l.Format("Countdown_Seconds", seconds)))
            .ToList();

    /// <summary>Null is "primary monitor"; the others are device names such as \\.\DISPLAY2.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LocalizedOption<string?>> Monitors { get; private set; } = [];

    [ObservableProperty]
    public partial LocalizedOption<AppLanguage> SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<ThemePreference> SelectedTheme { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<string?> SelectedAccent { get; set; }

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial bool ShowNotchBar { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<string?>? SelectedMonitor { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<NotchBarPosition> SelectedPosition { get; set; }

    [ObservableProperty]
    public partial double OpenDelayMs { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<int> SelectedCountdown { get; set; }

    [ObservableProperty]
    public partial bool ConvertCopyPaste { get; set; }

    [ObservableProperty]
    public partial bool ConvertWindowSwitches { get; set; }

    [ObservableProperty]
    public partial bool MergeTyping { get; set; }

    [ObservableProperty]
    public partial bool FindElements { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<int> SelectedPlaybackCountdown { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<InterferencePolicy> SelectedOnUserInput { get; set; }

    [ObservableProperty]
    public partial LocalizedOption<int> SelectedWindowWait { get; set; }

    // Instance properties, because XAML bindings cannot read static ones through the DataContext.
    public double MinOpenDelayMs => AppSettings.MinNotchBarOpenDelayMs;

    public double MaxOpenDelayMs => AppSettings.MaxNotchBarOpenDelayMs;

    [ObservableProperty]
    public partial KeyShortcut? OpenMainWindowShortcut { get; set; }

    [ObservableProperty]
    public partial KeyShortcut? ToggleRecordingShortcut { get; set; }

    [ObservableProperty]
    public partial KeyShortcut? PlayPauseShortcut { get; set; }

    [ObservableProperty]
    public partial KeyShortcut? EmergencyStopShortcut { get; set; }

    /// <summary>Why the last shortcut was refused, or which shortcut another program holds; empty when all is well.</summary>
    [ObservableProperty]
    public partial string ShortcutMessage { get; private set; } = "";

    [RelayCommand]
    private void ResetShortcuts()
    {
        _refusal = null;
        Save(s => s with
        {
            OpenMainWindowShortcut = AppSettings.DefaultOpenMainWindowShortcut,
            ToggleRecordingShortcut = AppSettings.DefaultToggleRecordingShortcut,
            PlayPauseShortcut = AppSettings.DefaultPlayPauseShortcut,
            EmergencyStopShortcut = AppSettings.DefaultEmergencyStopShortcut,
        });
        LoadShortcuts();
    }

    partial void OnOpenMainWindowShortcutChanged(KeyShortcut? value) =>
        ChangeShortcut(value, 0, (s, k) => s with { OpenMainWindowShortcut = k });

    partial void OnToggleRecordingShortcutChanged(KeyShortcut? value) =>
        ChangeShortcut(value, 1, (s, k) => s with { ToggleRecordingShortcut = k });

    partial void OnPlayPauseShortcutChanged(KeyShortcut? value) =>
        ChangeShortcut(value, 2, (s, k) => s with { PlayPauseShortcut = k });

    partial void OnEmergencyStopShortcutChanged(KeyShortcut? value) =>
        ChangeShortcut(value, 3, (s, k) => s with { EmergencyStopShortcut = k });

    /// <param name="index">Position in AppSettings.AppShortcuts; that shortcut does not conflict with itself.</param>
    private void ChangeShortcut(KeyShortcut? value, int index, Func<AppSettings, KeyShortcut, AppSettings> change)
    {
        if (_isLoading || value is not { } shortcut || shortcut == _settings.Current.AppShortcuts()[index])
        {
            return;
        }

        if (_shortcuts.FindOwner(shortcut, exceptAppShortcut: index) is { } owner)
        {
            _refusal = Localizer.Instance.Format("Shortcut_Taken", owner);
            LoadShortcuts();
            return;
        }

        _refusal = null;
        Save(s => change(s, shortcut));
    }

    private void LoadShortcuts()
    {
        bool wasLoading = _isLoading;
        _isLoading = true;
        AppSettings current = _settings.Current;
        OpenMainWindowShortcut = current.OpenMainWindowShortcut;
        ToggleRecordingShortcut = current.ToggleRecordingShortcut;
        PlayPauseShortcut = current.PlayPauseShortcut;
        EmergencyStopShortcut = current.EmergencyStopShortcut;
        _isLoading = wasLoading;

        // The stop shortcut is only registered while a macro plays, so it is not checked here.
        // A refusal stays until the next shortcut is taken: leaving the box registers the shortcuts again,
        // which comes back here.
        KeyShortcut[] held = [.. current.AppShortcuts().Take(3).Where(_shortcuts.Unavailable.Contains)];
        ShortcutMessage = _refusal ?? (held.Length == 0
            ? ""
            : string.Join(", ", held.Select(ShortcutService.NameOf)) + ": " + Localizer.Instance["Shortcut_HeldByOther"]);
    }

    /// <summary>Monitors can be plugged in or out while the app runs, so the list is rebuilt when the page opens.</summary>
    public void RefreshMonitors()
    {
        bool wasLoading = _isLoading;
        _isLoading = true;

        Monitors =
        [
            LocalizedOption<string?>.FromKey(null, "Monitor_Primary"),
            .. Native.Displays.Monitors.GetAll()
                .OrderBy(m => m.DisplayNumber)
                .Select(m => new LocalizedOption<string?>(
                    m.DeviceName,
                    l => l.Format("Monitor_Numbered", m.DisplayNumber, m.Bounds.Width, m.Bounds.Height))),
        ];
        SelectedMonitor = Monitors.FirstOrDefault(o => o.Value == _settings.Current.NotchBarMonitor) ?? Monitors[0];

        _isLoading = wasLoading;
    }

    partial void OnSelectedLanguageChanged(LocalizedOption<AppLanguage> value) =>
        Save(s => s with { Language = value.Value });

    partial void OnSelectedThemeChanged(LocalizedOption<ThemePreference> value) =>
        Save(s => s with { Theme = value.Value });

    partial void OnSelectedAccentChanged(LocalizedOption<string?> value) =>
        Save(s => s with { AccentColor = value.Value });

    partial void OnShowNotchBarChanged(bool value) =>
        Save(s => s with { ShowNotchBar = value });

    partial void OnSelectedMonitorChanged(LocalizedOption<string?>? value) =>
        Save(s => s with { NotchBarMonitor = value?.Value });

    partial void OnSelectedPositionChanged(LocalizedOption<NotchBarPosition> value) =>
        Save(s => s with { NotchBarPosition = value.Value });

    partial void OnOpenDelayMsChanged(double value) =>
        Save(s => s with { NotchBarOpenDelayMs = (int)Math.Round(value) });

    partial void OnSelectedCountdownChanged(LocalizedOption<int> value) =>
        Save(s => s with { RecordingCountdownSeconds = value.Value });

    partial void OnConvertCopyPasteChanged(bool value) =>
        Save(s => s with { RecordingConvertCopyPaste = value });

    partial void OnConvertWindowSwitchesChanged(bool value) =>
        Save(s => s with { RecordingConvertWindowSwitches = value });

    partial void OnMergeTypingChanged(bool value) =>
        Save(s => s with { RecordingMergeTyping = value });

    partial void OnFindElementsChanged(bool value) =>
        Save(s => s with { RecordingFindElements = value });

    partial void OnSelectedPlaybackCountdownChanged(LocalizedOption<int> value) =>
        Save(s => s with { PlaybackCountdownSeconds = value.Value });

    partial void OnSelectedOnUserInputChanged(LocalizedOption<InterferencePolicy> value) =>
        Save(s => s with { PlaybackOnUserInput = value.Value });

    partial void OnSelectedWindowWaitChanged(LocalizedOption<int> value) =>
        Save(s => s with { PlaybackWindowWaitSeconds = value.Value });

    /// <summary>A value edited by hand in settings.json may not be in the list; the nearest one is shown.</summary>
    private static LocalizedOption<int> Closest(IReadOnlyList<LocalizedOption<int>> options, int value) =>
        options.MinBy(option => Math.Abs(option.Value - value))!;

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        if (value)
        {
            StartupRegistration.Enable(_executablePath);
        }
        else
        {
            StartupRegistration.Disable();
        }
    }

    private void Save(Func<AppSettings, AppSettings> change)
    {
        if (!_isLoading)
        {
            _settings.Update(change);
        }
    }
}
