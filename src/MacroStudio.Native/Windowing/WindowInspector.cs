using MacroStudio.Core.Macros;
using MacroStudio.Core.Recording;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

public sealed unsafe class WindowInspector : IWindowInspector
{
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    public WindowInfo Describe(CapturedWindow window)
    {
        if (window.Handle == 0)
        {
            return new WindowInfo(WindowRole.Desktop, new WindowTarget("", "", "", window.Bounds));
        }

        GetWindowThreadProcessId(window.Handle, out uint processId);
        string className = WindowText.ClassNameOf(window.Handle);
        string processName = ProcessNames.Of(processId);
        var target = new WindowTarget(processName, WindowText.TitleOf(window.Handle), className, window.Bounds);
        return new WindowInfo(RoleOf(window.Handle, processId, processName, className), target);
    }

    private WindowRole RoleOf(nint handle, uint processId, string processName, string className)
    {
        if (processId == _ownProcessId)
        {
            return WindowRole.MacroStudio;
        }

        if (ShellPanels.IsShellPanel(processName, className))
        {
            return WindowRole.ShellPanel;
        }

        // Windows briefly gives the focus to hidden host windows (for example when the search panel closes).
        // Playback could never find them, so they are not recorded as windows.
        if (!WindowInterop.IsShown(handle))
        {
            return WindowRole.Hidden;
        }

        return className switch
        {
            "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "TaskListThumbnailWnd" => WindowRole.Taskbar,
            "Progman" or "WorkerW" => WindowRole.Desktop,
            "MultitaskingViewFrame" or "XamlExplorerHostIslandWindow" or "TaskSwitcherWnd" or "ForegroundStaging" => WindowRole.TaskSwitcher,
            _ => WindowRole.Application,
        };
    }
}
