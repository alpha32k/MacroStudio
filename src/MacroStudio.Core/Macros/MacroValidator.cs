using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Macros;

/// <summary>
/// Checks a macro against sensible limits before it is saved or played. A file edited by hand, damaged,
/// or received from someone else must not make the player hang or click for hours.
/// </summary>
public static class MacroValidator
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;
    public const int MaxSteps = 50_000;
    public const int MaxDelayMs = 3_600_000;
    public const int MaxPressCount = 1_000;
    public const int MaxTextLength = 10_000;
    public const int MaxPathPoints = 1_000;
    public const double MinSpeed = 0.25;
    public const double MaxSpeed = 5;

    /// <returns>English descriptions of every problem; empty when the macro is valid.</returns>
    public static IReadOnlyList<string> Validate(MacroDefinition macro)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(macro.Name) || macro.Name.Length > MaxNameLength)
        {
            problems.Add($"Name must have 1 to {MaxNameLength} characters.");
        }

        if (macro.Description.Length > MaxDescriptionLength)
        {
            problems.Add($"Description can have at most {MaxDescriptionLength} characters.");
        }

        if (macro.SchemaVersion is < 1 or > MacroDefinition.CurrentSchemaVersion)
        {
            problems.Add($"Schema version {macro.SchemaVersion} is not supported.");
        }

        if (macro.Steps.Count > MaxSteps)
        {
            problems.Add($"A macro can have at most {MaxSteps} steps.");
        }

        if (macro.SpeedMultiplier is < MinSpeed or > MaxSpeed)
        {
            problems.Add($"Speed must be between {MinSpeed} and {MaxSpeed}.");
        }

        if (macro.Loop.Count is < 1 or > LoopSettings.MaxCount)
        {
            problems.Add($"Loop count must be between 1 and {LoopSettings.MaxCount}.");
        }

        for (int i = 0; i < macro.Steps.Count; i++)
        {
            string? problem = ValidateStep(macro.Steps[i]);
            if (problem is not null)
            {
                problems.Add($"Step {i + 1}: {problem}");
            }
        }

        return problems;
    }

    private static string? ValidateStep(MacroStep step)
    {
        if (step.DelayBeforeMs is < 0 or > MaxDelayMs)
        {
            return $"delay must be between 0 and {MaxDelayMs} ms.";
        }

        return step switch
        {
            MouseClickStep { ClickCount: < 1 or > 2 } => "click count must be 1 or 2.",
            MouseDragStep { ScreenPath.Count: < 2 or > MaxPathPoints } => $"a drag needs 2 to {MaxPathPoints} points.",
            KeyPressStep { PressCount: < 1 or > MaxPressCount } => $"press count must be between 1 and {MaxPressCount}.",
            HotkeyStep { PressCount: < 1 or > MaxPressCount } => $"press count must be between 1 and {MaxPressCount}.",
            TypeTextStep { Text.Length: 0 or > MaxTextLength } => $"text must have 1 to {MaxTextLength} characters.",
            WaitStep { DurationMs: < 0 or > MaxDelayMs } => $"wait must be between 0 and {MaxDelayMs} ms.",
            _ => null,
        };
    }
}
