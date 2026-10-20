using MacroStudio.Core.Geometry;
using MacroStudio.Engine.Recording;

namespace MacroStudio.Engine.Tests.Recording;

public sealed class PathSimplifierTests
{
    [Fact]
    public void StraightLine_KeepsOnlyItsEnds()
    {
        ScreenPoint[] line = [.. Enumerable.Range(0, 50).Select(i => new ScreenPoint(i * 2, i))];

        Assert.Equal([line[0], line[^1]], PathSimplifier.Simplify(line, 2));
    }

    [Fact]
    public void Corner_IsKept()
    {
        ScreenPoint[] path = [new(0, 0), new(50, 0), new(100, 0), new(100, 50), new(100, 100)];

        Assert.Equal([new ScreenPoint(0, 0), new ScreenPoint(100, 0), new ScreenPoint(100, 100)], PathSimplifier.Simplify(path, 2));
    }
}
