using MacroStudio.Core.Geometry;
using MacroStudio.Core.Macros;

namespace MacroStudio.Engine.Recording;

/// <summary>
/// Reduces a drag path to the points where it changes direction (Ramer–Douglas–Peucker).
/// A one-second drag gives hundreds of points; a straight line needs only its two ends.
/// </summary>
public static class PathSimplifier
{
    public static IReadOnlyList<ScreenPoint> Simplify(IReadOnlyList<ScreenPoint> points, double tolerancePx)
    {
        IReadOnlyList<ScreenPoint> result = SimplifyOnce(points, tolerancePx);

        // Very long, shaky paths are simplified harder until they fit the file limit.
        while (result.Count > MacroValidator.MaxPathPoints)
        {
            tolerancePx *= 2;
            result = SimplifyOnce(points, tolerancePx);
        }

        return result;
    }

    private static List<ScreenPoint> SimplifyOnce(IReadOnlyList<ScreenPoint> points, double tolerancePx)
    {
        if (points.Count <= 2)
        {
            return [.. points];
        }

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[^1] = true;
        MarkPointsToKeep(points, 0, points.Count - 1, tolerancePx, keep);
        return points.Where((_, index) => keep[index]).ToList();
    }

    private static void MarkPointsToKeep(IReadOnlyList<ScreenPoint> points, int first, int last, double tolerancePx, bool[] keep)
    {
        double maxDistance = 0;
        int farthest = -1;
        for (int i = first + 1; i < last; i++)
        {
            double distance = DistanceToLine(points[i], points[first], points[last]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthest = i;
            }
        }

        if (farthest < 0 || maxDistance <= tolerancePx)
        {
            return;
        }

        keep[farthest] = true;
        MarkPointsToKeep(points, first, farthest, tolerancePx, keep);
        MarkPointsToKeep(points, farthest, last, tolerancePx, keep);
    }

    private static double DistanceToLine(ScreenPoint point, ScreenPoint lineStart, ScreenPoint lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length == 0)
        {
            return Math.Sqrt(Math.Pow(point.X - lineStart.X, 2) + Math.Pow(point.Y - lineStart.Y, 2));
        }

        return Math.Abs((dy * point.X) - (dx * point.Y) + (lineEnd.X * lineStart.Y) - (lineEnd.Y * lineStart.X)) / length;
    }
}
