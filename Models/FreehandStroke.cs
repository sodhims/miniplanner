namespace dfd2wasm.Models;

/// <summary>
/// Represents a freehand drawing stroke on the canvas.
/// Points are stored as a list of coordinates captured during mouse/pen movement.
/// </summary>
public class FreehandStroke
{
    public int Id { get; set; }

    /// <summary>
    /// Sequential points captured during drawing
    /// </summary>
    public List<StrokePoint> Points { get; set; } = new();

    /// <summary>
    /// Stroke color (hex format, e.g., "#374151")
    /// </summary>
    public string StrokeColor { get; set; } = "#374151";

    /// <summary>
    /// Stroke width in pixels
    /// </summary>
    public int StrokeWidth { get; set; } = 2;

    /// <summary>
    /// Dash pattern (e.g., "5,5" for dashed, "" for solid)
    /// </summary>
    public string StrokeDashArray { get; set; } = "";

    /// <summary>
    /// Whether the stroke is complete (mouse/pen released)
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Whether this stroke is currently selected
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Get the bounding box of this stroke
    /// </summary>
    public (double X, double Y, double Width, double Height) GetBounds()
    {
        if (Points.Count == 0) return (0, 0, 0, 0);

        var minX = Points.Min(p => p.X);
        var minY = Points.Min(p => p.Y);
        var maxX = Points.Max(p => p.X);
        var maxY = Points.Max(p => p.Y);

        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Generate SVG path data for this stroke
    /// </summary>
    public string ToSvgPath()
    {
        if (Points.Count < 2) return string.Empty;

        // Use polyline-style path: M x y L x y L x y ...
        var path = $"M {Points[0].X:F1} {Points[0].Y:F1}";
        for (int i = 1; i < Points.Count; i++)
        {
            path += $" L {Points[i].X:F1} {Points[i].Y:F1}";
        }
        return path;
    }

    /// <summary>
    /// Check if a point is near this stroke (for selection/hit testing)
    /// </summary>
    public bool HitTest(double x, double y, double tolerance = 8)
    {
        if (Points.Count < 2) return false;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            var dist = PointToSegmentDistance(x, y, Points[i].X, Points[i].Y, Points[i + 1].X, Points[i + 1].Y);
            if (dist <= tolerance + StrokeWidth / 2)
                return true;
        }
        return false;
    }

    private static double PointToSegmentDistance(double px, double py, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
            return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

        var t = Math.Max(0, Math.Min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSquared));
        var projX = x1 + t * dx;
        var projY = y1 + t * dy;

        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }
}

/// <summary>
/// A single point in a freehand stroke
/// </summary>
public record StrokePoint(double X, double Y)
{
    /// <summary>
    /// Optional pressure value from stylus (0.0 - 1.0)
    /// </summary>
    public double Pressure { get; init; } = 1.0;
}
