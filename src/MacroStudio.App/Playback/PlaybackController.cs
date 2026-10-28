using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.App.Recording;
using MacroStudio.App.Services;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Playback;
using MacroStudio.Engine.Playback;
using MacroStudio.Native.WindowsSettings;
using Serilog;

namespace MacroStudio.App.Playback;

/// <summary>
/// One playback from start to end: countdown, the macro on a background thread, pause and resume,
/// Esc or Ctrl+Alt+End to stop, and what happens when the user touches the mouse or keyboard.
/// </summary>
public sealed partial class PlaybackController : ObservableObject
{
    // Right after resuming, the hand is still leaving the mouse; that is not interference.
    private const int ResumeGraceMs = 1000;

    private readonly MacroPlayer _player;
    private readonly IUserActivityMonitor _activity;
    private readonly SettingsService _settings;
    private readonly HotkeyService _hotkeys;
    private readonly ShortcutService _shortcuts;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
    private readonly HashSet<Guid> _screenAccepted = [];
    private CancellationTokenSource? _cancellation;
    private long _ignoreUserInputUntilMs;

    public PlaybackController(MacroPlayer player, IUserActivityMonitor activity, SettingsService settings, HotkeyService hotkeys, ShortcutService shortcuts)
    {
        _player = player;
        _activity = activity;
        _settings = settings;
        _hotkeys = hotkeys;
        _shortcuts = shortcuts;

        // The player and the hooks report on their own threads; the user interface changes on its thread.
        _player.ProgressChanged += (_, progress) => _dispatcher.BeginInvoke(() => Progress = progress);
        _player.PasswordNeeded += (_, step) => _dispatcher.BeginInvoke(() =>
        {
            State = PlaybackState.Paused;
            PasswordNeeded?.Invoke(this, step);
        });
        _activity.UserInput += OnUserInput;
        _activity.StopRequested += (_, _) => Stop();
    }

    public event EventHandler<PlaybackFinishedEventArgs>? Finished;

    /// <summary>Paused because the user touched the mouse or keyboard.</summary>
    public event EventHandler? PausedByUser;

    /// <summary>Paused at a password field; carries the step number.</summary>
    public event EventHandler<int>? PasswordNeeded;

    [ObservableProperty]
    public partial PlaybackState State { get; private set; }

    [ObservableProperty]
    public partial PlaybackProgress? Progress { get; private set; }

    /// <param name="firstStep">"Play from here": the first round starts at this 1-based step.</param>
    /// <param name="lastStep">"Try this step": nothing after this 1-based step is played.</param>
    public async Task PlayAsync(MacroDefinition macro, LoopSettings loop, double speed, int firstStep = 1, int? lastStep = null)
    {
        if (State != PlaybackState.Idle)
        {
            return;
        }

        if (!ConfirmScreen(macro) || !TryReadList(macro, out var list))
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        Progress = null;
        try
        {
            State = PlaybackState.CountingDown;
            await CountdownWindow.RunAsync(
                _settings.Current.PlaybackCountdownSeconds, "Playback_CountdownTitle", "Playback_CountdownHint", cancellation.Token);

            var options = new PlaybackOptions
            {
                Loop = loop,
                Speed = speed,
                WindowWait = TimeSpan.FromSeconds(_settings.Current.PlaybackWindowWaitSeconds),
                FirstStep = firstStep,
                LastStep = lastStep,
                OnError = macro.OnError,
                ListRows = list?.Rows,
                ListColumns = list?.Columns ?? [],
                Culture = Localizer.Instance.Culture,
            };
            PlaybackResult? result = await RunAsync(macro, options, cancellation.Token);
            if (result is not null)
            {
                Finished?.Invoke(this, new PlaybackFinishedEventArgs(macro, result, isPartial: firstStep > 1 || lastStep is not null));
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped during the countdown: nothing was played.
        }
        finally
        {
            _cancellation = null;
            Progress = null;
            State = PlaybackState.Idle;
        }
    }

    public void TogglePause()
    {
        if (State == PlaybackState.Playing)
        {
            _player.Pause();
            State = PlaybackState.Paused;
        }
        else if (State == PlaybackState.Paused)
        {
            Volatile.Write(ref _ignoreUserInputUntilMs, Environment.TickCount64 + ResumeGraceMs);
            _player.Resume();
            State = PlaybackState.Playing;
        }
    }

    /// <summary>Safe from any thread; playback stops within a few milliseconds and releases every held key.</summary>
    public void Stop()
    {
        try
        {
            _cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The playback has just ended.
        }
    }

    /// <summary>The list file is read before the countdown, so a missing file is told before anything is played.</summary>
    private static bool TryReadList(MacroDefinition macro, out (IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string> Columns)? list)
    {
        list = null;
        if (macro.DataList is not { } dataList)
        {
            return true;
        }

        try
        {
            list = DataListReader.Read(dataList);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Warning(ex, "List file could not be read");
            MessageBox.Show(Localizer.Instance.Format("Playback_ListUnreadable", dataList.Path, ex.Message), "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>
    /// A clicking macro recorded on a screen of another size or scale may click beside its targets. Asked once per
    /// macro while the app runs, so a user who knows the macro still works is not asked every time.
    /// </summary>
    private bool ConfirmScreen(MacroDefinition macro)
    {
        if (_screenAccepted.Contains(macro.Id) || ScreenWarningText.For(macro) is not { } warning)
        {
            return true;
        }

        MessageBoxResult answer = MessageBox.Show(
            Localizer.Instance.Format("Playback_ScreenQuestion", warning),
            "Macro Studio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxOptions.DefaultDesktopOnly);
        if (answer != MessageBoxResult.Yes)
        {
            return false;
        }

        _screenAccepted.Add(macro.Id);
        return true;
    }

    /// <returns>Null when the hooks could not be installed (the user was told).</returns>
    private async Task<PlaybackResult?> RunAsync(MacroDefinition macro, PlaybackOptions options, CancellationToken cancellation)
    {
        using TimerResolution timer = TimerResolution.OneMs();
        try
        {
            _activity.Start(_shortcuts.Reserved);
        }
        catch (Win32Exception ex)
        {
            Log.Error(ex, "Playback could not start");
            MessageBox.Show(Localizer.Instance.Format("Playback_HooksFailed", ex.Message), "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        int? stopHotkey = _hotkeys.TryRegister(_shortcuts.EmergencyStop, Stop);
        try
        {
            Volatile.Write(ref _ignoreUserInputUntilMs, Environment.TickCount64 + ResumeGraceMs);
            State = PlaybackState.Playing;
            Log.Information(
                "Playback started: {StepCount} steps, loop {Loop}, speed {Speed}, steps {First} to {Last}",
                macro.Steps.Count,
                options.Loop,
                options.Speed,
                options.FirstStep,
                options.LastStep);

            PlaybackResult result = await Task.Factory.StartNew(
                () => _player.Play(macro, options, cancellation),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            Log.Information(
                "Playback ended: {Outcome} after {Rounds} rounds, failure {Failure} at step {Step}",
                result.Outcome,
                result.CompletedRounds,
                result.Failure,
                result.FailedStepNumber);
            return result;
        }
        finally
        {
            _activity.Stop();
            if (stopHotkey is int id)
            {
                _hotkeys.Unregister(id);
            }
        }
    }

    // Raised on the hook thread for every real mouse move, click or key press, so it must be quick.
    private void OnUserInput(object? sender, EventArgs e)
    {
        if (_player.IsPaused || Environment.TickCount64 < Volatile.Read(ref _ignoreUserInputUntilMs))
        {
            return;
        }

        switch (_settings.Current.PlaybackOnUserInput)
        {
            case InterferencePolicy.Pause:
                _player.Pause();
                _dispatcher.BeginInvoke(() =>
                {
                    if (State == PlaybackState.Playing)
                    {
                        State = PlaybackState.Paused;
                        PausedByUser?.Invoke(this, EventArgs.Empty);
                    }
                });
                break;
            case InterferencePolicy.Stop:
                Stop();
                break;
        }
    }
}
