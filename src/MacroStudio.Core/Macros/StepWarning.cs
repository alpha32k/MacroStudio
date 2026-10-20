namespace MacroStudio.Core.Macros;

/// <summary>A step that works when recorded but may go wrong in a loop.</summary>
public enum StepWarning
{
    None,

    /// <summary>The wheel in Excel: one notch does not always scroll the same number of rows.</summary>
    UnreliableExcelWheel,

    /// <summary>Alt+Tab kept as keys: it goes to the most recently used window, which may change.</summary>
    UnconvertedAltTab,
}
