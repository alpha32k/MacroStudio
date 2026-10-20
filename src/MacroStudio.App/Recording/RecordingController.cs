using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using MacroStudio.App.Localization;
using MacroStudio.App.Macros;
using MacroStudio.App.Services;
using MacroStudio.Core.Macros;
using MacroStudio.Core.Macros.Steps;
using MacroStudio.Core.Recording;
using MacroStudio.Core.Settings;
using MacroStudio.Engine.Recording;
using MacroStudio.Native.Displays;
using Serilog;
using MessageBox = System.Windows.MessageBox;

namespace MacroStudio.App.Recording;

/// <summary>
/// One recording from start to end: countdown, hooks on, hooks off, save window.
/// Started and stopped from the bar or with Ctrl+Alt+R.
/// </summary>
public sealed partial class RecordingController(MacroRecorder recorder, SettingsService settings, MacroLibraryService library)
    : ObservableObject
{
    private CancellationTokenSource? _countdown;

    /// <summary>A recording was saved; the bar selects the new macro.</summary>
    public event EventHandler<MacroFile>? MacroSaved;

    /// <summary>The recording ended without a single step.</summary>
    public event EventHandler? NothingRecorded;

    [ObservableProperty]
    public partial RecordingState State { get; private set; }

    public Task ToggleAsync() => State == RecordingState.Idle ? StartAsync() : StopAsync();

    public async Task StartAsync()
    {
        if (State != RecordingState.Idle)
        {
            return;
        }

        State = RecordingState.CountingDown;
        using (var countdown = new CancellationTokenSource())
        {
            _countdown = countdown;
            try
            {
                await CountdownWindow.RunAsync(
                    settings.Current.RecordingCountdownSeconds, "Recording_CountdownTitle", "Recording_CountdownHint", countdown.Token);
            }
            catch (OperationCanceledException)
            {
                State = RecordingState.Idle;
                return;
            }
            finally
            {
                _countdown = null;
            }
        }

        try
        {
            recorder.Start(CreateOptions(settings.Current));
            State = RecordingState.Recording;
            Log.Information("Recording started");
        }
        catch (Win32Exception ex)
        {
            Log.Error(ex, "Recording could not start");
            State = RecordingState.Idle;
            ShowError("Recording_HooksFailed", ex);
        }
    }

    public async Task StopAsync()
    {
        if (State == RecordingState.CountingDown)
        {
            _countdown?.Cancel();
            return;
        }

        if (State != RecordingState.Recording)
        {
            return;
        }

        State = RecordingState.Saving;
        try
        {
            IReadOnlyList<MacroStep> steps = await recorder.StopAsync();

            // Only the number of steps is logged; what was typed or copied never goes into a log.
            Log.Information("Recording stopped with {StepCount} steps", steps.Count);
            if (steps.Count == 0)
            {
                NothingRecorded?.Invoke(this, EventArgs.Empty);
                return;
            }

            MacroFile? saved = AskToSave(steps);
            if (saved is not null)
            {
                MacroSaved?.Invoke(this, saved);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Error(ex, "Recording could not be completed");
            ShowError("Recording_Failed", ex);
        }
        finally
        {
            State = RecordingState.Idle;
        }
    }

    private static RecordingOptions CreateOptions(AppSettings current) => new()
    {
        ConvertCopyPaste = current.RecordingConvertCopyPaste,
        ConvertWindowSwitches = current.RecordingConvertWindowSwitches,
        MergeTyping = current.RecordingMergeTyping,
        DoubleClickTimeMs = SystemInformation.DoubleClickTime,

        // A hand moving a few pixels while clicking still makes a click, as in Windows' own drag-and-drop.
        DragThresholdPx = Math.Max(SystemInformation.DragSize.Width, SystemInformation.DragSize.Height),
        AppShortcuts = AppShortcuts.All,
    };

    private static void ShowError(string messageKey, Exception ex) =>
        MessageBox.Show(Localizer.Instance.Format(messageKey, ex.Message), "Macro Studio", MessageBoxButton.OK, MessageBoxImage.Error);

    private MacroFile? AskToSave(IReadOnlyList<MacroStep> steps)
    {
        var draft = new MacroDefinition
        {
            Name = "",
            CreatedAt = DateTimeOffset.Now,
            RecordedOn = Monitors.DescribePrimary(),
            Steps = steps,
        };
        var viewModel = new SaveMacroViewModel(draft, library, isImport: false);
        new SaveMacroWindow(viewModel).ShowDialog();
        return viewModel.SavedMacro;
    }
}
