using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

internal static unsafe class WindowText
{
    private const int MaxLength = 256;

    public static string ClassNameOf(nint hwnd)
    {
        char* buffer = stackalloc char[MaxLength];
        return new string(buffer, 0, GetClassName(hwnd, buffer, MaxLength));
    }

    /// <summary>Uses InternalGetWindowText, which never waits for the window, so a hung program cannot block it.</summary>
    public static string TitleOf(nint hwnd)
    {
        char* buffer = stackalloc char[MaxLength];
        return new string(buffer, 0, InternalGetWindowText(hwnd, buffer, MaxLength));
    }
}
