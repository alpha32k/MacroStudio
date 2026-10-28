using MacroStudio.Core;
using MacroStudio.Core.Macros;
using MacroStudio.Native.WindowsSettings;
using Serilog;

namespace MacroStudio.App.Services;

/// <summary>
/// Keeps each scheduled macro's Windows task in step with the macro: the task starts Macro Studio with
/// "--play &lt;id&gt;", so a renamed macro keeps its schedule.
/// </summary>
public sealed class ScheduleService(MacroLibraryService library)
{
    private readonly string _executable = Environment.ProcessPath!;

    /// <summary>Saves the macro with its new schedule (null removes it) and creates or removes its task.</summary>
    /// <returns>Null when it worked; otherwise Windows' message. The macro file is changed only when the task was.</returns>
    public string? SetSchedule(MacroFile file, MacroSchedule? schedule)
    {
        string? error = schedule is null ? ScheduledTasks.Delete(TaskName(file)) : Register(file.Macro, schedule);
        if (error is not null)
        {
            Log.Warning("Schedule of a macro could not be changed: {Error}", error);
            return error;
        }

        library.Save(file.Path, file.Macro with { Schedule = schedule });
        Log.Information("Macro schedule {Action}", schedule is null ? "removed" : schedule.Frequency.ToString());
        return null;
    }

    /// <summary>A macro going to the trash takes its task with it; restoring it brings the task back (see <see cref="Sync"/>).</summary>
    public void RemoveTask(MacroFile file)
    {
        if (file.Macro.Schedule is not null && ScheduledTasks.Delete(TaskName(file)) is { } error)
        {
            Log.Warning("Task of a deleted macro could not be removed: {Error}", error);
        }
    }

    /// <summary>
    /// Creates missing tasks, for example after a macro came back from the trash or the program moved.
    /// Called on the user interface thread; the tasks are written in the background.
    /// </summary>
    public Task Sync()
    {
        List<MacroDefinition> scheduled = [.. library.Macros.Select(m => m.Macro).Where(m => m.Schedule is not null)];
        return Task.Run(() =>
        {
            foreach (MacroDefinition macro in scheduled)
            {
                if (Register(macro, macro.Schedule!) is { } error)
                {
                    Log.Warning("Task of a scheduled macro could not be created: {Error}", error);
                }
            }
        });
    }

    private static string TaskName(MacroFile file) => ScheduledTaskXml.TaskName(file.Macro.Id);

    private string? Register(MacroDefinition macro, MacroSchedule schedule)
    {
        string arguments = $"{LaunchRequest.PlaySwitch} \"{macro.Id:D}\"";
        string xml = ScheduledTaskXml.Build(schedule, "Macro Studio: " + macro.Name, _executable, arguments);
        return ScheduledTasks.CreateOrReplace(ScheduledTaskXml.TaskName(macro.Id), xml);
    }
}
