using Microsoft.AspNetCore.Components;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    /// <summary>
    /// Generate a parallel path offset from the original edge path.
    /// </summary>
    private string GetParallelPathForEdge(Edge edge, double offset)
    {
        // Get the from and to nodes
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
        if (fromNode == null || toNode == null) return edge.PathData ?? "";

        // Get start and end points
        var (startX, startY) = GeometryService.GetConnectionPointCoordinates(
            fromNode, edge.FromConnection.Side, edge.FromConnection.Position);
        var (endX, endY) = GeometryService.GetConnectionPointCoordinates(
            toNode, edge.ToConnection.Side, edge.ToConnection.Position);

        // Build list of points along the path
        var points = new List<(double x, double y)>();
        points.Add((startX, startY));

        foreach (var wp in edge.Waypoints)
        {
            points.Add((wp.X, wp.Y));
        }
        points.Add((endX, endY));

        // Generate offset points for parallel line
        var offsetPoints = new List<(double x, double y)>();

        for (int i = 0; i < points.Count; i++)
        {
            double perpX = 0, perpY = 0;

            if (i == 0 && points.Count > 1)
            {
                // First point - use direction to next point
                var dx = points[1].x - points[0].x;
                var dy = points[1].y - points[0].y;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0)
                {
                    perpX = -dy / len;
                    perpY = dx / len;
                }
            }
            else if (i == points.Count - 1 && points.Count > 1)
            {
                // Last point - use direction from previous point
                var dx = points[i].x - points[i - 1].x;
                var dy = points[i].y - points[i - 1].y;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0)
                {
                    perpX = -dy / len;
                    perpY = dx / len;
                }
            }
            else if (points.Count > 2)
            {
                // Middle point - average of incoming and outgoing directions
                var dx1 = points[i].x - points[i - 1].x;
                var dy1 = points[i].y - points[i - 1].y;
                var len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);

                var dx2 = points[i + 1].x - points[i].x;
                var dy2 = points[i + 1].y - points[i].y;
                var len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                if (len1 > 0 && len2 > 0)
                {
                    var perpX1 = -dy1 / len1;
                    var perpY1 = dx1 / len1;
                    var perpX2 = -dy2 / len2;
                    var perpY2 = dx2 / len2;

                    perpX = (perpX1 + perpX2) / 2;
                    perpY = (perpY1 + perpY2) / 2;

                    // Normalize the average
                    var perpLen = Math.Sqrt(perpX * perpX + perpY * perpY);
                    if (perpLen > 0)
                    {
                        perpX /= perpLen;
                        perpY /= perpLen;
                    }
                }
            }

            offsetPoints.Add((points[i].x + perpX * offset, points[i].y + perpY * offset));
        }

        // Build SVG path from offset points
        if (offsetPoints.Count < 2) return edge.PathData ?? "";

        var pathBuilder = new System.Text.StringBuilder();
        pathBuilder.Append($"M {offsetPoints[0].x:F1} {offsetPoints[0].y:F1}");

        for (int i = 1; i < offsetPoints.Count; i++)
        {
            pathBuilder.Append($" L {offsetPoints[i].x:F1} {offsetPoints[i].y:F1}");
        }

        return pathBuilder.ToString();
    }

    /// <summary>
    /// Generate cross-hatch marks along an edge path for double-line visualization.
    /// Returns a list of line segments (x1, y1, x2, y2) for the cross marks.
    /// </summary>
    private List<(double x1, double y1, double x2, double y2)> GetCrossHatchMarks(Edge edge, double markLength = 8, double spacing = 30)
    {
        var marks = new List<(double x1, double y1, double x2, double y2)>();

        // Get the from and to nodes
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
        if (fromNode == null || toNode == null) return marks;

        // Get start and end points
        var (startX, startY) = GeometryService.GetConnectionPointCoordinates(
            fromNode, edge.FromConnection.Side, edge.FromConnection.Position);
        var (endX, endY) = GeometryService.GetConnectionPointCoordinates(
            toNode, edge.ToConnection.Side, edge.ToConnection.Position);

        // Build list of points along the path
        var points = new List<(double x, double y)>();
        points.Add((startX, startY));

        foreach (var wp in edge.Waypoints)
        {
            points.Add((wp.X, wp.Y));
        }
        points.Add((endX, endY));

        // Calculate total path length and generate marks at intervals
        double totalLength = 0;
        var segments = new List<(double x1, double y1, double x2, double y2, double length)>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            var segLength = Math.Sqrt(Math.Pow(p2.x - p1.x, 2) + Math.Pow(p2.y - p1.y, 2));
            segments.Add((p1.x, p1.y, p2.x, p2.y, segLength));
            totalLength += segLength;
        }

        if (totalLength < spacing * 2) return marks;

        // Generate marks at regular intervals, starting from spacing/2
        double distanceTraveled = spacing;
        int segmentIndex = 0;
        double distanceInSegment = spacing;

        while (distanceTraveled < totalLength - spacing / 2 && segmentIndex < segments.Count)
        {
            var seg = segments[segmentIndex];

            if (distanceInSegment <= seg.length)
            {
                // Calculate point on this segment
                var t = distanceInSegment / seg.length;
                var px = seg.x1 + t * (seg.x2 - seg.x1);
                var py = seg.y1 + t * (seg.y2 - seg.y1);

                // Calculate perpendicular direction
                var dx = seg.x2 - seg.x1;
                var dy = seg.y2 - seg.y1;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0)
                {
                    var perpX = -dy / len;
                    var perpY = dx / len;

                    // Create mark endpoints
                    marks.Add((
                        px - perpX * markLength / 2,
                        py - perpY * markLength / 2,
                        px + perpX * markLength / 2,
                        py + perpY * markLength / 2
                    ));
                }

                distanceInSegment += spacing;
                distanceTraveled += spacing;
            }
            else
            {
                // Move to next segment
                distanceInSegment -= seg.length;
                segmentIndex++;
            }
        }

        return marks;
    }

    private RenderFragment RenderEdgeLabel(Edge edge, (double X, double Y) midpoint) => builder =>
    {
        // Calculate offset perpendicular to edge direction so label doesn't overlap the line
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);

        double offsetX = 0, offsetY = -12; // Default offset above the line

        if (fromNode != null && toNode != null)
        {
            // Get edge direction
            var dx = (toNode.X + toNode.Width / 2) - (fromNode.X + fromNode.Width / 2);
            var dy = (toNode.Y + toNode.Height / 2) - (fromNode.Y + fromNode.Height / 2);
            var len = Math.Sqrt(dx * dx + dy * dy);

            if (len > 0)
            {
                // Perpendicular direction (rotated 90 degrees)
                var perpX = -dy / len;
                var perpY = dx / len;

                // Offset distance from line
                var offsetDist = 12;
                offsetX = perpX * offsetDist;
                offsetY = perpY * offsetDist;
            }
        }

        var labelX = midpoint.X + offsetX;
        var labelY = midpoint.Y + offsetY;

        // Estimate text width for background
        var textWidth = edge.Label.Length * 7 + 8;
        var textHeight = 16;

        // Background rectangle (transparent)
        builder.OpenElement(0, "rect");
        builder.AddAttribute(1, "x", labelX - textWidth / 2);
        builder.AddAttribute(2, "y", labelY - textHeight / 2);
        builder.AddAttribute(3, "width", textWidth);
        builder.AddAttribute(4, "height", textHeight);
        builder.AddAttribute(5, "fill", "none");
        builder.AddAttribute(6, "rx", "3");
        builder.AddAttribute(7, "style", "pointer-events: none;");
        builder.CloseElement();

        // Label text
        builder.OpenElement(8, "text");
        builder.AddAttribute(9, "x", labelX);
        builder.AddAttribute(10, "y", labelY);
        builder.AddAttribute(11, "text-anchor", "middle");
        builder.AddAttribute(12, "dominant-baseline", "middle");
        builder.AddAttribute(13, "fill", "#374151");
        builder.AddAttribute(14, "font-size", "12");
        builder.AddAttribute(15, "style", "pointer-events: none; user-select: none;");
        builder.AddContent(16, edge.Label);
        builder.CloseElement();
    };

    private RenderFragment RenderRowGuideLabel(double y, int rowNumber) => builder =>
    {
        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "x", "10");
        builder.AddAttribute(2, "y", (y - 5).ToString());
        builder.AddAttribute(3, "fill", "#ef4444");
        builder.AddAttribute(4, "font-size", "12");
        builder.AddAttribute(5, "font-weight", "bold");
        builder.AddContent(6, $"Row {rowNumber} — {y} px");
        builder.CloseElement();
    };

    private RenderFragment RenderColumnGuideLabel(double x, int columnNumber) => builder =>
    {
        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "x", (x + 5).ToString());
        builder.AddAttribute(2, "y", "20");
        builder.AddAttribute(3, "fill", "#3b82f6");
        builder.AddAttribute(4, "font-size", "12");
        builder.AddAttribute(5, "font-weight", "bold");
        builder.AddContent(6, $"Col {columnNumber} — {x} px");
        builder.CloseElement();
    };

    private RenderFragment RenderNodeText(Node node) => builder =>
    {
        var textLines = node.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var lineHeight = 16.0;
        var centerX = node.Width / 2;
        
        if (textLines.Length <= 1)
        {
            builder.OpenElement(0, "text");
            builder.AddAttribute(1, "x", centerX.ToString());
            builder.AddAttribute(2, "y", (node.Height / 2).ToString());
            builder.AddAttribute(3, "text-anchor", "middle");
            builder.AddAttribute(4, "dominant-baseline", "middle");
            builder.AddAttribute(5, "fill", "#374151");
            builder.AddAttribute(6, "font-size", "14");
            builder.AddAttribute(7, "style", "pointer-events: none; user-select: none;");
            builder.AddContent(8, node.Text);
            builder.CloseElement();
        }
        else
        {
            var totalHeight = textLines.Length * lineHeight;
            var startY = (node.Height - totalHeight) / 2 + lineHeight / 2;
            
            for (int i = 0; i < textLines.Length; i++)
            {
                var lineY = startY + i * lineHeight;
                builder.OpenElement(0, "text");
                builder.AddAttribute(1, "x", centerX.ToString());
                builder.AddAttribute(2, "y", lineY.ToString());
                builder.AddAttribute(3, "text-anchor", "middle");
                builder.AddAttribute(4, "dominant-baseline", "middle");
                builder.AddAttribute(5, "fill", "#374151");
                builder.AddAttribute(6, "font-size", "14");
                builder.AddAttribute(7, "style", "pointer-events: none; user-select: none;");
                builder.AddContent(8, textLines[i]);
                builder.CloseElement();
            }
        }
    };

    private RenderFragment CreateSvgText(string x, string y, string content, string? fill = "#374151", string? fontSize = "14", string? fontWeight = "bold") => builder =>
    {
        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "x", x);
        builder.AddAttribute(2, "y", y);
        builder.AddAttribute(3, "fill", fill);
        builder.AddAttribute(4, "font-size", fontSize);
        builder.AddAttribute(5, "font-weight", fontWeight);
        builder.AddAttribute(6, "style", "pointer-events: none; user-select: none;");
        builder.AddContent(7, content);
        builder.CloseElement();
    };

    private RenderFragment RenderSvgText(string content, double x, double y, 
        string textAnchor, string dominantBaseline, string fontSize, string fontWeight, 
        string fill, string? transform = null) => builder =>
    {
        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "x", x);
        builder.AddAttribute(2, "y", y);
        builder.AddAttribute(3, "text-anchor", textAnchor);
        builder.AddAttribute(4, "dominant-baseline", dominantBaseline);
        builder.AddAttribute(5, "font-size", fontSize);
        builder.AddAttribute(6, "font-weight", fontWeight);
        builder.AddAttribute(7, "fill", fill);
        if (!string.IsNullOrEmpty(transform))
        {
            builder.AddAttribute(8, "transform", transform);
        }
        builder.AddAttribute(9, "style", "pointer-events: none;");
        builder.AddContent(10, content);
        builder.CloseElement();
    };
}
