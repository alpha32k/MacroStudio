using System.Windows;
using MacroStudio.App.Localization;
using MacroStudio.App.Services;
using MacroStudio.Core.Playback;

namespace MacroStudio.App.Playback;

/// <summary>
/// Tells the user how a playback went: a short note next to the clock when it ended normally,
/// a message on top of every window when it stopped because of a problem.
/// </summary>
public sealed class PlaybackNotifier
{
    private readonly TrayIcon _tray;

    public PlaybackNotifier(PlaybackController playback, TrayIcon tray)
    {
        _tray = tray;
        playback.Finished += (_, e) => ShowResult(e.Macro.Name, e.Result);
        playback.PausedByUser += (_, _) => _tray.ShowInfo(Localizer.Instance["Playback_PausedByUser"]);
        playback.PasswordNeeded += (_, step) => _tray.ShowInfo(Localizer.Instance.Format("Playback_PasswordNeeded", step));
    }

    private static string DescribeFailure(PlaybackResult result)
    {
        Localizer text = Localizer.Instance;
        string window = result.Window is { } target
            ? text.Format("Window_Name", target.Title.Length > 0 ? target.Title : target.ProcessName, target.ProcessName)
            : "";
        return text.Format("Failure_" + result.Failure, window);
    }

    /// <summary>" (2 adım, 1 tur atlandı)"; empty when nothing was skipped.</summary>
    private static string Skipped(PlaybackResult result) =>
        result.SkippedSteps + result.SkippedRounds == 0
            ? ""
            : " " + Localizer.Instance.Format("Playback_Skipped", result.SkippedSteps, result.SkippedRounds);

    private void ShowResult(string macroName, PlaybackResult result)
    {
        Localizer text = Localizer.Instance;
        switch (result.Outcome)
        {
            case PlaybackOutcome.Completed:
                _tray.ShowInfo(text.Format("Playback_Completed", macroName, result.CompletedRounds) + Skipped(result));
                break;
            case PlaybackOutcome.ListEnded:
                _tray.ShowInfo(text.Format("Playback_ListEnded", macroName, result.CompletedRounds) + Skipped(result));
                break;
            case PlaybackOutcome.Stopped:
                _tray.ShowInfo(text.Format("Playback_Stopped", macroName, result.CompletedRounds));
                break;
            case PlaybackOutcome.Failed:
                // The user is usually in another program; the message must come on top of it.
                MessageBox.Show(
                    text.Format("Playback_Failed", macroName, result.FailedStepNumber ?? 0, DescribeFailure(result)),
                    "Macro Studio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning,
                    MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly);
                break;
        }
    }
}
