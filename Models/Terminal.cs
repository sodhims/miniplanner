namespace dfd2wasm.Models;

/// <summary>
/// Defines a terminal (connection point) on a shape.
/// Terminals are the specific points where edges can connect to nodes.
/// Used in STS (Source-Terminal-Sink) style diagrams.
/// </summary>
public class TerminalDefinition
{
    /// <summary>
    /// Unique identifier for this terminal within the shape
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name (e.g., "In", "Out", "A", "B", "GND", "VCC")
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Terminal type: Input, Output, or Bidirectional
    /// </summary>
    public TerminalType Type { get; set; } = TerminalType.Bidirectional;

    /// <summary>
    /// Position relative to node (0-1 normalized coordinates)
    /// X: 0=left edge, 1=right edge
    /// Y: 0=top edge, 1=bottom edge
    /// </summary>
    public double NormalizedX { get; set; } = 0.5;
    public double NormalizedY { get; set; } = 0.5;

    /// <summary>
    /// Direction the terminal "faces" - determines where edge emerges
    /// </summary>
    public TerminalDirection Direction { get; set; } = TerminalDirection.Auto;

    /// <summary>
    /// Visual style for the terminal
    /// </summary>
    public TerminalStyle Style { get; set; } = TerminalStyle.Circle;

    /// <summary>
    /// Optional color override (null = use default based on type)
    /// </summary>
    public string? Color { get; set; }
}

public enum TerminalType
{
    Input,        // Can only receive connections (green)
    Output,       // Can only send connections (red)
    Bidirectional // Can both send and receive (blue)
}

public enum TerminalDirection
{
    Auto,    // Automatically determine based on position
    Left,
    Right,
    Top,
    Bottom
}

public enum TerminalStyle
{
    Circle,      // Standard filled circle
    Square,      // Square terminal (like IC pins)
    Triangle,    // Arrow-shaped (indicates direction)
    Line,        // Just a line (like circuit terminals)
    Hidden       // Terminal exists but not visually shown
}

/// <summary>
/// Instance of a terminal on a specific node
/// </summary>
public class NodeTerminal
{
    /// <summary>
    /// Reference to the terminal definition ID
    /// </summary>
    public string TerminalId { get; set; } = "";

    /// <summary>
    /// The node this terminal belongs to
    /// </summary>
    public int NodeId { get; set; }

    /// <summary>
    /// Optional label override (e.g., for IC pins: "1", "2", "VCC", "GND")
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Is this terminal connected to an edge?
    /// </summary>
    public bool IsConnected { get; set; }
}

/// <summary>
/// Terminal layout presets for easy cycling (like in layoutbak)
/// </summary>
public static class TerminalLayouts
{
    public const string LeftRight = "left-right";
    public const string RightLeft = "right-left";
    public const string TopBottom = "top-bottom";
    public const string BottomTop = "bottom-top";
    public const string LeftTop = "left-top";
    public const string LeftBottom = "left-bottom";
    public const string RightTop = "right-top";
    public const string RightBottom = "right-bottom";
    public const string TopLeft = "top-left";
    public const string TopRight = "top-right";
    public const string BottomLeft = "bottom-left";
    public const string BottomRight = "bottom-right";

    /// <summary>
    /// Rotate terminal pair clockwise (Ctrl+F style)
    /// </summary>
    public static string GetNextLayout(string current)
    {
        return current switch
        {
            LeftRight => TopBottom,
            TopBottom => RightLeft,
            RightLeft => BottomTop,
            BottomTop => LeftRight,

            LeftTop => TopRight,
            TopRight => RightBottom,
            RightBottom => BottomLeft,
            BottomLeft => LeftTop,

            TopLeft => RightTop,
            RightTop => BottomRight,
            BottomRight => LeftBottom,
            LeftBottom => TopLeft,

            _ => LeftRight
        };
    }

    /// <summary>
    /// Parse layout string into input/output positions
    /// </summary>
    public static (string input, string output) ParseLayout(string layout)
    {
        var parts = layout.Split('-');
        if (parts.Length == 2)
            return (parts[0], parts[1]);
        return ("left", "right");
    }

    /// <summary>
    /// Get normalized coordinates for a position
    /// </summary>
    public static (double x, double y, TerminalDirection dir) GetPositionCoords(string position)
    {
        return position.ToLower() switch
        {
            "left" => (0.0, 0.5, TerminalDirection.Left),
            "right" => (1.0, 0.5, TerminalDirection.Right),
            "top" => (0.5, 0.0, TerminalDirection.Top),
            "bottom" => (0.5, 1.0, TerminalDirection.Bottom),
            _ => (0.5, 0.5, TerminalDirection.Auto)
        };
    }

    /// <summary>
    /// Get the direction for a normalized position on the node boundary
    /// </summary>
    public static TerminalDirection GetDirectionFromNormalizedPosition(double x, double y)
    {
        // Determine which edge the terminal is closest to
        double distLeft = x;
        double distRight = 1.0 - x;
        double distTop = y;
        double distBottom = 1.0 - y;

        double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        if (Math.Abs(minDist - distLeft) < 0.01) return TerminalDirection.Left;
        if (Math.Abs(minDist - distRight) < 0.01) return TerminalDirection.Right;
        if (Math.Abs(minDist - distTop) < 0.01) return TerminalDirection.Top;
        return TerminalDirection.Bottom;
    }

    /// <summary>
    /// Get the side name for a normalized position
    /// </summary>
    public static string GetSideFromNormalizedPosition(double x, double y)
    {
        var dir = GetDirectionFromNormalizedPosition(x, y);
        return dir switch
        {
            TerminalDirection.Left => "left",
            TerminalDirection.Right => "right",
            TerminalDirection.Top => "top",
            TerminalDirection.Bottom => "bottom",
            _ => "right"
        };
    }
}

/// <summary>
/// Configuration for terminal positions and types on a component
/// </summary>
public class ComponentTerminalConfig
{
    public TerminalType T1Type { get; set; } = TerminalType.Input;
    public double T1X { get; set; } = 0.0;  // Normalized X (0=left, 1=right)
    public double T1Y { get; set; } = 0.5;  // Normalized Y (0=top, 1=bottom)

    public TerminalType T2Type { get; set; } = TerminalType.Output;
    public double T2X { get; set; } = 1.0;
    public double T2Y { get; set; } = 0.5;

    public bool HasT3 { get; set; } = false;
    public TerminalType T3Type { get; set; } = TerminalType.Bidirectional;
    public double T3X { get; set; } = 0.5;
    public double T3Y { get; set; } = 1.0;
}
