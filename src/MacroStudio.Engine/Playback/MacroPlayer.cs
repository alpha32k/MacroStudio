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
/// <param name="elements">Finds clicked controls by name; without it every click goes to its recorded position.</param>
public sealed class MacroPlayer(IInputSimulator input, IWindowSystem windows, IClipboard clipboard, IPlaybackClock clock, IUiElements? elements = null)
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
    private static readonly TimeSpan RetryGap = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ConditionPoll = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ImagePoll = TimeSpan.FromMilliseconds(250);

    // Colors within this distance per channel count as the same: screens and smoothing change a few levels.
    private const int ColorTolerance = 24;

    // A round that fails at the same place again and again (the list does not move on) must not repeat forever.
    private const int MaxRoundsSkippedInARow = 3;
    private const double DragStepPx = 20;

    // A program that has not found its control in this time is clicked at the recorded position instead.
    private static readonly TimeSpan ElementSearchTimeout = TimeSpan.FromMilliseconds(1500);

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
        // A list file ends the macro with its last row, whatever the repeat says.
        bool isListShorter = options.ListRows is { } listRows && listRows.Count < rounds;
        if (options.ListRows is { } list)
        {
            rounds = Math.Min(options.Loop.Mode == LoopMode.UntilClipboardEmpty ? list.Count : rounds, list.Count);
        }

        int? totalRounds = options.Loop.Mode == LoopMode.Count || options.ListRows is not null ? rounds : null;

        var run = new PlaybackRun(options, cancellation, endsOnEmptyCopy);
        int completedRounds = 0;
        int currentStep = 0;
        try
        {
            for (int round = 1; round <= rounds; round++)
            {
                run.IsFirstCopyOfRound = true;
                run.Round = round;
                run.StepsToSkip = 0;
                bool isRoundSkipped = false;
                foreach ((int number, MacroStep step) in steps)
                {
                    if ((round == 1 && number < options.FirstStep) || number > options.LastStep)
                    {
                        continue;
                    }

                    // Left out by a condition step just before.
                    if (run.StepsToSkip > 0)
                    {
                        run.StepsToSkip--;
                        continue;
                    }

                    currentStep = number;
                    ProgressChanged?.Invoke(this, new PlaybackProgress(round, totalRounds, number, macro.Steps.Count));
                    Wait(TimeSpan.FromMilliseconds(step.DelayBeforeMs / options.Speed), run);
                    StepOutcome outcome = PlayWithRetries(step, number, run);
                    if (outcome == StepOutcome.ListEnded)
                    {
                        return Result(PlaybackOutcome.ListEnded, completedRounds, run);
                    }

                    if (outcome == StepOutcome.RoundSkipped)
                    {
                        isRoundSkipped = true;
                        break;
                    }

                    if (run.Decision == IfAction.StopMacro)
                    {
                        return Result(PlaybackOutcome.Completed, round, run);
                    }

                    if (run.Decision == IfAction.EndRound)
                    {
                        run.Decision = null;
                        break;
                    }
                }

                if (!isRoundSkipped)
                {
                    completedRounds = round;
                    run.HasCompletedRound = true;
                    run.RoundsSkippedInARow = 0;
                }
            }

            return Result(isListShorter || options.ListRows is not null && options.Loop.Mode == LoopMode.UntilClipboardEmpty ? PlaybackOutcome.ListEnded : PlaybackOutcome.Completed, completedRounds, run);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return Result(PlaybackOutcome.Stopped, completedRounds, run);
        }
        catch (StepFailedException ex)
        {
            return new PlaybackResult(PlaybackOutcome.Failed, completedRounds, currentStep, ex.Failure, ex.Window, run.SkippedSteps, run.SkippedRounds);
        }
        catch (InputBlockedException)
        {
            return new PlaybackResult(PlaybackOutcome.Failed, completedRounds, currentStep, PlaybackFailure.InputBlocked, null, run.SkippedSteps, run.SkippedRounds);
        }
        finally
        {
            input.ReleaseAll();
        }
    }

    private static PlaybackResult Result(PlaybackOutcome outcome, int completedRounds, PlaybackRun run) =>
        new(outcome, completedRounds, SkippedSteps: run.SkippedSteps, SkippedRounds: run.SkippedRounds);

    /// <summary>Plays a step; when it fails, tries again and then stops or skips as the macro's error policy says.</summary>
    private StepOutcome PlayWithRetries(MacroStep step, int number, PlaybackRun run)
    {
        ErrorPolicy policy = run.Options.OnError;
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return Play(step, number, run) ? StepOutcome.Played : StepOutcome.ListEnded;
            }
            catch (StepFailedException ex) when (ErrorPolicy.CanRetry(ex.Failure) && (attempt < policy.Retries || policy.Action != ErrorAction.Stop))
            {
                // Nothing stays pressed while waiting; the step starts over from its beginning.
                input.ReleaseAll();
                if (attempt < policy.Retries)
                {
                    Wait(RetryGap, run);
                    continue;
                }

                if (policy.Action == ErrorAction.SkipStep)
                {
                    run.SkippedSteps++;
                    return StepOutcome.Played;
                }

                if (++run.RoundsSkippedInARow >= MaxRoundsSkippedInARow)
                {
                    throw;
                }

                run.SkippedRounds++;
                return StepOutcome.RoundSkipped;
            }
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
                Type(FillFields(typed.Text, run), run);
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
            case ImageClickStep image:
                ClickImage(image, run);
                break;
            case IfStep check:
                Decide(check, run);
                break;
            case WaitForStep waitFor:
                WaitUntil(waitFor, run);
                break;
            case OpenStep open:
                if (!windows.TryOpen(open.Path, open.Arguments))
                {
                    throw new StepFailedException(PlaybackFailure.OpenFailed, null);
                }

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
        // A Windows panel such as search is already in front when its step comes; keys go to it as they are.
        if (target is null || ShellPanels.IsShellPanel(target))
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
    /// <returns>The window with where it is now; null for steps without a window.</returns>
    private WindowCandidate? ActivateForPointing(WindowTarget? target, PlaybackRun run)
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

        return window with { Bounds = bounds };
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
        WindowCandidate? window = ActivateForPointing(click.Target, run);
        input.MoveMouse(ClickPoint(click, window, run));
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

    /// <summary>
    /// The recorded control where it is now, at the same spot inside it; the recorded position (moved along with
    /// the window) when the step has no control or it is not found. A control that was not found is not looked
    /// for again in this run: every round would wait for it.
    /// </summary>
    private ScreenPoint ClickPoint(MouseClickStep click, WindowCandidate? window, PlaybackRun run)
    {
        ScreenPoint recorded = PositionMapper.Map(click.ScreenPosition, click.Target, window?.Bounds);
        if (elements is null || click.Element is not { } element || window is null || run.MissingElements.Contains(click.Id))
        {
            return recorded;
        }

        if (elements.Find(window.Handle, element, recorded, ElementSearchTimeout) is not { } found)
        {
            run.MissingElements.Add(click.Id);
            return recorded;
        }

        return new ScreenPoint(
            found.Left + (int)Math.Round(Math.Clamp(element.OffsetX, 0, 1) * Math.Max(0, found.Width - 1)),
            found.Top + (int)Math.Round(Math.Clamp(element.OffsetY, 0, 1) * Math.Max(0, found.Height - 1)));
    }

    private void Drag(MouseDragStep drag, PlaybackRun run)
    {
        ScreenRect? bounds = ActivateForPointing(drag.Target, run)?.Bounds;
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
        ScreenRect? bounds = ActivateForPointing(wheel.Target, run)?.Bounds;
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

    private static string FillFields(string text, PlaybackRun run)
    {
        if (!TextTemplate.HasFields(text))
        {
            return text;
        }

        PlaybackOptions options = run.Options;
        IReadOnlyList<string>? row = options.ListRows is { } rows && run.Round <= rows.Count ? rows[run.Round - 1] : null;
        return TextTemplate.Fill(text, new TemplateValues(row, options.ListColumns, run.Round, DateTime.Now, options.Culture));
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

        bool readsNextItem = run.EndsOnEmptyCopy && run.IsFirstCopyOfRound;
        run.IsFirstCopyOfRound = false;

        long deadline = clock.NowMs + (long)run.Options.ClipboardWait.TotalMilliseconds;
        while (clipboard.ChangeCount == before)
        {
            if (clock.NowMs >= deadline)
            {
                // Text programs (Notepad, browsers) copy nothing when the selection is empty, so past the last
                // line the clipboard stays as it was. The same copy worked in the rounds before: the list has ended.
                if (readsNextItem && run.HasCompletedRound)
                {
                    return false;
                }

                throw new StepFailedException(PlaybackFailure.ClipboardDidNotChange, null);
            }

            Sleep(ClipboardPoll, run);
        }

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

    /// <summary>Checks the condition every 100 ms; the longest time is not shortened by the speed, programs do not get faster.</summary>
    private void WaitUntil(WaitForStep step, PlaybackRun run)
    {
        // A spot inside a window is only visible when that window is in front.
        WindowCandidate? window = step.Condition == WaitCondition.ColorAppears ? ActivateForPointing(step.Target, run) : null;
        ScreenPoint spot = PositionMapper.Map(step.ScreenPosition, step.Target, window?.Bounds);
        long deadline = clock.NowMs + step.TimeoutMs;
        while (!IsMet(step, spot))
        {
            if (clock.NowMs >= deadline)
            {
                throw new StepFailedException(PlaybackFailure.WaitTimedOut, step.Target);
            }

            Wait(ConditionPoll, run);
        }
    }

    /// <summary>Looks for the picture every quarter second until its time is up, then clicks its middle where it is.</summary>
    private void ClickImage(ImageClickStep step, PlaybackRun run)
    {
        ScreenRect area = ActivateForPointing(step.Target, run)?.Bounds ?? windows.DesktopBounds;
        var picture = new Bgra32Image(step.ImageWidth, step.ImageHeight, step.ImagePixels);
        long deadline = clock.NowMs + step.TimeoutMs;
        while (true)
        {
            if (windows.Capture(area) is { } screen && ImageMatcher.Find(screen, picture) is (int x, int y))
            {
                input.MoveMouse(new ScreenPoint(area.Left + x + (picture.Width / 2), area.Top + y + (picture.Height / 2)));
                Sleep(MouseSettle, run);
                for (int i = 0; i < step.ClickCount; i++)
                {
                    if (i > 0)
                    {
                        Sleep(DoubleClickGap, run);
                    }

                    input.SetMouseButton(step.Button, true);
                    Sleep(ButtonHold, run);
                    input.SetMouseButton(step.Button, false);
                }

                return;
            }

            if (clock.NowMs >= deadline)
            {
                throw new StepFailedException(PlaybackFailure.ImageNotFound, step.Target);
            }

            Wait(ImagePoll, run);
        }
    }

    /// <summary>Looks once and, when the check is true, leaves out steps, the round or the rest of the macro.</summary>
    private void Decide(IfStep step, PlaybackRun run)
    {
        bool isColorCheck = step.Check is IfCheck.ColorIs or IfCheck.ColorIsNot;
        WindowCandidate? window = isColorCheck ? ActivateForPointing(step.Target, run) : null;
        bool isTrue = step.Check switch
        {
            IfCheck.WindowOpen => WindowMatcher.FindBest(step.Target!, windows.ListWindows()) is not null,
            IfCheck.WindowNotOpen => WindowMatcher.FindBest(step.Target!, windows.ListWindows()) is null,
            _ => windows.PixelAt(PositionMapper.Map(step.ScreenPosition, step.Target, window?.Bounds)) is int color
                && IsSameColor(color, step.Color) == (step.Check == IfCheck.ColorIs),
        };
        if (!isTrue)
        {
            return;
        }

        if (step.Action == IfAction.SkipSteps)
        {
            run.StepsToSkip = step.SkipCount;
        }
        else
        {
            run.Decision = step.Action;
        }
    }

    private bool IsMet(WaitForStep step, ScreenPoint spot) => step.Condition switch
    {
        WaitCondition.WindowAppears => WindowMatcher.FindBest(step.Target!, windows.ListWindows()) is not null,
        WaitCondition.WindowCloses => WindowMatcher.FindBest(step.Target!, windows.ListWindows()) is null,
        _ => windows.PixelAt(spot) is int color && IsSameColor(color, step.Color),
    };

    private static bool IsSameColor(int a, int b) =>
        Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) <= ColorTolerance
        && Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) <= ColorTolerance
        && Math.Abs((a & 0xFF) - (b & 0xFF)) <= ColorTolerance;

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

        public bool HasCompletedRound { get; set; }

        /// <summary>Click steps whose control was not found; they use their recorded position for the rest of the run.</summary>
        public HashSet<Guid> MissingElements { get; } = [];

        public int SkippedSteps { get; set; }

        public int SkippedRounds { get; set; }

        public int RoundsSkippedInARow { get; set; }

        /// <summary>1-based number of the round being played.</summary>
        public int Round { get; set; } = 1;

        /// <summary>Steps a condition said to leave out in this round.</summary>
        public int StepsToSkip { get; set; }

        /// <summary>A condition ended the round or the macro.</summary>
        public IfAction? Decision { get; set; }
    }

    private enum StepOutcome
    {
        Played,
        ListEnded,
        RoundSkipped,
    }
}
