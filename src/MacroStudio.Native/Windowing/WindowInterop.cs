using MacroStudio.Core.Geometry;
using MacroStudio.Core.Recording;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

public static unsafe class WindowInterop
{
    /// <summary>
    /// Clicking the window never takes the keyboard focus from the window the user works in,
    /// and the window is hidden from the taskbar and Alt+Tab.
    /// </summary>
    public static void MakeNonActivatingToolWindow(nint hwnd)
    {
        nint style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, style | (nint)(WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
    }

    /// <summary>
    /// A click-through window lets clicks, and Windows' "which window is under this point", pass to the window
    /// below. The closed notch bar needs this: otherwise its invisible part would swallow recorded clicks.
    /// </summary>
    public static void SetClickThrough(nint hwnd, bool isClickThrough)
    {
        nint style = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        nint updated = isClickThrough ? style | (nint)WS_EX_TRANSPARENT : style & ~(nint)WS_EX_TRANSPARENT;
        if (updated != style)
        {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, updated);
        }
    }

    /// <summary>Moves the window to a position in physical pixels without resizing or activating it.</summary>
    public static void MoveTo(nint hwnd, int x, int y) =>
        SetWindowPos(hwnd, 0, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    public static ScreenRect GetBounds(nint hwnd) =>
        GetWindowRect(hwnd, out RECT rect) ? new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom) : default;

    public static void BringToForeground(nint hwnd) => SetForegroundWindow(hwnd);

    /// <summary>The program window under a screen point, with its position, for adding a click to a macro.</summary>
    public static CapturedWindow TopLevelWindowAt(ScreenPoint point)
    {
        nint hwnd = GetAncestor(WindowFromPoint(new POINT { X = point.X, Y = point.Y }), GA_ROOT);
        return new CapturedWindow(hwnd, GetBounds(hwnd));
    }

    /// <summary>
    /// Windows lets only the program the user just used bring a window to the front. A newly started program
    /// calls this to pass that right on, for example to the copy of Macro Studio that is already running.
    /// </summary>
    public static void AllowOtherProcessToComeToFront() => AllowSetForegroundWindow(ASFW_ANY);

    /// <summary>
    /// True when the foreground window covers exactly the given monitor (slide show, video, game).
    /// Maximized windows are larger than the monitor by their hidden borders, so they do not count.
    /// </summary>
    public static bool IsFullscreenWindowOn(ScreenRect monitorBounds)
    {
        nint hwnd = GetForegroundWindow();
        if (hwnd == 0 || IsDesktopWindow(hwnd) || !GetWindowRect(hwnd, out RECT rect))
        {
            return false;
        }

        return new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom) == monitorBounds;
    }

    private static bool IsDesktopWindow(nint hwnd)
    {
        const int MaxClassNameLength = 64;
        char* buffer = stackalloc char[MaxClassNameLength];
        int length = GetClassName(hwnd, buffer, MaxClassNameLength);
        var className = new ReadOnlySpan<char>(buffer, length);
        return className.SequenceEqual("Progman") || className.SequenceEqual("WorkerW");
    }
}
