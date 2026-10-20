namespace MacroStudio.Core.Macros;

public enum LoopMode
{
    /// <summary>Stops as soon as a copy step copies an empty value (the end of an Excel list).</summary>
    UntilClipboardEmpty,

    Count,

    Infinite,
}
