using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Playback;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

public sealed unsafe class WindowSystem : IWindowSystem
{
    // Menus cannot be activated; while open they are already in front.
    private const string MenuClassName = "#32768";
    private const int ActivationChecks = 10;
    private static readonly TimeSpan ActivationCheckInterval = TimeSpan.FromMilliseconds(20);

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private readonly bool _isOurProcessElevated = Environment.IsPrivilegedProcess;

    public nint ForegroundWindow => GetForegroundWindow();

    public IReadOnlyList<WindowCandidate> ListWindows()
    {
        var handles = new List<nint>();
        GCHandle state = GCHandle.Alloc(handles);
        try
        {
            EnumWindows(&CollectHandle, GCHandle.ToIntPtr(state));
        }
        finally
        {
            state.Free();
        }

        var windows = new List<WindowCandidate>();
        foreach (nint hwnd in handles)
        {
            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == _ownProcessId || !WindowInterop.IsShown(hwnd) || GetBounds(hwnd) is not ScreenRect bounds)
            {
                continue;
            }

            windows.Add(new WindowCandidate(hwnd, ProcessNames.Of(processId), WindowText.ClassNameOf(hwnd), WindowText.TitleOf(hwnd), bounds));
        }

        return windows;
    }

    public bool TryActivate(nint window)
    {
        if (WindowText.ClassNameOf(window) == MenuClassName)
        {
            return true;
        }

        if (IsIconic(window))
        {
            ShowWindow(window, SW_RESTORE);
        }

        if (SetForegroundWindow(window) && GetForegroundWindow() == window)
        {
            return true;
        }

        // Windows lets only the program that has the keyboard input change the front window. Sharing that
        // program's input state for a moment is the documented way around it. Sharing needs a message queue
        // on this thread, and the playback thread has none until it asks for one.
        PeekMessage(out _, 0, 0, 0, PM_NOREMOVE);
        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint ownThread = GetCurrentThreadId();
        bool attached = foregroundThread != ownThread && AttachThreadInput(ownThread, foregroundThread, true);
        try
        {
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(ownThread, foregroundThread, false);
            }
        }

        for (int check = 0; check < ActivationChecks; check++)
        {
            if (GetForegroundWindow() == window)
            {
                return true;
            }

            Thread.Sleep(ActivationCheckInterval);
        }

        return false;
    }

    public ScreenRect? GetBounds(nint window) =>
        GetWindowRect(window, out RECT rect) ? new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom) : null;

    public bool TryMove(nint window, ScreenRect bounds)
    {
        // A place on a monitor that is no longer connected would hide the window.
        var center = new POINT { X = bounds.Left + (bounds.Width / 2), Y = bounds.Top + (bounds.Height / 2) };
        if (MonitorFromPoint(center, MONITOR_DEFAULTTONULL) == 0)
        {
            return false;
        }

        // A maximized window ignores a new size until it is restored.
        if (IsZoomed(window))
        {
            ShowWindow(window, SW_RESTORE);
        }

        return SetWindowPos(window, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    public bool IsElevatedAboveUs(nint window)
    {
        if (_isOurProcessElevated)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out uint processId);
        nint process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return false;
        }

        try
        {
            // A program that does not even let us read its rights runs above us.
            if (!OpenProcessToken(process, TOKEN_QUERY, out nint token))
            {
                return true;
            }

            try
            {
                return GetTokenInformation(token, TokenElevation, out int isElevated, sizeof(int), out _) && isElevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public bool TryOpen(string path, string arguments)
    {
        try
        {
            // %USERPROFILE% and the like work as in the Run box. The started program is not waited for.
            var start = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(path.Trim()), arguments) { UseShellExecute = true };
            Process.Start(start)?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }

    public int? PixelAt(ScreenPoint point) => ScreenPixels.At(point);

    public Bgra32Image? Capture(ScreenRect area) => ScreenPixels.Capture(area);

    public ScreenRect DesktopBounds
    {
        get
        {
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            return new ScreenRect(left, top, left + GetSystemMetrics(SM_CXVIRTUALSCREEN), top + GetSystemMetrics(SM_CYVIRTUALSCREEN));
        }
    }

    [UnmanagedCallersOnly]
    private static int CollectHandle(nint hwnd, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }
}
