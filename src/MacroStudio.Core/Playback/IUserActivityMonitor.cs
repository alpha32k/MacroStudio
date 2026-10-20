using MacroStudio.Core.Input;

namespace MacroStudio.Core.Playback;

/// <summary>Watches the real mouse and keyboard while a macro plays; the macro's own input is not reported.</summary>
public interface IUserActivityMonitor
{
    /// <summary>The user moved the mouse, clicked or pressed a key. Raised on a background thread.</summary>
    event EventHandler? UserInput;

    /// <summary>The user pressed Esc. The key does not reach the program. Raised on a background thread.</summary>
    event EventHandler? StopRequested;

    /// <param name="ignoredShortcuts">Macro Studio's own shortcuts, such as Ctrl+Alt+P to pause; they are not user input.</param>
    void Start(IReadOnlySet<KeyShortcut> ignoredShortcuts);

    void Stop();
}
