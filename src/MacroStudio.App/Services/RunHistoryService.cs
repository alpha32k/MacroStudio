using System.IO;
using MacroStudio.App.Playback;
using MacroStudio.Core.Playback;
using Serilog;

namespace MacroStudio.App.Services;

/// <summary>Remembers how the last full playback of each macro ended, for the "last run" lines in the lists.</summary>
public sealed class RunHistoryService
{
    private readonly RunHistoryStore _store;
    private readonly Dictionary<Guid, LastRun> _runs;

    public RunHistoryService(RunHistoryStore store, PlaybackController playback)
    {
        _store = store;
        _runs = store.Load();
        playback.Finished += (_, e) =>
        {
            // Trying a single step says nothing about how the whole macro runs.
            if (!e.IsPartial)
            {
                Record(e.Macro.Id, new LastRun(DateTimeOffset.Now, e.Result.Outcome, e.Result.CompletedRounds));
            }
        };
    }

    public event EventHandler? Changed;

    public LastRun? Of(Guid macroId) => _runs.GetValueOrDefault(macroId);

    private void Record(Guid macroId, LastRun run)
    {
        _runs[macroId] = run;
        try
        {
            _store.Save(_runs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only the list line is lost; the macro itself is not affected.
            Log.Warning(ex, "Run history could not be saved");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
