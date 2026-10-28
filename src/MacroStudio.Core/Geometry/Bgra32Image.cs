namespace MacroStudio.Core.Geometry;

/// <summary>A picture as Windows hands screen pixels over: 4 bytes per pixel (blue, green, red, unused), rows top to bottom.</summary>
public sealed record Bgra32Image(int Width, int Height, byte[] Pixels)
{
    public int ColorAt(int x, int y)
    {
        int i = ((y * Width) + x) * 4;
        return (Pixels[i + 2] << 16) | (Pixels[i + 1] << 8) | Pixels[i];
    }
}

/// <summary>Finds a small picture (a button) inside a large one (the window).</summary>
public static class ImageMatcher
{
    // Per color channel: screens, themes and smoothing change a few levels.
    private const int Tolerance = 24;

    // Some pixels may differ altogether (a blinking caret, a hover shadow).
    private const double MaxDifferentShare = 0.05;

    /// <returns>The top-left corner of the best match; null when there is none.</returns>
    public static (int X, int Y)? Find(Bgra32Image screen, Bgra32Image picture)
    {
        if (picture.Width > screen.Width || picture.Height > screen.Height || picture.Width == 0 || picture.Height == 0)
        {
            return null;
        }

        // A few spread-out pixels first: almost every place fails on one of them, so the full check is rare.
        (int X, int Y)[] samples =
        [
            (picture.Width / 2, picture.Height / 2), (0, 0), (picture.Width - 1, 0), (0, picture.Height - 1),
            (picture.Width - 1, picture.Height - 1), (picture.Width / 4, picture.Height / 3), ((picture.Width * 3) / 4, (picture.Height * 2) / 3),
        ];
        int allowedDifferent = (int)(picture.Width * picture.Height * MaxDifferentShare);
        (int X, int Y)? best = null;
        int bestDifferent = int.MaxValue;
        for (int y = 0; y <= screen.Height - picture.Height; y++)
        {
            for (int x = 0; x <= screen.Width - picture.Width; x++)
            {
                int sampleMisses = 0;
                foreach ((int sx, int sy) in samples)
                {
                    if (!IsClose(screen, x + sx, y + sy, picture, sx, sy) && ++sampleMisses > 1)
                    {
                        break;
                    }
                }

                if (sampleMisses > 1)
                {
                    continue;
                }

                int different = CountDifferent(screen, x, y, picture, Math.Min(allowedDifferent, bestDifferent - 1));
                if (different < bestDifferent)
                {
                    best = (x, y);
                    bestDifferent = different;
                    if (different == 0)
                    {
                        return best;
                    }
                }
            }
        }

        return best;
    }

    /// <returns>The number of different pixels; more than <paramref name="limit"/> stops counting early.</returns>
    private static int CountDifferent(Bgra32Image screen, int left, int top, Bgra32Image picture, int limit)
    {
        int different = 0;
        for (int y = 0; y < picture.Height; y++)
        {
            for (int x = 0; x < picture.Width; x++)
            {
                if (!IsClose(screen, left + x, top + y, picture, x, y) && ++different > limit)
                {
                    return int.MaxValue;
                }
            }
        }

        return different;
    }

    private static bool IsClose(Bgra32Image a, int ax, int ay, Bgra32Image b, int bx, int by)
    {
        int i = ((ay * a.Width) + ax) * 4;
        int j = ((by * b.Width) + bx) * 4;
        return Math.Abs(a.Pixels[i] - b.Pixels[j]) <= Tolerance
            && Math.Abs(a.Pixels[i + 1] - b.Pixels[j + 1]) <= Tolerance
            && Math.Abs(a.Pixels[i + 2] - b.Pixels[j + 2]) <= Tolerance;
    }
}
