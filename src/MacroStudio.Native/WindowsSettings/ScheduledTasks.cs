using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MacroStudio.Native.WindowsSettings;

/// <summary>
/// Tasks in the Windows Task Scheduler, through Windows' own "schtasks" tool. A task of the signed-in user needs no
/// administrator rights.
/// </summary>
public static class ScheduledTasks
{
    private const int TimeoutMs = 15_000;

    /// <summary>Creates the task, or replaces one with the same name.</summary>
    /// <returns>Null when it worked; otherwise Windows' message.</returns>
    public static string? CreateOrReplace(string name, string taskXml)
    {
        string file = Path.Combine(Path.GetTempPath(), $"MacroStudio-task-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks reads task XML reliably only as UTF-16.
            File.WriteAllText(file, taskXml, Encoding.Unicode);
            return Run("/Create", "/TN", name, "/XML", file, "/F");
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A file left in the temp folder does no harm.
            }
        }
    }

    /// <returns>Null when it worked or there was no such task; otherwise Windows' message.</returns>
    public static string? Delete(string name) => Exists(name) ? Run("/Delete", "/TN", name, "/F") : null;

    public static bool Exists(string name) => Run("/Query", "/TN", name) is null;

    private static string? Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start)!;
            Task<string> error = process.StandardError.ReadToEndAsync();
            _ = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(TimeoutMs))
            {
                process.Kill();
                return "schtasks did not answer.";
            }

            return process.ExitCode == 0 ? null : error.Result.Trim();
        }
        catch (Win32Exception ex)
        {
            return ex.Message;
        }
    }
}
