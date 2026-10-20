using System.Runtime.InteropServices;
using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Displays;

public static unsafe class Monitors
{
    private const double DefaultDpi = 96.0;

    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var handles = new List<nint>();
        GCHandle state = GCHandle.Alloc(handles);
        try
        {
            EnumDisplayMonitors(0, 0, &CollectHandle, GCHandle.ToIntPtr(state));
        }
        finally
        {
            state.Free();
        }

        return handles.Select(Describe).ToList();
    }

    /// <summary>Size and scale of the primary monitor, as stored with a recording.</summary>
    public static ScreenInfo DescribePrimary()
    {
        MonitorInfo primary = GetAll().First(m => m.IsPrimary);
        return new ScreenInfo(primary.Bounds.Width, primary.Bounds.Height, primary.Scale);
    }

    /// <summary>The monitor with the given device name, or the primary monitor when it is not connected.</summary>
    public static MonitorInfo FindOrPrimary(string? deviceName)
    {
        IReadOnlyList<MonitorInfo> all = GetAll();
        return all.FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            ?? all.First(m => m.IsPrimary);
    }

    [UnmanagedCallersOnly]
    private static int CollectHandle(nint monitor, nint hdc, RECT* clip, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(monitor);
        return 1;
    }

    private static MonitorInfo Describe(nint monitor)
    {
        var info = new MONITORINFOEXW { Size = sizeof(MONITORINFOEXW) };
        GetMonitorInfo(monitor, ref info);

        double dpi = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 ? dpiX : DefaultDpi;

        return new MonitorInfo(
            DeviceName: new string(info.DeviceName),
            Bounds: ToScreenRect(info.Monitor),
            WorkArea: ToScreenRect(info.WorkArea),
            IsPrimary: (info.Flags & MONITORINFOF_PRIMARY) != 0,
            Scale: dpi / DefaultDpi);
    }

    private static ScreenRect ToScreenRect(RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}
