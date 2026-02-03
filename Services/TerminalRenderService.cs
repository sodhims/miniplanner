using System.Text;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Universal terminal rendering service - provides consistent terminal styling across all templates.
/// All templates (Circuit, STS, QMaker, Flowchart, etc.) use this service for terminal rendering.
/// </summary>
public class TerminalRenderService
{
    // Universal terminal dimensions
    public const double StickOut = 12.0;      // How far terminal extends from node
    public const double Radius = 7.0;         // Terminal circle radius
    public const double HitRadius = 15.0;     // Clickable area radius
    public const double StrokeWidth = 2.0;    // Terminal circle stroke width
    public const double StemWidth = 2.0;      // Stem line width

    // Universal colors by terminal type
    public static class Colors
    {
        public const string Input = "#22c55e";        // Green
        public const string Output = "#ef4444";       // Red
        public const string Bidirectional = "#3b82f6"; // Blue

        // Hover/active states
        public const string InputHover = "#16a34a";
        public const string OutputHover = "#dc2626";
        public const string BidirectionalHover = "#2563eb";

        // Connected state (slightly darker)
        public const string InputConnected = "#15803d";
        public const string OutputConnected = "#b91c1c";
        public const string BidirectionalConnected = "#1d4ed8";
    }

    /// <summary>
    /// Get the color for a terminal based on its type
    /// </summary>
    public static string GetTerminalColor(TerminalType type, string? customColor = null)
    {
        if (!string.IsNullOrEmpty(customColor))
            return customColor;

        return type switch
        {
            TerminalType.Input => Colors.Input,
            TerminalType.Output => Colors.Output,
            TerminalType.Bidirectional => Colors.Bidirectional,
            _ => Colors.Input
        };
    }

    /// <summary>
    /// Get the stick-out offset for a terminal based on direction
    /// </summary>
    public static (double x, double y) GetStickOutOffset(TerminalDirection direction, double stickOut = StickOut)
    {
        return direction switch
        {
            TerminalDirection.Left => (-stickOut, 0),
            TerminalDirection.Right => (stickOut, 0),
            TerminalDirection.Top => (0, -stickOut),
            TerminalDirection.Bottom => (0, stickOut),
            _ => (stickOut, 0) // Default to right
        };
    }

    /// <summary>
    /// Render a single terminal (stem line + circle) as SVG
    /// Uses filled circles with colors based on terminal type
    /// </summary>
    public static string RenderTerminal(
        double baseX, double baseY,
        TerminalDirection direction,
        TerminalType type,
        string? customColor = null,
        double stickOut = StickOut,
        double radius = Radius,
        double strokeWidth = StrokeWidth)
    {
        var color = GetTerminalColor(type, customColor);
        var (offsetX, offsetY) = GetStickOutOffset(direction, stickOut);
        var termX = baseX + offsetX;
        var termY = baseY + offsetY;

        var sb = new StringBuilder();

        if (type == TerminalType.Bidirectional)
        {
            // Bidirectional: split circle - half input color, half output color
            sb.Append($"<line x1=\"{baseX:F1}\" y1=\"{baseY:F1}\" x2=\"{termX:F1}\" y2=\"{termY:F1}\" ");
            sb.Append($"stroke=\"#6b7280\" stroke-width=\"{StemWidth}\" stroke-linecap=\"round\" />");
            sb.Append(RenderSplitCircle(termX, termY, radius, Colors.Input, Colors.Output, direction));
        }
        else
        {
            // Single color filled terminal
            sb.Append($"<line x1=\"{baseX:F1}\" y1=\"{baseY:F1}\" x2=\"{termX:F1}\" y2=\"{termY:F1}\" ");
            sb.Append($"stroke=\"{color}\" stroke-width=\"{StemWidth}\" stroke-linecap=\"round\" />");
            sb.Append($"<circle cx=\"{termX:F1}\" cy=\"{termY:F1}\" r=\"{radius:F1}\" ");
            sb.Append($"fill=\"{color}\" stroke=\"white\" stroke-width=\"{strokeWidth:F1}\" />");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Render a split circle (half one color, half another) for bidirectional terminals
    /// The split is oriented based on the terminal direction
    /// </summary>
    private static string RenderSplitCircle(double cx, double cy, double radius, string color1, string color2, TerminalDirection dir)
    {
        var sb = new StringBuilder();

        // Determine split orientation based on terminal direction
        // For horizontal terminals (left/right), split vertically (top/bottom halves)
        // For vertical terminals (top/bottom), split horizontally (left/right halves)
        bool horizontalSplit = dir == TerminalDirection.Left || dir == TerminalDirection.Right;

        if (horizontalSplit)
        {
            // Split into top half (color1/input) and bottom half (color2/output)
            sb.Append($"<path d=\"M {cx - radius:F1} {cy:F1} A {radius:F1} {radius:F1} 0 0 1 {cx + radius:F1} {cy:F1}\" fill=\"{color1}\" />");
            sb.Append($"<path d=\"M {cx + radius:F1} {cy:F1} A {radius:F1} {radius:F1} 0 0 1 {cx - radius:F1} {cy:F1}\" fill=\"{color2}\" />");
        }
        else
        {
            // Split into left half (color1/input) and right half (color2/output)
            sb.Append($"<path d=\"M {cx:F1} {cy - radius:F1} A {radius:F1} {radius:F1} 0 0 0 {cx:F1} {cy + radius:F1}\" fill=\"{color1}\" />");
            sb.Append($"<path d=\"M {cx:F1} {cy + radius:F1} A {radius:F1} {radius:F1} 0 0 0 {cx:F1} {cy - radius:F1}\" fill=\"{color2}\" />");
        }

        // White stroke around the whole circle
        sb.Append($"<circle cx=\"{cx:F1}\" cy=\"{cy:F1}\" r=\"{radius:F1}\" fill=\"none\" stroke=\"white\" stroke-width=\"{StrokeWidth:F1}\" />");

        return sb.ToString();
    }

    private static TerminalDirection GetOppositeDirection(TerminalDirection dir)
    {
        return dir switch
        {
            TerminalDirection.Left => TerminalDirection.Right,
            TerminalDirection.Right => TerminalDirection.Left,
            TerminalDirection.Top => TerminalDirection.Bottom,
            TerminalDirection.Bottom => TerminalDirection.Top,
            _ => TerminalDirection.Right
        };
    }

    /// <summary>
    /// Create standard terminal definitions for a shape with input on left, output on right
    /// </summary>
    public static List<TerminalDefinition> CreateLeftRightTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "input",
                Name = "In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output",
                Name = "Out",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            }
        };
    }

    /// <summary>
    /// Create terminal definitions for a fork node (1 input, 2 outputs)
    /// </summary>
    public static List<TerminalDefinition> CreateForkTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "input",
                Name = "In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output1",
                Name = "Out 1",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.25,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output2",
                Name = "Out 2",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.75,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            }
        };
    }

    /// <summary>
    /// Create terminal definitions for a join node (2 inputs, 1 output)
    /// </summary>
    public static List<TerminalDefinition> CreateJoinTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "input1",
                Name = "In 1",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.25,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "input2",
                Name = "In 2",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.75,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output",
                Name = "Out",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            }
        };
    }

    /// <summary>
    /// Create terminal definitions for a source node (output only)
    /// </summary>
    public static List<TerminalDefinition> CreateSourceTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "output",
                Name = "Out",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            }
        };
    }

    /// <summary>
    /// Create terminal definitions for a sink node (input only)
    /// </summary>
    public static List<TerminalDefinition> CreateSinkTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "input",
                Name = "In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            }
        };
    }

    /// <summary>
    /// Create terminal definitions for a router node (1 input, 3 outputs)
    /// </summary>
    public static List<TerminalDefinition> CreateRouterTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "input",
                Name = "In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output1",
                Name = "Out 1",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.2,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output2",
                Name = "Out 2",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "output3",
                Name = "Out 3",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.8,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            }
        };
    }
}
