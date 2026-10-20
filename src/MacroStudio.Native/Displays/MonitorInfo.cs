using MacroStudio.Core.Geometry;

namespace MacroStudio.Native.Displays;

/// <param name="DeviceName">Stable Windows name such as \\.\DISPLAY2.</param>
/// <param name="Scale">Display scale: 1.0 = 100 %, 1.5 = 150 %.</param>
public sealed record MonitorInfo(
    string DeviceName,
    ScreenRect Bounds,
    ScreenRect WorkArea,
    bool IsPrimary,
    double Scale)
{
    /// <summary>The number Windows shows in display settings, taken from the device name.</summary>
    public int DisplayNumber =>
        int.TryParse(DeviceName.AsSpan(DeviceName.LastIndexOf('Y') + 1), out int number) ? number : 0;
}
