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
        var target = new WindowTarget(ProcessNames.Of(processId), WindowText.TitleOf(window.Handle), className, window.Bounds);
        return new WindowInfo(RoleOf(processId, className), target);
    }

    private WindowRole RoleOf(uint processId, string className)
    {
        if (processId == _ownProcessId)
        {
            return WindowRole.MacroStudio;
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
