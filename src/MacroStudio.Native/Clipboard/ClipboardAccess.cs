using System.Runtime.InteropServices;
using MacroStudio.Core.Playback;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Clipboard;

/// <summary>
/// Reads and writes clipboard text with the Win32 functions, which work from any thread
/// (the WPF clipboard needs the user interface thread).
/// </summary>
public sealed class ClipboardAccess : IClipboard
{
    private const int OpenAttempts = 10;
    private static readonly TimeSpan OpenRetryInterval = TimeSpan.FromMilliseconds(20);

    public uint ChangeCount => GetClipboardSequenceNumber();

    public string? ReadText()
    {
        if (!TryOpen())
        {
            return null;
        }

        try
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            {
                return "";
            }

            nint data = GetClipboardData(CF_UNICODETEXT);
            nint text = data == 0 ? 0 : GlobalLock(data);
            if (text == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(text) ?? "";
            }
            finally
            {
                GlobalUnlock(data);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public bool TryWriteText(string text)
    {
        nint memory = GlobalAlloc(GMEM_MOVEABLE, (nuint)((text.Length + 1) * sizeof(char)));
        if (memory == 0)
        {
            return false;
        }

        nint target = GlobalLock(memory);
        Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
        Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
        GlobalUnlock(memory);

        if (!TryOpen())
        {
            GlobalFree(memory);
            return false;
        }

        try
        {
            // On success Windows owns the memory; on failure it is ours to free.
            if (EmptyClipboard() && SetClipboardData(CF_UNICODETEXT, memory) != 0)
            {
                return true;
            }

            GlobalFree(memory);
            return false;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Another program may hold the clipboard for a moment while it fills it.</summary>
    private static bool TryOpen()
    {
        for (int attempt = 1; attempt <= OpenAttempts; attempt++)
        {
            if (OpenClipboard(0))
            {
                return true;
            }

            Thread.Sleep(OpenRetryInterval);
        }

        return false;
    }
}
