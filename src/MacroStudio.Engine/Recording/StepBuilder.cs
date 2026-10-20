using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;

namespace MacroStudio.Engine.Recording;

/// <summary>
/// Turns the raw events of a recording into macro steps, in order, each with the time since the previous step.
/// Pure logic without Windows calls, so every rule here is covered by unit tests.
/// </summary>
public sealed class StepBuilder(RecordingOptions options)
{
    // A longer pause while typing starts a new text step.
    private const int TypingPauseMs = 1000;

    // The same key pressed again within this time adds to the previous step (↓ ×5) instead of a new one.
    private const int KeyRepeatMs = 1000;

    // Wheel notches closer together than this become one step.
    private const int WheelMergeMs = 400;

    // A window that comes to the front this soon after a click into it was brought up by that click.
    private const int ClickActivationMs = 1000;

    // A window that comes to the front this soon after a taskbar click was chosen on the taskbar.
    private const int TaskbarSwitchMs = 1000;

    private const double DragPathTolerancePx = 2;

    private readonly List<MacroStep> _steps = [];
    private readonly Dictionary<MouseButton, PressedButton> _pressedButtons = [];

    private long? _lastStepEndMs;
    private nint _lastStepWindow;
    private TextRun? _typing;
    private bool _isTypingPassword;
    private bool _isSwitchingWindows;
    private int? _loneModifier;
    private long _lastMouseDownMs = long.MinValue / 2;
    private nint _lastMouseDownWindow;
    private long _lastClickUpMs;
    private int _taskbarClickIndex = -1;

    /// <summary>Mouse button, wheel events. Moves go to <see cref="AddMouseMove"/>.</summary>
    public void AddMouse(CapturedMouse input, WindowInfo window)
    {
        switch (input.Action)
        {
            case MouseAction.Down:
                _loneModifier = null;
                FlushTyping();
                _lastMouseDownMs = input.TimeMs;
                _lastMouseDownWindow = input.Window.Handle;
                _pressedButtons[input.Button] = new PressedButton(input, window);
                break;

            case MouseAction.Up when _pressedButtons.Remove(input.Button, out PressedButton? pressed):
                if (pressed.Window.Role != WindowRole.MacroStudio)
                {
                    AddClickOrDrag(pressed, input);
                }

                break;

            case MouseAction.Wheel or MouseAction.HorizontalWheel when window.Role != WindowRole.MacroStudio:
                _loneModifier = null;
                FlushTyping();
                AddWheel(input, window);
                break;
        }
    }

    /// <summary>Pointer moves; they only matter while a button is held, as the path of a drag.</summary>
    public void AddMouseMove(CapturedMouse input)
    {
        foreach (PressedButton pressed in _pressedButtons.Values)
        {
            pressed.Path.Add(input.Position);
        }
    }

    public void AddKey(CapturedKey input, WindowInfo window, bool isPasswordField)
    {
        if (VirtualKeys.IsModifier(input.VirtualKey))
        {
            AddModifierKey(input, window);
            return;
        }

        if (!input.IsDown || VirtualKeys.IsLock(input.VirtualKey))
        {
            return;
        }

        _loneModifier = null;
        if (window.Role == WindowRole.MacroStudio || _isSwitchingWindows || IsAppShortcut(input))
        {
            return;
        }

        if (options.ConvertWindowSwitches && IsWindowSwitchKey(input))
        {
            // Alt+Tab is not recorded; the window that comes to the front is (see AddForeground).
            FlushTyping();
            _isSwitchingWindows = true;
            return;
        }

        if (IsTypedText(input))
        {
            AddText(input, window, isPasswordField);
            return;
        }

        if (input.VirtualKey == VirtualKeys.Back && (_isTypingPassword || TryEraseTypedCharacter(input)))
        {
            return;
        }

        FlushTyping();
        _isTypingPassword = false;
        AddCommandKey(input, window);
    }

    public void AddForeground(CapturedForeground input, WindowInfo window)
    {
        FlushTyping();
        _isTypingPassword = false;
        if (window.Role != WindowRole.Application || !options.ConvertWindowSwitches)
        {
            return;
        }

        // The window was chosen on the taskbar. Taskbar buttons move, so the click becomes "bring to front".
        if (_taskbarClickIndex >= 0 && _taskbarClickIndex == _steps.Count - 1 && input.TimeMs - _lastClickUpMs <= TaskbarSwitchMs)
        {
            MacroStep click = _steps[^1];
            _taskbarClickIndex = -1;
            Replace(new FocusWindowStep { Id = click.Id, DelayBeforeMs = click.DelayBeforeMs, Target = window.Target }, input.TimeMs, input.Window.Handle);
            return;
        }

        // A click into the window brought it up; the click step already names the window.
        if (input.Window.Handle == _lastMouseDownWindow && input.TimeMs - _lastMouseDownMs <= ClickActivationMs)
        {
            return;
        }

        if (_steps is [.., FocusWindowStep] && _lastStepWindow == input.Window.Handle)
        {
            return;
        }

        Add(new FocusWindowStep { Target = window.Target }, input.TimeMs, input.TimeMs, input.Window.Handle);
    }

    public IReadOnlyList<MacroStep> Finish()
    {
        FlushTyping();
        _pressedButtons.Clear();
        return [.. _steps];
    }

    private static WindowTarget? TargetOf(WindowInfo window) =>
        window.Role == WindowRole.Application ? window.Target : null;

    private static int Distance(ScreenPoint a, ScreenPoint b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static bool IsWindowSwitchKey(CapturedKey input) =>
        input.VirtualKey is VirtualKeys.Tab or VirtualKeys.Escape
        && input.Modifiers.HasFlag(KeyModifiers.Alt)
        && !input.Modifiers.HasFlag(KeyModifiers.Control);

    private static bool IsTypedText(CapturedKey input)
    {
        if (input.Text is not { Length: > 0 } text || char.IsControl(text[0]))
        {
            return false;
        }

        // AltGr is reported as Ctrl+Alt; with it, keys type characters such as @ on a Turkish keyboard.
        KeyModifiers commandKeys = input.Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Windows);
        return commandKeys is KeyModifiers.None or (KeyModifiers.Control | KeyModifiers.Alt);
    }

    private bool IsAppShortcut(CapturedKey input) =>
        options.AppShortcuts.Contains(new KeyShortcut(input.Modifiers, input.VirtualKey));

    private bool IsSoonAfterLastStep(long timeMs, int withinMs) =>
        _lastStepEndMs is long last && timeMs - last <= withinMs;

    private void AddModifierKey(CapturedKey input, WindowInfo window)
    {
        if (input.IsDown)
        {
            // Holding a key repeats its down event; the same key alone stays a candidate.
            _loneModifier = input.Modifiers == KeyModifiers.None ? _loneModifier ?? input.VirtualKey : null;
            return;
        }

        if (VirtualKeys.IsAlt(input.VirtualKey))
        {
            _isSwitchingWindows = false;
        }

        bool wasPressedAlone = _loneModifier == input.VirtualKey;
        _loneModifier = null;

        // Alt alone shows the menu letters (the Excel ribbon keys), Windows alone opens Start.
        // Ctrl or Shift alone do nothing, so they are not recorded.
        bool doesSomethingAlone = VirtualKeys.IsAlt(input.VirtualKey) || VirtualKeys.IsWindows(input.VirtualKey);
        if (wasPressedAlone && doesSomethingAlone && window.Role != WindowRole.MacroStudio)
        {
            FlushTyping();
            AddCommandKey(input, window);
        }
    }

    private void AddText(CapturedKey input, WindowInfo window, bool isPasswordField)
    {
        if (isPasswordField)
        {
            // One "type the password yourself" step per password field; the characters are never kept.
            if (_isTypingPassword)
            {
                _lastStepEndMs = input.TimeMs;
                return;
            }

            FlushTyping();
            Add(new WaitStep { IsPasswordEntry = true }, input.TimeMs, input.TimeMs, input.Window.Handle);
            _isTypingPassword = true;
            return;
        }

        _isTypingPassword = false;
        if (_typing is not null
            && options.MergeTyping
            && _typing.Window == input.Window.Handle
            && input.TimeMs - _typing.LastKeyMs <= TypingPauseMs)
        {
            _typing.Append(input.Text!, input.TimeMs);
            return;
        }

        FlushTyping();
        _typing = new TextRun(input.Window.Handle, TargetOf(window), input.TimeMs, input.Text!);
    }

    private bool TryEraseTypedCharacter(CapturedKey input)
    {
        if (_typing is null || _typing.Window != input.Window.Handle || !_typing.TryRemoveLast())
        {
            return false;
        }

        _typing.LastKeyMs = input.TimeMs;
        return true;
    }

    private void FlushTyping()
    {
        if (_typing is not { } run)
        {
            return;
        }

        _typing = null;
        string text = run.Text;
        if (text.Length == 0)
        {
            // Everything typed was erased again.
            _lastStepEndMs = run.LastKeyMs;
            return;
        }

        Add(new TypeTextStep { Text = text, Target = run.Target }, run.StartMs, run.LastKeyMs, run.Window);
    }

    private void AddCommandKey(CapturedKey input, WindowInfo window)
    {
        WindowTarget? target = TargetOf(window);
        long time = input.TimeMs;
        nint handle = input.Window.Handle;

        if (options.ConvertCopyPaste && input.Modifiers == KeyModifiers.Control)
        {
            if (input.VirtualKey == VirtualKeys.C)
            {
                Add(new CopyStep { Target = target }, time, time, handle);
                return;
            }

            if (input.VirtualKey == VirtualKeys.V)
            {
                Add(new PasteStep { Target = target }, time, time, handle);
                return;
            }
        }

        bool isRepeat = _lastStepWindow == handle && IsSoonAfterLastStep(time, KeyRepeatMs);
        if (input.Modifiers == KeyModifiers.None)
        {
            if (isRepeat && _steps is [.., KeyPressStep previous]
                && previous.VirtualKey == input.VirtualKey
                && previous.PressCount < MacroValidator.MaxPressCount)
            {
                Replace(previous with { PressCount = previous.PressCount + 1 }, time);
                return;
            }

            Add(new KeyPressStep { VirtualKey = input.VirtualKey, ScanCode = input.ScanCode, IsExtended = input.IsExtended, Target = target }, time, time, handle);
            return;
        }

        if (isRepeat && _steps is [.., HotkeyStep previousHotkey]
            && previousHotkey.VirtualKey == input.VirtualKey
            && previousHotkey.Modifiers == input.Modifiers
            && previousHotkey.PressCount < MacroValidator.MaxPressCount)
        {
            Replace(previousHotkey with { PressCount = previousHotkey.PressCount + 1 }, time);
            return;
        }

        Add(new HotkeyStep { Modifiers = input.Modifiers, VirtualKey = input.VirtualKey, ScanCode = input.ScanCode, IsExtended = input.IsExtended, Target = target }, time, time, handle);
    }

    private void AddClickOrDrag(PressedButton pressed, CapturedMouse up)
    {
        CapturedMouse down = pressed.Down;
        WindowTarget? target = TargetOf(pressed.Window);
        nint handle = down.Window.Handle;
        pressed.Path.Add(up.Position);

        if (Distance(down.Position, up.Position) > options.DragThresholdPx)
        {
            _taskbarClickIndex = -1;
            Add(
                new MouseDragStep
                {
                    Button = down.Button,
                    Modifiers = down.Modifiers,
                    Target = target,
                    ScreenPath = PathSimplifier.Simplify(pressed.Path, DragPathTolerancePx),
                    DurationMs = (int)(up.TimeMs - down.TimeMs),
                },
                down.TimeMs,
                up.TimeMs,
                handle);
            return;
        }

        if (_steps is [.., MouseClickStep { ClickCount: 1 } previous]
            && previous.Button == down.Button
            && _lastStepWindow == handle
            && down.TimeMs - _lastClickUpMs <= options.DoubleClickTimeMs
            && Distance(previous.ScreenPosition, down.Position) <= options.DragThresholdPx)
        {
            _taskbarClickIndex = -1;
            Replace(previous with { ClickCount = 2 }, up.TimeMs);
            return;
        }

        Add(new MouseClickStep { Button = down.Button, Modifiers = down.Modifiers, Target = target, ScreenPosition = down.Position }, down.TimeMs, up.TimeMs, handle);
        _lastClickUpMs = up.TimeMs;
        _taskbarClickIndex = pressed.Window.Role == WindowRole.Taskbar && down.Button == MouseButton.Left ? _steps.Count - 1 : -1;
    }

    private void AddWheel(CapturedMouse input, WindowInfo window)
    {
        bool isHorizontal = input.Action == MouseAction.HorizontalWheel;
        if (_steps is [.., MouseWheelStep previous]
            && previous.IsHorizontal == isHorizontal
            && previous.Modifiers == input.Modifiers
            && Math.Sign(previous.Delta) == Math.Sign(input.WheelDelta)
            && _lastStepWindow == input.Window.Handle
            && IsSoonAfterLastStep(input.TimeMs, WheelMergeMs))
        {
            Replace(previous with { Delta = previous.Delta + input.WheelDelta }, input.TimeMs);
            return;
        }

        Add(
            new MouseWheelStep
            {
                Delta = input.WheelDelta,
                IsHorizontal = isHorizontal,
                Modifiers = input.Modifiers,
                Target = TargetOf(window),
                ScreenPosition = input.Position,
            },
            input.TimeMs,
            input.TimeMs,
            input.Window.Handle);
    }

    private void Add(MacroStep step, long startMs, long endMs, nint window)
    {
        int delay = _lastStepEndMs is long last ? (int)Math.Clamp(startMs - last, 0, MacroValidator.MaxDelayMs) : 0;
        _steps.Add(step with { DelayBeforeMs = delay });
        _lastStepEndMs = endMs;
        _lastStepWindow = window;
    }

    private void Replace(MacroStep step, long endMs, nint? window = null)
    {
        _steps[^1] = step;
        _lastStepEndMs = endMs;
        if (window is nint handle)
        {
            _lastStepWindow = handle;
        }
    }
}
