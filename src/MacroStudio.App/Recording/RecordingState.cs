namespace MacroStudio.App.Recording;

public enum RecordingState
{
    Idle,

    /// <summary>The countdown before the hooks start; the user switches to the starting window.</summary>
    CountingDown,

    Recording,

    /// <summary>The hooks have stopped; the save window is open.</summary>
    Saving,
}
