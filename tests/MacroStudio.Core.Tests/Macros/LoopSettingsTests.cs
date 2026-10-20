using MacroStudio.Core.Macros;

namespace MacroStudio.Core.Tests.Macros;

public sealed class LoopSettingsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 10)]
    [InlineData(LoopSettings.MaxCount + 1, LoopSettings.MaxCount)]
    public void Times_KeepsTheCountInRange(int requested, int expected)
    {
        LoopSettings loop = LoopSettings.Times(requested);

        Assert.Equal(LoopMode.Count, loop.Mode);
        Assert.Equal(expected, loop.Count);
    }
}
