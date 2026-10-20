namespace MacroStudio.Core.Recording;

public enum WindowRole
{
    /// <summary>A normal program window: recorded with its name.</summary>
    Application,

    /// <summary>The desktop: recorded at the screen position.</summary>
    Desktop,

    /// <summary>The taskbar: its buttons move, so a click that switches windows becomes "bring to front".</summary>
    Taskbar,

    /// <summary>
    /// A panel of Windows itself (Start, search, notifications): it opens with a key or a click and cannot be brought
    /// to the front like a window, so steps in it are recorded without a window.
    /// </summary>
    ShellPanel,

    /// <summary>A window that is not shown, such as a host window of Windows itself: never recorded as a window.</summary>
    Hidden,

    /// <summary>The Alt+Tab or Task View screen: never recorded.</summary>
    TaskSwitcher,

    /// <summary>A window of Macro Studio itself: never recorded.</summary>
    MacroStudio,
}
