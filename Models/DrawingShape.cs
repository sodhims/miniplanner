namespace dfd2wasm.Models;

/// <summary>
/// Drawing tool types for freehand sketching and shape drawing
/// </summary>
public enum DrawTool
{
    Pencil,     // Freehand drawing (existing FreehandStroke)
    Line,       // Straight line
    Rectangle,  // Rectangle/Square
    Ellipse,    // Ellipse/Circle
    Arrow,      // Line with arrowhead
    Triangle    // Triangle
}

/// <summary>
/// Brush types for different drawing styles
/// </summary>
public enum BrushType
{
    Pen,        // Fine, consistent line
    Marker,     // Thicker, semi-transparent
    Highlighter,// Wide, very transparent (for highlighting)
    Chalk,      // Rough, textured look
    Brush       // Variable width (pressure-like)
}

/// <summary>
/// Predefined stroke presets for quick selection
/// </summary>
public static class StrokePresets
{
    public static readonly StrokePreset FinePen = new("Fine Pen", "#374151", 1, "", 1.0);
    public static readonly StrokePreset MediumPen = new("Medium Pen", "#374151", 2, "", 1.0);
    public static readonly StrokePreset ThickPen = new("Thick Pen", "#374151", 4, "", 1.0);
    public static readonly StrokePreset Marker = new("Marker", "#3b82f6", 6, "", 0.8);
    public static readonly StrokePreset Highlighter = new("Highlighter", "#fbbf24", 16, "", 0.4);
    public static readonly StrokePreset DashedLine = new("Dashed", "#374151", 2, "8,4", 1.0);
    public static readonly StrokePreset DottedLine = new("Dotted", "#374151", 2, "2,4", 1.0);
    public static readonly StrokePreset ChalkLine = new("Chalk", "#64748b", 3, "1,3", 0.9);
    public static readonly StrokePreset RedPen = new("Red Pen", "#ef4444", 2, "", 1.0);
    public static readonly StrokePreset BluePen = new("Blue Pen", "#3b82f6", 2, "", 1.0);
    public static readonly StrokePreset GreenPen = new("Green Pen", "#22c55e", 2, "", 1.0);

    public static StrokePreset[] All => new[]
    {
        FinePen, MediumPen, ThickPen, Marker, Highlighter,
        DashedLine, DottedLine, ChalkLine,
        RedPen, BluePen, GreenPen
    };
}

/// <summary>
/// A stroke preset definition
/// </summary>
public record StrokePreset(string Name, string Color, int Width, string DashArray, double Opacity);

/// <summary>
/// Base class for all drawing shapes on the canvas
/// </summary>
public abstract class DrawingShape
{
    public int Id { get; set; }
    public string StrokeColor { get; set; } = "#374151";
    public int StrokeWidth { get; set; } = 2;
    public string StrokeDashArray { get; set; } = "";
    public string? FillColor { get; set; }
    public bool IsSelected { get; set; }
    public bool IsComplete { get; set; }

    /// <summary>
    /// Get the bounding box of this shape
    /// </summary>
    public abstract (double X, double Y, double Width, double Height) GetBounds();

    /// <summary>
    /// Generate SVG element for this shape
    /// </summary>
    public abstract string ToSvg();

    /// <summary>
    /// Check if a point is near this shape (for selection)
    /// </summary>
    public virtual bool HitTest(double x, double y, double tolerance = 8)
    {
        var bounds = GetBounds();
        return x >= bounds.X - tolerance && x <= bounds.X + bounds.Width + tolerance &&
               y >= bounds.Y - tolerance && y <= bounds.Y + bounds.Height + tolerance;
    }

    /// <summary>
    /// Move the shape by delta
    /// </summary>
    public abstract void Move(double dx, double dy);
}

/// <summary>
/// A straight line between two points
/// </summary>
public class LineShape : DrawingShape
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    public override (double X, double Y, double Width, double Height) GetBounds()
    {
        var minX = Math.Min(X1, X2);
        var minY = Math.Min(Y1, Y2);
        var maxX = Math.Max(X1, X2);
        var maxY = Math.Max(Y1, Y2);
        return (minX, minY, maxX - minX, maxY - minY);
    }

    public override string ToSvg()
    {
        var stroke = IsSelected ? "#3b82f6" : StrokeColor;
        var width = IsSelected ? StrokeWidth + 2 : StrokeWidth;
        return $@"<line x1=""{X1:F1}"" y1=""{Y1:F1}"" x2=""{X2:F1}"" y2=""{Y2:F1}""
                   stroke=""{stroke}"" stroke-width=""{width}"" stroke-dasharray=""{StrokeDashArray}""
                   stroke-linecap=""round"" />";
    }

    public override bool HitTest(double x, double y, double tolerance = 8)
    {
        // Point-to-line-segment distance
        var dist = PointToSegmentDistance(x, y, X1, Y1, X2, Y2);
        return dist <= tolerance + StrokeWidth / 2;
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

    public override void Move(double dx, double dy)
    {
        X1 += dx; Y1 += dy;
        X2 += dx; Y2 += dy;
    }
}

/// <summary>
/// A rectangle shape
/// </summary>
public class RectShape : DrawingShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double CornerRadius { get; set; } = 0;

    public override (double X, double Y, double Width, double Height) GetBounds()
        => (X, Y, Width, Height);

    public override string ToSvg()
    {
        var stroke = IsSelected ? "#3b82f6" : StrokeColor;
        var width = IsSelected ? StrokeWidth + 2 : StrokeWidth;
        var fill = FillColor ?? "none";
        return $@"<rect x=""{X:F1}"" y=""{Y:F1}"" width=""{Width:F1}"" height=""{Height:F1}""
                   rx=""{CornerRadius}"" ry=""{CornerRadius}""
                   stroke=""{stroke}"" stroke-width=""{width}"" stroke-dasharray=""{StrokeDashArray}""
                   fill=""{fill}"" />";
    }

    public override void Move(double dx, double dy)
    {
        X += dx;
        Y += dy;
    }
}

/// <summary>
/// An ellipse/circle shape
/// </summary>
public class EllipseShape : DrawingShape
{
    public double Cx { get; set; }
    public double Cy { get; set; }
    public double Rx { get; set; }
    public double Ry { get; set; }

    public override (double X, double Y, double Width, double Height) GetBounds()
        => (Cx - Rx, Cy - Ry, Rx * 2, Ry * 2);

    public override string ToSvg()
    {
        var stroke = IsSelected ? "#3b82f6" : StrokeColor;
        var width = IsSelected ? StrokeWidth + 2 : StrokeWidth;
        var fill = FillColor ?? "none";
        return $@"<ellipse cx=""{Cx:F1}"" cy=""{Cy:F1}"" rx=""{Rx:F1}"" ry=""{Ry:F1}""
                   stroke=""{stroke}"" stroke-width=""{width}"" stroke-dasharray=""{StrokeDashArray}""
                   fill=""{fill}"" />";
    }

    public override bool HitTest(double x, double y, double tolerance = 8)
    {
        // Check if point is near ellipse boundary or inside
        var dx = (x - Cx) / (Rx + tolerance);
        var dy = (y - Cy) / (Ry + tolerance);
        var distance = dx * dx + dy * dy;

        // Inside or on boundary
        if (distance <= 1.0) return true;

        // Near the stroke
        var innerDx = (x - Cx) / Math.Max(1, Rx - tolerance);
        var innerDy = (y - Cy) / Math.Max(1, Ry - tolerance);
        var innerDistance = innerDx * innerDx + innerDy * innerDy;
        return distance <= 1.0 || (innerDistance >= 1.0 && distance <= 1.0 + tolerance / Math.Max(Rx, Ry));
    }

    public override void Move(double dx, double dy)
    {
        Cx += dx;
        Cy += dy;
    }
}

/// <summary>
/// A line with arrowhead
/// </summary>
public class ArrowShape : DrawingShape
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double HeadSize { get; set; } = 12;

    public override (double X, double Y, double Width, double Height) GetBounds()
    {
        var minX = Math.Min(X1, X2) - HeadSize;
        var minY = Math.Min(Y1, Y2) - HeadSize;
        var maxX = Math.Max(X1, X2) + HeadSize;
        var maxY = Math.Max(Y1, Y2) + HeadSize;
        return (minX, minY, maxX - minX, maxY - minY);
    }

    public override string ToSvg()
    {
        var stroke = IsSelected ? "#3b82f6" : StrokeColor;
        var width = IsSelected ? StrokeWidth + 2 : StrokeWidth;

        // Calculate arrow head points
        var angle = Math.Atan2(Y2 - Y1, X2 - X1);
        var headAngle = Math.PI / 6; // 30 degrees

        var hx1 = X2 - HeadSize * Math.Cos(angle - headAngle);
        var hy1 = Y2 - HeadSize * Math.Sin(angle - headAngle);
        var hx2 = X2 - HeadSize * Math.Cos(angle + headAngle);
        var hy2 = Y2 - HeadSize * Math.Sin(angle + headAngle);

        return $@"<g>
            <line x1=""{X1:F1}"" y1=""{Y1:F1}"" x2=""{X2:F1}"" y2=""{Y2:F1}""
                  stroke=""{stroke}"" stroke-width=""{width}"" stroke-dasharray=""{StrokeDashArray}""
                  stroke-linecap=""round"" />
            <polygon points=""{X2:F1},{Y2:F1} {hx1:F1},{hy1:F1} {hx2:F1},{hy2:F1}""
                     fill=""{stroke}"" />
        </g>";
    }

    public override bool HitTest(double x, double y, double tolerance = 8)
    {
        var dist = PointToSegmentDistance(x, y, X1, Y1, X2, Y2);
        return dist <= tolerance + StrokeWidth / 2;
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

    public override void Move(double dx, double dy)
    {
        X1 += dx; Y1 += dy;
        X2 += dx; Y2 += dy;
    }
}

/// <summary>
/// A triangle shape
/// </summary>
public class TriangleShape : DrawingShape
{
    public double X1 { get; set; } // Apex
    public double Y1 { get; set; }
    public double X2 { get; set; } // Bottom left
    public double Y2 { get; set; }
    public double X3 { get; set; } // Bottom right
    public double Y3 { get; set; }

    public override (double X, double Y, double Width, double Height) GetBounds()
    {
        var minX = Math.Min(X1, Math.Min(X2, X3));
        var minY = Math.Min(Y1, Math.Min(Y2, Y3));
        var maxX = Math.Max(X1, Math.Max(X2, X3));
        var maxY = Math.Max(Y1, Math.Max(Y2, Y3));
        return (minX, minY, maxX - minX, maxY - minY);
    }

    public override string ToSvg()
    {
        var stroke = IsSelected ? "#3b82f6" : StrokeColor;
        var width = IsSelected ? StrokeWidth + 2 : StrokeWidth;
        var fill = FillColor ?? "none";
        return $@"<polygon points=""{X1:F1},{Y1:F1} {X2:F1},{Y2:F1} {X3:F1},{Y3:F1}""
                   stroke=""{stroke}"" stroke-width=""{width}"" stroke-dasharray=""{StrokeDashArray}""
                   fill=""{fill}"" stroke-linejoin=""round"" />";
    }

    public override void Move(double dx, double dy)
    {
        X1 += dx; Y1 += dy;
        X2 += dx; Y2 += dy;
        X3 += dx; Y3 += dy;
    }
}
