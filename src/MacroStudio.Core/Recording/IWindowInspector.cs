namespace MacroStudio.Core.Recording;

public interface IWindowInspector
{
    /// <summary>Program, title, class and kind of a window seen during recording.</summary>
    WindowInfo Describe(CapturedWindow window);
}
