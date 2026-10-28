using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Settings;
using Serilog;

namespace MacroStudio.App.Services;

/// <summary>
/// Macro Studio's system-wide shortcuts: its own (changeable in the settings) and the ones given to macros.
/// Registers them again whenever the settings or the macros change, and tells the user once about each shortcut
/// another program already holds.
/// </summary>
public sealed class ShortcutService(HotkeyService hotkeys, SettingsService settings, MacroLibraryService library)
{
    private readonly List<int> _registered = [];
    private readonly HashSet<KeyShortcut> _warned = [];
    private AppActions? _actions;
    private Action<string>? _warn;
    private bool _isSuspended;

    /// <summary>The shortcuts were registered again; <see cref="Unavailable"/> may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Shortcuts that could not be registered because another program holds them.</summary>
    public IReadOnlySet<KeyShortcut> Unavailable { get; private set; } = new HashSet<KeyShortcut>();

    public KeyShortcut EmergencyStop => settings.Current.EmergencyStopShortcut;

    /// <summary>
    /// Every shortcut that belongs to Macro Studio: it is not recorded into a macro and does not count as
    /// "the user touched the keyboard" while a macro plays.
    /// </summary>
    public IReadOnlySet<KeyShortcut> Reserved =>
        settings.Current.AppShortcuts().Concat(MacroShortcuts().Select(m => m.Shortcut)).ToHashSet();

    public static string NameOf(KeyShortcut shortcut) => KeyNames.WithModifiers(shortcut.Modifiers, shortcut.VirtualKey);

    /// <param name="warn">Shows a short warning to the user, for example as a tray balloon.</param>
    public void Start(AppActions actions, Action<string> warn)
    {
        _actions = actions;
        _warn = warn;
        settings.Changed += (_, _) => Register();
        library.Refreshed += (_, _) => Register();
        Register();
    }

    /// <summary>
    /// What else already uses this shortcut: one of Macro Studio's own (its text key), a macro's name, or null.
    /// </summary>
    /// <param name="exceptMacro">The macro being edited; its own shortcut is no conflict.</param>
    public string? FindOwner(KeyShortcut shortcut, Guid? exceptMacro = null, int? exceptAppShortcut = null)
    {
        IReadOnlyList<KeyShortcut> own = settings.Current.AppShortcuts();
        for (int i = 0; i < own.Count; i++)
        {
            if (own[i] == shortcut && i != exceptAppShortcut)
            {
                return Localizer.Instance[AppShortcutNameKeys[i]];
            }
        }

        return MacroShortcuts().FirstOrDefault(m => m.Shortcut == shortcut && m.Macro.Id != exceptMacro).Macro?.Name;
    }

    /// <summary>Text keys for the names of Macro Studio's own shortcuts, in the order of AppSettings.AppShortcuts.</summary>
    public static IReadOnlyList<string> AppShortcutNameKeys { get; } =
        ["Shortcut_OpenMainWindow", "Shortcut_ToggleRecording", "Shortcut_PlayPause", "Shortcut_EmergencyStop"];

    private IEnumerable<(MacroDefinition Macro, KeyShortcut Shortcut)> MacroShortcuts() =>
        library.Macros
            .Where(m => m.Macro.Shortcut is { IsUsable: true })
            .Select(m => (m.Macro, m.Macro.Shortcut!.Value));

    /// <summary>Switches every shortcut off while a shortcut box takes new keys, and back on afterwards.</summary>
    public void Suspend(bool isSuspended)
    {
        _isSuspended = isSuspended;
        if (isSuspended)
        {
            UnregisterAll();
        }
        else
        {
            Register();
        }
    }

    private void UnregisterAll()
    {
        foreach (int id in _registered)
        {
            hotkeys.Unregister(id);
        }

        _registered.Clear();
    }

    private void Register()
    {
        if (_actions is not { } actions || _isSuspended)
        {
            return;
        }

        UnregisterAll();
        var unavailable = new HashSet<KeyShortcut>();
        var taken = new HashSet<KeyShortcut>();
        AppSettings current = settings.Current;

        void Add(KeyShortcut shortcut, Action action, string name)
        {
            // Two of our own on the same keys: the first one keeps it.
            if (!taken.Add(shortcut))
            {
                return;
            }

            if (hotkeys.TryRegister(shortcut, action) is int id)
            {
                _registered.Add(id);
                return;
            }

            unavailable.Add(shortcut);
            if (_warned.Add(shortcut))
            {
                Log.Warning("Shortcut {Shortcut} ({Name}) is used by another program", NameOf(shortcut), name);
                _warn?.Invoke(Localizer.Instance.Format("Hotkey_InUse", NameOf(shortcut)));
            }
        }

        Add(current.OpenMainWindowShortcut, actions.OpenMainWindow, "open main window");
        Add(current.ToggleRecordingShortcut, actions.ToggleRecording, "record");
        Add(current.PlayPauseShortcut, actions.PlayPause, "play/pause");

        // Only reserved here: it is registered while a macro plays (see PlaybackController).
        taken.Add(current.EmergencyStopShortcut);

        foreach ((MacroDefinition macro, KeyShortcut shortcut) in MacroShortcuts().ToList())
        {
            Guid id = macro.Id;
            Add(shortcut, () => actions.PlayMacro(id), "macro");
        }

        Unavailable = unavailable;
        Localizer.Instance.SetTokens(new Dictionary<string, string>
        {
            ["OpenMainWindow"] = NameOf(current.OpenMainWindowShortcut),
            ["ToggleRecording"] = NameOf(current.ToggleRecordingShortcut),
            ["PlayPause"] = NameOf(current.PlayPauseShortcut),
            ["EmergencyStop"] = NameOf(current.EmergencyStopShortcut),
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What the shortcuts do; given by the app at start-up.</summary>
    public sealed record AppActions(Action OpenMainWindow, Action ToggleRecording, Action PlayPause, Action<Guid> PlayMacro);
}
