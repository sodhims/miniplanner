namespace dfd2wasm.Models
{
    // Project dependency types (used when IsProjectDependency == true)
    public enum ProjectDependencyType
    {
        FinishToStart,   // Successor starts after predecessor finishes
        StartToStart,    // Successor starts when predecessor starts
        FinishToFinish,  // Successor finishes when predecessor finishes
        StartToFinish    // Successor finishes when predecessor starts
    }

    // EdgeStyle enum for different connector types
    public enum EdgeStyle
    {
        Direct,      // Straight line A→B
        Ortho,       // Right angles only
        OrthoRound,  // Right angles with rounded corners
        Bezier,      // Smooth S-curve
        Arc,         // Single curved arc
        Stylized,    // Fancy with embellishments
        Circuit,     // Circuit/bus-style orthogonal routing with jumps
        SmartL       // Smart L-shape: horizontal-first or vertical-first based on best fit
    }

    // ArrowDirection enum for arrow placement
    public enum ArrowDirection
    {
        End,         // ---->  (default, arrow at destination)
        Start,       // <----  (arrow at source)
        Both,        // <--->  (arrows at both ends)
        None         // -----  (no arrows)
    }

    public class Edge
    {
        public int Id { get; set; }
        public int From { get; set; }
        public int To { get; set; }
        public ConnectionPoint FromConnection { get; set; }
        public ConnectionPoint ToConnection { get; set; }

        // Terminal-to-terminal connections
        // These identify which terminal the edge connects to (e.g., "input", "output", "third", "extra:1")
        public string? FromTerminal { get; set; }
        public string? ToTerminal { get; set; }

        public int? StrokeWidth { get; set; }
        public string? StrokeColor { get; set; }
        public string? StrokeDashArray { get; set; }
        public bool IsDoubleLine { get; set; }
        public double DoubleLineSpacing { get; set; } = 3.0; // Spacing between parallel lines in pixels

        // Replace IsOrthogonal with EdgeStyle
        public bool IsOrthogonal { get; set; } // Keep for backward compatibility
        public EdgeStyle Style { get; set; } = EdgeStyle.Direct;

        // Arrow direction
        public ArrowDirection ArrowDirection { get; set; } = ArrowDirection.End;

        public string PathData { get; set; } = "";
        public string Label { get; set; } = "";

        public string? CustomFromSide { get; set; }
        public string? CustomToSide { get; set; }

        public List<Waypoint> Waypoints { get; set; } = new();

        // SuperNode edge remapping - tracks original endpoints when edge is remapped to a SuperNode
        // Used to restore edges when SuperNode is expanded
        public int? OriginalFrom { get; set; }
        public int? OriginalTo { get; set; }
        public bool IsHiddenInternal { get; set; } = false;  // True for edges entirely within a collapsed SuperNode

        // Project dependency properties (used when IsProjectDependency == true)
        public bool IsProjectDependency { get; set; } = false;
        public ProjectDependencyType ProjectDepType { get; set; } = ProjectDependencyType.FinishToStart;
        public int ProjectLagDays { get; set; } = 0;
    }

    public class Waypoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        // Layer 0 = normal, 1 = via (jump)
        public int Layer { get; set; } = 0;
    }

    public enum EditorMode
    {
        Select,
        AddNode,
        Draw  // Freehand sketching mode
    }
}
