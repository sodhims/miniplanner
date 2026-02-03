using System.Collections.Generic;
using System.Linq;
using System.Text;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

public class ShapeLibraryService
{
    /// <summary>
    /// Configuration for terminal types on a shape
    /// </summary>
    public record TerminalConfig(
        TerminalType InputType = TerminalType.Input,
        TerminalType OutputType = TerminalType.Output,
        bool HasThirdTerminal = false,
        TerminalType ThirdType = TerminalType.Bidirectional,
        int InputCount = 1,   // Number of input terminals (for splitter/collector)
        int OutputCount = 1   // Number of output terminals (for splitter/collector)
    );

    // RenderFunc returns an SVG fragment (inner markup) for the node
    public record ShapeDescriptor(
        string Id,
        string DisplayName,
        Func<Node, string> Render,
        List<TerminalDefinition>? Terminals = null,  // Optional terminal definitions
        TerminalConfig? TerminalConfig = null  // Optional terminal type configuration
    );
    public record Template(
        string Id,
        string DisplayName,
        List<ShapeDescriptor> Shapes,
        bool UseTerminals = false  // Whether this template uses STS-style terminals
    );

    private readonly Dictionary<string, Template> templates = new();

    // Helper to render circuit component label next to the shape
    // Applies counter-rotation to keep text upright when node is rotated
    private static string RenderCircuitLabel(Node node, double labelX, double labelY, string? anchor = null)
    {
        if (string.IsNullOrEmpty(node.ComponentLabel)) return "";
        var sb = new StringBuilder();
        var textAnchor = anchor ?? "start";

        // Apply counter-rotation to keep text upright
        var counterRotation = GetCounterRotationTransform(node, labelX, labelY);

        sb.Append($"<text x=\"{labelX}\" y=\"{labelY}\" font-size=\"10\" fill=\"{node.StrokeColor}\" font-family=\"sans-serif\" text-anchor=\"{textAnchor}\"{counterRotation}>");
        sb.Append(node.ComponentLabel);
        if (!string.IsNullOrEmpty(node.ComponentValue))
        {
            sb.Append($"<tspan x=\"{labelX}\" dy=\"12\" font-size=\"9\" fill=\"#6b7280\">{node.ComponentValue}</tspan>");
        }
        sb.Append("</text>");
        return sb.ToString();
    }

    // Helper to generate counter-rotation transform attribute for text elements
    private static string GetCounterRotationTransform(Node node, double x, double y)
    {
        if (node.Rotation == 0) return "";
        // Counter-rotate around the text position to keep it upright
        return $" transform=\"rotate({-node.Rotation}, {x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {y.ToString(System.Globalization.CultureInfo.InvariantCulture)})\"";
    }

    // Helper to generate stroke-dasharray attribute for dashed lines
    private static string GetDashAttr(Node node)
    {
        if (string.IsNullOrEmpty(node.StrokeDashArray)) return "";
        return $" stroke-dasharray=\"{node.StrokeDashArray}\"";
    }

    public ShapeLibraryService()
    {
        RegisterFlowchartTemplate();
        RegisterCircuitTemplate();
        RegisterICDTemplate();
        RegisterNetworkTemplate();
        RegisterSTSTemplate();
        RegisterBPMNTemplate();
        RegisterFMECATemplate();
        RegisterProjectTemplate();
        RegisterGanttTemplate();
        RegisterQMakerTemplate();
        RegisterDecisionTreeTemplate();
    }

    private void RegisterProjectTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            // Task bar with beveled edges and progress indicator
            new ShapeDescriptor("task", "Task", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var bevel = Math.Min(6, h / 5);  // Bevel size
                var progress = node.ProjectPercentComplete;
                var fillColor = node.FillColor ?? "#3b82f6";
                var strokeColor = node.StrokeColor ?? "#1d4ed8";
                var strokeWidth = node.StrokeWidth ?? 1;

                var sb = new StringBuilder();

                // Background with beveled edges
                sb.Append($"<path d=\"");
                sb.Append($"M {bevel},0 ");
                sb.Append($"L {w - bevel},0 ");
                sb.Append($"L {w},{bevel} ");
                sb.Append($"L {w},{h - bevel} ");
                sb.Append($"L {w - bevel},{h} ");
                sb.Append($"L {bevel},{h} ");
                sb.Append($"L 0,{h - bevel} ");
                sb.Append($"L 0,{bevel} Z\" ");
                sb.Append($"fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" />");

                // Progress overlay (darker)
                if (progress > 0 && progress < 100)
                {
                    var progressWidth = w * progress / 100.0;
                    // Clip to progress width
                    sb.Append($"<clipPath id=\"prog-{node.Id}\"><rect x=\"0\" y=\"0\" width=\"{progressWidth:F1}\" height=\"{h}\" /></clipPath>");
                    sb.Append($"<path clip-path=\"url(#prog-{node.Id})\" d=\"");
                    sb.Append($"M {bevel},0 L {w - bevel},0 L {w},{bevel} L {w},{h - bevel} ");
                    sb.Append($"L {w - bevel},{h} L {bevel},{h} L 0,{h - bevel} L 0,{bevel} Z\" ");
                    sb.Append($"fill=\"rgba(0,0,0,0.15)\" />");
                }
                else if (progress >= 100)
                {
                    // Full green overlay for complete
                    sb.Append($"<path d=\"");
                    sb.Append($"M {bevel},0 L {w - bevel},0 L {w},{bevel} L {w},{h - bevel} ");
                    sb.Append($"L {w - bevel},{h} L {bevel},{h} L 0,{h - bevel} L 0,{bevel} Z\" ");
                    sb.Append($"fill=\"#22c55e\" stroke=\"#16a34a\" stroke-width=\"{strokeWidth}\" />");
                }

                // 3D highlight on top edge
                sb.Append($"<path d=\"M {bevel + 2},2 L {w - bevel - 2},2\" fill=\"none\" stroke=\"rgba(255,255,255,0.4)\" stroke-width=\"2\" />");

                // 3D shadow on bottom edge
                sb.Append($"<path d=\"M {bevel + 2},{h - 2} L {w - bevel - 2},{h - 2}\" fill=\"none\" stroke=\"rgba(0,0,0,0.15)\" stroke-width=\"2\" />");

                return sb.ToString();
            }),

            // Milestone diamond
            new ShapeDescriptor("milestone", "Milestone", node =>
            {
                var size = Math.Min(node.Width, node.Height);
                var cx = node.Width / 2;
                var cy = node.Height / 2;
                var half = size / 2 - 2;
                var fillColor = node.FillColor ?? "#8b5cf6";
                var strokeColor = node.StrokeColor ?? "#6d28d9";
                var strokeWidth = node.StrokeWidth ?? 2;

                var sb = new StringBuilder();

                // Diamond shape
                sb.Append($"<polygon points=\"{cx},{cy - half} {cx + half},{cy} {cx},{cy + half} {cx - half},{cy}\" ");
                sb.Append($"fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" />");

                // Highlight
                sb.Append($"<line x1=\"{cx - half + 4}\" y1=\"{cy}\" x2=\"{cx}\" y2=\"{cy - half + 4}\" ");
                sb.Append($"stroke=\"rgba(255,255,255,0.5)\" stroke-width=\"2\" stroke-linecap=\"round\" />");

                return sb.ToString();
            }),

            // Summary task (group header) with brackets
            new ShapeDescriptor("summary", "Summary", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var bracketSize = Math.Min(8, h / 2);
                var fillColor = node.FillColor ?? "#1f2937";

                var sb = new StringBuilder();

                // Main bar (thin horizontal)
                sb.Append($"<rect x=\"{bracketSize}\" y=\"{h / 2 - 2}\" width=\"{w - bracketSize * 2}\" height=\"4\" fill=\"{fillColor}\" />");

                // Left bracket (downward pointing)
                sb.Append($"<polygon points=\"0,0 {bracketSize},{h / 2} 0,{h}\" fill=\"{fillColor}\" />");

                // Right bracket (downward pointing)
                sb.Append($"<polygon points=\"{w},0 {w - bracketSize},{h / 2} {w},{h}\" fill=\"{fillColor}\" />");

                return sb.ToString();
            }),

            // Resource types - each appears as a separate shape in dropdown
            CreateResourceShape("resource", "👤 Person", ProjectResourceType.Person),
            CreateResourceShape("resource-team", "👥 Team", ProjectResourceType.Team),
            CreateResourceShape("resource-equipment", "⚙️ Equipment", ProjectResourceType.Equipment),
            CreateResourceShape("resource-vehicle", "🚗 Vehicle", ProjectResourceType.Vehicle),
            CreateResourceShape("resource-machine", "🏭 Machine", ProjectResourceType.Machine),
            CreateResourceShape("resource-tool", "🔧 Tool", ProjectResourceType.Tool),
            CreateResourceShape("resource-material", "📦 Material", ProjectResourceType.Material),
            CreateResourceShape("resource-room", "🏠 Room", ProjectResourceType.Room),
            CreateResourceShape("resource-computer", "💻 Computer", ProjectResourceType.Computer),
            CreateResourceShape("resource-custom", "⭐ Custom", ProjectResourceType.Custom)
        };

        templates["project"] = new Template("project", "Project Chart", shapes);
    }

    /// <summary>Creates a resource shape descriptor for the given resource type</summary>
    private static ShapeDescriptor CreateResourceShape(string id, string name, ProjectResourceType resourceType)
    {
        return new ShapeDescriptor(id, name, node =>
        {
            var size = Math.Min(node.Width, node.Height);
            var cx = node.Width / 2;
            var cy = node.Height / 2;
            var r = size / 2 - 2;
            var fillColor = node.FillColor ?? ProjectTimelineService.GetResourceTypeColor(resourceType);
            var strokeColor = node.StrokeColor ?? ProjectTimelineService.GetResourceTypeStrokeColor(resourceType);
            var strokeWidth = node.StrokeWidth ?? 2;

            var sb = new StringBuilder();

            // Circle background
            sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" />");

            // SVG icon path scaled to fit in circle
            var iconPath = ProjectTimelineService.GetResourceTypeSvgIcon(resourceType);
            var iconScale = (r * 1.2) / 12;  // Icon paths are based on 24x24, so scale from 12 (half)
            var iconOffsetX = cx - 12 * iconScale;
            var iconOffsetY = cy - 12 * iconScale;

            sb.Append($"<g transform=\"translate({iconOffsetX:F1},{iconOffsetY:F1}) scale({iconScale:F2})\">");
            sb.Append($"<path d=\"{iconPath}\" fill=\"white\" />");
            sb.Append("</g>");

            return sb.ToString();
        });
    }

    private void RegisterGanttTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            // Task bar with beveled edges and progress indicator (similar to project task)
            new ShapeDescriptor("task", "Task", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var bevel = Math.Min(4, h / 5);  // Smaller bevel for compact timeline
                var progress = node.GanttPercentComplete;
                var fillColor = node.FillColor ?? "#3b82f6";
                var strokeColor = node.StrokeColor ?? "#1d4ed8";
                var strokeWidth = node.StrokeWidth ?? 1;

                // Check if this task has a violation
                var isViolation = node.GanttIsViolation;
                if (isViolation)
                {
                    strokeColor = "#ef4444"; // Red stroke for violations
                    strokeWidth = 2;
                }

                var sb = new StringBuilder();

                // Background with beveled edges
                sb.Append($"<path d=\"");
                sb.Append($"M {bevel},0 ");
                sb.Append($"L {w - bevel},0 ");
                sb.Append($"L {w},{bevel} ");
                sb.Append($"L {w},{h - bevel} ");
                sb.Append($"L {w - bevel},{h} ");
                sb.Append($"L {bevel},{h} ");
                sb.Append($"L 0,{h - bevel} ");
                sb.Append($"L 0,{bevel} Z\" ");
                sb.Append($"fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" />");

                // Progress overlay (darker)
                if (progress > 0 && progress < 100)
                {
                    var progressWidth = w * progress / 100.0;
                    sb.Append($"<clipPath id=\"gprog-{node.Id}\"><rect x=\"0\" y=\"0\" width=\"{progressWidth:F1}\" height=\"{h}\" /></clipPath>");
                    sb.Append($"<path clip-path=\"url(#gprog-{node.Id})\" d=\"");
                    sb.Append($"M {bevel},0 L {w - bevel},0 L {w},{bevel} L {w},{h - bevel} ");
                    sb.Append($"L {w - bevel},{h} L {bevel},{h} L 0,{h - bevel} L 0,{bevel} Z\" ");
                    sb.Append($"fill=\"rgba(0,0,0,0.15)\" />");
                }
                else if (progress >= 100)
                {
                    // Full green overlay for complete
                    sb.Append($"<path d=\"");
                    sb.Append($"M {bevel},0 L {w - bevel},0 L {w},{bevel} L {w},{h - bevel} ");
                    sb.Append($"L {w - bevel},{h} L {bevel},{h} L 0,{h - bevel} L 0,{bevel} Z\" ");
                    sb.Append($"fill=\"#22c55e\" stroke=\"#16a34a\" stroke-width=\"{strokeWidth}\" />");
                }

                // Violation indicator (flashing red border effect)
                if (isViolation)
                {
                    sb.Append($"<path d=\"M {bevel},0 L {w - bevel},0 L {w},{bevel} L {w},{h - bevel} ");
                    sb.Append($"L {w - bevel},{h} L {bevel},{h} L 0,{h - bevel} L 0,{bevel} Z\" ");
                    sb.Append($"fill=\"none\" stroke=\"#ef4444\" stroke-width=\"2\" stroke-dasharray=\"4,2\" />");
                }

                // 3D highlight on top edge
                sb.Append($"<path d=\"M {bevel + 2},2 L {w - bevel - 2},2\" fill=\"none\" stroke=\"rgba(255,255,255,0.4)\" stroke-width=\"1\" />");

                return sb.ToString();
            }),

            // Job node - rectangle with color swatch
            new ShapeDescriptor("job", "Job", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var fillColor = node.FillColor ?? node.GanttJobColor ?? "#3b82f6";
                var strokeColor = node.StrokeColor ?? "#1d4ed8";
                var strokeWidth = node.StrokeWidth ?? 2;

                var sb = new StringBuilder();

                // Main rectangle with rounded corners
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"6\" ");
                sb.Append($"fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" />");

                // Color swatch indicator at top
                sb.Append($"<rect x=\"4\" y=\"4\" width=\"{w - 8}\" height=\"8\" rx=\"2\" ");
                sb.Append($"fill=\"white\" fill-opacity=\"0.3\" />");

                // Job icon (briefcase-like)
                var iconY = h / 2 + 2;
                sb.Append($"<rect x=\"{w / 2 - 10}\" y=\"{iconY - 6}\" width=\"20\" height=\"12\" rx=\"2\" ");
                sb.Append($"fill=\"none\" stroke=\"white\" stroke-width=\"1.5\" />");
                sb.Append($"<path d=\"M {w / 2 - 5} {iconY - 6} L {w / 2 - 5} {iconY - 9} Q {w / 2 - 5} {iconY - 11} {w / 2 - 3} {iconY - 11} ");
                sb.Append($"L {w / 2 + 3} {iconY - 11} Q {w / 2 + 5} {iconY - 11} {w / 2 + 5} {iconY - 9} L {w / 2 + 5} {iconY - 6}\" ");
                sb.Append($"fill=\"none\" stroke=\"white\" stroke-width=\"1.5\" />");

                return sb.ToString();
            }),

            // Machine node - rectangle with gear icon
            new ShapeDescriptor("machine", "Machine", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var machineType = node.GanttMachineType;
                var fillColor = node.FillColor ?? GanttTimelineService.GetMachineTypeColor(machineType);
                var strokeColor = node.StrokeColor ?? GanttTimelineService.GetMachineTypeStrokeColor(machineType);
                var strokeWidth = node.StrokeWidth ?? 2;

                var sb = new StringBuilder();

                // Main rectangle with rounded corners
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"6\" ");
                sb.Append($"fill=\"{fillColor}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" />");

                // SVG icon path scaled to fit
                var iconPath = GanttTimelineService.GetMachineTypeSvgIcon(machineType);
                var iconSize = Math.Min(w, h) * 0.5;
                var iconScale = iconSize / 24;  // Icon paths are based on 24x24
                var iconOffsetX = (w - 24 * iconScale) / 2;
                var iconOffsetY = (h - 24 * iconScale) / 2;

                sb.Append($"<g transform=\"translate({iconOffsetX:F1},{iconOffsetY:F1}) scale({iconScale:F2})\">");
                sb.Append($"<path d=\"{iconPath}\" fill=\"white\" />");
                sb.Append("</g>");

                return sb.ToString();
            })
        };

        templates["gantt"] = new Template("gantt", "Gantt (Machine Scheduling)", shapes);
    }

    private void RegisterFlowchartTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            new ShapeDescriptor("process", "Process", node =>
            {
                return $"<rect x=\"0\" y=\"0\" width=\"{node.Width}\" height=\"{node.Height}\" rx=\"6\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("decision", "Decision", node =>
            {
                var midX = node.Width / 2;
                var midY = node.Height / 2;
                var points = $"{midX},0 {node.Width},{midY} {midX},{node.Height} 0,{midY}";
                return $"<polygon points=\"{points}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("terminator", "Terminator", node =>
            {
                return $"<rect x=\"0\" y=\"0\" width=\"{node.Width}\" height=\"{node.Height}\" rx=\"{node.Height / 2}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("data", "Data (I/O)", node =>
            {
                var skew = 15.0;
                var points = $"{skew},0 {node.Width},0 {node.Width - skew},{node.Height} 0,{node.Height}";
                return $"<polygon points=\"{points}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("database", "Database", node =>
            {
                var rx = node.Width / 2;
                var ellipseRy = 10.0;
                var cy1 = ellipseRy;
                var cy2 = node.Height - ellipseRy;
                var sb = new StringBuilder();
                sb.Append($"<g style=\"cursor:inherit;\">");
                sb.Append($"<path d=\"M 0,{cy1} Q 0,{cy1 - ellipseRy} {rx},{cy1 - ellipseRy} Q {node.Width},{cy1 - ellipseRy} {node.Width},{cy1} L {node.Width},{cy2} Q {node.Width},{cy2 + ellipseRy} {rx},{cy2 + ellipseRy} Q 0,{cy2 + ellipseRy} 0,{cy2} Z\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<path d=\"M 0,{cy1} Q 0,{cy1 + ellipseRy / 2} {rx},{cy1 + ellipseRy / 2} Q {node.Width},{cy1 + ellipseRy / 2} {node.Width},{cy1}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append("</g>");
                return sb.ToString();
            }),

            new ShapeDescriptor("document", "Document", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var waveHeight = h * 0.15;
                return $"<path d=\"M 0,0 L {w},0 L {w},{h - waveHeight} Q {w * 0.75},{h - waveHeight * 2} {w * 0.5},{h - waveHeight} Q {w * 0.25},{h} 0,{h - waveHeight} Z\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("predefined", "Predefined Process", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var barWidth = w * 0.1;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<line x1=\"{barWidth}\" y1=\"0\" x2=\"{barWidth}\" y2=\"{h}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<line x1=\"{w - barWidth}\" y1=\"0\" x2=\"{w - barWidth}\" y2=\"{h}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                return sb.ToString();
            })
        };

        templates["flowchart"] = new Template("flowchart", "Flowchart", shapes);
    }

    private void RegisterCircuitTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            // Basic Components
            new ShapeDescriptor("resistor", "Resistor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cy = h / 2;
                var segment = w / 8.0;
                var zigHeight = h / 3;
                var pathSb = new StringBuilder();
                pathSb.Append($"M 0,{cy} L {segment},{cy} ");
                pathSb.Append($"L {segment * 1.5},{cy - zigHeight} ");
                pathSb.Append($"L {segment * 2.5},{cy + zigHeight} ");
                pathSb.Append($"L {segment * 3.5},{cy - zigHeight} ");
                pathSb.Append($"L {segment * 4.5},{cy + zigHeight} ");
                pathSb.Append($"L {segment * 5.5},{cy - zigHeight} ");
                pathSb.Append($"L {segment * 6.5},{cy + zigHeight} ");
                pathSb.Append($"L {segment * 7},{cy} ");
                pathSb.Append($"L {w},{cy}");
                var sb = new StringBuilder();
                sb.Append($"<path d=\"{pathSb}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, cy - zigHeight - 8, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("capacitor", "Capacitor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var plateHeight = h * 0.6;
                var gap = w * 0.08;
                var sb = new StringBuilder();
                sb.Append($"<line x1=\"0\" y1=\"{cy}\" x2=\"{cx - gap}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx - gap}\" y1=\"{cy - plateHeight / 2}\" x2=\"{cx - gap}\" y2=\"{cy + plateHeight / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx + gap}\" y1=\"{cy - plateHeight / 2}\" x2=\"{cx + gap}\" y2=\"{cy + plateHeight / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx + gap}\" y1=\"{cy}\" x2=\"{w}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, cx, cy - plateHeight / 2 - 8, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("inductor", "Inductor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cy = h / 2;
                var loopCount = 4;
                var loopWidth = (w - 20) / loopCount;
                var loopRadius = loopWidth / 2;
                var pathSb = new StringBuilder();
                pathSb.Append($"M 0,{cy} L 10,{cy} ");
                for (int i = 0; i < loopCount; i++)
                {
                    var startX = 10 + i * loopWidth;
                    pathSb.Append($"A {loopRadius},{loopRadius} 0 0 1 {startX + loopWidth},{cy} ");
                }
                pathSb.Append($"L {w},{cy}");
                var sb = new StringBuilder();
                sb.Append($"<path d=\"{pathSb}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, cy - loopRadius - 8, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("diode", "Diode", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var triSize = Math.Min(w, h) * 0.4;
                var sb = new StringBuilder();
                sb.Append($"<line x1=\"0\" y1=\"{cy}\" x2=\"{cx - triSize / 2}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<polygon points=\"{cx - triSize / 2},{cy - triSize / 2} {cx - triSize / 2},{cy + triSize / 2} {cx + triSize / 2},{cy}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx + triSize / 2}\" y1=\"{cy - triSize / 2}\" x2=\"{cx + triSize / 2}\" y2=\"{cy + triSize / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx + triSize / 2}\" y1=\"{cy}\" x2=\"{w}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, cx, cy - triSize / 2 - 8, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("transistor-npn", "NPN Transistor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var radius = Math.Min(w, h) * 0.35;
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{radius}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Base line
                sb.Append($"<line x1=\"0\" y1=\"{cy}\" x2=\"{cx - radius * 0.3}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx - radius * 0.3}\" y1=\"{cy - radius * 0.5}\" x2=\"{cx - radius * 0.3}\" y2=\"{cy + radius * 0.5}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Emitter (with arrow)
                sb.Append($"<line x1=\"{cx - radius * 0.3}\" y1=\"{cy + radius * 0.25}\" x2=\"{cx + radius * 0.5}\" y2=\"{cy + radius * 0.7}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx + radius * 0.5}\" y1=\"{cy + radius * 0.7}\" x2=\"{cx + radius * 0.5}\" y2=\"{h}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Collector
                sb.Append($"<line x1=\"{cx - radius * 0.3}\" y1=\"{cy - radius * 0.25}\" x2=\"{cx + radius * 0.5}\" y2=\"{cy - radius * 0.7}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx + radius * 0.5}\" y1=\"{cy - radius * 0.7}\" x2=\"{cx + radius * 0.5}\" y2=\"0\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, cx + radius + 5, cy - radius, "start"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal(), new TerminalConfig(
                InputType: TerminalType.Bidirectional,  // Base
                OutputType: TerminalType.Output,        // Collector
                HasThirdTerminal: true,
                ThirdType: TerminalType.Input           // Emitter
            )),

            new ShapeDescriptor("ground", "Ground", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var sb = new StringBuilder();
                sb.Append($"<line x1=\"{cx}\" y1=\"0\" x2=\"{cx}\" y2=\"{h * 0.4}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx - w * 0.3}\" y1=\"{h * 0.4}\" x2=\"{cx + w * 0.3}\" y2=\"{h * 0.4}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx - w * 0.2}\" y1=\"{h * 0.55}\" x2=\"{cx + w * 0.2}\" y2=\"{h * 0.55}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"{cx - w * 0.1}\" y1=\"{h * 0.7}\" x2=\"{cx + w * 0.1}\" y2=\"{h * 0.7}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, cx + w * 0.35, h * 0.5, "start"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsVertical()),

            new ShapeDescriptor("vcc", "VCC/Power", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var sb = new StringBuilder();
                sb.Append($"<line x1=\"{cx}\" y1=\"{h}\" x2=\"{cx}\" y2=\"{h * 0.4}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<polygon points=\"{cx},{h * 0.15} {cx - w * 0.15},{h * 0.4} {cx + w * 0.15},{h * 0.4}\" fill=\"{node.StrokeColor}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, cx + w * 0.2, h * 0.25, "start"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsVertical()),

            // Logic Gates
            new ShapeDescriptor("and-gate", "AND Gate", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<path d=\"M 0,0 L {w * 0.5},0 A {w * 0.5},{h / 2} 0 0 1 {w * 0.5},{h} L 0,{h} Z\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Input lines
                sb.Append($"<line x1=\"-10\" y1=\"{h * 0.3}\" x2=\"0\" y2=\"{h * 0.3}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<line x1=\"-10\" y1=\"{h * 0.7}\" x2=\"0\" y2=\"{h * 0.7}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Output line
                sb.Append($"<line x1=\"{w}\" y1=\"{h / 2}\" x2=\"{w + 10}\" y2=\"{h / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("or-gate", "OR Gate", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<path d=\"M 0,0 Q {w * 0.3},0 {w * 0.5},{h * 0.1} Q {w},{h * 0.3} {w},{h / 2} Q {w},{h * 0.7} {w * 0.5},{h * 0.9} Q {w * 0.3},{h} 0,{h} Q {w * 0.2},{h / 2} 0,0 Z\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("not-gate", "NOT Gate", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var triWidth = w * 0.8;
                var circleR = w * 0.08;
                var sb = new StringBuilder();
                sb.Append($"<polygon points=\"0,0 {triWidth},{h / 2} 0,{h}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append($"<circle cx=\"{triWidth + circleR}\" cy=\"{h / 2}\" r=\"{circleR}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("ic-chip", "IC Chip", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var pinCount = 4;
                var pinWidth = 8;
                var pinHeight = 4;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"{pinWidth}\" y=\"0\" width=\"{w - pinWidth * 2}\" height=\"{h}\" rx=\"2\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Notch
                sb.Append($"<circle cx=\"{pinWidth + 10}\" cy=\"10\" r=\"4\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                // Pins on left
                for (int i = 0; i < pinCount; i++)
                {
                    var py = (h / (pinCount + 1)) * (i + 1);
                    sb.Append($"<rect x=\"0\" y=\"{py - pinHeight / 2}\" width=\"{pinWidth}\" height=\"{pinHeight}\" fill=\"{node.StrokeColor}\" />");
                }
                // Pins on right
                for (int i = 0; i < pinCount; i++)
                {
                    var py = (h / (pinCount + 1)) * (i + 1);
                    sb.Append($"<rect x=\"{w - pinWidth}\" y=\"{py - pinHeight / 2}\" width=\"{pinWidth}\" height=\"{pinHeight}\" fill=\"{node.StrokeColor}\" />");
                }
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("op-amp", "Op-Amp", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<polygon points=\"0,0 {w},{h / 2} 0,{h}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // + and - inputs
                sb.Append($"<text x=\"8\" y=\"{h * 0.35}\" font-size=\"14\" fill=\"{node.StrokeColor}\">+</text>");
                sb.Append($"<text x=\"8\" y=\"{h * 0.75}\" font-size=\"14\" fill=\"{node.StrokeColor}\">−</text>");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("battery", "Battery", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Battery symbol: alternating long/short lines
                var cx = w / 2;
                var gap = 4;
                var longLineH = h * 0.7;
                var shortLineH = h * 0.4;
                var startY = (h - longLineH) / 2;
                // Left terminal line
                sb.Append($"<line x1=\"0\" y1=\"{h / 2}\" x2=\"{cx - gap * 2}\" y2=\"{h / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Long line (positive)
                sb.Append($"<line x1=\"{cx - gap}\" y1=\"{startY}\" x2=\"{cx - gap}\" y2=\"{startY + longLineH}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Short line (negative)
                sb.Append($"<line x1=\"{cx + gap}\" y1=\"{startY + (longLineH - shortLineH) / 2}\" x2=\"{cx + gap}\" y2=\"{startY + (longLineH + shortLineH) / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2) + 1}\" />");
                // Right terminal line
                sb.Append($"<line x1=\"{cx + gap * 2}\" y1=\"{h / 2}\" x2=\"{w}\" y2=\"{h / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // + and - labels
                sb.Append($"<text x=\"{cx - gap - 3}\" y=\"{startY - 2}\" font-size=\"10\" text-anchor=\"middle\" fill=\"{node.StrokeColor}\">+</text>");
                sb.Append($"<text x=\"{cx + gap + 3}\" y=\"{startY - 2}\" font-size=\"10\" text-anchor=\"middle\" fill=\"{node.StrokeColor}\">−</text>");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("ac-source", "AC Source", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var r = Math.Min(w, h) * 0.35;
                // Circle
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Sine wave inside circle
                var waveW = r * 1.2;
                var waveH = r * 0.5;
                sb.Append($"<path d=\"M {cx - waveW / 2},{cy} Q {cx - waveW / 4},{cy - waveH} {cx},{cy} Q {cx + waveW / 4},{cy + waveH} {cx + waveW / 2},{cy}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Left terminal line
                sb.Append($"<line x1=\"0\" y1=\"{cy}\" x2=\"{cx - r}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Right terminal line
                sb.Append($"<line x1=\"{cx + r}\" y1=\"{cy}\" x2=\"{w}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal()),

            new ShapeDescriptor("dc-source", "DC Source", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var r = Math.Min(w, h) * 0.35;
                // Circle
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{node.FillColor ?? "white"}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // + and - inside circle
                sb.Append($"<text x=\"{cx - r * 0.35}\" y=\"{cy + 5}\" font-size=\"14\" text-anchor=\"middle\" fill=\"{node.StrokeColor}\">+</text>");
                sb.Append($"<text x=\"{cx + r * 0.35}\" y=\"{cy + 5}\" font-size=\"14\" text-anchor=\"middle\" fill=\"{node.StrokeColor}\">−</text>");
                // Left terminal line
                sb.Append($"<line x1=\"0\" y1=\"{cy}\" x2=\"{cx - r}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                // Right terminal line
                sb.Append($"<line x1=\"{cx + r}\" y1=\"{cy}\" x2=\"{w}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{node.StrokeWidth ?? 2}\" />");
                sb.Append(RenderCircuitLabel(node, w / 2, -5, "middle"));
                sb.Append(RenderCircuitTerminals(node, w, h));
                return sb.ToString();
            }, CreateCircuitTerminalsHorizontal())
        };

        templates["circuit"] = new Template("circuit", "Circuit Diagram", shapes, UseTerminals: true);
    }

    // Helper to create circuit component terminals (horizontal: left input, right output)
    private static List<TerminalDefinition> CreateCircuitTerminalsHorizontal()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "in",
                Name = "In",
                Type = TerminalType.Bidirectional, // Circuit connections are bidirectional
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle,
                Color = "#3b82f6" // Blue for bidirectional
            },
            new TerminalDefinition
            {
                Id = "out",
                Name = "Out",
                Type = TerminalType.Bidirectional,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle,
                Color = "#3b82f6"
            }
        };
    }

    // Helper to create circuit component terminals (vertical: top/bottom)
    private static List<TerminalDefinition> CreateCircuitTerminalsVertical()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "in",
                Name = "In",
                Type = TerminalType.Bidirectional,
                NormalizedX = 0.5,
                NormalizedY = 0.0,
                Direction = TerminalDirection.Top,
                Style = TerminalStyle.Circle,
                Color = "#3b82f6"
            },
            new TerminalDefinition
            {
                Id = "out",
                Name = "Out",
                Type = TerminalType.Bidirectional,
                NormalizedX = 0.5,
                NormalizedY = 1.0,
                Direction = TerminalDirection.Bottom,
                Style = TerminalStyle.Circle,
                Color = "#3b82f6"
            }
        };
    }

    // Render circuit terminals (uses custom colors if set, otherwise blue for bidirectional)
    // Now supports precise T1X/T1Y/T2X/T2Y/T3X/T3Y coordinates and third terminal
    private static string RenderCircuitTerminals(Node node, double w, double h)
    {
        if (!node.ShowTerminals) return "";

        var sb = new StringBuilder();
        var stickOut = 12.0;  // Larger stick-out for easier clicking
        var radius = 7.0;     // Larger radius for visibility

        // Use custom colors if set, otherwise default blue for circuit (bidirectional)
        var inputColor = node.InputTerminalColor ?? "#3b82f6";
        var outputColor = node.OutputTerminalColor ?? "#3b82f6";

        // Get T1 (input) position - use precise coordinates if set, otherwise fall back to TerminalLayout
        double inX, inY;
        TerminalDirection inDir;
        if (node.T1X.HasValue && node.T1Y.HasValue)
        {
            inX = node.T1X.Value;
            inY = node.T1Y.Value;
            inDir = TerminalLayouts.GetDirectionFromNormalizedPosition(inX, inY);
        }
        else
        {
            var (inputPos, _) = TerminalLayouts.ParseLayout(node.TerminalLayout);
            (inX, inY, inDir) = TerminalLayouts.GetPositionCoords(inputPos);
        }

        // Get T2 (output) position - use precise coordinates if set, otherwise fall back to TerminalLayout
        double outX, outY;
        TerminalDirection outDir;
        if (node.T2X.HasValue && node.T2Y.HasValue)
        {
            outX = node.T2X.Value;
            outY = node.T2Y.Value;
            outDir = TerminalLayouts.GetDirectionFromNormalizedPosition(outX, outY);
        }
        else
        {
            var (_, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);
            (outX, outY, outDir) = TerminalLayouts.GetPositionCoords(outputPos);
        }

        // Render input terminal (T1) with stem line and label
        var inPx = inX * w;
        var inPy = inY * h;
        var (inStickX, inStickY) = GetStickOutOffset(inDir, stickOut);
        sb.Append($"<line x1=\"{inPx}\" y1=\"{inPy}\" x2=\"{inPx + inStickX}\" y2=\"{inPy + inStickY}\" stroke=\"{inputColor}\" stroke-width=\"2\" />");
        sb.Append($"<circle cx=\"{inPx + inStickX}\" cy=\"{inPy + inStickY}\" r=\"{radius}\" fill=\"{inputColor}\" stroke=\"white\" stroke-width=\"2\" data-terminal-type=\"input\" style=\"cursor: crosshair\" />");
        // T1 label - position based on direction
        var (t1LabelX, t1LabelY, t1Anchor) = GetTerminalLabelPosition(inPx + inStickX, inPy + inStickY, inDir, radius);
        sb.Append($"<text x=\"{t1LabelX}\" y=\"{t1LabelY}\" font-size=\"10\" fill=\"#666\" text-anchor=\"{t1Anchor}\" font-weight=\"bold\">T1</text>");

        // Render output terminal (T2) with stem line and label
        var outPx = outX * w;
        var outPy = outY * h;
        var (outStickX, outStickY) = GetStickOutOffset(outDir, stickOut);
        sb.Append($"<line x1=\"{outPx}\" y1=\"{outPy}\" x2=\"{outPx + outStickX}\" y2=\"{outPy + outStickY}\" stroke=\"{outputColor}\" stroke-width=\"2\" />");
        sb.Append($"<circle cx=\"{outPx + outStickX}\" cy=\"{outPy + outStickY}\" r=\"{radius}\" fill=\"{outputColor}\" stroke=\"white\" stroke-width=\"2\" data-terminal-type=\"output\" style=\"cursor: crosshair\" />");
        // T2 label - position based on direction
        var (t2LabelX, t2LabelY, t2Anchor) = GetTerminalLabelPosition(outPx + outStickX, outPy + outStickY, outDir, radius);
        sb.Append($"<text x=\"{t2LabelX}\" y=\"{t2LabelY}\" font-size=\"10\" fill=\"#666\" text-anchor=\"{t2Anchor}\" font-weight=\"bold\">T2</text>");

        // Render third terminal (T3) if enabled
        if (node.HasThirdTerminal)
        {
            // Get T3 position - use precise coordinates if set, otherwise default to bottom center
            double t3X = node.T3X ?? 0.5;
            double t3Y = node.T3Y ?? 1.0;
            var t3Dir = TerminalLayouts.GetDirectionFromNormalizedPosition(t3X, t3Y);

            // Color based on terminal type
            var thirdColor = node.ThirdTerminalColor ?? node.ThirdTerminalType switch
            {
                TerminalType.Input => "#22c55e",      // green
                TerminalType.Output => "#ef4444",    // red
                TerminalType.Bidirectional => "#3b82f6", // blue
                _ => "#3b82f6"
            };

            var t3Px = t3X * w;
            var t3Py = t3Y * h;
            var (t3StickX, t3StickY) = GetStickOutOffset(t3Dir, stickOut);
            sb.Append($"<line x1=\"{t3Px}\" y1=\"{t3Py}\" x2=\"{t3Px + t3StickX}\" y2=\"{t3Py + t3StickY}\" stroke=\"{thirdColor}\" stroke-width=\"2\" />");
            sb.Append($"<circle cx=\"{t3Px + t3StickX}\" cy=\"{t3Py + t3StickY}\" r=\"{radius}\" fill=\"{thirdColor}\" stroke=\"white\" stroke-width=\"2\" data-terminal-type=\"third\" style=\"cursor: crosshair\" />");
            // T3 label - position based on direction
            var (t3LabelX, t3LabelY, t3Anchor) = GetTerminalLabelPosition(t3Px + t3StickX, t3Py + t3StickY, t3Dir, radius);
            sb.Append($"<text x=\"{t3LabelX}\" y=\"{t3LabelY}\" font-size=\"10\" fill=\"#666\" text-anchor=\"{t3Anchor}\" font-weight=\"bold\">T3</text>");
        }

        return sb.ToString();
    }

    private void RegisterICDTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            // System Blocks
            new ShapeDescriptor("system", "System", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e0f2fe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"18\" rx=\"4\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<text x=\"{w / 2}\" y=\"13\" text-anchor=\"middle\" font-size=\"10\" fill=\"white\" font-weight=\"bold\">SYSTEM</text>");
                return sb.ToString();
            }),

            new ShapeDescriptor("subsystem", "Subsystem", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#fef3c7")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" stroke-dasharray=\"5,3\" />");
                sb.Append($"<text x=\"5\" y=\"14\" font-size=\"9\" fill=\"{node.StrokeColor}\" font-style=\"italic\">subsystem</text>");
                return sb.ToString();
            }),

            new ShapeDescriptor("external-system", "External System", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"0\" fill=\"{(node.FillColor ?? "#fee2e2")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 3)}\" />");
                sb.Append($"<line x1=\"0\" y1=\"18\" x2=\"{w}\" y2=\"18\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<text x=\"{w / 2}\" y=\"13\" text-anchor=\"middle\" font-size=\"9\" fill=\"{node.StrokeColor}\">EXTERNAL</text>");
                return sb.ToString();
            }),

            new ShapeDescriptor("hardware", "Hardware Component", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#d1fae5")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // 3D effect
                sb.Append($"<path d=\"M {w},0 L {w + 8},{-6} L {w + 8},{h - 6} L {w},{h}\" fill=\"{(node.FillColor ?? "#a7f3d0")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<path d=\"M 0,0 L 8,-6 L {w + 8},-6 L {w},0\" fill=\"{(node.FillColor ?? "#6ee7b7")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("software", "Software Component", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"8\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Code icon
                sb.Append($"<text x=\"8\" y=\"{h / 2 + 4}\" font-size=\"12\" fill=\"{node.StrokeColor}\" font-family=\"monospace\">&lt;/&gt;</text>");
                return sb.ToString();
            }),

            // Interface Types
            new ShapeDescriptor("data-interface", "Data Interface", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var arrowSize = 12;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"{arrowSize}\" y=\"0\" width=\"{w - arrowSize * 2}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#f0fdf4")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Left arrow
                sb.Append($"<polygon points=\"0,{h / 2} {arrowSize},{h / 4} {arrowSize},{h * 3 / 4}\" fill=\"{node.StrokeColor}\" />");
                // Right arrow
                sb.Append($"<polygon points=\"{w},{h / 2} {w - arrowSize},{h / 4} {w - arrowSize},{h * 3 / 4}\" fill=\"{node.StrokeColor}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("control-interface", "Control Interface", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#fef9c3")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Control symbol (gear-like)
                var cx = w - 15;
                var cy = 15;
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"8\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"3\" fill=\"{node.StrokeColor}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("power-interface", "Power Interface", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#fee2e2")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Lightning bolt
                sb.Append($"<path d=\"M {w - 20},{5} L {w - 25},{h / 2} L {w - 18},{h / 2} L {w - 22},{h - 5} L {w - 12},{h / 2 - 2} L {w - 18},{h / 2 - 2} L {w - 14},{5} Z\" fill=\"{node.StrokeColor}\" />");
                return sb.ToString();
            }),

            // Connectors
            new ShapeDescriptor("connector-serial", "Serial Port", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // DB9-like pins
                var pinRows = 2;
                var pinsPerRow = new int[] { 5, 4 };
                for (int row = 0; row < pinRows; row++)
                {
                    var pins = pinsPerRow[row];
                    var rowY = h / 3 + row * (h / 3);
                    var startX = (w - (pins * 8 + (pins - 1) * 4)) / 2;
                    for (int p = 0; p < pins; p++)
                    {
                        sb.Append($"<circle cx=\"{startX + p * 12 + 4}\" cy=\"{rowY}\" r=\"3\" fill=\"{node.StrokeColor}\" />");
                    }
                }
                return sb.ToString();
            }),

            new ShapeDescriptor("connector-ethernet", "Ethernet Port", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"2\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // RJ45-like shape
                var portW = w * 0.6;
                var portH = h * 0.5;
                var portX = (w - portW) / 2;
                var portY = (h - portH) / 2;
                sb.Append($"<rect x=\"{portX}\" y=\"{portY}\" width=\"{portW}\" height=\"{portH}\" fill=\"{node.StrokeColor}\" rx=\"2\" />");
                sb.Append($"<rect x=\"{portX + 2}\" y=\"{portY + 2}\" width=\"{portW - 4}\" height=\"{portH * 0.4}\" fill=\"{(node.FillColor ?? "white")}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("connector-usb", "USB Port", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // USB symbol
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy - 8}\" r=\"4\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<line x1=\"{cx}\" y1=\"{cy - 4}\" x2=\"{cx}\" y2=\"{cy + 10}\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<line x1=\"{cx - 8}\" y1=\"{cy + 2}\" x2=\"{cx + 8}\" y2=\"{cy + 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<rect x=\"{cx - 10}\" y=\"{cy}\" width=\"4\" height=\"6\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<rect x=\"{cx + 6}\" y=\"{cy}\" width=\"4\" height=\"6\" fill=\"{node.StrokeColor}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("connector-wireless", "Wireless Interface", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // WiFi symbol
                var cx = w / 2;
                var cy = h / 2 + 5;
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"3\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<path d=\"M {cx - 8},{cy - 8} A 12,12 0 0 1 {cx + 8},{cy - 8}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<path d=\"M {cx - 14},{cy - 14} A 20,20 0 0 1 {cx + 14},{cy - 14}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("interface-block", "Interface Block", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Main block with notched corner
                sb.Append($"<path d=\"M 0,0 L {w - 15},0 L {w},15 L {w},{h} L 0,{h} Z\" fill=\"{(node.FillColor ?? "#f3e8ff")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<path d=\"M {w - 15},0 L {w - 15},15 L {w},15\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            })
        };

        templates["icd"] = new Template("icd", "Interface Control Diagram", shapes);
    }

    private void RegisterNetworkTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            new ShapeDescriptor("router", "Router", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{w / 2}\" cy=\"{h / 2}\" r=\"{Math.Min(w, h) / 2 - 2}\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Arrow cross pattern
                var cx = w / 2;
                var cy = h / 2;
                var arrowLen = Math.Min(w, h) * 0.25;
                sb.Append($"<line x1=\"{cx}\" y1=\"{cy - arrowLen}\" x2=\"{cx}\" y2=\"{cy + arrowLen}\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<line x1=\"{cx - arrowLen}\" y1=\"{cy}\" x2=\"{cx + arrowLen}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                // Arrow heads
                sb.Append($"<polygon points=\"{cx},{cy - arrowLen - 5} {cx - 4},{cy - arrowLen + 2} {cx + 4},{cy - arrowLen + 2}\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<polygon points=\"{cx},{cy + arrowLen + 5} {cx - 4},{cy + arrowLen - 2} {cx + 4},{cy + arrowLen - 2}\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<polygon points=\"{cx - arrowLen - 5},{cy} {cx - arrowLen + 2},{cy - 4} {cx - arrowLen + 2},{cy + 4}\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<polygon points=\"{cx + arrowLen + 5},{cy} {cx + arrowLen - 2},{cy - 4} {cx + arrowLen - 2},{cy + 4}\" fill=\"{node.StrokeColor}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("switch", "Switch", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#d1fae5")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Port indicators
                var portCount = 4;
                var portW = 8;
                var portH = 6;
                var spacing = (w - portCount * portW) / (portCount + 1);
                for (int i = 0; i < portCount; i++)
                {
                    var px = spacing + i * (portW + spacing);
                    sb.Append($"<rect x=\"{px}\" y=\"{h - portH - 4}\" width=\"{portW}\" height=\"{portH}\" fill=\"{node.StrokeColor}\" rx=\"1\" />");
                }
                // Arrows showing switching
                sb.Append($"<path d=\"M {w * 0.3},{h * 0.3} L {w * 0.7},{h * 0.3}\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" marker-end=\"url(#arrowhead)\" />");
                sb.Append($"<path d=\"M {w * 0.7},{h * 0.5} L {w * 0.3},{h * 0.5}\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" marker-end=\"url(#arrowhead)\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("server", "Server", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var unitH = h / 3;
                for (int i = 0; i < 3; i++)
                {
                    var y = i * unitH;
                    sb.Append($"<rect x=\"0\" y=\"{y}\" width=\"{w}\" height=\"{unitH - 2}\" rx=\"2\" fill=\"{(node.FillColor ?? "#e5e7eb")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                    // LED indicators
                    sb.Append($"<circle cx=\"{w - 12}\" cy=\"{y + unitH / 2}\" r=\"3\" fill=\"#22c55e\" />");
                    sb.Append($"<circle cx=\"{w - 24}\" cy=\"{y + unitH / 2}\" r=\"3\" fill=\"#f59e0b\" />");
                    // Drive slots
                    sb.Append($"<rect x=\"8\" y=\"{y + 4}\" width=\"{w * 0.4}\" height=\"{unitH - 10}\" fill=\"{node.StrokeColor}\" rx=\"1\" opacity=\"0.3\" />");
                }
                return sb.ToString();
            }),

            new ShapeDescriptor("firewall", "Firewall", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#fee2e2")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Brick pattern
                var brickH = h / 4;
                var brickW = w / 3;
                for (int row = 0; row < 4; row++)
                {
                    var offset = (row % 2 == 0) ? 0 : brickW / 2;
                    for (double x = offset; x < w; x += brickW)
                    {
                        sb.Append($"<rect x=\"{x}\" y=\"{row * brickH}\" width=\"{Math.Min(brickW - 2, w - x)}\" height=\"{brickH - 2}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                    }
                }
                return sb.ToString();
            }),

            new ShapeDescriptor("cloud", "Cloud", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<path d=\"M {w * 0.25},{h * 0.7} ");
                sb.Append($"A {w * 0.2},{h * 0.25} 0 1 1 {w * 0.35},{h * 0.4} ");
                sb.Append($"A {w * 0.15},{h * 0.2} 0 1 1 {w * 0.55},{h * 0.25} ");
                sb.Append($"A {w * 0.2},{h * 0.25} 0 1 1 {w * 0.8},{h * 0.45} ");
                sb.Append($"A {w * 0.15},{h * 0.2} 0 1 1 {w * 0.75},{h * 0.7} Z\" ");
                sb.Append($"fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("database-server", "Database Server", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var rx = w / 2;
                var ellipseRy = h * 0.12;
                var sb = new StringBuilder();
                // Multiple stacked cylinders
                for (int i = 2; i >= 0; i--)
                {
                    var yOffset = i * 8;
                    var cy1 = ellipseRy + yOffset;
                    var cy2 = h - ellipseRy - (2 - i) * 8;
                    if (i == 0)
                    {
                        sb.Append($"<path d=\"M 0,{cy1} Q 0,{cy1 - ellipseRy} {rx},{cy1 - ellipseRy} Q {w},{cy1 - ellipseRy} {w},{cy1} L {w},{cy2} Q {w},{cy2 + ellipseRy} {rx},{cy2 + ellipseRy} Q 0,{cy2 + ellipseRy} 0,{cy2} Z\" fill=\"{(node.FillColor ?? "#fef3c7")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                        sb.Append($"<path d=\"M 0,{cy1} Q 0,{cy1 + ellipseRy} {rx},{cy1 + ellipseRy} Q {w},{cy1 + ellipseRy} {w},{cy1}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                    }
                }
                return sb.ToString();
            }),

            new ShapeDescriptor("workstation", "Workstation", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Monitor
                var monitorH = h * 0.65;
                sb.Append($"<rect x=\"{w * 0.1}\" y=\"0\" width=\"{w * 0.8}\" height=\"{monitorH}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e5e7eb")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<rect x=\"{w * 0.15}\" y=\"4\" width=\"{w * 0.7}\" height=\"{monitorH - 12}\" fill=\"#1e293b\" />");
                // Stand
                sb.Append($"<rect x=\"{w * 0.4}\" y=\"{monitorH}\" width=\"{w * 0.2}\" height=\"{h * 0.15}\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<rect x=\"{w * 0.25}\" y=\"{h * 0.85}\" width=\"{w * 0.5}\" height=\"{h * 0.08}\" rx=\"2\" fill=\"{node.StrokeColor}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("laptop", "Laptop", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Screen
                sb.Append($"<rect x=\"{w * 0.1}\" y=\"0\" width=\"{w * 0.8}\" height=\"{h * 0.6}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e5e7eb")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<rect x=\"{w * 0.14}\" y=\"4\" width=\"{w * 0.72}\" height=\"{h * 0.52}\" fill=\"#1e293b\" />");
                // Keyboard base
                sb.Append($"<path d=\"M 0,{h * 0.65} L {w * 0.1},{h * 0.6} L {w * 0.9},{h * 0.6} L {w},{h * 0.65} L {w},{h} L 0,{h} Z\" fill=\"{(node.FillColor ?? "#d1d5db")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("mobile", "Mobile Device", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"8\" fill=\"{(node.FillColor ?? "#e5e7eb")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Screen
                sb.Append($"<rect x=\"4\" y=\"{h * 0.1}\" width=\"{w - 8}\" height=\"{h * 0.75}\" fill=\"#1e293b\" rx=\"2\" />");
                // Home button
                sb.Append($"<circle cx=\"{w / 2}\" cy=\"{h * 0.92}\" r=\"{Math.Min(w, h) * 0.06}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("printer", "Printer", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Paper tray top
                sb.Append($"<rect x=\"{w * 0.15}\" y=\"0\" width=\"{w * 0.7}\" height=\"{h * 0.2}\" fill=\"white\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                // Main body
                sb.Append($"<rect x=\"0\" y=\"{h * 0.2}\" width=\"{w}\" height=\"{h * 0.5}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e5e7eb")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Paper tray bottom
                sb.Append($"<rect x=\"{w * 0.1}\" y=\"{h * 0.7}\" width=\"{w * 0.8}\" height=\"{h * 0.25}\" fill=\"{(node.FillColor ?? "#f3f4f6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("internet", "Internet", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var r = Math.Min(w, h) / 2 - 2;
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Globe lines
                sb.Append($"<ellipse cx=\"{cx}\" cy=\"{cy}\" rx=\"{r * 0.4}\" ry=\"{r}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<line x1=\"{cx - r}\" y1=\"{cy}\" x2=\"{cx + r}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<path d=\"M {cx - r},{cy - r * 0.5} Q {cx},{cy - r * 0.4} {cx + r},{cy - r * 0.5}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<path d=\"M {cx - r},{cy + r * 0.5} Q {cx},{cy + r * 0.4} {cx + r},{cy + r * 0.5}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            })
        };

        templates["network"] = new Template("network", "Network Diagram", shapes);
    }

    private void RegisterBPMNTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            new ShapeDescriptor("task", "Task", node =>
            {
                var w = node.Width;
                var h = node.Height;
                return $"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"8\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("start-event", "Start Event", node =>
            {
                var cx = node.Width / 2;
                var cy = node.Height / 2;
                var r = Math.Min(node.Width, node.Height) / 2 - 2;
                return $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#d1fae5")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />";
            }),

            new ShapeDescriptor("end-event", "End Event", node =>
            {
                var cx = node.Width / 2;
                var cy = node.Height / 2;
                var r = Math.Min(node.Width, node.Height) / 2 - 2;
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#fee2e2")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 4)}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("intermediate-event", "Intermediate Event", node =>
            {
                var cx = node.Width / 2;
                var cy = node.Height / 2;
                var r = Math.Min(node.Width, node.Height) / 2 - 2;
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#fef3c7")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r * 0.8}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("exclusive-gateway", "Exclusive Gateway (XOR)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var sb = new StringBuilder();
                sb.Append($"<polygon points=\"{cx},0 {w},{cy} {cx},{h} 0,{cy}\" fill=\"{(node.FillColor ?? "#fef3c7")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // X mark
                var markSize = Math.Min(w, h) * 0.2;
                sb.Append($"<line x1=\"{cx - markSize}\" y1=\"{cy - markSize}\" x2=\"{cx + markSize}\" y2=\"{cy + markSize}\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                sb.Append($"<line x1=\"{cx + markSize}\" y1=\"{cy - markSize}\" x2=\"{cx - markSize}\" y2=\"{cy + markSize}\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("parallel-gateway", "Parallel Gateway (AND)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var sb = new StringBuilder();
                sb.Append($"<polygon points=\"{cx},0 {w},{cy} {cx},{h} 0,{cy}\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // + mark
                var markSize = Math.Min(w, h) * 0.2;
                sb.Append($"<line x1=\"{cx}\" y1=\"{cy - markSize}\" x2=\"{cx}\" y2=\"{cy + markSize}\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                sb.Append($"<line x1=\"{cx - markSize}\" y1=\"{cy}\" x2=\"{cx + markSize}\" y2=\"{cy}\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("inclusive-gateway", "Inclusive Gateway (OR)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var sb = new StringBuilder();
                sb.Append($"<polygon points=\"{cx},0 {w},{cy} {cx},{h} 0,{cy}\" fill=\"{(node.FillColor ?? "#d1fae5")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Circle mark
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{Math.Min(w, h) * 0.18}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("subprocess", "Sub-Process", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"8\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Collapse marker
                var markerSize = 12;
                var mx = (w - markerSize) / 2;
                var my = h - markerSize - 4;
                sb.Append($"<rect x=\"{mx}\" y=\"{my}\" width=\"{markerSize}\" height=\"{markerSize}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<line x1=\"{mx + markerSize / 2}\" y1=\"{my + 2}\" x2=\"{mx + markerSize / 2}\" y2=\"{my + markerSize - 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<line x1=\"{mx + 2}\" y1=\"{my + markerSize / 2}\" x2=\"{mx + markerSize - 2}\" y2=\"{my + markerSize / 2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("pool", "Pool", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var headerW = 30;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{headerW}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#f3f4f6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("lane", "Lane", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 1)}\" stroke-dasharray=\"5,3\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("data-object", "Data Object", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var foldSize = 15;
                var sb = new StringBuilder();
                sb.Append($"<path d=\"M 0,0 L {w - foldSize},0 L {w},{foldSize} L {w},{h} L 0,{h} Z\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<path d=\"M {w - foldSize},0 L {w - foldSize},{foldSize} L {w},{foldSize}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("data-store", "Data Store", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var rx = w / 2;
                var ellipseRy = 8;
                var sb = new StringBuilder();
                sb.Append($"<path d=\"M 0,{ellipseRy} Q 0,0 {rx},0 Q {w},0 {w},{ellipseRy} L {w},{h - ellipseRy} Q {w},{h} {rx},{h} Q 0,{h} 0,{h - ellipseRy} Z\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<path d=\"M 0,{ellipseRy} Q 0,{ellipseRy * 2} {rx},{ellipseRy * 2} Q {w},{ellipseRy * 2} {w},{ellipseRy}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                return sb.ToString();
            }),

            new ShapeDescriptor("annotation", "Annotation", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#fefce8")}\" stroke=\"none\" />");
                sb.Append($"<path d=\"M 10,0 L 0,0 L 0,{h} L 10,{h}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                return sb.ToString();
            })
        };

        templates["bpmn"] = new Template("bpmn", "BPMN (Business Process)", shapes);
    }

    private void RegisterSTSTemplate()
    {
        // STS (Source-Terminal-Sink) style - shapes with explicit input/output terminals
        // Each shape has defined terminal points where edges connect
        var shapes = new List<ShapeDescriptor>
        {
            // Machine/Process with input on left, output on right
            new ShapeDescriptor("machine", "Machine", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Main body
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e0f2fe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Gear icon in center
                var cx = w / 2;
                var cy = h / 2;
                var gr = Math.Min(w, h) * 0.25;
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{gr}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{gr * 0.4}\" fill=\"{node.StrokeColor}\" />");
                // Terminals rendered separately
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Buffer/Queue with single input/output
            new ShapeDescriptor("buffer", "Buffer/Queue", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Stack representation
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#fef3c7")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Queue lines
                var lineSpacing = h / 5;
                for (int i = 1; i < 5; i++)
                {
                    sb.Append($"<line x1=\"5\" y1=\"{i * lineSpacing}\" x2=\"{w - 5}\" y2=\"{i * lineSpacing}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" stroke-dasharray=\"3,3\" />");
                }
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Source (output only)
            new ShapeDescriptor("source", "Source", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Arrow pointing right
                sb.Append($"<polygon points=\"0,0 {w * 0.7},0 {w},{h / 2} {w * 0.7},{h} 0,{h}\" fill=\"{(node.FillColor ?? "#d1fae5")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "out", Name = "Out", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.5, Direction = TerminalDirection.Right }
            }),

            // Sink (input only)
            new ShapeDescriptor("sink", "Sink", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Arrow pointing into
                sb.Append($"<polygon points=\"{w * 0.3},0 {w},0 {w},{h} {w * 0.3},{h} 0,{h / 2}\" fill=\"{(node.FillColor ?? "#fee2e2")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in", Name = "In", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.5, Direction = TerminalDirection.Left }
            }),

            // Workstation (multiple inputs/outputs possible)
            new ShapeDescriptor("workstation", "Workstation", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"8\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Person icon
                var cx = w / 2;
                var cy = h / 2 - 5;
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy - 8}\" r=\"8\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<path d=\"M {cx - 12},{cy + 5} Q {cx},{cy + 2} {cx + 12},{cy + 5} L {cx + 10},{cy + 20} L {cx - 10},{cy + 20} Z\" fill=\"{node.StrokeColor}\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Inspection station
            new ShapeDescriptor("inspection", "Inspection", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Diamond shape
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<polygon points=\"{cx},0 {w},{cy} {cx},{h} 0,{cy}\" fill=\"{(node.FillColor ?? "#fef9c3")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Checkmark
                sb.Append($"<path d=\"M {cx - 10},{cy} L {cx - 3},{cy + 8} L {cx + 12},{cy - 8}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Robot/Automation
            new ShapeDescriptor("robot", "Robot", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"{h * 0.2}\" width=\"{w}\" height=\"{h * 0.8}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e0e7ff")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Robot head
                sb.Append($"<rect x=\"{w * 0.25}\" y=\"0\" width=\"{w * 0.5}\" height=\"{h * 0.25}\" rx=\"2\" fill=\"{(node.FillColor ?? "#e0e7ff")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Eyes
                sb.Append($"<circle cx=\"{w * 0.35}\" cy=\"{h * 0.12}\" r=\"4\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<circle cx=\"{w * 0.65}\" cy=\"{h * 0.12}\" r=\"4\" fill=\"{node.StrokeColor}\" />");
                // Arm
                sb.Append($"<line x1=\"{w}\" y1=\"{h * 0.5}\" x2=\"{w + 15}\" y2=\"{h * 0.3}\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Conveyor segment
            new ShapeDescriptor("conveyor", "Conveyor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Belt
                sb.Append($"<rect x=\"0\" y=\"{h * 0.3}\" width=\"{w}\" height=\"{h * 0.4}\" fill=\"{(node.FillColor ?? "#9ca3af")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Rollers
                sb.Append($"<circle cx=\"{h * 0.3}\" cy=\"{h * 0.5}\" r=\"{h * 0.25}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<circle cx=\"{w - h * 0.3}\" cy=\"{h * 0.5}\" r=\"{h * 0.25}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                // Arrow indicating direction
                sb.Append($"<path d=\"M {w * 0.4},{h * 0.5} L {w * 0.6},{h * 0.5} L {w * 0.55},{h * 0.4} M {w * 0.6},{h * 0.5} L {w * 0.55},{h * 0.6}\" fill=\"none\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Storage/Warehouse
            new ShapeDescriptor("storage", "Storage", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Warehouse shape
                sb.Append($"<path d=\"M 0,{h * 0.3} L {w / 2},0 L {w},{h * 0.3} L {w},{h} L 0,{h} Z\" fill=\"{(node.FillColor ?? "#f3e8ff")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Shelves
                sb.Append($"<line x1=\"{w * 0.2}\" y1=\"{h * 0.5}\" x2=\"{w * 0.8}\" y2=\"{h * 0.5}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append($"<line x1=\"{w * 0.2}\" y1=\"{h * 0.7}\" x2=\"{w * 0.8}\" y2=\"{h * 0.7}\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, CreateStandardTerminals()),

            // Splitter (1 input, multiple outputs)
            new ShapeDescriptor("splitter", "Splitter", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Triangle pointing right
                sb.Append($"<polygon points=\"0,{h / 2} {w},{0} {w},{h}\" fill=\"{(node.FillColor ?? "#fce7f3")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in", Name = "In", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.5, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "out1", Name = "Out 1", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.25, Direction = TerminalDirection.Right },
                new TerminalDefinition { Id = "out2", Name = "Out 2", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.75, Direction = TerminalDirection.Right }
            }),

            // Merger (multiple inputs, 1 output)
            new ShapeDescriptor("merger", "Merger", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Triangle pointing right (inverted)
                sb.Append($"<polygon points=\"0,0 0,{h} {w},{h / 2}\" fill=\"{(node.FillColor ?? "#ccfbf1")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderTerminals(node, w, h));
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in1", Name = "In 1", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.25, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "in2", Name = "In 2", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.75, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "out", Name = "Out", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.5, Direction = TerminalDirection.Right }
            })
        };

        templates["sts"] = new Template("sts", "STS (Source-Terminal-Sink)", shapes, UseTerminals: true);
    }

    private void RegisterFMECATemplate()
    {
        // FMECA (Failure Mode, Effects, and Criticality Analysis) template
        // Based on shipyard/industrial control system diagram symbols
        var shapes = new List<ShapeDescriptor>
        {
            // ============================================
            // EQUIPMENT BLOCKS
            // ============================================

            // Robot Arm (e.g., Niryo, X-Arm)
            new ShapeDescriptor("robot-arm", "Robot Arm", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#0369a1")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Robot arm icon
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<circle cx=\"{cx - 15}\" cy=\"{cy}\" r=\"6\" fill=\"white\" stroke=\"white\" stroke-width=\"1\" />");
                sb.Append($"<line x1=\"{cx - 9}\" y1=\"{cy}\" x2=\"{cx + 5}\" y2=\"{cy - 10}\" stroke=\"white\" stroke-width=\"3\" />");
                sb.Append($"<line x1=\"{cx + 5}\" y1=\"{cy - 10}\" x2=\"{cx + 15}\" y2=\"{cy}\" stroke=\"white\" stroke-width=\"3\" />");
                sb.Append($"<circle cx=\"{cx + 15}\" cy=\"{cy}\" r=\"4\" fill=\"white\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDEB")),

            // PLC/Controller
            new ShapeDescriptor("plc", "PLC/Controller", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"2\" fill=\"{(node.FillColor ?? "#374151")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // LED indicators
                for (int i = 0; i < 4; i++)
                {
                    var ledX = 8 + i * 12;
                    sb.Append($"<circle cx=\"{ledX}\" cy=\"8\" r=\"3\" fill=\"#22c55e\" />");
                }
                // Label area
                sb.Append($"<rect x=\"4\" y=\"{h - 20}\" width=\"{w - 8}\" height=\"14\" rx=\"2\" fill=\"#1f2937\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDE")),

            // CNC Machine
            new ShapeDescriptor("cnc", "CNC Machine", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#65a30d")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // CNC icon (spindle)
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<rect x=\"{cx - 8}\" y=\"{cy - 15}\" width=\"16\" height=\"20\" fill=\"white\" rx=\"2\" />");
                sb.Append($"<line x1=\"{cx}\" y1=\"{cy + 5}\" x2=\"{cx}\" y2=\"{cy + 15}\" stroke=\"white\" stroke-width=\"3\" />");
                sb.Append($"<polygon points=\"{cx - 5},{cy + 15} {cx + 5},{cy + 15} {cx},{cy + 22}\" fill=\"white\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDES")),

            // Laser Engraver
            new ShapeDescriptor("laser", "Laser Engraver", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#7c3aed")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Laser beam icon
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<rect x=\"{cx - 10}\" y=\"{cy - 12}\" width=\"20\" height=\"8\" fill=\"white\" rx=\"2\" />");
                sb.Append($"<line x1=\"{cx}\" y1=\"{cy - 4}\" x2=\"{cx}\" y2=\"{cy + 12}\" stroke=\"#ef4444\" stroke-width=\"2\" stroke-dasharray=\"4,2\" />");
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy + 14}\" r=\"4\" fill=\"#ef4444\" opacity=\"0.7\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDES")),

            // Conveyor
            new ShapeDescriptor("conveyor", "Conveyor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"{h * 0.3}\" width=\"{w}\" height=\"{h * 0.4}\" fill=\"{(node.FillColor ?? "#6b7280")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Rollers
                sb.Append($"<circle cx=\"{h * 0.25}\" cy=\"{h * 0.5}\" r=\"{h * 0.2}\" fill=\"#374151\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<circle cx=\"{w - h * 0.25}\" cy=\"{h * 0.5}\" r=\"{h * 0.2}\" fill=\"#374151\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                // Direction arrow
                sb.Append($"<path d=\"M {w * 0.35},{h * 0.5} L {w * 0.65},{h * 0.5}\" stroke=\"white\" stroke-width=\"2\" />");
                sb.Append($"<polygon points=\"{w * 0.6},{h * 0.4} {w * 0.7},{h * 0.5} {w * 0.6},{h * 0.6}\" fill=\"white\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PD")),

            // Camera
            new ShapeDescriptor("camera", "Camera", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"{h * 0.2}\" width=\"{w * 0.7}\" height=\"{h * 0.6}\" rx=\"4\" fill=\"{(node.FillColor ?? "#f3f4f6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Lens
                sb.Append($"<circle cx=\"{w * 0.35}\" cy=\"{h * 0.5}\" r=\"{h * 0.2}\" fill=\"#1f2937\" stroke=\"{node.StrokeColor}\" stroke-width=\"2\" />");
                sb.Append($"<circle cx=\"{w * 0.35}\" cy=\"{h * 0.5}\" r=\"{h * 0.1}\" fill=\"#3b82f6\" />");
                // Mount
                sb.Append($"<rect x=\"{w * 0.7}\" y=\"{h * 0.35}\" width=\"{w * 0.3}\" height=\"{h * 0.3}\" fill=\"#6b7280\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDE")),

            // Vacuum System
            new ShapeDescriptor("vacuum", "Vacuum System", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#dc2626")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Vacuum icon (suction cup)
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<ellipse cx=\"{cx}\" cy=\"{cy + 5}\" rx=\"15\" ry=\"8\" fill=\"none\" stroke=\"white\" stroke-width=\"2\" />");
                sb.Append($"<line x1=\"{cx}\" y1=\"{cy - 10}\" x2=\"{cx}\" y2=\"{cy - 3}\" stroke=\"white\" stroke-width=\"3\" />");
                sb.Append($"<path d=\"M {cx - 8},{cy - 10} L {cx},{cy - 15} L {cx + 8},{cy - 10}\" fill=\"none\" stroke=\"white\" stroke-width=\"2\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDA")),

            // Motor/DC Motor
            new ShapeDescriptor("motor", "Motor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Motor body (circle)
                var cx = w / 2;
                var cy = h / 2;
                var r = Math.Min(w, h) * 0.4;
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#1f2937")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // M symbol
                sb.Append($"<text x=\"{cx}\" y=\"{cy + 5}\" font-size=\"16\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">M</text>");
                // Shaft
                sb.Append($"<rect x=\"{cx + r - 2}\" y=\"{cy - 4}\" width=\"{w * 0.2}\" height=\"8\" fill=\"#6b7280\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PD")),

            // Network Switch
            new ShapeDescriptor("switch", "Network Switch", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"2\" fill=\"{(node.FillColor ?? "#9ca3af")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Port indicators
                var portCount = 8;
                var portWidth = (w - 10) / portCount;
                for (int i = 0; i < portCount; i++)
                {
                    var px = 5 + i * portWidth;
                    sb.Append($"<rect x=\"{px}\" y=\"{h * 0.6}\" width=\"{portWidth - 2}\" height=\"{h * 0.25}\" fill=\"#1f2937\" rx=\"1\" />");
                }
                // Status LEDs
                sb.Append($"<circle cx=\"10\" cy=\"{h * 0.3}\" r=\"4\" fill=\"#22c55e\" />");
                sb.Append($"<circle cx=\"22\" cy=\"{h * 0.3}\" r=\"4\" fill=\"#f59e0b\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PE")),

            // Computer/Laptop
            new ShapeDescriptor("computer", "Computer", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Screen
                sb.Append($"<rect x=\"{w * 0.1}\" y=\"0\" width=\"{w * 0.8}\" height=\"{h * 0.7}\" rx=\"4\" fill=\"{(node.FillColor ?? "#1e3a5f")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Screen inner
                sb.Append($"<rect x=\"{w * 0.15}\" y=\"{h * 0.05}\" width=\"{w * 0.7}\" height=\"{h * 0.55}\" fill=\"#0ea5e9\" />");
                // Base
                sb.Append($"<rect x=\"0\" y=\"{h * 0.75}\" width=\"{w}\" height=\"{h * 0.25}\" rx=\"2\" fill=\"#374151\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                // Keyboard lines
                sb.Append($"<line x1=\"{w * 0.1}\" y1=\"{h * 0.85}\" x2=\"{w * 0.9}\" y2=\"{h * 0.85}\" stroke=\"#6b7280\" stroke-width=\"1\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PEW")),

            // Database/SQL
            new ShapeDescriptor("database", "Database", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Cylinder shape
                var ry = h * 0.15;
                sb.Append($"<ellipse cx=\"{w / 2}\" cy=\"{ry}\" rx=\"{w / 2}\" ry=\"{ry}\" fill=\"{(node.FillColor ?? "#f3f4f6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<rect x=\"0\" y=\"{ry}\" width=\"{w}\" height=\"{h - 2 * ry}\" fill=\"{(node.FillColor ?? "#f3f4f6")}\" stroke=\"none\" />");
                sb.Append($"<line x1=\"0\" y1=\"{ry}\" x2=\"0\" y2=\"{h - ry}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<line x1=\"{w}\" y1=\"{ry}\" x2=\"{w}\" y2=\"{h - ry}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<ellipse cx=\"{w / 2}\" cy=\"{h - ry}\" rx=\"{w / 2}\" ry=\"{ry}\" fill=\"{(node.FillColor ?? "#f3f4f6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("E")),

            // IR Sensor
            new ShapeDescriptor("ir-sensor", "IR Sensor", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#fbbf24")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // IR waves
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<path d=\"M {cx - 10},{cy - 8} Q {cx},{cy - 12} {cx + 10},{cy - 8}\" fill=\"none\" stroke=\"#dc2626\" stroke-width=\"2\" />");
                sb.Append($"<path d=\"M {cx - 15},{cy} Q {cx},{cy - 5} {cx + 15},{cy}\" fill=\"none\" stroke=\"#dc2626\" stroke-width=\"2\" />");
                sb.Append($"<path d=\"M {cx - 10},{cy + 8} Q {cx},{cy + 4} {cx + 10},{cy + 8}\" fill=\"none\" stroke=\"#dc2626\" stroke-width=\"2\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PB")),

            // E-Stop Button
            new ShapeDescriptor("estop", "E-Stop", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Hexagonal housing
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.45;
                var points = new StringBuilder();
                for (int i = 0; i < 6; i++)
                {
                    var angle = i * Math.PI / 3 - Math.PI / 6;
                    var px = cx + size * Math.Cos(angle);
                    var py = cy + size * Math.Sin(angle);
                    if (i > 0) points.Append(" ");
                    points.Append($"{px:F1},{py:F1}");
                }
                sb.Append($"<polygon points=\"{points}\" fill=\"{(node.FillColor ?? "#fbbf24")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Red button
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{size * 0.6}\" fill=\"#dc2626\" stroke=\"#991b1b\" stroke-width=\"2\" />");
                // Stop symbol
                sb.Append($"<rect x=\"{cx - 8}\" y=\"{cy - 3}\" width=\"16\" height=\"6\" fill=\"white\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("P")),

            // Limit Switch
            new ShapeDescriptor("limit-switch", "Limit Switch", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"{h * 0.25}\" width=\"{w * 0.7}\" height=\"{h * 0.5}\" rx=\"2\" fill=\"{(node.FillColor ?? "#1f2937")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Lever arm
                sb.Append($"<line x1=\"{w * 0.7}\" y1=\"{h * 0.5}\" x2=\"{w}\" y2=\"{h * 0.2}\" stroke=\"{node.StrokeColor}\" stroke-width=\"3\" />");
                sb.Append($"<circle cx=\"{w}\" cy=\"{h * 0.2}\" r=\"5\" fill=\"#ef4444\" stroke=\"{node.StrokeColor}\" stroke-width=\"1\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PD")),

            // Driver/Controller Board
            new ShapeDescriptor("driver", "Driver Board", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"2\" fill=\"{(node.FillColor ?? "#166534")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // IC chip
                sb.Append($"<rect x=\"{w * 0.2}\" y=\"{h * 0.3}\" width=\"{w * 0.6}\" height=\"{h * 0.4}\" fill=\"#1f2937\" />");
                // Pins
                for (int i = 0; i < 4; i++)
                {
                    var px = w * 0.25 + i * w * 0.15;
                    sb.Append($"<line x1=\"{px}\" y1=\"{h * 0.3}\" x2=\"{px}\" y2=\"{h * 0.15}\" stroke=\"#9ca3af\" stroke-width=\"2\" />");
                    sb.Append($"<line x1=\"{px}\" y1=\"{h * 0.7}\" x2=\"{px}\" y2=\"{h * 0.85}\" stroke=\"#9ca3af\" stroke-width=\"2\" />");
                }
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDES")),

            // Power Supply
            new ShapeDescriptor("power-supply", "Power Supply", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"2\" fill=\"{(node.FillColor ?? "#374151")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                // Lightning bolt
                var cx = w / 2;
                var cy = h / 2;
                sb.Append($"<polygon points=\"{cx},{cy - 15} {cx - 8},{cy + 2} {cx - 2},{cy + 2} {cx - 5},{cy + 15} {cx + 8},{cy - 2} {cx + 2},{cy - 2}\" fill=\"#fbbf24\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("P")),

            // Generic Equipment Block
            new ShapeDescriptor("equipment", "Equipment", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"4\" fill=\"{(node.FillColor ?? "#e5e7eb")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append(RenderFMECATerminals(node, w, h));
                return sb.ToString();
            }, CreateFMECATerminals("PDESA")),

            // ============================================
            // CONNECTOR TERMINALS (standalone)
            // ============================================

            // Power Connector (P)
            new ShapeDescriptor("conn-power", "Power (P)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.4;
                // Triangle pointing right
                sb.Append($"<polygon points=\"{cx - size},{cy - size} {cx + size},{cy} {cx - size},{cy + size}\" fill=\"{(node.FillColor ?? "#3b82f6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx - 2}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">P</text>");
                return sb.ToString();
            }),

            // Data Connector (D)
            new ShapeDescriptor("conn-data", "Data (D)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.4;
                sb.Append($"<polygon points=\"{cx - size},{cy - size} {cx + size},{cy} {cx - size},{cy + size}\" fill=\"{(node.FillColor ?? "#8b5cf6")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx - 2}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">D</text>");
                return sb.ToString();
            }),

            // Ethernet Connector (E)
            new ShapeDescriptor("conn-ethernet", "Ethernet (E)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.4;
                sb.Append($"<polygon points=\"{cx - size},{cy - size} {cx + size},{cy} {cx - size},{cy + size}\" fill=\"{(node.FillColor ?? "#6b7280")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx - 2}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">E</text>");
                return sb.ToString();
            }),

            // Wireless Connector (W)
            new ShapeDescriptor("conn-wireless", "Wireless (W)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.4;
                sb.Append($"<polygon points=\"{cx - size},{cy - size} {cx + size},{cy} {cx - size},{cy + size}\" fill=\"{(node.FillColor ?? "#0ea5e9")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx - 2}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">W</text>");
                return sb.ToString();
            }),

            // Bus Connector (B)
            new ShapeDescriptor("conn-bus", "Bus (B)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.35;
                // Diamond shape for bus
                sb.Append($"<polygon points=\"{cx},{cy - size} {cx + size},{cy} {cx},{cy + size} {cx - size},{cy}\" fill=\"{(node.FillColor ?? "#f59e0b")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">B</text>");
                return sb.ToString();
            }),

            // Serial Connector (S)
            new ShapeDescriptor("conn-serial", "Serial (S)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var size = Math.Min(w, h) * 0.35;
                sb.Append($"<polygon points=\"{cx},{cy - size} {cx + size},{cy} {cx},{cy + size} {cx - size},{cy}\" fill=\"{(node.FillColor ?? "#10b981")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">S</text>");
                return sb.ToString();
            }),

            // Analog Connector (A)
            new ShapeDescriptor("conn-analog", "Analog (A)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var r = Math.Min(w, h) * 0.35;
                // Circle for analog
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#ec4899")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                sb.Append($"<text x=\"{cx}\" y=\"{cy + 4}\" font-size=\"12\" font-weight=\"bold\" fill=\"white\" text-anchor=\"middle\">A</text>");
                return sb.ToString();
            })
        };

        templates["fmeca"] = new Template("fmeca", "FMECA (Shipyard)", shapes, UseTerminals: true);
    }

    // Create FMECA-style terminals based on connection types string (e.g., "PDEB" for Power, Data, Ethernet, Bus)
    private static List<TerminalDefinition> CreateFMECATerminals(string connectionTypes)
    {
        var terminals = new List<TerminalDefinition>();
        var inputCount = connectionTypes.Length;
        var outputCount = connectionTypes.Length;

        // Create input terminals on left side
        for (int i = 0; i < inputCount; i++)
        {
            var type = connectionTypes[i];
            var (name, color) = GetFMECATerminalInfo(type);
            var yPos = (i + 1.0) / (inputCount + 1.0);
            terminals.Add(new TerminalDefinition
            {
                Id = $"in_{char.ToLower(type)}",
                Name = $"{name} In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = yPos,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Triangle,
                Color = color
            });
        }

        // Create output terminals on right side
        for (int i = 0; i < outputCount; i++)
        {
            var type = connectionTypes[i];
            var (name, color) = GetFMECATerminalInfo(type);
            var yPos = (i + 1.0) / (outputCount + 1.0);
            terminals.Add(new TerminalDefinition
            {
                Id = $"out_{char.ToLower(type)}",
                Name = $"{name} Out",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = yPos,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Triangle,
                Color = color
            });
        }

        return terminals;
    }

    private static (string name, string color) GetFMECATerminalInfo(char type)
    {
        return type switch
        {
            'P' => ("Power", "#3b82f6"),      // Blue
            'D' => ("Data", "#8b5cf6"),       // Purple
            'E' => ("Ethernet", "#6b7280"),   // Gray
            'W' => ("Wireless", "#0ea5e9"),   // Cyan
            'B' => ("Bus", "#f59e0b"),        // Orange
            'S' => ("Serial", "#10b981"),     // Green
            'A' => ("Analog", "#ec4899"),     // Pink
            _ => ("Unknown", "#9ca3af")       // Default gray
        };
    }

    // Render FMECA-style terminals (triangular connectors with type labels)
    private static string RenderFMECATerminals(Node node, double w, double h)
    {
        if (!node.ShowTerminals) return "";

        var sb = new StringBuilder();
        var stickOut = 10.0;
        var size = 8.0;

        // Get terminal types from a property or default to "PD"
        var connectionTypes = "PD"; // Default
        // Could be extended to read from node properties

        var terminalCount = connectionTypes.Length;

        // Render input terminals (left side, triangles pointing right)
        for (int i = 0; i < terminalCount; i++)
        {
            var type = connectionTypes[i];
            var (name, color) = GetFMECATerminalInfo(type);
            var yPos = h * (i + 1.0) / (terminalCount + 1.0);

            // Stem line
            sb.Append($"<line x1=\"0\" y1=\"{yPos}\" x2=\"{-stickOut}\" y2=\"{yPos}\" stroke=\"{color}\" stroke-width=\"2\" />");
            // Triangle pointing right (input)
            sb.Append($"<polygon points=\"{-stickOut - size},{yPos - size / 2} {-stickOut},{yPos} {-stickOut - size},{yPos + size / 2}\" fill=\"{color}\" stroke=\"white\" stroke-width=\"1\" data-terminal-type=\"input\" style=\"cursor: crosshair\" />");
            // Label
            sb.Append($"<text x=\"{-stickOut - size - 2}\" y=\"{yPos + 3}\" font-size=\"8\" fill=\"{color}\" text-anchor=\"end\">{type}</text>");
        }

        // Render output terminals (right side, triangles pointing right)
        for (int i = 0; i < terminalCount; i++)
        {
            var type = connectionTypes[i];
            var (name, color) = GetFMECATerminalInfo(type);
            var yPos = h * (i + 1.0) / (terminalCount + 1.0);

            // Stem line
            sb.Append($"<line x1=\"{w}\" y1=\"{yPos}\" x2=\"{w + stickOut}\" y2=\"{yPos}\" stroke=\"{color}\" stroke-width=\"2\" />");
            // Triangle pointing right (output)
            sb.Append($"<polygon points=\"{w + stickOut},{yPos} {w + stickOut + size},{yPos - size / 2} {w + stickOut + size},{yPos + size / 2}\" fill=\"{color}\" stroke=\"white\" stroke-width=\"1\" data-terminal-type=\"output\" style=\"cursor: crosshair\" />");
            // Label
            sb.Append($"<text x=\"{w + stickOut + size + 4}\" y=\"{yPos + 3}\" font-size=\"8\" fill=\"{color}\" text-anchor=\"start\">{type}</text>");
        }

        return sb.ToString();
    }

    // Helper to create standard input/output terminals (left input, right output)
    private static List<TerminalDefinition> CreateStandardTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "in",
                Name = "In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle,
                Color = "#22c55e" // Green
            },
            new TerminalDefinition
            {
                Id = "out",
                Name = "Out",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle,
                Color = "#ef4444" // Red
            }
        };
    }

    // Render terminal circles on the node (STS style - green input, red output)
    /// <summary>
    /// Render terminals for STS/Circuit templates using the universal TerminalRenderService.
    /// This ensures consistent styling across all template types.
    /// </summary>
    private static string RenderTerminals(Node node, double w, double h)
    {
        if (!node.ShowTerminals) return "";

        var sb = new StringBuilder();

        // Parse terminal layout or use custom positions
        var (inputPos, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);

        // Input terminal position
        double inX, inY;
        TerminalDirection inDir;
        if (node.T1X.HasValue && node.T1Y.HasValue)
        {
            inX = node.T1X.Value;
            inY = node.T1Y.Value;
            inDir = TerminalLayouts.GetDirectionFromNormalizedPosition(inX, inY);
        }
        else
        {
            (inX, inY, inDir) = TerminalLayouts.GetPositionCoords(inputPos);
        }

        // Output terminal position
        double outX, outY;
        TerminalDirection outDir;
        if (node.T2X.HasValue && node.T2Y.HasValue)
        {
            outX = node.T2X.Value;
            outY = node.T2Y.Value;
            outDir = TerminalLayouts.GetDirectionFromNormalizedPosition(outX, outY);
        }
        else
        {
            (outX, outY, outDir) = TerminalLayouts.GetPositionCoords(outputPos);
        }

        // Render input terminal using universal service
        var inPx = inX * w;
        var inPy = inY * h;
        sb.Append(Services.TerminalRenderService.RenderTerminal(inPx, inPy, inDir, node.InputTerminalType, node.InputTerminalColor));

        // Render output terminal using universal service
        var outPx = outX * w;
        var outPy = outY * h;
        sb.Append(Services.TerminalRenderService.RenderTerminal(outPx, outPy, outDir, node.OutputTerminalType, node.OutputTerminalColor));

        return sb.ToString();
    }

    private static (double x, double y) GetStickOutOffset(TerminalDirection dir, double stickOut)
    {
        return dir switch
        {
            TerminalDirection.Left => (-stickOut, 0),
            TerminalDirection.Right => (stickOut, 0),
            TerminalDirection.Top => (0, -stickOut),
            TerminalDirection.Bottom => (0, stickOut),
            _ => (0, 0)
        };
    }

    /// <summary>
    /// Get the position and anchor for a terminal label based on terminal direction
    /// </summary>
    private static (double x, double y, string anchor) GetTerminalLabelPosition(double termX, double termY, TerminalDirection dir, double radius)
    {
        var offset = radius + 4; // Offset from terminal circle
        return dir switch
        {
            TerminalDirection.Left => (termX - offset - 2, termY + 4, "end"),
            TerminalDirection.Right => (termX + offset + 2, termY + 4, "start"),
            TerminalDirection.Top => (termX, termY - offset - 2, "middle"),
            TerminalDirection.Bottom => (termX, termY + offset + 10, "middle"),
            _ => (termX + offset, termY + 4, "start")
        };
    }

    public IEnumerable<Template> GetTemplates() => templates.Values;

    public Template? GetTemplate(string id)
    {
        templates.TryGetValue(id, out var tpl);
        return tpl;
    }

    public ShapeDescriptor? GetShape(string templateId, string shapeId)
    {
        var tpl = GetTemplate(templateId);
        return tpl?.Shapes.FirstOrDefault(s => s.Id == shapeId);
    }

    /// <summary>
    /// Get the terminal configuration for a specific shape.
    /// Returns null if the shape doesn't have a specific config (use defaults).
    /// </summary>
    public TerminalConfig? GetTerminalConfig(string? templateId, string? shapeId)
    {
        if (string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(shapeId))
            return null;
        var shape = GetShape(templateId, shapeId);
        return shape?.TerminalConfig;
    }

    // ============================================
    // QMAKER TEMPLATE - Queueing Networks
    // ============================================

    private void RegisterQMakerTemplate()
    {
        // Professional color scheme for queueing networks
        const string queueStroke = "#0ea5e9";      // Sky blue for queue borders
        const string serverStroke = "#ea580c";     // Orange-600 for server borders
        const string serverAccent = "#c2410c";     // Orange-700 for server text
        const string capacityColor = "#64748b";    // Slate for capacity text

        var shapes = new List<ShapeDescriptor>
        {
            // Queue-Server Group (Combined buffer + server as one element) - Professional Design
            new ShapeDescriptor("queue-server", "Queue + Server", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var bufferWidth = w * 0.48;
                var serverWidth = w * 0.42;
                var gap = w * 0.10;
                var serverX = bufferWidth + gap;
                var cornerRadius = Math.Min(6, h * 0.12);
                var strokeW = node.StrokeWidth ?? 2.5;

                // Queue buffer - rounded rectangle with gradient effect
                sb.Append($"<defs><linearGradient id=\"queueGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"0%\" y2=\"100%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#f1f5f9;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#e2e8f0;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{bufferWidth}\" height=\"{h}\" rx=\"{cornerRadius}\" ry=\"{cornerRadius}\" fill=\"url(#queueGrad_{node.Id})\" stroke=\"{queueStroke}\" stroke-width=\"{strokeW}\" />");

                // Queue slots visualization (entities waiting)
                var slotCount = 4;
                var slotHeight = h * 0.15;
                var slotGap = (h - slotHeight * slotCount) / (slotCount + 1);
                for (int i = 0; i < slotCount; i++)
                {
                    var slotY = slotGap + i * (slotHeight + slotGap);
                    var slotOpacity = 0.3 + (i * 0.15); // Fade effect
                    sb.Append($"<rect x=\"4\" y=\"{slotY}\" width=\"{bufferWidth - 8}\" height=\"{slotHeight}\" rx=\"2\" fill=\"{queueStroke}\" opacity=\"{slotOpacity}\" />");
                }

                // Capacity badge (top-right of queue)
                var badgeSize = Math.Min(16, h * 0.28);
                sb.Append($"<circle cx=\"{bufferWidth - 2}\" cy=\"4\" r=\"{badgeSize / 2}\" fill=\"white\" stroke=\"{queueStroke}\" stroke-width=\"1.5\" />");
                sb.Append($"<text x=\"{bufferWidth - 2}\" y=\"{4 + badgeSize * 0.15}\" font-size=\"{badgeSize * 0.6}\" fill=\"{capacityColor}\" text-anchor=\"middle\" font-weight=\"600\">∞</text>");

                // Server - circle with gradient and thick orange border
                var serverR = Math.Min(serverWidth, h) * 0.38;
                var serverCX = serverX + serverWidth / 2;
                var serverCY = h / 2;
                sb.Append($"<defs><radialGradient id=\"serverGrad_{node.Id}\" cx=\"30%\" cy=\"30%\" r=\"70%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fff;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fed7aa;stop-opacity:1\" />");
                sb.Append($"</radialGradient></defs>");
                sb.Append($"<circle cx=\"{serverCX}\" cy=\"{serverCY}\" r=\"{serverR}\" fill=\"url(#serverGrad_{node.Id})\" stroke=\"{serverStroke}\" stroke-width=\"{strokeW + 0.5}\" />");

                // Server μ symbol
                sb.Append($"<text x=\"{serverCX}\" y=\"{serverCY + serverR * 0.2}\" font-size=\"{serverR * 0.8}\" fill=\"{serverAccent}\" text-anchor=\"middle\" font-weight=\"700\" font-family=\"serif\">μ</text>");

                // Flow arrow from queue to server
                var arrowY = h / 2;
                var arrowStart = bufferWidth + 2;
                var arrowEnd = serverX - serverR - 4;
                sb.Append($"<line x1=\"{arrowStart}\" y1=\"{arrowY}\" x2=\"{arrowEnd}\" y2=\"{arrowY}\" stroke=\"{capacityColor}\" stroke-width=\"2\" stroke-dasharray=\"4,2\" />");
                sb.Append($"<polygon points=\"{arrowEnd},{arrowY - 4} {arrowEnd + 6},{arrowY} {arrowEnd},{arrowY + 4}\" fill=\"{capacityColor}\" />");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, CreateQMakerTerminals()),

            // Single Server - Professional Orange Design
            new ShapeDescriptor("server", "Server", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var r = Math.Min(w, h) * 0.42;
                var cx = w / 2;
                var cy = h / 2;
                var strokeW = node.StrokeWidth ?? 3;

                // Server with gradient
                sb.Append($"<defs><radialGradient id=\"srvGrad_{node.Id}\" cx=\"30%\" cy=\"30%\" r=\"70%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fff;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fed7aa;stop-opacity:1\" />");
                sb.Append($"</radialGradient></defs>");
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"url(#srvGrad_{node.Id})\" stroke=\"{serverStroke}\" stroke-width=\"{strokeW}\" />");

                // Inner ring for depth
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r * 0.75}\" fill=\"none\" stroke=\"{serverStroke}\" stroke-width=\"1\" opacity=\"0.3\" />");

                // μ symbol
                sb.Append($"<text x=\"{cx}\" y=\"{cy + r * 0.2}\" font-size=\"{r * 0.9}\" fill=\"{serverAccent}\" text-anchor=\"middle\" font-weight=\"700\" font-family=\"serif\">μ</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, CreateQMakerTerminals()),

            // Buffer/Queue only - Professional Design with Capacity Display
            new ShapeDescriptor("buffer", "Buffer/Queue", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cornerRadius = Math.Min(8, h * 0.15);
                var strokeW = node.StrokeWidth ?? 2.5;

                // Main queue container with gradient
                sb.Append($"<defs><linearGradient id=\"bufGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"0%\" y2=\"100%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#f8fafc;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#e2e8f0;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"{cornerRadius}\" ry=\"{cornerRadius}\" fill=\"url(#bufGrad_{node.Id})\" stroke=\"{queueStroke}\" stroke-width=\"{strokeW}\" />");

                // Queue slots (entities)
                var slotCount = 5;
                var slotWidth = (w - 16) / slotCount;
                var slotHeight = h * 0.5;
                var slotY = (h - slotHeight) / 2;
                for (int i = 0; i < slotCount; i++)
                {
                    var slotX = 8 + i * slotWidth;
                    var slotOpacity = 0.2 + (i * 0.12);
                    sb.Append($"<rect x=\"{slotX}\" y=\"{slotY}\" width=\"{slotWidth * 0.85}\" height=\"{slotHeight}\" rx=\"3\" fill=\"{queueStroke}\" opacity=\"{slotOpacity}\" />");
                }

                // Capacity indicator at top
                sb.Append($"<rect x=\"{w / 2 - 20}\" y=\"2\" width=\"40\" height=\"14\" rx=\"7\" fill=\"white\" stroke=\"{queueStroke}\" stroke-width=\"1\" />");
                sb.Append($"<text x=\"{w / 2}\" y=\"12\" font-size=\"9\" fill=\"{capacityColor}\" text-anchor=\"middle\" font-weight=\"600\">K = ∞</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, CreateQMakerTerminals()),

            // Arrival Source (λ) - Professional Design
            new ShapeDescriptor("source", "Arrival Source (λ)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var strokeW = node.StrokeWidth ?? 2.5;

                // Rounded arrow shape
                sb.Append($"<defs><linearGradient id=\"srcGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#dcfce7;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#bbf7d0;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<path d=\"M 6,0 L {w * 0.65},0 Q {w * 0.75},0 {w * 0.8},{h * 0.25} L {w},{h / 2} L {w * 0.8},{h * 0.75} Q {w * 0.75},{h} {w * 0.65},{h} L 6,{h} Q 0,{h} 0,{h - 6} L 0,6 Q 0,0 6,0 Z\" fill=\"url(#srcGrad_{node.Id})\" stroke=\"#16a34a\" stroke-width=\"{strokeW}\" />");

                // λ symbol with arrival rate annotation
                sb.Append($"<text x=\"{w * 0.35}\" y=\"{h / 2 + 6}\" font-size=\"{h * 0.4}\" fill=\"#15803d\" text-anchor=\"middle\" font-weight=\"700\" font-family=\"serif\">λ</text>");

                // Small "IN" label
                sb.Append($"<text x=\"{w * 0.35}\" y=\"{h - 4}\" font-size=\"8\" fill=\"#166534\" text-anchor=\"middle\" font-weight=\"500\">arrivals</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "out", Name = "Out", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.5, Direction = TerminalDirection.Right }
            }),

            // Departure Sink - Professional Design
            new ShapeDescriptor("sink", "Departure Sink", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var strokeW = node.StrokeWidth ?? 2.5;

                // Rounded arrow shape pointing in
                sb.Append($"<defs><linearGradient id=\"sinkGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fecaca;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fee2e2;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<path d=\"M {w * 0.2},{h * 0.25} Q {w * 0.25},0 {w * 0.35},0 L {w - 6},0 Q {w},0 {w},6 L {w},{h - 6} Q {w},{h} {w - 6},{h} L {w * 0.35},{h} Q {w * 0.25},{h} {w * 0.2},{h * 0.75} L 0,{h / 2} Z\" fill=\"url(#sinkGrad_{node.Id})\" stroke=\"#dc2626\" stroke-width=\"{strokeW}\" />");

                // Exit symbol
                sb.Append($"<text x=\"{w * 0.6}\" y=\"{h / 2 + 5}\" font-size=\"{h * 0.28}\" fill=\"#b91c1c\" text-anchor=\"middle\" font-weight=\"700\">EXIT</text>");

                // Small departures label
                sb.Append($"<text x=\"{w * 0.6}\" y=\"{h - 4}\" font-size=\"8\" fill=\"#991b1b\" text-anchor=\"middle\" font-weight=\"500\">departures</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in", Name = "In", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.5, Direction = TerminalDirection.Left }
            }),

            // M/M/1 Queue - Professional Kendall Notation Style
            new ShapeDescriptor("mm1", "M/M/1 Queue", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var bufferWidth = w * 0.45;
                var serverX = bufferWidth + w * 0.08;
                var cornerRadius = Math.Min(6, h * 0.12);
                var strokeW = node.StrokeWidth ?? 2.5;

                // Queue with gradient
                sb.Append($"<defs><linearGradient id=\"mm1qGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"0%\" y2=\"100%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#f8fafc;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#e2e8f0;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<rect x=\"0\" y=\"{h * 0.1}\" width=\"{bufferWidth}\" height=\"{h * 0.8}\" rx=\"{cornerRadius}\" fill=\"url(#mm1qGrad_{node.Id})\" stroke=\"{queueStroke}\" stroke-width=\"{strokeW}\" />");

                // Queue slots
                var slotCount = 3;
                var slotHeight = h * 0.18;
                var slotGap = (h * 0.8 - slotHeight * slotCount) / (slotCount + 1);
                for (int i = 0; i < slotCount; i++)
                {
                    var slotY = h * 0.1 + slotGap + i * (slotHeight + slotGap);
                    sb.Append($"<rect x=\"4\" y=\"{slotY}\" width=\"{bufferWidth - 8}\" height=\"{slotHeight}\" rx=\"2\" fill=\"{queueStroke}\" opacity=\"{0.25 + i * 0.15}\" />");
                }

                // Server circle
                var serverR = Math.Min(w * 0.42, h * 0.7) * 0.38;
                var serverCX = serverX + (w - serverX) / 2;
                sb.Append($"<defs><radialGradient id=\"mm1sGrad_{node.Id}\" cx=\"30%\" cy=\"30%\" r=\"70%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fff;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fed7aa;stop-opacity:1\" />");
                sb.Append($"</radialGradient></defs>");
                sb.Append($"<circle cx=\"{serverCX}\" cy=\"{h / 2}\" r=\"{serverR}\" fill=\"url(#mm1sGrad_{node.Id})\" stroke=\"{serverStroke}\" stroke-width=\"{strokeW}\" />");
                sb.Append($"<text x=\"{serverCX}\" y=\"{h / 2 + serverR * 0.2}\" font-size=\"{serverR * 0.8}\" fill=\"{serverAccent}\" text-anchor=\"middle\" font-weight=\"700\" font-family=\"serif\">μ</text>");

                // Connection arrow
                sb.Append($"<line x1=\"{bufferWidth + 2}\" y1=\"{h / 2}\" x2=\"{serverCX - serverR - 4}\" y2=\"{h / 2}\" stroke=\"{capacityColor}\" stroke-width=\"1.5\" stroke-dasharray=\"3,2\" />");
                sb.Append($"<polygon points=\"{serverCX - serverR - 4},{h / 2 - 3} {serverCX - serverR},{h / 2} {serverCX - serverR - 4},{h / 2 + 3}\" fill=\"{capacityColor}\" />");

                // M/M/1 label badge
                sb.Append($"<rect x=\"{w / 2 - 18}\" y=\"{h - 14}\" width=\"36\" height=\"12\" rx=\"6\" fill=\"#1e293b\" />");
                sb.Append($"<text x=\"{w / 2}\" y=\"{h - 5}\" font-size=\"8\" fill=\"white\" text-anchor=\"middle\" font-weight=\"600\">M/M/1</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, CreateQMakerTerminals()),

            // M/M/c Queue (multi-server) - Professional Design
            new ShapeDescriptor("mmc", "M/M/c Queue", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var bufferWidth = w * 0.38;
                var serverAreaX = bufferWidth + w * 0.08;
                var cornerRadius = Math.Min(6, h * 0.1);
                var strokeW = node.StrokeWidth ?? 2.5;

                // Queue
                sb.Append($"<defs><linearGradient id=\"mmcqGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"0%\" y2=\"100%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#f8fafc;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#e2e8f0;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<rect x=\"0\" y=\"{h * 0.08}\" width=\"{bufferWidth}\" height=\"{h * 0.84}\" rx=\"{cornerRadius}\" fill=\"url(#mmcqGrad_{node.Id})\" stroke=\"{queueStroke}\" stroke-width=\"{strokeW}\" />");

                // Queue slots
                for (int i = 0; i < 3; i++)
                {
                    var slotY = h * 0.15 + i * (h * 0.22);
                    sb.Append($"<rect x=\"4\" y=\"{slotY}\" width=\"{bufferWidth - 8}\" height=\"{h * 0.16}\" rx=\"2\" fill=\"{queueStroke}\" opacity=\"{0.2 + i * 0.15}\" />");
                }

                // Multiple servers (3 circles)
                var serverR = h * 0.14;
                var serverCX = serverAreaX + (w - serverAreaX) / 2;
                sb.Append($"<defs><radialGradient id=\"mmcsGrad_{node.Id}\" cx=\"30%\" cy=\"30%\" r=\"70%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fff;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fed7aa;stop-opacity:1\" />");
                sb.Append($"</radialGradient></defs>");

                var serverSpacing = h * 0.26;
                for (int i = 0; i < 3; i++)
                {
                    var cy = h * 0.22 + i * serverSpacing;
                    sb.Append($"<circle cx=\"{serverCX}\" cy=\"{cy}\" r=\"{serverR}\" fill=\"url(#mmcsGrad_{node.Id})\" stroke=\"{serverStroke}\" stroke-width=\"2\" />");
                }

                // "c" servers label
                sb.Append($"<text x=\"{serverCX}\" y=\"{h - 4}\" font-size=\"10\" fill=\"{serverAccent}\" text-anchor=\"middle\" font-weight=\"700\">c</text>");

                // Branching connection
                sb.Append($"<line x1=\"{bufferWidth + 2}\" y1=\"{h / 2}\" x2=\"{serverAreaX + 4}\" y2=\"{h / 2}\" stroke=\"{capacityColor}\" stroke-width=\"1.5\" />");
                sb.Append($"<line x1=\"{serverAreaX + 4}\" y1=\"{h * 0.22}\" x2=\"{serverAreaX + 4}\" y2=\"{h * 0.78}\" stroke=\"{capacityColor}\" stroke-width=\"1\" />");

                // M/M/c label badge
                sb.Append($"<rect x=\"{w / 2 - 18}\" y=\"1\" width=\"36\" height=\"12\" rx=\"6\" fill=\"#1e293b\" />");
                sb.Append($"<text x=\"{w / 2}\" y=\"10\" font-size=\"8\" fill=\"white\" text-anchor=\"middle\" font-weight=\"600\">M/M/c</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, CreateQMakerTerminals()),

            // Splitter (1 input, N outputs) - Dynamic terminal count
            // Uses InputTerminalCount=1, OutputTerminalCount=N (default 2)
            new ShapeDescriptor("splitter", "Splitter", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var strokeW = node.StrokeWidth ?? 2.5;
                var outputCount = Math.Max(2, node.OutputTerminalCount);

                sb.Append($"<defs><linearGradient id=\"splitterGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fce7f3;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fbcfe8;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<polygon points=\"0,{h / 2} {w * 0.92},2 {w * 0.92},{h - 2} \" fill=\"url(#splitterGrad_{node.Id})\" stroke=\"#db2777\" stroke-width=\"{strokeW}\" stroke-linejoin=\"round\" />");

                // Dynamic split lines based on output count
                double margin = h * 0.15;
                double usableHeight = h - 2 * margin;
                for (int i = 0; i < outputCount; i++)
                {
                    double t = outputCount == 1 ? 0.5 : (double)i / (outputCount - 1);
                    double outY = margin + t * usableHeight;
                    sb.Append($"<line x1=\"{w * 0.3}\" y1=\"{h / 2}\" x2=\"{w * 0.7}\" y2=\"{outY}\" stroke=\"#be185d\" stroke-width=\"2\" />");
                }

                // Output count indicator
                sb.Append($"<text x=\"{w * 0.85}\" y=\"{h - 4}\" font-size=\"9\" fill=\"#db2777\" text-anchor=\"middle\" font-weight=\"600\">1:{outputCount}</text>");

                return sb.ToString();
            }, CreateQMakerTerminals(), new TerminalConfig(InputCount: 1, OutputCount: 2)),

            // Collector (N inputs, 1 output) - Dynamic terminal count
            // Uses InputTerminalCount=N (default 2), OutputTerminalCount=1
            new ShapeDescriptor("collector", "Collector", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var strokeW = node.StrokeWidth ?? 2.5;
                var inputCount = Math.Max(2, node.InputTerminalCount);

                sb.Append($"<defs><linearGradient id=\"collectorGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#ccfbf1;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#99f6e4;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<polygon points=\"{w * 0.08},2 {w * 0.08},{h - 2} {w},{h / 2}\" fill=\"url(#collectorGrad_{node.Id})\" stroke=\"#0d9488\" stroke-width=\"{strokeW}\" stroke-linejoin=\"round\" />");

                // Dynamic merge lines based on input count
                double margin = h * 0.15;
                double usableHeight = h - 2 * margin;
                for (int i = 0; i < inputCount; i++)
                {
                    double t = inputCount == 1 ? 0.5 : (double)i / (inputCount - 1);
                    double inY = margin + t * usableHeight;
                    sb.Append($"<line x1=\"{w * 0.3}\" y1=\"{inY}\" x2=\"{w * 0.7}\" y2=\"{h / 2}\" stroke=\"#0f766e\" stroke-width=\"2\" />");
                }

                // Input count indicator
                sb.Append($"<text x=\"{w * 0.15}\" y=\"{h - 4}\" font-size=\"9\" fill=\"#0d9488\" text-anchor=\"middle\" font-weight=\"600\">{inputCount}:1</text>");

                return sb.ToString();
            }, CreateQMakerTerminals(), new TerminalConfig(InputCount: 2, OutputCount: 1)),

            // Fork (legacy alias for Splitter, 1 input, 2 outputs) - Professional
            new ShapeDescriptor("fork", "Fork", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var strokeW = node.StrokeWidth ?? 2.5;

                sb.Append($"<defs><linearGradient id=\"forkGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#fce7f3;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#fbcfe8;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<polygon points=\"0,{h / 2} {w * 0.92},2 {w * 0.92},{h - 2} \" fill=\"url(#forkGrad_{node.Id})\" stroke=\"#db2777\" stroke-width=\"{strokeW}\" stroke-linejoin=\"round\" />");

                // Fork lines visualization
                sb.Append($"<line x1=\"{w * 0.3}\" y1=\"{h / 2}\" x2=\"{w * 0.7}\" y2=\"{h * 0.25}\" stroke=\"#be185d\" stroke-width=\"2\" />");
                sb.Append($"<line x1=\"{w * 0.3}\" y1=\"{h / 2}\" x2=\"{w * 0.7}\" y2=\"{h * 0.75}\" stroke=\"#be185d\" stroke-width=\"2\" />");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in", Name = "In", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.5, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "out1", Name = "Out 1", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.25, Direction = TerminalDirection.Right },
                new TerminalDefinition { Id = "out2", Name = "Out 2", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.75, Direction = TerminalDirection.Right }
            }),

            // Join (legacy alias for Collector, 2 inputs, 1 output) - Professional
            new ShapeDescriptor("join", "Join", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var strokeW = node.StrokeWidth ?? 2.5;

                sb.Append($"<defs><linearGradient id=\"joinGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#ccfbf1;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#99f6e4;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<polygon points=\"{w * 0.08},2 {w * 0.08},{h - 2} {w},{h / 2}\" fill=\"url(#joinGrad_{node.Id})\" stroke=\"#0d9488\" stroke-width=\"{strokeW}\" stroke-linejoin=\"round\" />");

                // Join lines visualization
                sb.Append($"<line x1=\"{w * 0.3}\" y1=\"{h * 0.25}\" x2=\"{w * 0.7}\" y2=\"{h / 2}\" stroke=\"#0f766e\" stroke-width=\"2\" />");
                sb.Append($"<line x1=\"{w * 0.3}\" y1=\"{h * 0.75}\" x2=\"{w * 0.7}\" y2=\"{h / 2}\" stroke=\"#0f766e\" stroke-width=\"2\" />");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in1", Name = "In 1", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.25, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "in2", Name = "In 2", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.75, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "out", Name = "Out", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.5, Direction = TerminalDirection.Right }
            }),

            // Router (probabilistic routing) - Professional
            new ShapeDescriptor("router", "Router", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cx = w / 2;
                var cy = h / 2;
                var strokeW = node.StrokeWidth ?? 2.5;

                sb.Append($"<defs><linearGradient id=\"routerGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"100%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#e0e7ff;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#c7d2fe;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<polygon points=\"{cx},2 {w - 2},{cy} {cx},{h - 2} 2,{cy}\" fill=\"url(#routerGrad_{node.Id})\" stroke=\"#4f46e5\" stroke-width=\"{strokeW}\" stroke-linejoin=\"round\" />");

                // "p" for probability with decorative circle
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{Math.Min(w, h) * 0.18}\" fill=\"white\" stroke=\"#6366f1\" stroke-width=\"1.5\" />");
                sb.Append($"<text x=\"{cx}\" y=\"{cy + 5}\" font-size=\"{Math.Min(w, h) * 0.25}\" fill=\"#4338ca\" text-anchor=\"middle\" font-style=\"italic\" font-weight=\"600\">p</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, new List<TerminalDefinition>
            {
                new TerminalDefinition { Id = "in", Name = "In", Type = TerminalType.Input, NormalizedX = 0.0, NormalizedY = 0.5, Direction = TerminalDirection.Left },
                new TerminalDefinition { Id = "out1", Name = "Out 1", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.25, Direction = TerminalDirection.Right },
                new TerminalDefinition { Id = "out2", Name = "Out 2", Type = TerminalType.Output, NormalizedX = 1.0, NormalizedY = 0.75, Direction = TerminalDirection.Right }
            }),

            // Delay element (infinite server) - Professional
            new ShapeDescriptor("delay", "Delay (∞ servers)", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                var cornerRadius = h * 0.4;
                var strokeW = node.StrokeWidth ?? 2.5;

                sb.Append($"<defs><linearGradient id=\"delayGrad_{node.Id}\" x1=\"0%\" y1=\"0%\" x2=\"100%\" y2=\"0%\">");
                sb.Append($"<stop offset=\"0%\" style=\"stop-color:#f5f3ff;stop-opacity:1\" />");
                sb.Append($"<stop offset=\"100%\" style=\"stop-color:#ede9fe;stop-opacity:1\" />");
                sb.Append($"</linearGradient></defs>");
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"{cornerRadius}\" ry=\"{cornerRadius}\" fill=\"url(#delayGrad_{node.Id})\" stroke=\"#7c3aed\" stroke-width=\"{strokeW}\" />");

                // Clock/timer icon suggestion with ∞
                sb.Append($"<text x=\"{w / 2}\" y=\"{h / 2 + h * 0.12}\" font-size=\"{h * 0.45}\" fill=\"#6d28d9\" text-anchor=\"middle\" font-weight=\"600\">∞</text>");

                // "delay" label
                sb.Append($"<text x=\"{w / 2}\" y=\"{h - 4}\" font-size=\"8\" fill=\"#7c3aed\" text-anchor=\"middle\" font-weight=\"500\">delay</text>");

                // Terminals rendered by universal TerminalRenderService
                return sb.ToString();
            }, CreateQMakerTerminals())
        };

        templates["qmaker"] = new Template("qmaker", "QMaker (Queueing Networks)", shapes, UseTerminals: true);
    }

    // Helper to create standard QMaker terminals (input left, output right)
    // Colors are determined by TerminalType via TerminalRenderService
    private static List<TerminalDefinition> CreateQMakerTerminals()
    {
        return new List<TerminalDefinition>
        {
            new TerminalDefinition
            {
                Id = "in",
                Name = "In",
                Type = TerminalType.Input,
                NormalizedX = 0.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Left,
                Style = TerminalStyle.Circle
            },
            new TerminalDefinition
            {
                Id = "out",
                Name = "Out",
                Type = TerminalType.Output,
                NormalizedX = 1.0,
                NormalizedY = 0.5,
                Direction = TerminalDirection.Right,
                Style = TerminalStyle.Circle
            }
        };
    }

    private void RegisterDecisionTreeTemplate()
    {
        var shapes = new List<ShapeDescriptor>
        {
            // Decision Node - Square with value (choice point)
            new ShapeDescriptor("decision", "Decision Node", node =>
            {
                var w = node.Width;
                var h = node.Height;
                return $"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#dbeafe")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\"{GetDashAttr(node)} />";
            }),

            // Chance Node - Circle (probability/uncertainty point)
            new ShapeDescriptor("chance", "Chance Node", node =>
            {
                var cx = node.Width / 2;
                var cy = node.Height / 2;
                var r = Math.Min(node.Width, node.Height) / 2 - 2;
                return $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "#99f6e4")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\"{GetDashAttr(node)} />";
            }),

            // Terminal/End Node - Rectangle with only left edge (payoff/outcome)
            new ShapeDescriptor("terminal", "Terminal Node", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Background fill with no border
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#f0fdf4")}\" stroke=\"none\" />");
                // Left edge only (black)
                sb.Append($"<line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"{h}\" stroke=\"#000000\" stroke-width=\"{(node.StrokeWidth ?? 2)}\" />");
                return sb.ToString();
            }),

            // Value Label - For showing monetary values like $270,000
            new ShapeDescriptor("value-label", "Value Label", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 1)}\"{GetDashAttr(node)} />");
                return sb.ToString();
            }),

            // Payoff Box - Right-aligned result box
            new ShapeDescriptor("payoff", "Payoff Box", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var sb = new StringBuilder();
                // Left bar indicator
                sb.Append($"<rect x=\"0\" y=\"0\" width=\"4\" height=\"{h}\" fill=\"{node.StrokeColor}\" />");
                sb.Append($"<rect x=\"4\" y=\"0\" width=\"{w - 4}\" height=\"{h}\" fill=\"{(node.FillColor ?? "#fefce8")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 1)}\"{GetDashAttr(node)} />");
                return sb.ToString();
            }),

            // Probability Label - Small circle with probability value
            new ShapeDescriptor("probability", "Probability Label", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var cx = w / 2;
                var cy = h / 2;
                var r = Math.Min(w, h) / 2 - 1;
                var sb = new StringBuilder();
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? "white")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 1)}\"{GetDashAttr(node)} />");
                return sb.ToString();
            }),

            // Branch Point - Small filled circle for branch intersections
            new ShapeDescriptor("branch-point", "Branch Point", node =>
            {
                var cx = node.Width / 2;
                var cy = node.Height / 2;
                var r = Math.Min(node.Width, node.Height) / 2 - 1;
                return $"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{(node.FillColor ?? node.StrokeColor)}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 1)}\"{GetDashAttr(node)} />";
            }),

            // Outcome Node - Triangle pointing right (for final outcomes)
            new ShapeDescriptor("outcome", "Outcome", node =>
            {
                var w = node.Width;
                var h = node.Height;
                var points = $"0,0 {w},{h / 2} 0,{h}";
                return $"<polygon points=\"{points}\" fill=\"{(node.FillColor ?? "#fee2e2")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\"{GetDashAttr(node)} />";
            }),

            // Utility Node - Rounded rectangle for utility/expected values
            new ShapeDescriptor("utility", "Utility Node", node =>
            {
                var w = node.Width;
                var h = node.Height;
                return $"<rect x=\"0\" y=\"0\" width=\"{w}\" height=\"{h}\" rx=\"{h / 2}\" fill=\"{(node.FillColor ?? "#e0e7ff")}\" stroke=\"{node.StrokeColor}\" stroke-width=\"{(node.StrokeWidth ?? 2)}\"{GetDashAttr(node)} />";
            })
        };

        templates["decision-tree"] = new Template("decision-tree", "Decision Tree", shapes);
    }
}
