using MacroStudio.Core.Geometry;
using static MacroStudio.Native.Interop.NativeMethods;

namespace MacroStudio.Native.Windowing;

public static class ScreenPixels
{
    /// <summary>A copy of a screen area; null when it cannot be read (locked screen) or is empty.</summary>
    public static unsafe Bgra32Image? Capture(ScreenRect area)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return null;
        }

        nint screen = GetDC(0);
        nint memory = CreateCompatibleDC(screen);

        // A negative height asks for rows top to bottom.
        var header = new BITMAPINFOHEADER { Size = (uint)sizeof(BITMAPINFOHEADER), Width = area.Width, Height = -area.Height, Planes = 1, BitCount = 32 };
        nint bitmap = CreateDIBSection(screen, in header, DIB_RGB_COLORS, out nint bits, 0, 0);
        try
        {
            if (screen == 0 || memory == 0 || bitmap == 0)
            {
                return null;
            }

            nint previous = SelectObject(memory, bitmap);
            bool copied = BitBlt(memory, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, SRCCOPY | CAPTUREBLT);
            SelectObject(memory, previous);
            if (!copied)
            {
                return null;
            }

            var pixels = new byte[area.Width * area.Height * 4];
            new ReadOnlySpan<byte>((void*)bits, pixels.Length).CopyTo(pixels);
            return new Bgra32Image(area.Width, area.Height, pixels);
        }
        finally
        {
            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            if (memory != 0)
            {
                DeleteDC(memory);
            }

            if (screen != 0)
            {
                _ = ReleaseDC(0, screen);
            }
        }
    }

    /// <summary>The color at a screen point as 0xRRGGBB; null when the screen cannot be read (locked, UAC prompt).</summary>
    public static int? At(ScreenPoint point)
    {
        nint screen = GetDC(0);
        if (screen == 0)
        {
            return null;
        }

        try
        {
            uint colorRef = GetPixel(screen, point.X, point.Y);
            if (colorRef == CLR_INVALID)
            {
                return null;
            }

            // Windows keeps colors as 0x00BBGGRR.
            return (int)(((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF));
        }
        finally
        {
            _ = ReleaseDC(0, screen);
        }
    }
}
