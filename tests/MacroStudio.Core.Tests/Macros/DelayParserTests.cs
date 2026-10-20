using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Tests.Macros;

public sealed class DelayParserTests
{
    [Theory]
    [InlineData("230", 230)]
    [InlineData(" 230 ", 230)]
    [InlineData("230 ms", 230)]
    [InlineData("230ms", 230)]
    [InlineData("0,23 sn", 230)]
    [InlineData("0.23 s", 230)]
    [InlineData("1,5 saniye", 1500)]
    [InlineData("2 SN", 2000)]
    [InlineData("0", 0)]
    public void Parse_UnderstandsTurkishAndEnglishWriting(string text, int expectedMs)
    {
        Assert.Equal(expectedMs, DelayParser.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("5 dakika")]
    [InlineData("99999999")]
    public void Parse_RejectsWhatIsNotAWait(string text)
    {
        Assert.Null(DelayParser.Parse(text));
    }
}
