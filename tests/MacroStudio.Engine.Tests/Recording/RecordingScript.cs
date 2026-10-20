using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;
using MacroStudio.Engine.Recording;

namespace MacroStudio.Engine.Tests.Recording;

/// <summary>
/// Feeds a <see cref="StepBuilder"/> with events in the order Windows sends them, for example
/// Ctrl+C as "Ctrl down, C down, C up, Ctrl up", with a clock that moves forward between actions.
/// </summary>
internal sealed class RecordingScript(RecordingOptions? options = null)
{
    public static readonly TestWindow Excel = Application(100, "EXCEL", "Kitap1 - Excel", "XLMAIN");
    public static readonly TestWindow Sap = Application(200, "saplogon", "SAP Easy Access", "SAP_FRONTEND_SESSION");
    public static readonly TestWindow Dialog = Application(250, "saplogon", "Bilgi", "#32770");
    public static readonly TestWindow Taskbar = Shell(300, WindowRole.Taskbar, "Shell_TrayWnd");
    public static readonly TestWindow Desktop = Shell(310, WindowRole.Desktop, "Progman");
    public static readonly TestWindow Bar = Shell(400, WindowRole.MacroStudio, "HwndWrapper");

    private readonly StepBuilder _builder = new(options ?? new RecordingOptions());
    private long _timeMs;

    public long TimeMs => _timeMs;

    public RecordingScript Wait(int milliseconds)
    {
        _timeMs += milliseconds;
        return this;
    }

    public RecordingScript Click(TestWindow window, int x, int y, MouseButton button = MouseButton.Left, KeyModifiers modifiers = KeyModifiers.None)
    {
        Mouse(window, MouseAction.Down, button, new ScreenPoint(x, y), modifiers);
        _timeMs += 60;
        Mouse(window, MouseAction.Up, button, new ScreenPoint(x, y), modifiers);
        return this;
    }

    public RecordingScript Drag(TestWindow window, ScreenPoint from, ScreenPoint to, int moves = 20)
    {
        Mouse(window, MouseAction.Down, MouseButton.Left, from);
        for (int i = 1; i <= moves; i++)
        {
            _timeMs += 10;
            var point = new ScreenPoint(from.X + ((to.X - from.X) * i / moves), from.Y + ((to.Y - from.Y) * i / moves));
            _builder.AddMouseMove(new CapturedMouse(_timeMs, MouseAction.Move, MouseButton.Left, point, 0, KeyModifiers.None, window.Captured));
        }

        Mouse(window, MouseAction.Up, MouseButton.Left, to);
        return this;
    }

    public RecordingScript Wheel(TestWindow window, int delta, KeyModifiers modifiers = KeyModifiers.None)
    {
        _builder.AddMouse(new CapturedMouse(_timeMs, MouseAction.Wheel, MouseButton.Left, new ScreenPoint(500, 400), delta, modifiers, window.Captured), window.Info);
        return this;
    }

    /// <summary>Presses and releases one key; <paramref name="text"/> is what the key types, if anything.</summary>
    public RecordingScript Key(TestWindow window, int virtualKey, string? text = null, KeyModifiers modifiers = KeyModifiers.None, bool isPasswordField = false)
    {
        KeyEvent(window, virtualKey, isDown: true, text, modifiers, isPasswordField);
        _timeMs += 30;
        KeyEvent(window, virtualKey, isDown: false, text: null, modifiers, isPasswordField);
        _timeMs += 30;
        return this;
    }

    public RecordingScript Type(TestWindow window, string text, bool isPasswordField = false)
    {
        foreach (char character in text)
        {
            int virtualKey = char.IsAsciiLetterOrDigit(character) ? char.ToUpperInvariant(character) : 0xBA;
            Key(window, virtualKey, character.ToString(), KeyModifiers.None, isPasswordField);
        }

        return this;
    }

    /// <summary>Holds the modifiers, presses the key, releases everything: Ctrl+C, Shift+↓, Ctrl+Alt+R …</summary>
    public RecordingScript Chord(TestWindow window, KeyModifiers modifiers, int virtualKey, string? text = null)
    {
        int[] modifierKeys = ModifierKeys(modifiers);
        KeyModifiers held = KeyModifiers.None;
        foreach (int modifierKey in modifierKeys)
        {
            KeyEvent(window, modifierKey, isDown: true, text: null, held);
            held |= ToModifier(modifierKey);
            _timeMs += 20;
        }

        Key(window, virtualKey, text, held);
        foreach (int modifierKey in modifierKeys.Reverse())
        {
            held &= ~ToModifier(modifierKey);
            KeyEvent(window, modifierKey, isDown: false, text: null, held);
            _timeMs += 20;
        }

        return this;
    }

    /// <summary>Alt+Tab: the keys go to the old window, then the new one comes to the front.</summary>
    public RecordingScript AltTab(TestWindow from, TestWindow to)
    {
        KeyEvent(from, VirtualKeys.LeftMenu, isDown: true, text: null, KeyModifiers.None);
        _timeMs += 50;
        Key(from, VirtualKeys.Tab, text: null, KeyModifiers.Alt);
        _timeMs += 200;
        KeyEvent(from, VirtualKeys.LeftMenu, isDown: false, text: null, KeyModifiers.None);
        _timeMs += 10;
        return Foreground(to);
    }

    public RecordingScript Foreground(TestWindow window)
    {
        _builder.AddForeground(new CapturedForeground(_timeMs, window.Captured), window.Info);
        return this;
    }

    public IReadOnlyList<MacroStep> Finish() => _builder.Finish();

    private static TestWindow Application(nint handle, string process, string title, string className) =>
        new(handle, new WindowInfo(WindowRole.Application, new WindowTarget(process, title, className, new ScreenRect(0, 0, 1200, 800))));

    private static TestWindow Shell(nint handle, WindowRole role, string className) =>
        new(handle, new WindowInfo(role, new WindowTarget("explorer", "", className, new ScreenRect(0, 1040, 1920, 1080))));

    private static int[] ModifierKeys(KeyModifiers modifiers)
    {
        var keys = new List<int>();
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            keys.Add(VirtualKeys.LeftControl);
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            keys.Add(VirtualKeys.LeftMenu);
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            keys.Add(VirtualKeys.LeftShift);
        }

        if (modifiers.HasFlag(KeyModifiers.Windows))
        {
            keys.Add(VirtualKeys.LeftWindows);
        }

        return [.. keys];
    }

    private static KeyModifiers ToModifier(int virtualKey) => virtualKey switch
    {
        VirtualKeys.LeftControl => KeyModifiers.Control,
        VirtualKeys.LeftMenu => KeyModifiers.Alt,
        VirtualKeys.LeftShift => KeyModifiers.Shift,
        _ => KeyModifiers.Windows,
    };

    private void Mouse(TestWindow window, MouseAction action, MouseButton button, ScreenPoint point, KeyModifiers modifiers = KeyModifiers.None) =>
        _builder.AddMouse(new CapturedMouse(_timeMs, action, button, point, 0, modifiers, window.Captured), window.Info);

    private void KeyEvent(TestWindow window, int virtualKey, bool isDown, string? text, KeyModifiers modifiers, bool isPasswordField = false) =>
        _builder.AddKey(new CapturedKey(_timeMs, virtualKey, 0, false, isDown, text, modifiers, window.Captured), window.Info, isPasswordField);
}

internal sealed record TestWindow(nint Handle, WindowInfo Info)
{
    public CapturedWindow Captured => new(Handle, Info.Target.Bounds);
}
