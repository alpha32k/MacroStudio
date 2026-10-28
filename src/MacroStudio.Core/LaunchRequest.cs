namespace MacroStudio.Core;

public enum LaunchKind
{
    ShowMainWindow,

    /// <summary>A double-clicked .msmacro file: show its steps, then import it.</summary>
    OpenFile,

    /// <summary>"--play": play a saved macro, found by its name or id; from a shortcut, a script or the Task Scheduler.</summary>
    Play,
}

/// <summary>What Macro Studio was started for, from its command line, and the same as one line for the running copy.</summary>
public sealed record LaunchRequest(LaunchKind Kind, string? Value = null)
{
    public const string PlaySwitch = "--play";

    public static LaunchRequest Parse(IReadOnlyList<string> args)
    {
        if (args.Count >= 2 && string.Equals(args[0], PlaySwitch, StringComparison.OrdinalIgnoreCase) && args[1].Trim().Length > 0)
        {
            return new LaunchRequest(LaunchKind.Play, args[1].Trim());
        }

        return args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
            ? new LaunchRequest(LaunchKind.OpenFile, args[0])
            : new LaunchRequest(LaunchKind.ShowMainWindow);
    }

    /// <summary>
    /// The line sent to the running copy. Older versions sent the file path or an empty line, which still read the
    /// same way; a path can never start with "--play ".
    /// </summary>
    public string ToMessage() => Kind switch
    {
        LaunchKind.Play => PlaySwitch + " " + Value,
        LaunchKind.OpenFile => Value ?? "",
        _ => "",
    };

    public static LaunchRequest FromMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new LaunchRequest(LaunchKind.ShowMainWindow);
        }

        return message.StartsWith(PlaySwitch + " ", StringComparison.OrdinalIgnoreCase)
            ? new LaunchRequest(LaunchKind.Play, message[(PlaySwitch.Length + 1)..].Trim())
            : new LaunchRequest(LaunchKind.OpenFile, message);
    }
}
