using MacroStudio.Core.Geometry;

namespace MacroStudio.Core.Tests.Macros;

public sealed class ImageMatcherTests
{
    [Fact]
    public void Find_LocatesAButtonOnANoisyBackground()
    {
        Bgra32Image screen = Noise(300, 200, seed: 1);
        Bgra32Image button = Noise(30, 12, seed: 2);
        Paste(button, screen, 211, 147);

        Assert.Equal((211, 147), ImageMatcher.Find(screen, button));
    }

    [Fact]
    public void Find_ToleratesSlightlyDifferentColorsAndAFewWrongPixels()
    {
        Bgra32Image screen = Noise(200, 100, seed: 3);
        Bgra32Image button = Noise(40, 20, seed: 4);
        Paste(button, screen, 50, 60);
        for (int i = 0; i < screen.Pixels.Length; i++)
        {
            screen.Pixels[i] = (byte)Math.Min(255, screen.Pixels[i] + 10);
        }

        // A caret blinking over the button: one column of 20 pixels is wholly different.
        for (int y = 0; y < 20; y++)
        {
            Set(screen, 70, 60 + y, 0, 0, 0);
        }

        Assert.Equal((50, 60), ImageMatcher.Find(screen, button));
    }

    [Fact]
    public void Find_ReturnsNothingWhenThePictureIsNotThere()
    {
        Assert.Null(ImageMatcher.Find(Noise(200, 100, seed: 5), Noise(40, 20, seed: 6)));
    }

    internal static Bgra32Image Noise(int width, int height, int seed)
    {
        var random = new Random(seed);
        var pixels = new byte[width * height * 4];
        random.NextBytes(pixels);
        return new Bgra32Image(width, height, pixels);
    }

    internal static void Paste(Bgra32Image picture, Bgra32Image screen, int left, int top)
    {
        for (int y = 0; y < picture.Height; y++)
        {
            Array.Copy(picture.Pixels, y * picture.Width * 4, screen.Pixels, (((top + y) * screen.Width) + left) * 4, picture.Width * 4);
        }
    }

    private static void Set(Bgra32Image image, int x, int y, byte r, byte g, byte b)
    {
        int i = ((y * image.Width) + x) * 4;
        (image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2]) = (b, g, r);
    }
}
