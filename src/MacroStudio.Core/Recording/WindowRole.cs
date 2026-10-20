namespace MacroStudio.Core.Recording;

public enum WindowRole
{
    /// <summary>A normal program window: recorded with its name.</summary>
    Application,

    /// <summary>The desktop: recorded at the screen position.</summary>
    Desktop,

    /// <summary>The taskbar: its buttons move, so a click that switches windows becomes "bring to front".</summary>
    Taskbar,

    /// <summary>The Alt+Tab or Task View screen: never recorded.</summary>
    TaskSwitcher,

    /// <summary>A window of Macro Studio itself: never recorded.</summary>
    MacroStudio,
}
