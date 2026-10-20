using System.ComponentModel;
using System.Runtime.InteropServices;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

/// <summary>
/// Reports when the user starts and stops dragging or resizing any window.
/// Events arrive on the thread that created the watcher; that thread needs a message loop (the UI thread).
/// </summary>
public sealed unsafe class WindowDragWatcher : IDisposable
{
    private static readonly Dictionary<nint, WindowDragWatcher> ActiveWatchers = [];

    private readonly nint _hook;

    public WindowDragWatcher()
    {
        _hook = SetWinEventHook(
            EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND, 0, &OnWinEvent, 0, 0, WINEVENT_OUTOFCONTEXT);
        if (_hook == 0)
        {
            throw new Win32Exception();
        }

        ActiveWatchers[_hook] = this;
    }

    public event EventHandler? DragStarted;

    public event EventHandler? DragEnded;

    public void Dispose()
    {
        if (ActiveWatchers.Remove(_hook))
        {
            UnhookWinEvent(_hook);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(
        nint hook, uint eventType, nint hwnd, int objectId, int childId, uint threadId, uint timeMs)
    {
        if (!ActiveWatchers.TryGetValue(hook, out WindowDragWatcher? watcher))
        {
            return;
        }

        EventHandler? handler = eventType == EVENT_SYSTEM_MOVESIZESTART ? watcher.DragStarted : watcher.DragEnded;
        handler?.Invoke(watcher, EventArgs.Empty);
    }
}
