using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MacroStudio.App.NotchBar;

/// <summary>
/// Upside-down trapezoid that hangs from the top edge of the screen: wide on top, narrow at the bottom,
/// slanted sides and rounded bottom corners. Its geometry follows Width and Height, so both can be animated.
/// </summary>
public sealed class NotchShape : Shape
{
    // Horizontal run of each slanted side as a share of the height (about 59 degrees).
    private const double SlantPerHeight = 0.6;
    private const double MaxCornerRadius = 8;

    // Shape asks for this again after every layout pass, so a changed Width or Height redraws the notch.
    protected override Geometry DefiningGeometry
    {
        get
        {
            double width = double.IsNaN(Width) ? ActualWidth : Width;
            double height = double.IsNaN(Height) ? ActualHeight : Height;
            return Create(width, height);
        }
    }

    private static Geometry Create(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return Geometry.Empty;
        }

        double slant = height * SlantPerHeight;
        double radius = Math.Min(MaxCornerRadius, Math.Min(height / 2, slant));

        var topLeft = new Point(0, 0);
        var topRight = new Point(width, 0);
        var bottomRight = new Point(width - slant, height);
        var bottomLeft = new Point(slant, height);

        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            // The figure starts at the top right and is left open, so the top edge (the screen edge) gets no stroke.
            context.BeginFigure(topRight, isFilled: true, isClosed: false);
            AddRoundedCorner(context, topRight, bottomRight, bottomLeft, radius);
            AddRoundedCorner(context, bottomRight, bottomLeft, topLeft, radius);
            context.LineTo(topLeft, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static void AddRoundedCorner(StreamGeometryContext context, Point from, Point corner, Point to, double radius)
    {
        Point start = corner + (Normalize(from - corner) * radius);
        Point end = corner + (Normalize(to - corner) * radius);
        context.LineTo(start, isStroked: true, isSmoothJoin: false);
        context.QuadraticBezierTo(corner, end, isStroked: true, isSmoothJoin: true);
    }

    private static Vector Normalize(Vector vector)
    {
        vector.Normalize();
        return vector;
    }
}
