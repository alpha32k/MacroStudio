using System.IO;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

internal static unsafe class ProcessNames
{
    private const int MaxPathLength = 1024;

    /// <summary>"EXCEL" for EXCEL.EXE; empty when the process has ended. Fast enough to call for every window.</summary>
    public static string Of(uint processId)
    {
        nint process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return "";
        }

        try
        {
            char* buffer = stackalloc char[MaxPathLength];
            int length = MaxPathLength;
            return QueryFullProcessImageName(process, 0, buffer, ref length)
                ? Path.GetFileNameWithoutExtension(new string(buffer, 0, length))
                : "";
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
