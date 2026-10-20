using MacroStudio.Core.Geometry;
using MacroStudio.Core.Input;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Playback;

namespace MacroStudio.Engine.Playback;

/// <summary>
/// Plays a macro: waits the recorded time before each step (divided by the speed), finds the step's window,
/// brings it to the front and sends the input. Runs on a background thread; Pause and Resume may be called
/// from any thread. A pause only takes effect between steps, never in the middle of one, so no key is left held.
/// </summary>
public sealed class MacroPlayer(IInputSimulator input, IWindowSystem windows, IClipboard clipboard, IPlaybackClock clock)
{
    private const int WheelNotch = 120;
    private const int ScanCodeC = 0x2E;
    private const int ScanCodeV = 0x2F;
    private const int ClipboardReadAttempts = 10;

    // Short fixed pauses inside a step: programs need them to see a press and a release as two events.
    private static readonly TimeSpan MouseSettle = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan ButtonHold = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan DoubleClickGap = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan KeyGap = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan CharacterGap = TimeSpan.FromMilliseconds(4);
    private static readonly TimeSpan WheelNotchGap = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan ClipboardPoll = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan WindowPoll = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PauseCheck = TimeSpan.FromMilliseconds(50);
    private const double DragStepPx = 20;

    // Modifier keys in pressing order; they are released in reverse.
    private static readonly (KeyModifiers Modifier, int VirtualKey, int ScanCode, bool IsExtended)[] ModifierKeys =
    [
        (KeyModifiers.Control, VirtualKeys.LeftControl, 0x1D, false),
        (KeyModifiers.Alt, VirtualKeys.LeftMenu, 0x38, false),
        (KeyModifiers.Shift, VirtualKeys.LeftShift, 0x2A, false),
        (KeyModifiers.Windows, VirtualKeys.LeftWindows, 0x5B, true),
    ];

    private readonly ManualResetEventSlim _running = new(initialState: true);

    /// <summary>Raised before each step, on the playback thread.</summary>
    public event EventHandler<PlaybackProgress>? ProgressChanged;

    /// <summary>
    /// Playback reached a password field and paused itself; <see cref="Resume"/> continues after the user typed
    /// the password. Carries the step number. Raised on the playback thread.
    /// </summary>
    public event EventHandler<int>? PasswordNeeded;

    public bool IsPaused => !_running.IsSet;

    public void Pause() => _running.Reset();

    public void Resume() => _running.Set();

    public PlaybackResult Play(MacroDefinition macro, PlaybackOptions options, CancellationToken cancellation)
    {
        _running.Set();
        var steps = macro.Steps
            .Select((step, index) => (Number: index + 1, Step: step))
            .Where(item => item.Step.IsEnabled)
            .ToList();

        bool endsOnEmptyCopy = options.Loop.Mode == LoopMode.UntilClipboardEmpty && steps.Any(item => item.Step is CopyStep);
        int rounds = options.Loop.Mode switch
        {
            LoopMode.Count => options.Loop.Count,
            LoopMode.Infinite => int.MaxValue,

            // Without a copy step the list can never end, so the macro plays once.
            _ => endsOnEmptyCopy ? int.MaxValue : 1,
        };
        int? totalRounds = options.Loop.Mode == LoopMode.Count ? options.Loop.Count : null;

        var run = new PlaybackRun(options, cancellation, endsOnEmptyCopy);
        int completedRounds = 0;
        int currentStep = 0;
        try
        {
            for (int round = 1; round <= rounds; round++)
            {
                run.IsFirstCopyOfRound = true;
                foreach ((int number, MacroStep step) in steps)
                {
                    if ((round == 1 && number < options.FirstStep) || number > options.LastStep)
                    {
                        continue;
                    }

                    currentStep = number;
                    ProgressChanged?.Invoke(this, new PlaybackProgress(round, totalRounds, number, macro.Steps.Count));
                    Wait(TimeSpan.FromMilliseconds(step.DelayBeforeMs / options.Speed), run);
                    if (!Play(step, number, run))
                    {
                        return new PlaybackResult(PlaybackOutcome.ListEnded, completedRounds);
                    }
                }

                completedRounds = round;
            }

            return new PlaybackResult(PlaybackOutcome.Completed, completedRounds);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return new PlaybackResult(PlaybackOutcome.Stopped, completedRounds);
        }
        catch (StepFailedException ex)
        {
            return new PlaybackResult(PlaybackOutcome.Failed, completedRounds, currentStep, ex.Failure, ex.Window);
        }
        catch (InputBlockedException)
        {
            return new PlaybackResult(PlaybackOutcome.Failed, completedRounds, currentStep, PlaybackFailure.InputBlocked);
        }
        finally
        {
            input.ReleaseAll();
        }
    }

    /// <returns>False when a copy step found the end of the list.</returns>
    private bool Play(MacroStep step, int number, PlaybackRun run)
    {
        switch (step)
        {
            case MouseClickStep click:
                Click(click, run);
                break;
            case MouseDragStep drag:
                Drag(drag, run);
                break;
            case MouseWheelStep wheel:
                Wheel(wheel, run);
                break;
            case KeyPressStep key:
                Activate(key.Target, run);
                Press(KeyModifiers.None, key.VirtualKey, key.ScanCode, key.IsExtended, key.PressCount, run);
                break;
            case HotkeyStep hotkey:
                Activate(hotkey.Target, run);
                Press(hotkey.Modifiers, hotkey.VirtualKey, hotkey.ScanCode, hotkey.IsExtended, hotkey.PressCount, run);
                break;
            case TypeTextStep typed:
                Activate(typed.Target, run);
                Type(typed.Text, run);
                break;
            case FocusWindowStep focus:
                Activate(focus.Target, run);
                break;
            case CopyStep copy:
                Activate(copy.Target, run);
                return Copy(run);
            case PasteStep paste:
                Activate(paste.Target, run);
                Paste(paste.CleanText, run);
                break;
            case WaitStep { IsPasswordEntry: true }:
                Pause();
                PasswordNeeded?.Invoke(this, number);
                Wait(TimeSpan.Zero, run);
                break;
            case WaitStep wait:
                Wait(TimeSpan.FromMilliseconds(wait.DurationMs / run.Options.Speed), run);
                break;
        }

        return true;
    }

    /// <summary>Finds the step's window, waiting for it to appear, and brings it to the front.</summary>
    /// <returns>The window; null for steps without a window.</returns>
    private WindowCandidate? Activate(WindowTarget? target, PlaybackRun run)
    {
        if (target is null)
        {
            return null;
        }

        WindowCandidate window = WaitForWindow(target, run);
        if (windows.IsElevatedAboveUs(window.Handle))
        {
            throw new StepFailedException(PlaybackFailure.TargetIsElevated, target);
        }

        if (windows.ForegroundWindow != window.Handle && !windows.TryActivate(window.Handle))
        {
            throw new StepFailedException(PlaybackFailure.WindowNotActivated, target);
        }

        return window;
    }

    /// <summary>
    /// For steps that point at a spot. A window that now has another size than at recording gets its recorded place
    /// and size back first: controls anchored to the right or bottom edge move when a window is resized, and the
    /// click would land beside them.
    /// </summary>
    /// <returns>Where the window is now; null for steps without a window.</returns>
    private ScreenRect? ActivateForPointing(WindowTarget? target, PlaybackRun run)
    {
        if (Activate(target, run) is not { } window)
        {
            return null;
        }

        ScreenRect bounds = windows.GetBounds(window.Handle) ?? window.Bounds;
        bool isResized = bounds.Width != target!.Bounds.Width || bounds.Height != target.Bounds.Height;
        if (isResized && windows.TryMove(window.Handle, target.Bounds))
        {
            bounds = windows.GetBounds(window.Handle) ?? target.Bounds;
        }

        return bounds;
    }

    private WindowCandidate WaitForWindow(WindowTarget target, PlaybackRun run)
    {
        long deadline = clock.NowMs + (long)run.Options.WindowWait.TotalMilliseconds;
        while (true)
        {
            WindowCandidate? match = WindowMatcher.FindBest(target, windows.ListWindows());
            if (match is not null)
            {
                return match;
            }

            if (clock.NowMs >= deadline)
            {
                throw new StepFailedException(PlaybackFailure.WindowNotFound, target);
            }

            Wait(WindowPoll, run);
        }
    }

    private void Click(MouseClickStep click, PlaybackRun run)
    {
        ScreenRect? bounds = ActivateForPointing(click.Target, run);
        input.MoveMouse(PositionMapper.Map(click.ScreenPosition, click.Target, bounds));
        Sleep(MouseSettle, run);
        HoldModifiers(click.Modifiers, true, run);
        for (int i = 0; i < click.ClickCount; i++)
        {
            if (i > 0)
            {
                Sleep(DoubleClickGap, run);
            }

            input.SetMouseButton(click.Button, true);
            Sleep(ButtonHold, run);
            input.SetMouseButton(click.Button, false);
        }

        HoldModifiers(click.Modifiers, false, run);
    }

    private void Drag(MouseDragStep drag, PlaybackRun run)
    {
        ScreenRect? bounds = ActivateForPointing(drag.Target, run);
        List<ScreenPoint> path = Interpolate(drag.ScreenPath.Select(point => PositionMapper.Map(point, drag.Target, bounds)).ToList());
        TimeSpan perMove = TimeSpan.FromMilliseconds(drag.DurationMs / run.Options.Speed / Math.Max(1, path.Count - 1));

        input.MoveMouse(path[0]);
        Sleep(MouseSettle, run);
        HoldModifiers(drag.Modifiers, true, run);
        input.SetMouseButton(drag.Button, true);
        foreach (ScreenPoint point in path.Skip(1))
        {
            Sleep(perMove, run);
            input.MoveMouse(point);
        }

        Sleep(MouseSettle, run);
        input.SetMouseButton(drag.Button, false);
        HoldModifiers(drag.Modifiers, false, run);
    }

    /// <summary>Programs start a drag only after seeing the pointer move while pressed, so long jumps get in-between points.</summary>
    private static List<ScreenPoint> Interpolate(List<ScreenPoint> path)
    {
        var result = new List<ScreenPoint> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            ScreenPoint from = path[i - 1];
            ScreenPoint to = path[i];
            double length = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
            int parts = Math.Max(1, (int)Math.Ceiling(length / DragStepPx));
            for (int part = 1; part <= parts; part++)
            {
                result.Add(new ScreenPoint(from.X + ((to.X - from.X) * part / parts), from.Y + ((to.Y - from.Y) * part / parts)));
            }
        }

        return result;
    }

    private void Wheel(MouseWheelStep wheel, PlaybackRun run)
    {
        ScreenRect? bounds = ActivateForPointing(wheel.Target, run);
        input.MoveMouse(PositionMapper.Map(wheel.ScreenPosition, wheel.Target, bounds));
        Sleep(MouseSettle, run);
        HoldModifiers(wheel.Modifiers, true, run);

        // One notch at a time, as a real wheel does.
        int remaining = wheel.Delta;
        while (remaining != 0)
        {
            int part = Math.Sign(remaining) * Math.Min(WheelNotch, Math.Abs(remaining));
            input.TurnWheel(part, wheel.IsHorizontal);
            remaining -= part;
            if (remaining != 0)
            {
                Sleep(WheelNotchGap, run);
            }
        }

        HoldModifiers(wheel.Modifiers, false, run);
    }

    private void Press(KeyModifiers modifiers, int virtualKey, int scanCode, bool isExtended, int pressCount, PlaybackRun run)
    {
        HoldModifiers(modifiers, true, run);
        for (int i = 0; i < pressCount; i++)
        {
            if (i > 0)
            {
                Sleep(KeyGap, run);
            }

            input.SetKey(virtualKey, scanCode, isExtended, true);
            Sleep(KeyGap, run);
            input.SetKey(virtualKey, scanCode, isExtended, false);
        }

        HoldModifiers(modifiers, false, run);
    }

    private void HoldModifiers(KeyModifiers modifiers, bool isDown, PlaybackRun run)
    {
        IEnumerable<(KeyModifiers Modifier, int VirtualKey, int ScanCode, bool IsExtended)> keys =
            isDown ? ModifierKeys : ModifierKeys.Reverse();
        foreach ((KeyModifiers modifier, int virtualKey, int scanCode, bool isExtended) in keys)
        {
            if (modifiers.HasFlag(modifier))
            {
                input.SetKey(virtualKey, scanCode, isExtended, isDown);
                Sleep(KeyGap, run);
            }
        }
    }

    private void Type(string text, PlaybackRun run)
    {
        foreach (char character in text)
        {
            input.TypeCharacter(character);
            Sleep(CharacterGap, run);
        }
    }

    /// <returns>False when this was the first copy of the round and it copied nothing: the list has ended.</returns>
    private bool Copy(PlaybackRun run)
    {
        uint before = clipboard.ChangeCount;
        Press(KeyModifiers.Control, VirtualKeys.C, ScanCodeC, false, 1, run);

        long deadline = clock.NowMs + (long)run.Options.ClipboardWait.TotalMilliseconds;
        while (clipboard.ChangeCount == before)
        {
            if (clock.NowMs >= deadline)
            {
                throw new StepFailedException(PlaybackFailure.ClipboardDidNotChange, null);
            }

            Sleep(ClipboardPoll, run);
        }

        bool readsNextItem = run.EndsOnEmptyCopy && run.IsFirstCopyOfRound;
        run.IsFirstCopyOfRound = false;

        // The first copy of a round reads the next item of the list; an empty cell means the list has ended.
        return !readsNextItem || !string.IsNullOrWhiteSpace(ReadClipboard(run));
    }

    private void Paste(bool cleanText, PlaybackRun run)
    {
        if (cleanText)
        {
            string text = ReadClipboard(run);
            string trimmed = text.Trim();
            if (trimmed.Length != text.Length && !clipboard.TryWriteText(trimmed))
            {
                throw new StepFailedException(PlaybackFailure.ClipboardUnavailable, null);
            }
        }

        Press(KeyModifiers.Control, VirtualKeys.V, ScanCodeV, false, 1, run);
    }

    /// <summary>Programs keep the clipboard locked for a moment while they fill it, so reading is retried.</summary>
    private string ReadClipboard(PlaybackRun run)
    {
        for (int attempt = 1; attempt <= ClipboardReadAttempts; attempt++)
        {
            if (clipboard.ReadText() is { } text)
            {
                return text;
            }

            Sleep(ClipboardPoll, run);
        }

        throw new StepFailedException(PlaybackFailure.ClipboardUnavailable, null);
    }

    /// <summary>A wait between steps: it can be paused and stopped.</summary>
    private void Wait(TimeSpan duration, PlaybackRun run)
    {
        TimeSpan remaining = duration;
        while (remaining > TimeSpan.Zero)
        {
            _running.Wait(run.Cancellation);
            TimeSpan slice = remaining < PauseCheck ? remaining : PauseCheck;
            clock.Wait(slice, run.Cancellation);
            remaining -= slice;
        }

        _running.Wait(run.Cancellation);
    }

    /// <summary>A short wait inside a step: it can be stopped, but not paused, so no key stays held during a pause.</summary>
    private void Sleep(TimeSpan duration, PlaybackRun run) => clock.Wait(duration, run.Cancellation);

    private sealed class PlaybackRun(PlaybackOptions options, CancellationToken cancellation, bool endsOnEmptyCopy)
    {
        public PlaybackOptions Options { get; } = options;

        public CancellationToken Cancellation { get; } = cancellation;

        public bool EndsOnEmptyCopy { get; } = endsOnEmptyCopy;

        public bool IsFirstCopyOfRound { get; set; }
    }
}
