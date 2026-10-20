using MacroStudio.Core.Macros.Steps;

namespace MacroStudio.Core.Macros;

/// <summary>
/// A recorded macro: the content of one .msmacro file.
/// Properties use "set", not "init", so fields missing from a file keep the defaults below (see MacroStep).
/// </summary>
public sealed record MacroDefinition
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Lets later versions of the app read and upgrade older files.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string Description { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public ScreenInfo? RecordedOn { get; set; }

    public LoopSettings Loop { get; set; } = LoopSettings.UntilClipboardEmpty;

    public double SpeedMultiplier { get; set; } = 1;

    public required IReadOnlyList<MacroStep> Steps { get; set; }
}
