using MacroStudio.Core.Input;

namespace MacroStudio.Core.Recording;

public sealed record RecordingOptions
{
    /// <summary>Ctrl+C and Ctrl+V become copy and paste steps that wait for the clipboard and clean the text.</summary>
    public bool ConvertCopyPaste { get; init; } = true;

    /// <summary>Alt+Tab and taskbar clicks become "bring window to front" steps.</summary>
    public bool ConvertWindowSwitches { get; init; } = true;

    /// <summary>Clicks also note the control's name, so playback can find a button that moved.</summary>
    public bool FindElements { get; init; } = true;

    /// <summary>Letters typed one after another become one text step.</summary>
    public bool MergeTyping { get; init; } = true;

    /// <summary>The Windows double-click time; two clicks closer than this become a double click.</summary>
    public int DoubleClickTimeMs { get; init; } = 500;

    /// <summary>Movement in pixels after which a press and release is a drag, not a click.</summary>
    public int DragThresholdPx { get; init; } = 4;

    /// <summary>Macro Studio's own shortcuts (for example Ctrl+Alt+R to stop recording); they are not recorded.</summary>
    public IReadOnlySet<KeyShortcut> AppShortcuts { get; init; } = new HashSet<KeyShortcut>();
}
