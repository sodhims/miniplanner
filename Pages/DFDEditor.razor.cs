using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // Injected services - these override the @inject in razor file
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected HttpClient HttpClient { get; set; } = default!;
    [Inject] protected GeometryService GeometryService { get; set; } = default!;
    [Inject] protected PathService PathService { get; set; } = default!;
    [Inject] protected UndoService UndoService { get; set; } = default!;
    [Inject] protected ExportService ExportService { get; set; } = default!;
    [Inject] protected ShapeLibraryService shapeLibrary { get; set; } = default!;
    [Inject] protected TemplateConfigService TemplateConfig { get; set; } = default!;

    // Core state
    private List<Node> nodes = new();
    private List<Edge> edges = new();
    private List<EdgeLabel> edgeLabels = new();
    private List<FreehandStroke> freehandStrokes = new();
    private List<DrawingShape> drawingShapes = new();
    private List<int> selectedNodes = new();
    private List<int> selectedEdges = new();
    private List<int> selectedLabels = new();
    private List<int> selectedStrokes = new();
    private List<int> selectedDrawingShapes = new();
    private int nextStrokeId = 1;
    private int nextShapeId = 1;

    // Element references
    private ElementReference canvasRef;
    private ElementReference minimapRef;
    private ElementReference textInputRef;
    private ElementReference textEditTextarea;

    // Editor mode
    private EditorMode mode = EditorMode.Select;
    private NodeShape selectedShape = NodeShape.Rectangle;
    private bool useOrthoPlacement = false;
    private bool snapToGrid = false;

    // Drawing tool state
    private DrawTool currentDrawTool = DrawTool.Pencil;
    private bool chainStrokeMode = false;
    private void ApplyPreset(StrokePreset preset) { /* TODO: Implement preset application */ }

    // Recording state
    private Recording? currentRecording = null;
    private System.Diagnostics.Stopwatch? recordingStopwatch = null;
    private bool isRecording = false;
    private bool isPlayingRecording = false;
    private int playbackIndex = 0;
    private string recordingName = "";
    private bool showRecordingDialog = false;
    private double playbackSpeed = 1.0;
    private CancellationTokenSource? playbackCts = null;
    [Inject] protected RecordingService RecordingService { get; set; } = default!;

    // Simulation state
    private SimulationEngineService? simulationEngine = null;
    private double simulationTime = 0.0;
    private double simulationSpeed = 1.0;
    private List<string> simulationLog = new();
    private bool showSimulationLog = false;

    // Connection state
    private int? pendingConnectionNodeId = null;
    private ConnectionPoint? pendingConnection = null;
    private (double X, double Y)? pendingConnectionPoint = null;
    private int? hoveredNodeId = null;

    // Terminal connection state (for terminal-to-terminal connections)
    private int? pendingTerminalNodeId = null;
    private string? pendingTerminalType = null; // "input", "output", "third", or "extra:N"

    // Rubberbanding state for edge creation/reconnection
    private bool isRubberbanding = false;
    private int? rubberbandFromNodeId = null;
    private string? rubberbandFromTerminal = null;
    private double rubberbandEndX = 0;
    private double rubberbandEndY = 0;
    // For reconnecting existing edges
    private int? rubberbandEdgeId = null;  // The edge being reconnected (null = new connection)
    private string? rubberbandEdgeEnd = null;  // "from" or "to" - which end is being reconnected

    // Edge reconnection state (legacy - keeping for compatibility)
    private int? pendingEdgeReconnectId = null;
    private string? pendingEdgeReconnectEnd = null;

    // Drag state
    private int? draggingNodeId = null;
    private double dragOffsetX = 0;
    private double dragOffsetY = 0;
    private double dragStartX = 0;
    private double dragStartY = 0;

    // Resize/drag state
    private int? resizingNodeId = null;
    private int? draggingEdgeId = null;
    private int draggingWaypointIndex = -1;
    private int? draggingLabelId = null;
    private int? resizingLabelId = null;

    // Editing state
    private int? editingNodeId = null;
    private int? editingLabelId = null;

    // ID counters
    private int nextId = 1;
    private int nextEdgeId = 1;
    private int nextLabelId = 1;

    // Dialog state
    private bool showExportDialog = false;
    private bool showLoadDialog = false;
    private bool showHelpDialog = false;
    private bool showAboutDialog = false;

    // Theme configurator state
    private bool showThemeConfigurator = false;
    private string themeConfigTab = "select";
    private EditableThemeData? editingThemeData = null;
    private string themeImportJson = "";
    private string themeImportError = "";
    private string? editingThemeId = null;
    private string rowColorMode = "individual"; // "individual", "all-same", "gradient"
    private string rowColorAllSame = "#ffffff";
    private string rowColorGradientFrom = "#ffffff";
    private string rowColorGradientTo = "#e0e0e0";

    // Context menu state
    private bool showNodeContextMenu = false;
    private int contextMenuNodeId = 0;
    private double contextMenuX = 0;
    private double contextMenuY = 0;
    private string exportedContent = "";
    private string exportDialogTitle = "";
    private string exportDialogDescription = "";
    private string loadDiagramJson = "";
    private string loadErrorMessage = "";
    private string importFormat = "auto";


    // Panel collapse states (entire panels)
    private bool isLeftPanelCollapsed = false;
    private bool isRightPanelCollapsed = false;

    // Panel section collapse states (sections within panels)
    private bool collapseCanvas = false;
    private bool collapseShape = false;
    private bool collapseEdgeStyle = false;
    private bool collapseProperties = false;
    private bool collapseMinimap = false;
    private bool collapseInfo = false;
    private bool collapseNodeProperties = false;
    private bool collapseProjectResources = false;
    private bool collapseLayers = false;

    // Layer editing state
    private string? editingLayerName = null;
    private string editingLayerNameValue = "";
    private bool isBaseLayerVisible = true;

    // Template selection state (used by the properties UI)
    private string? selectedTemplateId = null;
    private string? selectedTemplateShapeId = null;

    // Circuit component counters for auto-labeling (R1, R2, C1, C2, etc.)
    private Dictionary<string, int> circuitComponentCounters = new()
    {
        ["resistor"] = 0,
        ["capacitor"] = 0,
        ["inductor"] = 0,
        ["diode"] = 0,
        ["transistor-npn"] = 0,
        ["ground"] = 0,
        ["vcc"] = 0,
        ["and-gate"] = 0,
        ["or-gate"] = 0,
        ["not-gate"] = 0,
        ["ic-chip"] = 0,
        ["op-amp"] = 0
    };

    // Component prefix mapping for circuit labels
    private static readonly Dictionary<string, string> CircuitComponentPrefixes = new()
    {
        ["resistor"] = "R",
        ["capacitor"] = "C",
        ["inductor"] = "L",
        ["diode"] = "D",
        ["transistor-npn"] = "Q",
        ["ground"] = "GND",
        ["vcc"] = "VCC",
        ["and-gate"] = "U",
        ["or-gate"] = "U",
        ["not-gate"] = "U",
        ["ic-chip"] = "U",
        ["op-amp"] = "U"
    };

    // Template-specific edge style defaults
    public record TemplateEdgeDefaults(
        ArrowDirection ArrowDirection,
        EdgeStyle EdgeStyle,
        int StrokeWidth,
        string StrokeColor,
        string StrokeDashArray,
        bool IsDoubleLine
    );

    private static readonly Dictionary<string, TemplateEdgeDefaults> TemplateEdgeStyles = new()
    {
        // Circuit: no arrows, ortho routing, thin lines
        ["circuit"] = new TemplateEdgeDefaults(
            ArrowDirection.None,
            EdgeStyle.Ortho,
            1,
            "#374151",
            "",
            false
        ),
        // Flowchart: standard arrows at end
        ["flowchart"] = new TemplateEdgeDefaults(
            ArrowDirection.End,
            EdgeStyle.Direct,
            1,
            "#374151",
            "",
            false
        ),
        // ICD: bidirectional arrows
        ["icd"] = new TemplateEdgeDefaults(
            ArrowDirection.Both,
            EdgeStyle.Direct,
            1,
            "#475569",
            "",
            false
        ),
        // Network: no arrows (connections are bidirectional)
        ["network"] = new TemplateEdgeDefaults(
            ArrowDirection.None,
            EdgeStyle.Direct,
            1,
            "#374151",
            "",
            false
        ),
        // BPMN: standard arrows
        ["bpmn"] = new TemplateEdgeDefaults(
            ArrowDirection.End,
            EdgeStyle.Direct,
            1,
            "#374151",
            "",
            false
        ),
        // STS: no arrows (terminals indicate direction), ortho routing
        ["sts"] = new TemplateEdgeDefaults(
            ArrowDirection.None,
            EdgeStyle.Ortho,
            1,
            "#374151",
            "",
            false
        )
    };

    // Get current edge defaults based on selected template
    private TemplateEdgeDefaults GetCurrentEdgeDefaults()
    {
        if (!string.IsNullOrEmpty(selectedTemplateId) && TemplateEdgeStyles.TryGetValue(selectedTemplateId, out var defaults))
        {
            return defaults;
        }
        // Default: standard arrows at end, thin line
        return new TemplateEdgeDefaults(ArrowDirection.End, EdgeStyle.Direct, 1, "#374151", "", false);
    }

    // Template-specific node style defaults
    public record TemplateNodeDefaults(
        string FillColor,
        string StrokeColor,
        int StrokeWidth,
        string? InputTerminalColor = null,  // null = use default (green)
        string? OutputTerminalColor = null  // null = use default (red)
    );

    // Default styles (used for reset)
    private static readonly Dictionary<string, TemplateNodeDefaults> DefaultTemplateNodeStyles = new()
    {
        ["circuit"] = new TemplateNodeDefaults("#ffffff", "#3b82f6", 2, "#3b82f6", "#3b82f6"),
        ["flowchart"] = new TemplateNodeDefaults("#ffffff", "#059669", 2, null, null),
        ["icd"] = new TemplateNodeDefaults("#f8fafc", "#475569", 2, null, null),
        ["network"] = new TemplateNodeDefaults("#ffffff", "#374151", 2, null, null),
        ["bpmn"] = new TemplateNodeDefaults("#ffffff", "#374151", 2, null, null),
        ["sts"] = new TemplateNodeDefaults("#fef9c3", "#374151", 2, "#ec4899", "#f59e0b")
    };

    // Mutable template styles (user-configurable)
    private Dictionary<string, TemplateNodeDefaults> templateNodeStyles = new()
    {
        ["circuit"] = new TemplateNodeDefaults("#ffffff", "#3b82f6", 2, "#3b82f6", "#3b82f6"),
        ["flowchart"] = new TemplateNodeDefaults("#ffffff", "#059669", 2, null, null),
        ["icd"] = new TemplateNodeDefaults("#f8fafc", "#475569", 2, null, null),
        ["network"] = new TemplateNodeDefaults("#ffffff", "#374151", 2, null, null),
        ["bpmn"] = new TemplateNodeDefaults("#ffffff", "#374151", 2, null, null),
        ["sts"] = new TemplateNodeDefaults("#fef9c3", "#374151", 2, "#ec4899", "#f59e0b")
    };

    // Template styles dialog state
    private bool showTemplateStylesDialog = false;
    private string editingTemplateId = "circuit";
    private string editingShapeId = "";  // Empty = editing defaults, otherwise specific component
    private string tplEditFillColor = "#ffffff";
    private string tplEditStrokeColor = "#374151";
    private int tplEditStrokeWidth = 2;
    private string tplEditInputTerminalColor = "#22c55e";
    private string tplEditOutputTerminalColor = "#ef4444";
    // Per-component terminal type editing
    private TerminalType tplEditInputTerminalType = TerminalType.Input;
    private TerminalType tplEditOutputTerminalType = TerminalType.Output;
    private bool tplEditHasThirdTerminal = false;
    private TerminalType tplEditThirdTerminalType = TerminalType.Bidirectional;
    // Storage for per-component terminal configurations (templateId:shapeId -> config)
    // Includes terminal types and precise positions (normalized 0-1 coordinates)
    private Dictionary<string, ComponentTerminalConfig> componentTerminalConfigs = new();
    // Preview terminal positions as normalized coordinates (0-1)
    // X: 0=left, 1=right; Y: 0=top, 1=bottom
    private double previewT1X = 0.0;  // T1 (input) - default left center
    private double previewT1Y = 0.5;
    private double previewT2X = 1.0;  // T2 (output) - default right center
    private double previewT2Y = 0.5;
    private double previewT3X = 0.5;  // T3 (third) - default bottom center
    private double previewT3Y = 1.0;
    // Preview terminal dragging state
    private string? previewDraggingTerminal = null;

    // Get current node defaults based on selected template and current theme
    private TemplateNodeDefaults GetCurrentNodeDefaults()
    {
        // Use theme colors for the current template and shape
        var (fill, stroke, _) = dfdTheme.GetColorsForShape(selectedTemplateId, selectedTemplateShapeId);

        // Get other defaults from template styles (stroke width, terminal colors)
        if (!string.IsNullOrEmpty(selectedTemplateId) && templateNodeStyles.TryGetValue(selectedTemplateId, out var defaults))
        {
            return new TemplateNodeDefaults(fill, stroke, defaults.StrokeWidth, defaults.InputTerminalColor, defaults.OutputTerminalColor);
        }
        // Default: theme colors with standard stroke width
        return new TemplateNodeDefaults(fill, stroke, 2, null, null);
    }

    /// <summary>
    /// Get the effective terminal configuration for a node.
    /// Priority: 1) User-customized per-component config, 2) Shape library defaults, 3) Global defaults
    /// </summary>
    private (TerminalType inputType, TerminalType outputType, bool hasThird, TerminalType thirdType, int inputCount, int outputCount) GetEffectiveTerminalConfig(string? templateId, string? shapeId)
    {
        // Check for user-customized per-component configuration first
        if (!string.IsNullOrEmpty(templateId) && !string.IsNullOrEmpty(shapeId))
        {
            var key = $"{templateId}:{shapeId}";
            if (componentTerminalConfigs.TryGetValue(key, out var customConfig))
            {
                return (customConfig.T1Type, customConfig.T2Type, customConfig.HasT3, customConfig.T3Type, 1, 1);
            }
        }

        // Check for shape library defaults
        var shapeConfig = shapeLibrary?.GetTerminalConfig(templateId, shapeId);
        if (shapeConfig != null)
        {
            return (shapeConfig.InputType, shapeConfig.OutputType, shapeConfig.HasThirdTerminal, shapeConfig.ThirdType, shapeConfig.InputCount, shapeConfig.OutputCount);
        }

        // Fall back to global defaults
        return (defaultInputTerminalType, defaultOutputTerminalType, defaultHasThirdTerminal, defaultThirdTerminalType, 1, 1);
    }

    /// <summary>
    /// Get the effective terminal layout for a node (legacy - for backward compatibility).
    /// </summary>
    private string GetEffectiveTerminalLayout(string? templateId, string? shapeId)
    {
        if (!string.IsNullOrEmpty(templateId) && !string.IsNullOrEmpty(shapeId))
        {
            var key = $"{templateId}:{shapeId}";
            if (componentTerminalConfigs.TryGetValue(key, out var customConfig))
            {
                // Convert normalized positions to side names
                var t1Side = TerminalLayouts.GetSideFromNormalizedPosition(customConfig.T1X, customConfig.T1Y);
                var t2Side = TerminalLayouts.GetSideFromNormalizedPosition(customConfig.T2X, customConfig.T2Y);
                return $"{t1Side}-{t2Side}";
            }
        }
        return "left-right";
    }

    /// <summary>
    /// Get the full terminal positions config for a node.
    /// Returns (T1X, T1Y, T2X, T2Y, T3X, T3Y) or null if no custom config.
    /// </summary>
    private ComponentTerminalConfig? GetEffectiveTerminalPositions(string? templateId, string? shapeId)
    {
        if (!string.IsNullOrEmpty(templateId) && !string.IsNullOrEmpty(shapeId))
        {
            var key = $"{templateId}:{shapeId}";
            if (componentTerminalConfigs.TryGetValue(key, out var customConfig))
            {
                return customConfig;
            }
        }
        return null;
    }

    // Open the template styles dialog
    private void OpenTemplateStylesDialog()
    {
        editingTemplateId = selectedTemplateId ?? "circuit";
        editingShapeId = "";
        LoadTemplateStyleForEditing(editingTemplateId);
        LoadTerminalConfigForEditing();
        showTemplateStylesDialog = true;
        StateHasChanged();
    }

    // Called when template dropdown changes
    private void OnEditingTemplateChanged()
    {
        editingShapeId = "";  // Reset to default
        LoadTemplateStyleForEditing(editingTemplateId);
        LoadTerminalConfigForEditing();
    }

    // Called when component/shape dropdown changes
    private void OnEditingShapeChanged()
    {
        LoadTerminalConfigForEditing();
    }

    // Load a template's styles into the editing fields
    private void LoadTemplateStyleForEditing(string templateId)
    {
        if (templateNodeStyles.TryGetValue(templateId, out var style))
        {
            tplEditFillColor = style.FillColor;
            tplEditStrokeColor = style.StrokeColor;
            tplEditStrokeWidth = style.StrokeWidth;
            tplEditInputTerminalColor = style.InputTerminalColor ?? "#22c55e";
            tplEditOutputTerminalColor = style.OutputTerminalColor ?? "#ef4444";
        }
    }

    // Load terminal config for the currently selected component
    private void LoadTerminalConfigForEditing()
    {
        if (string.IsNullOrEmpty(editingShapeId))
        {
            // Editing global defaults
            tplEditInputTerminalType = defaultInputTerminalType;
            tplEditOutputTerminalType = defaultOutputTerminalType;
            tplEditHasThirdTerminal = defaultHasThirdTerminal;
            tplEditThirdTerminalType = defaultThirdTerminalType;
            // Reset preview positions to defaults
            previewT1X = 0.0; previewT1Y = 0.5;  // left center
            previewT2X = 1.0; previewT2Y = 0.5;  // right center
            previewT3X = 0.5; previewT3Y = 1.0;  // bottom center
        }
        else
        {
            // Editing specific component - check for saved config first
            var key = $"{editingTemplateId}:{editingShapeId}";
            if (componentTerminalConfigs.TryGetValue(key, out var customConfig))
            {
                tplEditInputTerminalType = customConfig.T1Type;
                tplEditOutputTerminalType = customConfig.T2Type;
                tplEditHasThirdTerminal = customConfig.HasT3;
                tplEditThirdTerminalType = customConfig.T3Type;
                previewT1X = customConfig.T1X;
                previewT1Y = customConfig.T1Y;
                previewT2X = customConfig.T2X;
                previewT2Y = customConfig.T2Y;
                previewT3X = customConfig.T3X;
                previewT3Y = customConfig.T3Y;
            }
            else
            {
                // Fall back to shape library defaults
                var config = GetEffectiveTerminalConfig(editingTemplateId, editingShapeId);
                tplEditInputTerminalType = config.inputType;
                tplEditOutputTerminalType = config.outputType;
                tplEditHasThirdTerminal = config.hasThird;
                tplEditThirdTerminalType = config.thirdType;
                // Reset preview positions to defaults
                previewT1X = 0.0; previewT1Y = 0.5;  // left center
                previewT2X = 1.0; previewT2Y = 0.5;  // right center
                previewT3X = 0.5; previewT3Y = 1.0;  // bottom center
            }
        }
    }

    // Set terminal type for a specific terminal
    private void SetEditingTerminalType(int terminal, TerminalType type)
    {
        switch (terminal)
        {
            case 1:
                tplEditInputTerminalType = type;
                break;
            case 2:
                tplEditOutputTerminalType = type;
                break;
            case 3:
                tplEditThirdTerminalType = type;
                break;
        }
        SaveCurrentTerminalConfig();
    }

    // Set whether third terminal is enabled
    private void SetEditingHasThirdTerminal(bool hasThird)
    {
        tplEditHasThirdTerminal = hasThird;
        SaveCurrentTerminalConfig();
    }

    // Save the current terminal config
    private void SaveCurrentTerminalConfig()
    {
        if (string.IsNullOrEmpty(editingShapeId))
        {
            // Save to global defaults
            defaultInputTerminalType = tplEditInputTerminalType;
            defaultOutputTerminalType = tplEditOutputTerminalType;
            defaultHasThirdTerminal = tplEditHasThirdTerminal;
            defaultThirdTerminalType = tplEditThirdTerminalType;
        }
        else
        {
            // Save to per-component config including terminal positions
            var key = $"{editingTemplateId}:{editingShapeId}";
            componentTerminalConfigs[key] = new ComponentTerminalConfig
            {
                T1Type = tplEditInputTerminalType,
                T1X = previewT1X,
                T1Y = previewT1Y,
                T2Type = tplEditOutputTerminalType,
                T2X = previewT2X,
                T2Y = previewT2Y,
                HasT3 = tplEditHasThirdTerminal,
                T3Type = tplEditThirdTerminalType,
                T3X = previewT3X,
                T3Y = previewT3Y
            };
        }
    }

    // Get display name for a shape
    private string GetShapeDisplayName(string templateId, string shapeId)
    {
        var shape = shapeLibrary?.GetShape(templateId, shapeId);
        return shape?.DisplayName ?? shapeId;
    }

    // Save the current editing values to the template
    private void SaveTemplateStyle()
    {
        templateNodeStyles[editingTemplateId] = new TemplateNodeDefaults(
            tplEditFillColor,
            tplEditStrokeColor,
            tplEditStrokeWidth,
            tplEditInputTerminalColor,
            tplEditOutputTerminalColor
        );
        SaveTemplateStylesToLocalStorage();
        SaveTerminalConfigsToLocalStorage();
        StateHasChanged();
    }

    // Reset a template to its default style
    private void ResetTemplateStyle()
    {
        if (DefaultTemplateNodeStyles.TryGetValue(editingTemplateId, out var defaultStyle))
        {
            templateNodeStyles[editingTemplateId] = defaultStyle;
            LoadTemplateStyleForEditing(editingTemplateId);
            SaveTemplateStylesToLocalStorage();
            StateHasChanged();
        }
    }

    // Close the dialog
    private void CloseTemplateStylesDialog()
    {
        showTemplateStylesDialog = false;
        StateHasChanged();
    }

    // Get preview shape SVG for current editing component
    private string GetPreviewShapeSvg()
    {
        // Preview area: viewBox 0 0 200 140, shape at (40, 35) with size 120x70
        const double shapeX = 40;
        const double shapeY = 35;
        const double shapeW = 120;
        const double shapeH = 70;

        // Determine which shape to display:
        // 1. If a specific component is selected in the dropdown, use that
        // 2. Otherwise, try to use the currently selected shape from the sidebar
        // 3. Fall back to a generic rectangle
        var shapeIdToUse = !string.IsNullOrEmpty(editingShapeId)
            ? editingShapeId
            : (editingTemplateId == selectedTemplateId ? selectedTemplateShapeId : null);

        // Try to get shape from library
        var shape = !string.IsNullOrEmpty(shapeIdToUse)
            ? shapeLibrary?.GetShape(editingTemplateId, shapeIdToUse)
            : null;

        if (shape == null)
        {
            // Default rectangle preview
            return $"<rect x=\"{shapeX}\" y=\"{shapeY}\" width=\"{shapeW}\" height=\"{shapeH}\" rx=\"6\" fill=\"{tplEditFillColor}\" stroke=\"{tplEditStrokeColor}\" stroke-width=\"{tplEditStrokeWidth}\" />" +
                   $"<text x=\"{shapeX + shapeW/2}\" y=\"{shapeY + shapeH/2 + 5}\" text-anchor=\"middle\" font-size=\"14\" fill=\"{tplEditStrokeColor}\">Node</text>";
        }

        // Create a preview node with current editing values
        var previewNode = new Node
        {
            Width = shapeW,
            Height = shapeH,
            FillColor = tplEditFillColor,
            StrokeColor = tplEditStrokeColor,
            StrokeWidth = tplEditStrokeWidth,
            ComponentLabel = GetPreviewLabel(shapeIdToUse),
            Rotation = 0
        };

        // Wrap in a group to offset to center of preview area
        return $"<g transform=\"translate({shapeX}, {shapeY})\">{shape.Render(previewNode)}</g>";
    }

    // Get a preview label for the component
    private string GetPreviewLabel(string? shapeId = null)
    {
        var id = shapeId ?? editingShapeId;
        return id switch
        {
            "resistor" => "R1",
            "capacitor" => "C1",
            "inductor" => "L1",
            "diode" => "D1",
            "transistor-npn" => "Q1",
            "transistor-pnp" => "Q1",
            "opamp" => "U1",
            "led" => "D1",
            "ground" => "",
            "vcc" => "",
            _ => ""
        };
    }

    // Get terminal position coordinates for preview from normalized coordinates
    // Returns (x, y, stickX, stickY) in preview SVG coordinates
    private (double x, double y, double stickX, double stickY) GetPreviewTerminalCoordsFromNormalized(double normX, double normY, double width, double height)
    {
        const double stickOut = 15.0;
        double x = normX * width;
        double y = normY * height;

        // Determine stick-out direction based on which edge is closest
        var dir = TerminalLayouts.GetDirectionFromNormalizedPosition(normX, normY);
        var (stickX, stickY) = dir switch
        {
            TerminalDirection.Left => (-stickOut, 0.0),
            TerminalDirection.Right => (stickOut, 0.0),
            TerminalDirection.Top => (0.0, -stickOut),
            TerminalDirection.Bottom => (0.0, stickOut),
            _ => (0.0, 0.0)
        };

        return (x, y, stickX, stickY);
    }

    // Get color for terminal based on type
    private string GetPreviewTerminalColor(TerminalType type)
    {
        return type switch
        {
            TerminalType.Input => tplEditInputTerminalColor,
            TerminalType.Output => tplEditOutputTerminalColor,
            TerminalType.Bidirectional => "#3b82f6",
            _ => "#6b7280"
        };
    }

    // Handle preview terminal drag start
    private void OnPreviewTerminalMouseDown(string terminalId)
    {
        previewDraggingTerminal = terminalId;
    }

    // Handle preview mouse move for terminal dragging - now with precise positioning
    private void OnPreviewMouseMove(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        if (previewDraggingTerminal == null) return;

        // Preview SVG has fixed width=200 height=140 matching viewBox, so 1:1 coordinate mapping
        // Shape is at (40, 35) with size 120x70
        const double shapeX = 40;
        const double shapeY = 35;
        const double shapeW = 120;
        const double shapeH = 70;

        // Calculate position relative to shape (0-1 normalized)
        // OffsetX/Y are directly in viewBox coordinates since SVG width matches viewBox
        double normX = (e.OffsetX - shapeX) / shapeW;
        double normY = (e.OffsetY - shapeY) / shapeH;

        // Clamp to shape boundary (snap to edge)
        normX = Math.Clamp(normX, 0.0, 1.0);
        normY = Math.Clamp(normY, 0.0, 1.0);

        // Snap to nearest edge (terminals must be on the boundary)
        double distLeft = normX;
        double distRight = 1.0 - normX;
        double distTop = normY;
        double distBottom = 1.0 - normY;
        double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

        // Snap to the closest edge while preserving position along that edge
        if (Math.Abs(minDist - distLeft) < 0.01)
        {
            normX = 0.0;  // Snap to left edge
        }
        else if (Math.Abs(minDist - distRight) < 0.01)
        {
            normX = 1.0;  // Snap to right edge
        }
        else if (Math.Abs(minDist - distTop) < 0.01)
        {
            normY = 0.0;  // Snap to top edge
        }
        else
        {
            normY = 1.0;  // Snap to bottom edge
        }

        // Update the appropriate terminal position
        switch (previewDraggingTerminal)
        {
            case "input":
                previewT1X = normX;
                previewT1Y = normY;
                break;
            case "output":
                previewT2X = normX;
                previewT2Y = normY;
                break;
            case "third":
                previewT3X = normX;
                previewT3Y = normY;
                break;
        }

        StateHasChanged();
    }

    // Handle preview mouse up - save the config
    private void OnPreviewMouseUp()
    {
        if (previewDraggingTerminal != null)
        {
            // Save config when done dragging
            SaveCurrentTerminalConfig();
        }
        previewDraggingTerminal = null;
    }

    // Save template styles to localStorage
    private async void SaveTemplateStylesToLocalStorage()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(templateNodeStyles);
            await JS.InvokeVoidAsync("localStorage.setItem", "dfd2wasm_templateStyles", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving template styles: {ex.Message}");
        }
    }

    // Save terminal configs to localStorage
    private async void SaveTerminalConfigsToLocalStorage()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(componentTerminalConfigs);
            await JS.InvokeVoidAsync("localStorage.setItem", "dfd2wasm_terminalConfigs", json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving terminal configs: {ex.Message}");
        }
    }

    // Load template styles from localStorage
    private async Task LoadTemplateStylesFromLocalStorage()
    {
        try
        {
            var json = await JS.InvokeAsync<string>("localStorage.getItem", "dfd2wasm_templateStyles");
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, TemplateNodeDefaults>>(json);
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        templateNodeStyles[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading template styles: {ex.Message}");
        }
    }

    // Load terminal configs from localStorage
    private async Task LoadTerminalConfigsFromLocalStorage()
    {
        try
        {
            var json = await JS.InvokeAsync<string>("localStorage.getItem", "dfd2wasm_terminalConfigs");
            if (!string.IsNullOrEmpty(json))
            {
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ComponentTerminalConfig>>(json);
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        componentTerminalConfigs[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading terminal configs: {ex.Message}");
        }
    }

    // Helper to create an edge with template-appropriate defaults
    private Edge CreateEdgeWithDefaults(int fromId, int toId, ConnectionPoint fromConn, ConnectionPoint toConn)
    {
        var defaults = GetCurrentEdgeDefaults();
        return new Edge
        {
            Id = nextEdgeId++,
            From = fromId,
            To = toId,
            FromConnection = fromConn,
            ToConnection = toConn,
            IsOrthogonal = defaults.EdgeStyle == EdgeStyle.Ortho || useOrthoPlacement,
            Style = defaults.EdgeStyle,
            StrokeWidth = defaults.StrokeWidth,
            StrokeColor = defaults.StrokeColor,
            StrokeDashArray = defaults.StrokeDashArray,
            IsDoubleLine = defaults.IsDoubleLine,
            ArrowDirection = defaults.ArrowDirection
        };
    }

    private string GetNextComponentLabel(string shapeId)
    {
        if (!CircuitComponentPrefixes.TryGetValue(shapeId, out var prefix))
            return $"Node {nodes.Count + 1}";

        if (!circuitComponentCounters.ContainsKey(shapeId))
            circuitComponentCounters[shapeId] = 0;

        circuitComponentCounters[shapeId]++;
        return $"{prefix}{circuitComponentCounters[shapeId]}";
    }

    private IEnumerable<ShapeLibraryService.Template> GetAvailableTemplates() =>
        (shapeLibrary?.GetTemplates() ?? Enumerable.Empty<ShapeLibraryService.Template>())
        .Where(t => TemplateConfig?.IsTemplateEnabled(t.Id) ?? true);

    private IEnumerable<ShapeLibraryService.ShapeDescriptor> GetShapesForTemplate(string? templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return Enumerable.Empty<ShapeLibraryService.ShapeDescriptor>();
        var tpl = shapeLibrary.GetTemplate(templateId);
        return tpl?.Shapes ?? Enumerable.Empty<ShapeLibraryService.ShapeDescriptor>();
    }

    private async Task OnTemplateChanged(ChangeEventArgs e)
    {
        selectedTemplateId = e.Value?.ToString();

        // Special handling for Project template - auto-enable Project mode
        if (selectedTemplateId == "project")
        {
            if (!isProjectMode)
            {
                ToggleProjectMode();
            }
            // Exit Gantt mode if it was active
            if (isGanttMode)
            {
                ExitGanttMode();
            }
            // Continue to set up shape dropdown for Project shapes (task, milestone, summary, resource)
        }
        // Special handling for Gantt template - auto-enable Gantt mode
        else if (selectedTemplateId == "gantt")
        {
            if (!isGanttMode)
            {
                await ToggleGanttMode();
            }
            // Exit Project mode if it was active
            if (isProjectMode)
            {
                ExitProjectMode();
            }
        }
        else
        {
            // Exit Project mode if switching to a different template
            if (isProjectMode)
            {
                ExitProjectMode();
            }
            // Exit Gantt mode if switching to a different template
            if (isGanttMode)
            {
                ExitGanttMode();
            }
        }

        // When template changes, auto-select first shape in that template
        if (!string.IsNullOrEmpty(selectedTemplateId))
        {
            var shapes = GetShapesForTemplate(selectedTemplateId);
            selectedTemplateShapeId = shapes.FirstOrDefault()?.Id;
            // Auto-activate Add Shape mode when template is selected
            mode = EditorMode.AddNode;
            chainMode = false;
            ClearMultiConnectState();
            ClearConnectMode();  // Clear any active connection mode
            isRubberbanding = false;  // Clear any active rubberbanding
            rubberbandFromNodeId = null;
            rubberbandFromTerminal = null;
            rubberbandEdgeId = null;
            rubberbandEdgeEnd = null;
        }
        else
        {
            selectedTemplateShapeId = null;
        }
        StateHasChanged();
    }

    // Called when shape selection changes within a template
    private void OnTemplateShapeChanged(ChangeEventArgs e)
    {
        selectedTemplateShapeId = e.Value?.ToString();
        // Auto-activate Add Shape mode when shape is selected
        if (!string.IsNullOrEmpty(selectedTemplateShapeId))
        {
            mode = EditorMode.AddNode;
            chainMode = false;
            ClearMultiConnectState();
        }
        StateHasChanged();
    }

    private void ApplyTemplateToSelectedNodes()
    {
        if (selectedNodes.Count == 0) return;

        foreach (var nodeId in selectedNodes)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) continue;
            node.TemplateId = selectedTemplateId;
            node.TemplateShapeId = selectedTemplateShapeId;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void ClearTemplateFromSelectedNodes()
    {
        foreach (var nodeId in selectedNodes)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) continue;
            node.TemplateId = null;
            node.TemplateShapeId = null;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    /// <summary>
    /// Update the arrow direction for the current template's edge defaults
    /// </summary>
    private void UpdateTemplateArrowDirection(ChangeEventArgs e)
    {
        if (string.IsNullOrEmpty(selectedTemplateId)) return;

        if (Enum.TryParse<ArrowDirection>(e.Value?.ToString(), out var newDirection))
        {
            var current = GetCurrentEdgeDefaults();
            TemplateEdgeStyles[selectedTemplateId] = current with { ArrowDirection = newDirection };
            StateHasChanged();
        }
    }

    /// <summary>
    /// Update the edge style for the current template's edge defaults
    /// </summary>
    private void UpdateTemplateEdgeStyle(ChangeEventArgs e)
    {
        if (string.IsNullOrEmpty(selectedTemplateId)) return;

        if (Enum.TryParse<EdgeStyle>(e.Value?.ToString(), out var newStyle))
        {
            var current = GetCurrentEdgeDefaults();
            TemplateEdgeStyles[selectedTemplateId] = current with { EdgeStyle = newStyle };

            // Update ortho mode based on edge style
            useOrthoPlacement = (newStyle == EdgeStyle.Ortho || newStyle == EdgeStyle.OrthoRound);

            StateHasChanged();
        }
    }

    /// <summary>
    /// Render terminals for nodes using the universal TerminalRenderService
    /// Colors are based on terminal type: green=Input, red=Output, blue=Bidirectional
    /// Supports dynamic terminal counts (N inputs, M outputs)
    /// </summary>
    private string RenderGenericTerminals(Node node)
    {
        if (!node.ShowTerminals) return "";

        var sb = new System.Text.StringBuilder();
        var w = node.Width;
        var h = node.Height;

        // Parse terminal layout or use custom positions
        var (inputPos, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);

        // Get input side direction
        var (_, _, inDir) = TerminalLayouts.GetPositionCoords(inputPos);
        // Get output side direction
        var (_, _, outDir) = TerminalLayouts.GetPositionCoords(outputPos);

        // Render N input terminals
        int inputCount = Math.Max(1, node.InputTerminalCount);
        var inputPositions = GetTerminalPositions(inputCount, inputPos, w, h);
        foreach (var (px, py) in inputPositions)
        {
            sb.Append(TerminalRenderService.RenderTerminal(px, py, inDir, node.InputTerminalType, node.InputTerminalColor));
        }

        // Render M output terminals
        int outputCount = Math.Max(1, node.OutputTerminalCount);
        var outputPositions = GetTerminalPositions(outputCount, outputPos, w, h);
        foreach (var (px, py) in outputPositions)
        {
            sb.Append(TerminalRenderService.RenderTerminal(px, py, outDir, node.OutputTerminalType, node.OutputTerminalColor));
        }

        // Render third terminal if enabled (legacy support)
        if (node.HasThirdTerminal)
        {
            double t3X, t3Y;
            TerminalDirection t3Dir;
            if (node.T3X.HasValue && node.T3Y.HasValue)
            {
                t3X = node.T3X.Value;
                t3Y = node.T3Y.Value;
                t3Dir = TerminalLayouts.GetDirectionFromNormalizedPosition(t3X, t3Y);
            }
            else
            {
                t3X = 0.5;
                t3Y = 1.0;
                t3Dir = TerminalDirection.Bottom;
            }

            var t3Px = t3X * w;
            var t3Py = t3Y * h;
            sb.Append(TerminalRenderService.RenderTerminal(t3Px, t3Py, t3Dir, node.ThirdTerminalType, node.ThirdTerminalColor));
        }

        // Render extra terminals (legacy support for manually added terminals)
        foreach (var extra in node.ExtraTerminals)
        {
            var (px, py, dir) = GetExtraTerminalPosition(node, extra);
            sb.Append(TerminalRenderService.RenderTerminal(px, py, dir, extra.Type));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Calculate evenly-spaced terminal positions along a side
    /// </summary>
    private List<(double x, double y)> GetTerminalPositions(int count, string side, double width, double height)
    {
        var positions = new List<(double x, double y)>();
        if (count <= 0) return positions;

        // Determine if terminals are horizontal (left/right) or vertical (top/bottom)
        bool isVerticalSide = side == "left" || side == "right";
        double fixedCoord = side switch
        {
            "left" => 0,
            "right" => width,
            "top" => 0,
            "bottom" => height,
            _ => 0
        };

        // Calculate spacing along the variable axis
        double length = isVerticalSide ? height : width;
        double margin = length * 0.15; // 15% margin from edges
        double usableLength = length - 2 * margin;

        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0.5 : (double)i / (count - 1);
            double varCoord = margin + t * usableLength;

            if (isVerticalSide)
            {
                positions.Add((fixedCoord, varCoord));
            }
            else
            {
                positions.Add((varCoord, fixedCoord));
            }
        }

        return positions;
    }

    /// <summary>
    /// Render clickable hit areas for terminal-to-terminal connections
    /// Returns a RenderFragment with transparent circles over each terminal
    /// Supports dynamic terminal counts (N inputs, M outputs)
    /// </summary>
    private RenderFragment RenderTerminalHitAreas(Node node, int nodeId) => builder =>
    {
        if (!node.ShowTerminals) return;

        var termStickOut = 12.0;
        var hitRadius = 15.0;
        var (inputPos, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);

        // Get directions
        var (_, _, inDir) = TerminalLayouts.GetPositionCoords(inputPos);
        var (_, _, outDir) = TerminalLayouts.GetPositionCoords(outputPos);

        int seq = 0;

        // Determine if we should highlight terminals (during rubberbanding)
        var highlightFill = isRubberbanding ? "rgba(59, 130, 246, 0.3)" : "rgba(255, 0, 0, 0.1)";
        var debugStroke = "rgba(255, 0, 0, 0.5)";

        // Render N input terminal hit areas
        int inputCount = Math.Max(1, node.InputTerminalCount);
        var inputPositions = GetTerminalPositions(inputCount, inputPos, node.Width, node.Height);
        for (int i = 0; i < inputPositions.Count; i++)
        {
            var (px, py) = inputPositions[i];
            var (stickX, stickY) = GetStickOutOffset(inDir, termStickOut);
            var termX = px + stickX;
            var termY = py + stickY;
            var terminalId = inputCount == 1 ? "input" : $"input:{i}";

            builder.OpenElement(seq++, "circle");
            builder.AddAttribute(seq++, "cx", termX);
            builder.AddAttribute(seq++, "cy", termY);
            builder.AddAttribute(seq++, "r", hitRadius);
            builder.AddAttribute(seq++, "fill", highlightFill);
            builder.AddAttribute(seq++, "stroke", debugStroke);
            builder.AddAttribute(seq++, "stroke-width", "1");
            builder.AddAttribute(seq++, "style", "cursor: crosshair;");
            builder.AddAttribute(seq++, "data-terminal-type", terminalId);
            builder.AddAttribute(seq++, "data-clickable", "true");
            builder.AddAttribute(seq++, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => HandleTerminalClick(nodeId, terminalId)));
            builder.AddEventStopPropagationAttribute(seq++, "onclick", true);
            builder.CloseElement();
        }

        // Render M output terminal hit areas
        int outputCount = Math.Max(1, node.OutputTerminalCount);
        var outputPositions = GetTerminalPositions(outputCount, outputPos, node.Width, node.Height);
        for (int i = 0; i < outputPositions.Count; i++)
        {
            var (px, py) = outputPositions[i];
            var (stickX, stickY) = GetStickOutOffset(outDir, termStickOut);
            var termX = px + stickX;
            var termY = py + stickY;
            var terminalId = outputCount == 1 ? "output" : $"output:{i}";

            builder.OpenElement(seq++, "circle");
            builder.AddAttribute(seq++, "cx", termX);
            builder.AddAttribute(seq++, "cy", termY);
            builder.AddAttribute(seq++, "r", hitRadius);
            builder.AddAttribute(seq++, "fill", highlightFill);
            builder.AddAttribute(seq++, "stroke", debugStroke);
            builder.AddAttribute(seq++, "stroke-width", "1");
            builder.AddAttribute(seq++, "style", "cursor: crosshair;");
            builder.AddAttribute(seq++, "data-terminal-type", terminalId);
            builder.AddAttribute(seq++, "data-clickable", "true");
            builder.AddAttribute(seq++, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => HandleTerminalClick(nodeId, terminalId)));
            builder.AddEventStopPropagationAttribute(seq++, "onclick", true);
            builder.CloseElement();
        }

        // Third terminal if enabled
        if (node.HasThirdTerminal)
        {
            var thirdX = node.T3X ?? 0.5;
            var thirdY = node.T3Y ?? 1.0;
            var thirdDir = TerminalLayouts.GetDirectionFromNormalizedPosition(thirdX, thirdY);
            var thirdPx = thirdX * node.Width;
            var thirdPy = thirdY * node.Height;
            var (thirdStickX, thirdStickY) = GetStickOutOffset(thirdDir, termStickOut);
            var thirdTermX = thirdPx + thirdStickX;
            var thirdTermY = thirdPy + thirdStickY;

            builder.OpenElement(seq++, "circle");
            builder.AddAttribute(seq++, "cx", thirdTermX);
            builder.AddAttribute(seq++, "cy", thirdTermY);
            builder.AddAttribute(seq++, "r", hitRadius);
            builder.AddAttribute(seq++, "fill", highlightFill);
            builder.AddAttribute(seq++, "stroke", debugStroke);
            builder.AddAttribute(seq++, "stroke-width", "1");
            builder.AddAttribute(seq++, "style", "cursor: crosshair;");
            builder.AddAttribute(seq++, "data-terminal-type", "third");
            builder.AddAttribute(seq++, "data-clickable", "true");
            builder.AddAttribute(seq++, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => HandleTerminalClick(nodeId, "third")));
            builder.AddEventStopPropagationAttribute(seq++, "onclick", true);
            builder.CloseElement();
        }

        // Extra terminals
        foreach (var extra in node.ExtraTerminals)
        {
            var (epx, epy, edir) = GetExtraTerminalPosition(node, extra);
            var (estickX, estickY) = GetStickOutOffset(edir, termStickOut);
            var extraTermX = epx + estickX;
            var extraTermY = epy + estickY;
            var extraId = extra.Id;

            builder.OpenElement(seq++, "circle");
            builder.AddAttribute(seq++, "cx", extraTermX);
            builder.AddAttribute(seq++, "cy", extraTermY);
            builder.AddAttribute(seq++, "r", hitRadius);
            builder.AddAttribute(seq++, "fill", highlightFill);
            builder.AddAttribute(seq++, "stroke", debugStroke);
            builder.AddAttribute(seq++, "stroke-width", "1");
            builder.AddAttribute(seq++, "style", "cursor: crosshair;");
            builder.AddAttribute(seq++, "data-terminal-type", $"extra:{extraId}");
            builder.AddAttribute(seq++, "data-clickable", "true");
            builder.AddAttribute(seq++, "onclick", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => HandleTerminalClick(nodeId, $"extra:{extraId}")));
            builder.AddEventStopPropagationAttribute(seq++, "onclick", true);
            builder.CloseElement();
        }
    };

    /// <summary>
    /// Get the position and direction for an extra terminal
    /// </summary>
    private (double px, double py, TerminalDirection dir) GetExtraTerminalPosition(Node node, ExtraTerminal terminal)
    {
        var w = node.Width;
        var h = node.Height;
        var offset = terminal.Position * 20; // 20px per position unit

        return terminal.Side.ToLower() switch
        {
            "left" => (0, h / 2 + offset, TerminalDirection.Left),
            "right" => (w, h / 2 + offset, TerminalDirection.Right),
            "top" => (w / 2 + offset, 0, TerminalDirection.Top),
            "bottom" => (w / 2 + offset, h, TerminalDirection.Bottom),
            _ => (0, h / 2 + offset, TerminalDirection.Left)
        };
    }

    private static (double x, double y) GetStickOutOffset(TerminalDirection dir, double stickOut)
    {
        return dir switch
        {
            TerminalDirection.Left => (-stickOut, 0),
            TerminalDirection.Right => (stickOut, 0),
            TerminalDirection.Top => (0, -stickOut),
            TerminalDirection.Bottom => (0, stickOut),
            _ => (stickOut, 0)
        };
    }

    // Guide state
    private bool showRowGuide = false;
    private bool showColumnGuide = false;
    private bool showClearConfirm = false;
    private bool showBackgroundConfig = false;

    // Optimization settings
    private bool showOptimizationSettings = false;
    private int annealingIterations = 5000;
    private double annealingCooling = 0.995;

    // Circuit layout interactive settings
    private bool showCircuitSettings = false;
    private double circuitGridSpacing = 40.0;
    private double circuitObstacleMargin = 12.0;
    private double circuitBendPenalty = 6.0;
    private double circuitViaPenalty = 30.0;
    private double circuitProximityPenalty = 10.0;
    private int circuitMaxGridSize = 300;
    private int circuitRowSpacing = 140;
    private int circuitColSpacing = 220;
    private int circuitStartX = 100;
    private int circuitStartY = 100;

    private void ApplyQuickPreset() { annealingIterations = 1000; annealingCooling = 0.99; }
    private void ApplyBalancedPreset() { annealingIterations = 5000; annealingCooling = 0.995; }
    private void ApplyThoroughPreset() { annealingIterations = 15000; annealingCooling = 0.998; }

    // Text edit dialog state
    private bool showTextEditDialog = false;
    private int? editingTextNodeId = null;
    private int? editingTextLabelId = null;
    private string editingText = "";

    // Selection state
    private bool isSelecting = false;
    private bool justFinishedAreaSelect = false;
    private (double X, double Y)? selectionStart = null;
    private (double X, double Y) currentMousePosition = (0, 0);

    // Toolbar select toggle (allows the Select button to be active without changing `mode`)
    private bool selectToolActive = false;

    // Print area selection
    private bool isPrintAreaSelection = false;
    private (double X, double Y, double Width, double Height)? printArea = null;

    // Edge style editing
    private int editStrokeWidth = 1;
    private string editStrokeColor = "#374151";
    private string editStrokeDashArray = "";
    private bool editIsDoubleLine = false;
    private double editDoubleLineSpacing = 3.0;
    private EdgeStyle editEdgeStyle = EdgeStyle.Direct;

    // Edge style defaults
    private int defaultStrokeWidth = 1;
    private string defaultStrokeColor = "#374151";
    private string defaultStrokeDashArray = "";
    private bool defaultIsDoubleLine = false;
    private double defaultDoubleLineSpacing = 3.0;
    private string defaultArrowStyle = "filled";
    private EdgeStyle defaultEdgeStyle = EdgeStyle.Direct;

    // Mouse tracking
    private double svgMouseX = 0;
    private double svgMouseY = 0;
    private double lastMouseX = 0;
    private double lastMouseY = 0;

    // Edge attribute variables
    private int strokeWidth = 1;
    private string selectedStrokeColor = "#374151";
    private string strokeDashArray = "";
    private bool isDoubleLine = false;
    private bool showEdgeStylePanel = false;
    private ArrowDirection editArrowDirection = ArrowDirection.End;

    // Project dependency editing (for edge style panel)
    private ProjectDependencyType editProjectDepType = ProjectDependencyType.FinishToStart;
    private int editProjectLagDays = 0;

    // Array placement state
    private bool arrayMode = false;
    private string arrayOrientation = "horizontal";
    private int arrayCount = 3;
    private int arraySpacing = 150;

    // Terminal mode - show terminals on all new shapes regardless of template
    private bool terminalModeEnabled = false;

    // Default terminal types for new nodes (Input terminal on left, Output terminal on right by default)
    private TerminalType defaultInputTerminalType = TerminalType.Input;
    private TerminalType defaultOutputTerminalType = TerminalType.Output;

    // Third terminal settings (for transistors, op-amps, etc.)
    private bool defaultHasThirdTerminal = false;
    private TerminalType defaultThirdTerminalType = TerminalType.Bidirectional;

    // Chain mode state
    private bool chainMode = false;
    private bool rearrangeMode = false;
    private int? lastChainedNodeId = null;

    // ============================================
    // CONNECTION MODE SYSTEM (replaces old multiConnect)
    // ============================================
    
    /// <summary>
    /// Connection modes for creating edges between nodes
    /// </summary>
    public enum ConnectionModeType
    {
        /// <summary>Normal: No connection mode active, standard selection behavior</summary>
        Normal,
        /// <summary>TwoClick: Click source, click target, edge created, done (default connect mode)</summary>
        TwoClick,
        /// <summary>Chain: Click nodes in sequence to create a chain of connections</summary>
        Chain,
        /// <summary>1:N Click: Click source once, then click each target to connect</summary>
        OneToN,
        /// <summary>1:N Area: Click source once, then area-select targets</summary>
        OneToNArea,
        /// <summary>Rearrange: Drag nodes freely without creating connections</summary>
        Rearrange
    }

    /// <summary>
    /// Selection modes for the Select dropdown
    /// </summary>
    public enum SelectModeType
    {
        /// <summary>Click to select individual items</summary>
        Single,
        /// <summary>Drag to select multiple items in an area</summary>
        Area
    }

    private ConnectionModeType connectionMode = ConnectionModeType.Normal;
    private SelectModeType selectMode = SelectModeType.Single;

    // Two-click mode state (default connect mode)
    private int? twoClickSourceNode = null;

    // 1:N mode state
    private int? oneToNSourceNode = null;
    private ConnectionPoint? oneToNSourcePoint = null;
    private (double X, double Y)? oneToNSourceCoords = null;

    // 1:N Area mode state
    private bool isOneToNAreaSelecting = false;
    private (double X, double Y)? oneToNAreaStart = null;

    // Legacy multi-connect (keep for compatibility, but redirect to new system)
    private bool multiConnectMode 
    { 
        get => connectionMode != ConnectionModeType.Normal;
        set 
        {
            if (!value) connectionMode = ConnectionModeType.Normal;
            // If setting to true, default to OneToN
            else if (connectionMode == ConnectionModeType.Normal) connectionMode = ConnectionModeType.OneToN;
        }
    }
    private int? multiConnectSourceNode => oneToNSourceNode;
    private ConnectionPoint? multiConnectSourcePoint => oneToNSourcePoint;
    private (double X, double Y)? multiConnectSourceCoords => oneToNSourceCoords;

    // Helper to get current select mode label
    private string GetSelectModeLabel()
    {
        if (selectMode == SelectModeType.Area) return "(Area)";
        return "";
    }

    // Helper to get current connect mode label
    private string GetConnectModeLabel()
    {
        if (connectionMode == ConnectionModeType.TwoClick) return "";
        if (connectionMode == ConnectionModeType.Chain) return "(Chain)";
        if (connectionMode == ConnectionModeType.OneToN) return "(1:N)";
        if (connectionMode == ConnectionModeType.OneToNArea) return "(1:N▢)";
        return "";
    }

    // Check if any connect mode is active
    private bool IsConnectModeActive()
    {
        return connectionMode == ConnectionModeType.TwoClick ||
               connectionMode == ConnectionModeType.Chain ||
               connectionMode == ConnectionModeType.OneToN ||
               connectionMode == ConnectionModeType.OneToNArea;
    }

    // Clear all connect modes and return to normal select
    private void ClearConnectMode()
    {
        chainMode = false;
        connectionMode = ConnectionModeType.Normal;
        lastChainedNodeId = null;
        twoClickSourceNode = null;
        ClearOneToNState();
    }

    // Activate the default two-click connect mode
    private void ActivateTwoClickMode()
    {
        connectionMode = ConnectionModeType.TwoClick;
        chainMode = false;
        lastChainedNodeId = null;
        twoClickSourceNode = null;
        ClearOneToNState();
    }

    // Activate chain connect mode
    private void ActivateChainMode()
    {
        connectionMode = ConnectionModeType.Chain;
        chainMode = true;
        lastChainedNodeId = null;
        twoClickSourceNode = null;
        ClearOneToNState();
    }

    // Select all nodes
    private void SelectAllNodes()
    {
        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();
        foreach (var node in nodes)
        {
            selectedNodes.Add(node.Id);
        }
        StateHasChanged();
    }

    // Select all edges
    private void SelectAllEdges()
    {
        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();
        foreach (var edge in edges)
        {
            selectedEdges.Add(edge.Id);
        }
        StateHasChanged();
    }
    // Helper to get connection mode button class
    private string GetConnectionModeClass(ConnectionModeType modeType)
    {
        return connectionMode == modeType ? "active" : "";
    }

    // Set connection mode
private void SetConnectionMode(ConnectionModeType newMode)
{
    // If clicking the same mode, toggle off
    if (connectionMode == newMode)
    {
        connectionMode = ConnectionModeType.Normal;
    }
    else
    {
        connectionMode = newMode;
    }
    
    // Clear any existing connection state when changing modes
    ClearOneToNState();
    
    // Only clear chain mode when switching to a non-Normal connection mode
    if (newMode != ConnectionModeType.Normal)
    {
        chainMode = false;
        lastChainedNodeId = null;
    }
    
    StateHasChanged();
}
    // Helper to clear 1:N state
    private void ClearOneToNState()
    {
        oneToNSourceNode = null;
        oneToNSourcePoint = null;
        oneToNSourceCoords = null;
        isOneToNAreaSelecting = false;
        oneToNAreaStart = null;
    }

    // Legacy helper - redirects to new system
    private void ClearMultiConnectState()
    {
        ClearOneToNState();
    }


    private readonly GraphLayoutService _layoutService = new();
    private readonly EdgeRoutingService _routingService = new();


    // Check if we have an active 1:N source
    private bool HasOneToNSource => oneToNSourceNode.HasValue;

    // Preset colors
    private readonly List<string> presetColors = new()
    {
        "#374151", "#ef4444", "#3b82f6", "#10b981",
        "#f59e0b", "#8b5cf6", "#ec4899", "#000000"
    };

    // Node fill color presets (lighter colors work better as fills)
    private readonly string[] nodeFillColors = new[]
    {
        "#ffffff", "#f3f4f6", "#fef3c7", "#d1fae5", "#dbeafe", 
        "#ede9fe", "#fce7f3", "#fee2e2", "#e0f2fe", "#f0fdf4"
    };

    // Line style options
    private readonly Dictionary<string, string> lineStyles = new()
    {
        { "", "Solid" },
        { "5,5", "Dashed" },
        { "2,2", "Dotted" },
        { "10,5,2,5", "Dash-Dot" }
    };

    // Canvas configuration
    private string canvasBackground = "grid";
    private double canvasWidth = 4000;
    private double canvasHeight = 4000;

    // Viewport tracking for minimap
    private double scrollX = 0;
    private double scrollY = 0;
    private double viewportWidth = 800;
    private double viewportHeight = 600;

    // Minimap cache - avoids re-scanning nodes on every scroll/render
    private List<Node>? _minimapGanttMachines;
    private List<Node>? _minimapGanttTasks;
    private List<Node>? _minimapProjectNodes;
    private double _minimapGanttWidth;
    private double _minimapGanttHeight;
    private double _minimapProjectWidth;
    private double _minimapProjectHeight;
    private bool _minimapDirty = true;
    private int _minimapNodeCount = -1;
    private int _minimapEdgeCount = -1;

    /// <summary>Mark minimap cache as stale. Call when nodes/edges/layers change.</summary>
    private void InvalidateMinimapCache() => _minimapDirty = true;

    /// <summary>Rebuild minimap cached data only when the data has actually changed.</summary>
    private void EnsureMinimapCache()
    {
        // Auto-detect staleness: if node/edge count changed, data is definitely stale
        if (nodes.Count != _minimapNodeCount || edges.Count != _minimapEdgeCount)
        {
            _minimapDirty = true;
            _minimapNodeCount = nodes.Count;
            _minimapEdgeCount = edges.Count;
        }

        if (!_minimapDirty) return;
        _minimapDirty = false;

        if (isGanttMode && ganttTimeline != null && ganttTimelineView)
        {
            _minimapGanttMachines = GetGanttMachineNodes().ToList();
            _minimapGanttWidth = ganttTimeline.GetTimelineWidth();
            _minimapGanttHeight = ganttTimeline.GetTimelineHeight(_minimapGanttMachines.Count);

            var visibleLayers = layerService.GetVisibleLayers().ToList();
            var allTasks = GetGanttTaskNodes().ToList();
            if (isBaseLayerVisible)
            {
                _minimapGanttTasks = visibleLayers.Count > 0
                    ? allTasks.Select(t => layerService.GetEffectiveNode(t, visibleLayers)).ToList()
                    : allTasks;
            }
            else if (visibleLayers.Count > 0)
            {
                var overriddenIds = visibleLayers.SelectMany(l => l.NodeOverrides.Keys).ToHashSet();
                _minimapGanttTasks = allTasks
                    .Where(t => overriddenIds.Contains(t.Id))
                    .Select(t => layerService.GetEffectiveNode(t, visibleLayers)).ToList();
            }
            else
            {
                _minimapGanttTasks = new List<Node>();
            }
        }

        if (isProjectMode && projectTimeline != null)
        {
            _minimapProjectNodes = GetprojectNodes().ToList();
            _minimapProjectWidth = GetProjectTotalWidth();
            _minimapProjectHeight = GetProjectTotalHeight();
        }
    }

    // Scroll throttle for minimap - only re-render at most every N ms during scroll
    private System.Timers.Timer? _scrollThrottleTimer;
    private bool _scrollPending = false;
    private const int ScrollThrottleMs = 60;

    // Zoom
    private double zoomLevel = 1.0;
    private readonly double[] zoomLevels = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0 };

    // Swimlane/column configuration
    private int swimlaneCount = 3;
    private int columnCount = 4;
    private List<string> swimlaneLabels = new() { "Lane 1", "Lane 2", "Lane 3" };
    private List<string> columnLabels = new() { "Column 1", "Column 2", "Column 3", "Column 4" };

    // ============================================
    // Project chart SYSTEM (Node-Based)
    // ============================================

    /// <summary>Whether Project chart view is currently active</summary>
    private bool isProjectMode = false;

    /// <summary>Flag to initialize Project view after first render (set when defaultMode is "project")</summary>
    private bool _pendingProjectInitialization = false;

    /// <summary>Project view mode: "timeline" (bars on timeline) or "node" (compact nodes for connections)</summary>
    private string projectViewMode = "timeline";

    /// <summary>Timeline service for date-to-pixel conversions</summary>
    private ProjectTimelineService projectTimeline = new();

    /// <summary>Theme service for DFD and Project color palettes</summary>
    private DfdThemeService dfdTheme = new();

    // Project deadline marker state
    private NodaTime.LocalDate? projectDeadlineDate = null;
    private bool isDraggingProjectDeadline = false;
    private double projectDeadlineDragStartX;

    // Project compression mode: "earliest" moves tasks left, "latest" moves tasks right (toward deadline)
    public enum ProjectCompressionMode { Earliest, Latest }
    private ProjectCompressionMode projectCompressionMode = ProjectCompressionMode.Earliest;

    /// <summary>Solution layer service for managing solver result layers</summary>
    private SolutionLayerService layerService = new();

    // ========================================
    // LAYER MANAGEMENT HELPERS
    // ========================================

    /// <summary>Create a new manual layer</summary>
    private void CreateNewLayer()
    {
        var templateMode = selectedTemplateId ?? "dfd";
        var layerCount = layerService.GetLayers().Count + 1;
        var layer = layerService.CreateLayer($"Layer {layerCount}", "Manual", templateMode);
        layerService.SetActiveLayer(layer.Id);
        StateHasChanged();
    }

    /// <summary>Start renaming a layer</summary>
    private void StartLayerRename(string layerId, string currentName)
    {
        editingLayerName = layerId;
        editingLayerNameValue = currentName;
        StateHasChanged();
    }

    /// <summary>Finish renaming a layer</summary>
    private void FinishLayerRename(string layerId, string newName)
    {
        if (!string.IsNullOrWhiteSpace(newName))
        {
            layerService.RenameLayer(layerId, newName.Trim());
        }
        editingLayerName = null;
        StateHasChanged();
    }

    /// <summary>Create layer from Gantt solver results</summary>
    private void CreateGanttSolutionLayer(string name, string solverType, IEnumerable<Node> scheduledNodes)
    {
        var ganttTasks = nodes.Where(n => n.IsGanttTask).ToList();
        var layer = layerService.CreateLayerFromGanttSchedule(name, solverType, ganttTasks, scheduledNodes);

        // Calculate and store metrics for this layer
        var metrics = CalculateGanttMetrics(scheduledNodes);
        layerService.UpdateLayerMetrics(layer.Id, metrics);

        StateHasChanged();
    }

    /// <summary>Calculate Gantt metrics for a set of scheduled nodes</summary>
    private Dictionary<string, decimal> CalculateGanttMetrics(IEnumerable<Node> scheduledNodes)
    {
        var metrics = new Dictionary<string, decimal>();
        var tasks = scheduledNodes.Where(n => n.IsGanttTask && n.GanttStartTime.HasValue && n.GanttDuration.HasValue).ToList();

        if (tasks.Count == 0) return metrics;

        // Makespan (max end time)
        var maxEndTime = tasks.Max(t => t.GanttStartTime!.Value + t.GanttDuration!.Value);
        metrics["Makespan"] = (decimal)maxEndTime.TotalMinutes;

        // Total processing time
        var totalProcessing = tasks.Sum(t => t.GanttDuration!.Value.TotalMinutes);
        metrics["TotalProcessing"] = (decimal)totalProcessing;

        // Average machine utilization
        var machines = GetGanttMachineNodes().ToList();
        if (machines.Count > 0 && maxEndTime.TotalMinutes > 0)
        {
            var utilization = (totalProcessing / (machines.Count * maxEndTime.TotalMinutes)) * 100;
            metrics["AvgUtilization"] = (decimal)utilization;
        }

        return metrics;
    }

    /// <summary>
    /// Apply a theme and update all node colors accordingly
    /// </summary>
    private void ApplyTheme(string themeId)
    {
        dfdTheme.SetTheme(themeId);

        // Update all nodes with theme colors based on their template/shape
        foreach (var node in nodes)
        {
            // Only update nodes that don't have custom colors set
            // (i.e., nodes using default colors)
            if (node.FillColor == null || IsDefaultThemeColor(node.FillColor))
            {
                var (fill, stroke, text) = dfdTheme.GetColorsForShape(node.TemplateId, node.TemplateShapeId);
                node.FillColor = fill;
                node.StrokeColor = stroke;
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// Check if a color is a default theme color (from any theme)
    /// </summary>
    private bool IsDefaultThemeColor(string? color)
    {
        if (string.IsNullOrEmpty(color)) return true;

        // List of colors used as defaults in any theme
        var defaultColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Blues
            "#dbeafe", "#3b82f6", "#1d4ed8", "#1e40af", "#60a5fa",
            // Purples
            "#f3e8ff", "#8b5cf6", "#6d28d9", "#5b21b6", "#d0bfff", "#b197fc",
            // Ambers/Yellows
            "#fef3c7", "#f59e0b", "#92400e", "#ffd700", "#daa520",
            // Greens
            "#d8f3dc", "#2d6a4f", "#1b4332", "#74c69d", "#40916c", "#52b788", "#95d5b2", "#b7e4c7",
            // Teals/Cyans
            "#caf0f8", "#0077b6", "#023e8a", "#00b4d8", "#0096c7", "#48cae4", "#90e0ef", "#ade8f4",
            // Pinks
            "#fae0e4", "#f72585", "#b5179e", "#f9bec7", "#c77dff", "#e0aaff",
            // Reds
            "#fee2e2", "#ef4444", "#dc2626", "#dd0000", "#aa0000",
            // Grays
            "#ffffff", "#f8f9fa", "#e9ecef", "#dee2e6", "#ced4da", "#adb5bd", "#6c757d", "#495057", "#343a40", "#212529",
            // Blacks
            "#1a1a1a", "#000000", "#1f2937", "#374151"
        };

        return defaultColors.Contains(color);
    }

    #region Theme Configurator Methods

    /// <summary>
    /// Initialize editing theme data from current or specified theme
    /// </summary>
    private void InitEditingTheme(string? themeId = null)
    {
        var sourceId = themeId ?? dfdTheme.CurrentThemeId;
        editingThemeId = sourceId;

        // Find the theme
        DfdTheme? source = null;
        if (dfdTheme.Themes.TryGetValue(sourceId, out var t))
            source = t;
        else if (dfdTheme.CustomThemes.TryGetValue(sourceId, out var ct))
            source = ct;

        source ??= dfdTheme.CurrentTheme;

        editingThemeData = new EditableThemeData
        {
            Name = source.Name,
            Description = source.Description,
            ProcessFill = source.ProcessFill,
            ProcessStroke = source.ProcessStroke,
            DecisionFill = source.DecisionFill,
            DecisionStroke = source.DecisionStroke,
            ExternalEntityFill = source.ExternalEntityFill,
            ExternalEntityStroke = source.ExternalEntityStroke,
            DataStoreFill = source.DataStoreFill,
            DataStoreStroke = source.DataStoreStroke,
            NodeFill = source.NodeFill,
            NodeStroke = source.NodeStroke,
            EdgeStroke = source.EdgeStroke,
            EdgeArrow = source.EdgeArrow,
            CanvasBackground = source.CanvasBackground,
            CanvasGrid = source.CanvasGrid,
            TaskFill = source.TaskFill,
            TaskStroke = source.TaskStroke,
            MilestoneFill = source.MilestoneFill,
            CriticalFill = source.CriticalFill,
            GroupFill = source.GroupFill,
            SelectionStroke = source.SelectionStroke,
            WeekendFill = source.WeekendFill,
            SaturdayFill = source.SaturdayFill,
            SundayFill = source.SundayFill,
            HolidayFill = source.HolidayFill,
            ChristmasFill = source.ChristmasFill,
            NewYearFill = source.NewYearFill,
            CompanyEventFill = source.CompanyEventFill,
            VacationFill = source.VacationFill,
            RowColor1 = source.RowColor1,
            RowColor2 = source.RowColor2,
            RowColor3 = source.RowColor3,
            RowColor4 = source.RowColor4,
            RowColor5 = source.RowColor5,
            RowColor6 = source.RowColor6,
            RowColor7 = source.RowColor7,
            RowColor8 = source.RowColor8,
            RowColor9 = source.RowColor9,
            RowColor10 = source.RowColor10,
            GroupRowColor = source.GroupRowColor
        };

        themeConfigTab = "edit";
    }

    /// <summary>
    /// Create a new blank theme for editing
    /// </summary>
    private void CreateNewTheme()
    {
        editingThemeId = null;
        editingThemeData = new EditableThemeData
        {
            Name = "New Theme",
            Description = "Custom theme",
            ProcessFill = "#dbeafe",
            ProcessStroke = "#3b82f6",
            DecisionFill = "#fef3c7",
            DecisionStroke = "#f59e0b",
            ExternalEntityFill = "#f3e8ff",
            ExternalEntityStroke = "#8b5cf6",
            DataStoreFill = "#fef3c7",
            DataStoreStroke = "#f59e0b",
            NodeFill = "#ffffff",
            NodeStroke = "#374151",
            EdgeStroke = "#374151",
            EdgeArrow = "#374151",
            CanvasBackground = "#ffffff",
            CanvasGrid = "#e5e7eb",
            TaskFill = "#3b82f6",
            TaskStroke = "#1d4ed8",
            MilestoneFill = "#8b5cf6",
            CriticalFill = "#fee2e2",
            GroupFill = "#1f2937",
            SelectionStroke = "#3b82f6",
            WeekendFill = "#f1f5f9",
            SaturdayFill = "#f1f5f9",
            SundayFill = "#fee2e2",
            HolidayFill = "#fef3c7",
            ChristmasFill = "#dcfce7",
            NewYearFill = "#fae8ff",
            CompanyEventFill = "#dbeafe",
            VacationFill = "#e0f2fe",
            RowColor1 = "#ffffff",
            RowColor2 = "#e0f2fe",
            RowColor3 = "#ffffff",
            RowColor4 = "#dbeafe",
            RowColor5 = "#ffffff",
            RowColor6 = "#e0e7ff",
            RowColor7 = "#ffffff",
            RowColor8 = "#dbeafe",
            RowColor9 = "#ffffff",
            RowColor10 = "#e0f2fe",
            GroupRowColor = "#1e40af"
        };
        themeConfigTab = "edit";
    }

    /// <summary>
    /// Apply the same color to all row colors
    /// </summary>
    private void ApplyAllSameRowColor()
    {
        if (editingThemeData == null) return;
        editingThemeData.RowColor1 = rowColorAllSame;
        editingThemeData.RowColor2 = rowColorAllSame;
        editingThemeData.RowColor3 = rowColorAllSame;
        editingThemeData.RowColor4 = rowColorAllSame;
        editingThemeData.RowColor5 = rowColorAllSame;
        editingThemeData.RowColor6 = rowColorAllSame;
        editingThemeData.RowColor7 = rowColorAllSame;
        editingThemeData.RowColor8 = rowColorAllSame;
        editingThemeData.RowColor9 = rowColorAllSame;
        editingThemeData.RowColor10 = rowColorAllSame;
    }

    /// <summary>
    /// Apply a gradient from one color to another across all row colors
    /// </summary>
    private void ApplyGradientRowColors()
    {
        if (editingThemeData == null) return;

        var fromColor = ParseHexColor(rowColorGradientFrom);
        var toColor = ParseHexColor(rowColorGradientTo);

        for (int i = 0; i < 10; i++)
        {
            float t = i / 9f; // 0 to 1 over 10 rows
            int r = (int)(fromColor.r + (toColor.r - fromColor.r) * t);
            int g = (int)(fromColor.g + (toColor.g - fromColor.g) * t);
            int b = (int)(fromColor.b + (toColor.b - fromColor.b) * t);
            var color = $"#{r:X2}{g:X2}{b:X2}";

            switch (i)
            {
                case 0: editingThemeData.RowColor1 = color; break;
                case 1: editingThemeData.RowColor2 = color; break;
                case 2: editingThemeData.RowColor3 = color; break;
                case 3: editingThemeData.RowColor4 = color; break;
                case 4: editingThemeData.RowColor5 = color; break;
                case 5: editingThemeData.RowColor6 = color; break;
                case 6: editingThemeData.RowColor7 = color; break;
                case 7: editingThemeData.RowColor8 = color; break;
                case 8: editingThemeData.RowColor9 = color; break;
                case 9: editingThemeData.RowColor10 = color; break;
            }
        }
    }

    /// <summary>
    /// Parse a hex color string to RGB components
    /// </summary>
    private (int r, int g, int b) ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return (255, 255, 255);
        return (
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16)
        );
    }

    /// <summary>
    /// Save the currently editing theme as a custom theme
    /// </summary>
    private void SaveEditingTheme()
    {
        if (editingThemeData == null) return;

        // Generate a unique ID for the theme
        var themeId = editingThemeId ?? $"custom_{DateTime.Now.Ticks}";

        // Create a DfdTheme from the editing data
        var theme = new DfdTheme
        {
            Name = editingThemeData.Name ?? "Custom Theme",
            Description = editingThemeData.Description ?? "",
            ProcessFill = editingThemeData.ProcessFill ?? "#dbeafe",
            ProcessStroke = editingThemeData.ProcessStroke ?? "#3b82f6",
            ProcessText = "#1e40af",
            DecisionFill = editingThemeData.DecisionFill ?? "#fef3c7",
            DecisionStroke = editingThemeData.DecisionStroke ?? "#f59e0b",
            DecisionText = "#92400e",
            ExternalEntityFill = editingThemeData.ExternalEntityFill ?? "#f3e8ff",
            ExternalEntityStroke = editingThemeData.ExternalEntityStroke ?? "#8b5cf6",
            ExternalEntityText = "#5b21b6",
            DataStoreFill = editingThemeData.DataStoreFill ?? "#fef3c7",
            DataStoreStroke = editingThemeData.DataStoreStroke ?? "#f59e0b",
            DataStoreText = "#92400e",
            NodeFill = editingThemeData.NodeFill ?? "#ffffff",
            NodeStroke = editingThemeData.NodeStroke ?? "#374151",
            NodeText = "#1f2937",
            EdgeStroke = editingThemeData.EdgeStroke ?? "#374151",
            EdgeArrow = editingThemeData.EdgeArrow ?? "#374151",
            EdgeText = "#4b5563",
            CanvasBackground = editingThemeData.CanvasBackground ?? "#ffffff",
            CanvasGrid = editingThemeData.CanvasGrid ?? "#e5e7eb",
            TaskFill = editingThemeData.TaskFill ?? "#3b82f6",
            TaskStroke = editingThemeData.TaskStroke ?? "#1d4ed8",
            TaskText = "#ffffff",
            MilestoneFill = editingThemeData.MilestoneFill ?? "#8b5cf6",
            MilestoneStroke = "#6d28d9",
            CriticalFill = editingThemeData.CriticalFill ?? "#fee2e2",
            CriticalStroke = "#ef4444",
            CriticalText = "#991b1b",
            GroupFill = editingThemeData.GroupFill ?? "#1f2937",
            GroupStroke = "#f59e0b",
            GroupText = "#ffffff",
            SelectionStroke = editingThemeData.SelectionStroke ?? "#3b82f6",
            SelectionFill = "rgba(59, 130, 246, 0.1)",
            HeaderFill = "#e2e8f0",
            HeaderStroke = "#cbd5e1",
            HeaderText = "#374151",
            GridLine = "#e2e8f0",
            WeekendFill = editingThemeData.WeekendFill ?? "#f1f5f9",
            SaturdayFill = editingThemeData.SaturdayFill ?? "#f1f5f9",
            SundayFill = editingThemeData.SundayFill ?? "#fee2e2",
            HolidayFill = editingThemeData.HolidayFill ?? "#fef3c7",
            ChristmasFill = editingThemeData.ChristmasFill ?? "#dcfce7",
            NewYearFill = editingThemeData.NewYearFill ?? "#fae8ff",
            CompanyEventFill = editingThemeData.CompanyEventFill ?? "#dbeafe",
            VacationFill = editingThemeData.VacationFill ?? "#e0f2fe",
            DependencyStroke = "#475569",
            DependencyArrow = "#374151",
            TodayLine = "#ef4444",
            TaskNameText = "#1e40af",
            DateText = "#64748b",
            WeekendText = "#94a3b8",
            RowColor1 = editingThemeData.RowColor1 ?? "#ffffff",
            RowColor2 = editingThemeData.RowColor2 ?? "#e0f2fe",
            RowColor3 = editingThemeData.RowColor3 ?? "#ffffff",
            RowColor4 = editingThemeData.RowColor4 ?? "#dbeafe",
            RowColor5 = editingThemeData.RowColor5 ?? "#ffffff",
            RowColor6 = editingThemeData.RowColor6 ?? "#e0e7ff",
            RowColor7 = editingThemeData.RowColor7 ?? "#ffffff",
            RowColor8 = editingThemeData.RowColor8 ?? "#dbeafe",
            RowColor9 = editingThemeData.RowColor9 ?? "#ffffff",
            RowColor10 = editingThemeData.RowColor10 ?? "#e0f2fe",
            GroupRowColor = editingThemeData.GroupRowColor ?? "#1e40af"
        };

        dfdTheme.AddCustomTheme(themeId, theme);
        editingThemeId = themeId;

        // Apply the new theme
        ApplyTheme(themeId);

        themeConfigTab = "select";
        StateHasChanged();
    }

    /// <summary>
    /// Preview the currently editing theme without saving
    /// </summary>
    private void PreviewEditingTheme()
    {
        if (editingThemeData == null) return;

        // Temporarily save and apply to preview
        var tempId = $"preview_{DateTime.Now.Ticks}";
        var theme = new DfdTheme
        {
            Name = editingThemeData.Name ?? "Preview",
            Description = "Preview theme",
            ProcessFill = editingThemeData.ProcessFill ?? "#dbeafe",
            ProcessStroke = editingThemeData.ProcessStroke ?? "#3b82f6",
            ProcessText = "#1e40af",
            DecisionFill = editingThemeData.DecisionFill ?? "#fef3c7",
            DecisionStroke = editingThemeData.DecisionStroke ?? "#f59e0b",
            DecisionText = "#92400e",
            ExternalEntityFill = editingThemeData.ExternalEntityFill ?? "#f3e8ff",
            ExternalEntityStroke = editingThemeData.ExternalEntityStroke ?? "#8b5cf6",
            ExternalEntityText = "#5b21b6",
            DataStoreFill = editingThemeData.DataStoreFill ?? "#fef3c7",
            DataStoreStroke = editingThemeData.DataStoreStroke ?? "#f59e0b",
            DataStoreText = "#92400e",
            NodeFill = editingThemeData.NodeFill ?? "#ffffff",
            NodeStroke = editingThemeData.NodeStroke ?? "#374151",
            NodeText = "#1f2937",
            EdgeStroke = editingThemeData.EdgeStroke ?? "#374151",
            EdgeArrow = editingThemeData.EdgeArrow ?? "#374151",
            EdgeText = "#4b5563",
            CanvasBackground = editingThemeData.CanvasBackground ?? "#ffffff",
            CanvasGrid = editingThemeData.CanvasGrid ?? "#e5e7eb",
            TaskFill = editingThemeData.TaskFill ?? "#3b82f6",
            TaskStroke = editingThemeData.TaskStroke ?? "#1d4ed8",
            TaskText = "#ffffff",
            MilestoneFill = editingThemeData.MilestoneFill ?? "#8b5cf6",
            MilestoneStroke = "#6d28d9",
            CriticalFill = editingThemeData.CriticalFill ?? "#fee2e2",
            CriticalStroke = "#ef4444",
            CriticalText = "#991b1b",
            GroupFill = editingThemeData.GroupFill ?? "#1f2937",
            GroupStroke = "#f59e0b",
            GroupText = "#ffffff",
            SelectionStroke = editingThemeData.SelectionStroke ?? "#3b82f6",
            SelectionFill = "rgba(59, 130, 246, 0.1)",
            WeekendFill = editingThemeData.WeekendFill ?? "#f1f5f9",
            SaturdayFill = editingThemeData.SaturdayFill ?? "#f1f5f9",
            SundayFill = editingThemeData.SundayFill ?? "#fee2e2",
            HolidayFill = editingThemeData.HolidayFill ?? "#fef3c7",
            ChristmasFill = editingThemeData.ChristmasFill ?? "#dcfce7",
            NewYearFill = editingThemeData.NewYearFill ?? "#fae8ff",
            CompanyEventFill = editingThemeData.CompanyEventFill ?? "#dbeafe",
            VacationFill = editingThemeData.VacationFill ?? "#e0f2fe"
        };

        dfdTheme.AddCustomTheme(tempId, theme);
        ApplyTheme(tempId);

        // Clean up the preview theme (leave it for now, it'll be replaced)
        StateHasChanged();
    }

    /// <summary>
    /// Copy current theme to clipboard as JSON
    /// </summary>
    private async Task CopyThemeToClipboard()
    {
        var json = dfdTheme.ExportThemeAsJson();
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", json);
    }

    /// <summary>
    /// Import a theme from JSON string
    /// </summary>
    private void ImportThemeFromJson()
    {
        if (string.IsNullOrWhiteSpace(themeImportJson))
        {
            themeImportError = "Please paste theme JSON to import";
            return;
        }

        var theme = dfdTheme.ImportThemeFromJson(themeImportJson);
        if (theme != null)
        {
            var themeId = $"imported_{DateTime.Now.Ticks}";
            dfdTheme.AddCustomTheme(themeId, theme);
            ApplyTheme(themeId);

            themeImportJson = "";
            themeImportError = "";
            themeConfigTab = "select";
            StateHasChanged();
        }
        else
        {
            themeImportError = "Invalid theme JSON. Please check the format.";
        }
    }

    /// <summary>
    /// Delete a custom theme
    /// </summary>
    private void DeleteCustomTheme(string themeId)
    {
        if (dfdTheme.CustomThemes.ContainsKey(themeId))
        {
            dfdTheme.RemoveCustomTheme(themeId);
            StateHasChanged();
        }
    }

    #endregion

    /// <summary>Calendar for working day calculations</summary>
    private ProjectCalendar ProjectCalendar = new();

    /// <summary>CPM calculation results for the current project (node ID -> result)</summary>
    private Dictionary<int, CpmTaskResult>? projectCpmResults = null;

    /// <summary>Currently selected Project task node ID</summary>
    private int? selectedProjectTaskId = null;

    /// <summary>Node being dragged (for move/resize)</summary>
    private int? draggingProjectTaskId = null;

    /// <summary>Whether we're resizing (true) or moving (false) the task</summary>
    private bool isResizingProjectTask = false;

    /// <summary>Starting X position when dragging began</summary>
    private double projectDragStartX = 0;

    /// <summary>Original task start date when dragging began</summary>
    private DateTime? projectDragOriginalStart = null;

    /// <summary>Original task duration when dragging began</summary>
    private int? projectDragOriginalDuration = null;

    /// <summary>Whether we're in dependency creation mode</summary>
    private bool isCreatingProjectDependency = false;

    /// <summary>Source task for dependency creation</summary>
    private int? ProjectDependencySourceTaskId = null;

    /// <summary>Show task edit dialog</summary>
    private bool showProjectTaskDialog = false;

    /// <summary>Node being edited in dialog</summary>
    private Node? editingProjectTask = null;

    /// <summary>Show node properties dialog (for qmaker and other templates)</summary>
    private bool showNodePropertiesDialog = false;

    /// <summary>Node being edited in properties dialog</summary>
    private Node? propertiesDialogNode = null;

    /// <summary>Collapse state for Project panel sections</summary>
    private bool collapseProjectTasks = false;
    private bool collapseprojectTimeline = false;

    /// <summary>Show the Project import dialog</summary>
    private bool showProjectImportDialog = false;

    /// <summary>Show the Project solver dialog</summary>
    private bool showProjectSolverDialog = false;

    /// <summary>Solver configuration (goals and costs)</summary>
    private SolverConfiguration solverConfig = new();

    /// <summary>Result from the last solver run</summary>
    private SolverResult? solverResult = null;

    /// <summary>Whether solver is currently running</summary>
    private bool solverRunning = false;

    /// <summary>Text content for import</summary>
    private string projectImportText = "";

    /// <summary>Error message for Project import</summary>
    private string projectImportError = "";

    // ============================================
    // GANTT IMPORT/EXPORT STATE
    // ============================================

    /// <summary>Show the Gantt import dialog</summary>
    private bool showGanttImportDialog = false;

    /// <summary>Text content for Gantt import</summary>
    private string ganttImportText = "";

    /// <summary>Error message for Gantt import</summary>
    private string ganttImportError = "";

    /// <summary>Service for Gantt import/export operations</summary>
    private GanttImportExportService ganttImportExport = new();

    // ============================================
    // RESOURCE ASSIGNMENT DRAG STATE
    // ============================================

    /// <summary>Resource node being dragged for assignment</summary>
    private int? draggingResourceId = null;

    /// <summary>Task node that is current drop target during resource drag</summary>
    private int? resourceDragTargetTaskId = null;

    /// <summary>Show the resource assignment dialog</summary>
    private bool showResourceAssignmentDialog = false;

    /// <summary>Resource being assigned in the dialog</summary>
    private Node? assigningResource = null;

    /// <summary>Task being assigned to in the dialog</summary>
    private Node? assigningToTask = null;

    /// <summary>Resource assignment quantity (e.g., 2 people)</summary>
    private int resourceAssignmentQuantity = 1;

    /// <summary>Resource assignment percentage (e.g., 50% of time)</summary>
    private int resourceAssignmentPercentage = 100;

    /// <summary>Name for new resource being created</summary>
    private string newResourceName = "";

    // ============================================
    // RESOURCE EDIT DIALOG STATE
    // ============================================

    /// <summary>Show the resource edit dialog</summary>
    private bool showProjectResourceDialog = false;

    /// <summary>Resource being edited in dialog</summary>
    private Node? editingProjectResource = null;

    /// <summary>Edit fields for resource dialog</summary>
    private string projectEditResourceName = "";
    private ProjectResourceType projectEditResourceType = ProjectResourceType.Person;
    private int projectEditResourceCapacity = 100;
    private decimal projectEditResourceRate = 100m;
    private string projectEditResourceEmail = "";

    /// <summary>Get all Project task nodes (nodes with TemplateId == "project")</summary>
    private IEnumerable<Node> GetprojectNodes() =>
        nodes.Where(n => n.TemplateId == "project" && !n.IsProjectResource);

    /// <summary>Get Project nodes visible in Node View mode (filters out grouped children when collapsed)</summary>
    private IEnumerable<Node> GetprojectNodesForNodeView()
    {
        return nodes.Where(n =>
        {
            if (n.TemplateId != "project" || n.IsProjectResource) return false;

            // SuperNodes (groups) are always visible
            if (n.IsSuperNode) return true;

            // If node has no parent group, it's visible
            if (!n.ParentSuperNodeId.HasValue) return true;

            // If node is in a group, check if the group is collapsed
            var parentGroup = nodes.FirstOrDefault(p => p.Id == n.ParentSuperNodeId.Value);
            if (parentGroup == null) return true;

            // Show children only if group is expanded (not collapsed)
            return !parentGroup.IsCollapsed;
        });
    }

    /// <summary>Get all Project resource nodes</summary>
    private IEnumerable<Node> GetProjectResourceNodes() =>
        nodes.Where(n => n.TemplateId == "project" && n.IsProjectResource);

    /// <summary>Get count of Project resource nodes</summary>
    private int GetProjectResourceCount() =>
        nodes.Count(n => n.TemplateId == "project" && n.IsProjectResource);

    /// <summary>Toggle the Project resources panel collapse state</summary>
    private void ToggleProjectResourcesPanel()
    {
        collapseProjectResources = !collapseProjectResources;
        StateHasChanged();
    }

    /// <summary>Get all resources assigned to a specific task</summary>
    private IEnumerable<Node> GetResourcesAssignedToTask(int taskId) =>
        nodes.Where(n => n.IsProjectResource &&
                        n.ProjectAssignedTaskIds != null &&
                        n.ProjectAssignedTaskIds.Contains(taskId));

    /// <summary>Get all Project dependency edges</summary>
    private IEnumerable<Edge> GetProjectDependencies() =>
        edges.Where(e => e.IsProjectDependency);

    /// <summary>Toggle Project chart mode - if already in Project, toggle between timeline and node view</summary>
    private void ToggleProjectMode()
    {
        if (isProjectMode)
        {
            // Already in Project mode - toggle between timeline and node view
            if (projectViewMode == "timeline")
            {
                projectViewMode = "node";
                LayoutprojectNodesForNodeView();
            }
            else
            {
                projectViewMode = "timeline";
                // Auto zoom-fit when switching to timeline view
                ProjectZoomToFit();
            }
        }
        else
        {
            // Enter Project mode
            isProjectMode = true;
            projectViewMode = "timeline";  // Default to timeline view
            InitializeProjectView();
            // Set template to project and default shape to task for unified Add Shape
            selectedTemplateId = "project";
            selectedTemplateShapeId = "task";
            mode = EditorMode.Select;  // Start in Select mode for easier task manipulation

            // Auto zoom-fit if there are tasks
            var hasProjectTasks = nodes.Any(n => n.TemplateId == "project" && !n.IsProjectResource);
            if (hasProjectTasks)
            {
                ProjectZoomToFit();
            }
        }
        StateHasChanged();
    }

    /// <summary>Exit Project mode and return to regular canvas</summary>
    private void ExitProjectMode()
    {
        isProjectMode = false;
        projectTimeline = new ProjectTimelineService();
        selectedProjectTaskId = null;
        draggingProjectTaskId = null;
        isResizingProjectTask = false;
        isCreatingProjectDependency = false;
        ProjectDependencyFromTaskId = null;
    }

    /// <summary>Initialize Project view (timeline service, calendar)</summary>
    private void InitializeProjectView()
    {
        projectTimeline = new ProjectTimelineService();
        ProjectCalendar = new ProjectCalendar();
        projectTimeline.Calendar = ProjectCalendar;
        UpdateProjectView();
    }

    /// <summary>Update Project view calculations - positions all Project nodes</summary>
    private void UpdateProjectView()
    {
        // Guard against null timeline (may happen if editing edge before Project mode initialized)
        if (projectTimeline == null)
        {
            Console.WriteLine("UpdateProjectView: projectTimeline is null, skipping update");
            return;
        }

        var projectNodes = GetprojectNodes().ToList();

        // Set view range from nodes
        projectTimeline.SetViewRangeFromNodes(projectNodes);

        // Assign row indices if not set
        AssignProjectRowIndices(projectNodes);

        // Position nodes based on current view mode
        if (projectViewMode == "node")
        {
            // In node view, use compact layout preserving relative positions
            LayoutprojectNodesForNodeView();
        }
        else
        {
            // In timeline view, position nodes based on their dates
            foreach (var node in projectNodes)
            {
                projectTimeline.PositionNodeForTimeline(node);
            }
        }

        // Recalculate CPM if there are dependencies
        var dependencies = GetProjectDependencies().ToList();
        if (dependencies.Count > 0)
        {
            try
            {
                projectCpmResults = CalculateNodeBasedCpm(projectNodes, dependencies);
            }
            catch
            {
                projectCpmResults = null;
            }
        }
        else
        {
            projectCpmResults = null;
        }
    }

    /// <summary>Assign row indices to Project nodes that don't have them</summary>
    private void AssignProjectRowIndices(List<Node> projectNodes)
    {
        // Sort by start date, then by ID
        var orderedNodes = projectNodes
            .OrderBy(n => n.ProjectStartDate ?? DateTime.MaxValue)
            .ThenBy(n => n.Id)
            .ToList();

        for (int i = 0; i < orderedNodes.Count; i++)
        {
            if (orderedNodes[i].ProjectRowIndex < 0)
            {
                orderedNodes[i].ProjectRowIndex = i;
            }
        }
    }

    /// <summary>Add a new task node to the Project chart</summary>
    private void AddProjectTask()
    {
        var projectNodes = GetprojectNodes().ToList();

        var startDate = projectNodes.Count > 0 && projectNodes.Any(n => n.ProjectEndDate != null)
            ? projectNodes.Max(n => n.ProjectEndDate ?? DateTime.Today).AddDays(1)
            : DateTime.Today;

        var newId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        var newNode = ProjectTimelineService.CreateProjectTaskNode(
            newId,
            $"Task {projectNodes.Count + 1}",
            startDate,
            5);
        newNode.ProjectRowIndex = projectNodes.Count;

        nodes.Add(newNode);
        selectedProjectTaskId = newNode.Id;
        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>Add a milestone node to the Project chart</summary>
    private void AddProjectMilestone()
    {
        var projectNodes = GetprojectNodes().ToList();

        var date = projectNodes.Count > 0 && projectNodes.Any(n => n.ProjectEndDate != null)
            ? projectNodes.Max(n => n.ProjectEndDate ?? DateTime.Today)
            : DateTime.Today;

        var newId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        var newNode = ProjectTimelineService.CreateProjectMilestoneNode(
            newId,
            $"Milestone {projectNodes.Count + 1}",
            date);
        newNode.ProjectRowIndex = projectNodes.Count;

        nodes.Add(newNode);
        selectedProjectTaskId = newNode.Id;
        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>Add a resource node to the Project chart</summary>
    private void AddProjectResource()
    {
        var resourceNodes = GetProjectResourceNodes().ToList();

        var newId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        var newNode = ProjectTimelineService.CreateProjectResourceNode(
            newId,
            $"Resource {resourceNodes.Count + 1}");

        nodes.Add(newNode);
        StateHasChanged();
    }

    /// <summary>Delete the selected Project task node</summary>
    private void DeleteSelectedProjectTask()
    {
        if (selectedProjectTaskId == null) return;

        var node = nodes.FirstOrDefault(n => n.Id == selectedProjectTaskId);
        if (node != null)
        {
            nodes.Remove(node);
            // Remove related dependencies
            edges.RemoveAll(e => e.IsProjectDependency &&
                (e.From == node.Id || e.To == node.Id));
        }

        selectedProjectTaskId = null;
        UpdateProjectView();
        StateHasChanged();
    }

    
    /// <summary>Delete a specific Project task by ID (used by double-click on outline column)</summary>
    private void DeleteProjectTask(int taskId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == taskId);
        if (node != null)
        {
            nodes.Remove(node);
            // Remove related dependencies
            edges.RemoveAll(e => e.IsProjectDependency &&
                (e.From == node.Id || e.To == node.Id));
        }

        if (selectedProjectTaskId == taskId)
            selectedProjectTaskId = null;

        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>Delete all selected Project tasks (bulk delete) - deletes one by one for visual feedback</summary>
    private async void DeleteSelectedProjectTasks()
    {
        if (!selectedNodes.Any()) return;

        // Save state for undo (saves all at once so undo restores everything)
        UndoService.SaveState(nodes, edges, edgeLabels);

        // Get the list of task IDs to delete, sorted by row index (bottom to top for visual effect)
        var taskIdsToDelete = selectedNodes
            .Select(id => nodes.FirstOrDefault(n => n.Id == id))
            .Where(n => n != null && n.TemplateId == "project")
            .OrderByDescending(n => n!.ProjectRowIndex)
            .Select(n => n!.Id)
            .ToList();

        // Clear selection immediately
        selectedNodes.Clear();
        selectedProjectTaskId = null;

        // Force immediate UI update to show selection cleared
        await InvokeAsync(StateHasChanged);

        // Delete each task one by one with a delay for visual feedback
        for (int i = 0; i < taskIdsToDelete.Count; i++)
        {
            var taskId = taskIdsToDelete[i];
            var node = nodes.FirstOrDefault(n => n.Id == taskId);
            if (node != null)
            {
                // If this is a group, also delete contained tasks
                if (node.IsSuperNode && node.ContainedNodeIds.Any())
                {
                    foreach (var containedId in node.ContainedNodeIds.ToList())
                    {
                        var containedNode = nodes.FirstOrDefault(n => n.Id == containedId);
                        if (containedNode != null)
                        {
                            nodes.Remove(containedNode);
                            edges.RemoveAll(e => e.IsProjectDependency &&
                                (e.From == containedId || e.To == containedId));
                        }
                    }
                }

                nodes.Remove(node);
                // Remove related dependencies
                edges.RemoveAll(e => e.IsProjectDependency &&
                    (e.From == node.Id || e.To == node.Id));

                // Update view after each deletion for visual feedback
                UpdateProjectView();

                // Use InvokeAsync to ensure UI update happens on render thread
                await InvokeAsync(StateHasChanged);

                // Delay between deletions (400ms) for clearly visible effect
                if (i < taskIdsToDelete.Count - 1)
                {
                    await Task.Delay(400);
                }
            }
        }

        // Final update
        UpdateProjectView();
        await InvokeAsync(StateHasChanged);
    }

/// <summary>Check if a task is on the critical path</summary>
    private bool IsTaskCritical(int taskId)
    {
        return projectCpmResults?.TryGetValue(taskId, out var result) == true && result.IsCritical;
    }

    /// <summary>Get task color (red for critical path)</summary>
    private string GetProjectTaskColor(Node node)
    {
        if (showProjectCriticalPath && IsTaskCritical(node.Id))
            return "#ef4444"; // Red for critical path
        return node.FillColor ?? "#3b82f6";
    }

    /// <summary>Whether to show the critical path highlighting</summary>
    private bool showProjectCriticalPath = false;

    /// <summary>Source task ID for dependency creation rubberband</summary>
    private int? ProjectDependencyFromTaskId = null;

    /// <summary>Current rubberband X position</summary>
    private double projectRubberbandX = 0;

    /// <summary>Current rubberband Y position</summary>
    private double projectRubberbandY = 0;

    /// <summary>Whether we're resizing from the left side</summary>
    private bool isResizingProjectFromLeft = false;

    /// <summary>Get the total width of the Project chart in pixels</summary>
    private double GetProjectTotalWidth()
    {
        return projectTimeline?.GetTotalWidth() ?? 800;
    }

    /// <summary>Get the total height of the Project chart in pixels</summary>
    private double GetProjectTotalHeight()
    {
        var taskCount = GetprojectNodes().Count();
        return projectTimeline?.GetTotalHeight(Math.Max(taskCount, 5)) ?? 300;
    }

    // Note: SelectProjectTask, EditProjectTask, StartProjectResize, HandleProjectMouseDown,
    // HandleProjectMouseMove, HandleProjectMouseUp are defined in DFDEditor.ProjectHandlers.cs

    /// <summary>Auto-schedule all tasks based on dependencies</summary>
    private void AutoScheduleProject()
    {
        var projectNodes = GetprojectNodes().ToList();
        var dependencies = GetProjectDependencies().ToList();

        if (projectNodes.Count == 0) return;

        try
        {
            AutoScheduleNodes(projectNodes, dependencies);
            UpdateProjectView();
            StateHasChanged();
        }
        catch (InvalidOperationException ex)
        {
            // Handle circular dependency error
            Console.WriteLine($"Auto-schedule failed: {ex.Message}");
        }
    }

    /// <summary>Auto-schedule nodes based on dependencies (forward pass)</summary>
    private void AutoScheduleNodes(List<Node> projectNodes, List<Edge> dependencies)
    {
        // Build predecessor map
        var predecessors = new Dictionary<int, List<(int predId, ProjectDependencyType type, int lag)>>();
        foreach (var node in projectNodes)
        {
            predecessors[node.Id] = new List<(int, ProjectDependencyType, int)>();
        }

        foreach (var dep in dependencies)
        {
            if (predecessors.ContainsKey(dep.To))
            {
                predecessors[dep.To].Add((dep.From, dep.ProjectDepType, dep.ProjectLagDays));
            }
        }

        // Topological sort
        var sorted = TopologicalSortNodes(projectNodes, dependencies);

        // Forward pass - schedule each node
        foreach (var node in sorted)
        {
            if (predecessors[node.Id].Count == 0) continue;

            DateTime? earliestStart = null;

            foreach (var (predId, depType, lag) in predecessors[node.Id])
            {
                var pred = projectNodes.FirstOrDefault(n => n.Id == predId);
                if (pred?.ProjectStartDate == null || pred.ProjectEndDate == null) continue;

                DateTime constraintDate = depType switch
                {
                    ProjectDependencyType.FinishToStart => pred.ProjectEndDate.Value.AddDays(1 + lag),
                    ProjectDependencyType.StartToStart => pred.ProjectStartDate.Value.AddDays(lag),
                    ProjectDependencyType.FinishToFinish => pred.ProjectEndDate.Value.AddDays(lag - node.ProjectDurationDays + 1),
                    ProjectDependencyType.StartToFinish => pred.ProjectStartDate.Value.AddDays(lag - node.ProjectDurationDays + 1),
                    _ => pred.ProjectEndDate.Value.AddDays(1 + lag)
                };

                if (earliestStart == null || constraintDate > earliestStart)
                {
                    earliestStart = constraintDate;
                }
            }

            if (earliestStart != null)
            {
                node.ProjectStartDate = earliestStart;
                ProjectTimelineService.SetNodeEndDateFromDuration(node);
            }
        }
    }

    /// <summary>Topological sort of nodes based on dependencies</summary>
    private List<Node> TopologicalSortNodes(List<Node> projectNodes, List<Edge> dependencies)
    {
        var nodeMap = projectNodes.ToDictionary(n => n.Id);
        var inDegree = projectNodes.ToDictionary(n => n.Id, _ => 0);

        foreach (var dep in dependencies)
        {
            if (inDegree.ContainsKey(dep.To))
            {
                inDegree[dep.To]++;
            }
        }

        var queue = new Queue<int>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<Node>();

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (nodeMap.TryGetValue(nodeId, out var node))
            {
                sorted.Add(node);
            }

            foreach (var dep in dependencies.Where(d => d.From == nodeId))
            {
                if (inDegree.ContainsKey(dep.To))
                {
                    inDegree[dep.To]--;
                    if (inDegree[dep.To] == 0)
                    {
                        queue.Enqueue(dep.To);
                    }
                }
            }
        }

        if (sorted.Count != projectNodes.Count)
        {
            // Circular dependency detected - return all nodes in original order as fallback
            Console.WriteLine("Warning: Circular dependency detected in Project chart. Using original order.");
            return projectNodes.ToList();
        }

        return sorted;
    }

    /// <summary>Calculate CPM (Critical Path Method) for nodes</summary>
    private Dictionary<int, CpmTaskResult> CalculateNodeBasedCpm(List<Node> projectNodes, List<Edge> dependencies)
    {
        var results = new Dictionary<int, CpmTaskResult>();

        // Guard: need at least one node to calculate CPM
        if (projectNodes == null || projectNodes.Count == 0)
        {
            return results;
        }

        // Helper to convert DateTime to LocalDate
        NodaTime.LocalDate ToLocalDate(DateTime? dt) =>
            dt.HasValue ? NodaTime.LocalDate.FromDateTime(dt.Value) : NodaTime.LocalDate.FromDateTime(DateTime.Today);

        // Use a far-future date that won't overflow with arithmetic
        var maxDate = NodaTime.LocalDate.FromDateTime(DateTime.Today.AddYears(100));

        // Initialize results
        foreach (var node in projectNodes)
        {
            results[node.Id] = new CpmTaskResult
            {
                TaskId = node.Id,
                EarlyStart = ToLocalDate(node.ProjectStartDate),
                EarlyFinish = ToLocalDate(node.ProjectEndDate),
                LateStart = maxDate,
                LateFinish = maxDate
            };
        }

        // Forward pass (early start/finish)
        var sorted = TopologicalSortNodes(projectNodes, dependencies);
        foreach (var node in sorted)
        {
            var es = ToLocalDate(node.ProjectStartDate);

            foreach (var dep in dependencies.Where(d => d.To == node.Id))
            {
                var pred = projectNodes.FirstOrDefault(n => n.Id == dep.From);
                if (pred?.ProjectEndDate == null) continue;

                var predEnd = ToLocalDate(pred.ProjectEndDate);
                var predStart = ToLocalDate(pred.ProjectStartDate);
                var constraintDate = dep.ProjectDepType switch
                {
                    ProjectDependencyType.FinishToStart => predEnd.PlusDays(1 + dep.ProjectLagDays),
                    ProjectDependencyType.StartToStart => predStart.PlusDays(dep.ProjectLagDays),
                    _ => predEnd.PlusDays(1 + dep.ProjectLagDays)
                };

                if (constraintDate > es)
                {
                    es = constraintDate;
                }
            }

            results[node.Id].EarlyStart = es;
            results[node.Id].EarlyFinish = es.PlusDays(Math.Max(0, node.ProjectDurationDays - 1));
        }

        // Backward pass (late start/finish)
        var projectEnd = results.Values.Max(r => r.EarlyFinish);
        sorted.Reverse();

        foreach (var node in sorted)
        {
            var lf = projectEnd;

            var successorDeps = dependencies.Where(d => d.From == node.Id).ToList();
            if (successorDeps.Count > 0)
            {
                foreach (var dep in successorDeps)
                {
                    if (results.TryGetValue(dep.To, out var succResult))
                    {
                        var constraintDate = dep.ProjectDepType switch
                        {
                            ProjectDependencyType.FinishToStart => succResult.LateStart.PlusDays(-1 - dep.ProjectLagDays),
                            ProjectDependencyType.StartToStart => succResult.LateStart.PlusDays(-dep.ProjectLagDays),
                            _ => succResult.LateStart.PlusDays(-1 - dep.ProjectLagDays)
                        };

                        if (constraintDate < lf)
                        {
                            lf = constraintDate;
                        }
                    }
                }
            }

            results[node.Id].LateFinish = lf;
            results[node.Id].LateStart = lf.PlusDays(-Math.Max(0, node.ProjectDurationDays - 1));
        }

        // Calculate float (IsCritical is computed automatically from TotalFloat)
        foreach (var result in results.Values)
        {
            result.TotalFloat = NodaTime.Period.Between(result.EarlyStart, result.LateStart, NodaTime.PeriodUnits.Days).Days;
        }

        return results;
    }

    /// <summary>Toggle critical path highlighting</summary>
    private void ToggleProjectCriticalPath()
    {
        showProjectCriticalPath = !showProjectCriticalPath;
        if (showProjectCriticalPath)
        {
            UpdateProjectView(); // Recalculate CPM
        }
        StateHasChanged();
    }

    /// <summary>Zoom in on the Project timeline</summary>
    private void ProjectZoomIn()
    {
        projectTimeline?.ZoomIn();
        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>Zoom out on the Project timeline</summary>
    private void ProjectZoomOut()
    {
        projectTimeline?.ZoomOut();
        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>Zoom to fit all tasks in view</summary>
    private void ProjectZoomToFit()
    {
        if (projectTimeline != null)
        {
            // Estimate available width (viewport width minus label column)
            projectTimeline.ZoomToFit(GetprojectNodes(), 1200);
            UpdateProjectView();
            StateHasChanged();
        }
    }

    /// <summary>Get the project's latest end date (makespan equivalent)</summary>
    private NodaTime.LocalDate GetProjectMakespan()
    {
        var maxEnd = NodaTime.LocalDate.MinIsoValue;
        foreach (var task in GetprojectNodes().Where(n => n.ProjectEndDate.HasValue))
        {
            var taskEndDate = NodaTime.LocalDate.FromDateTime(task.ProjectEndDate!.Value);
            if (taskEndDate > maxEnd)
                maxEnd = taskEndDate;
        }
        return maxEnd == NodaTime.LocalDate.MinIsoValue
            ? NodaTime.LocalDate.FromDateTime(DateTime.Today).PlusDays(30)
            : maxEnd;
    }

    /// <summary>Set the project deadline to the current makespan</summary>
    private void SetProjectDeadlineToMakespan()
    {
        projectDeadlineDate = GetProjectMakespan();
        StateHasChanged();
    }

    /// <summary>Clear the project deadline marker</summary>
    private void ClearProjectDeadline()
    {
        projectDeadlineDate = null;
        StateHasChanged();
    }

    /// <summary>Start dragging the project deadline marker</summary>
    private void StartProjectDeadlineDrag(MouseEventArgs e)
    {
        isDraggingProjectDeadline = true;
        projectDeadlineDragStartX = e.OffsetX;
    }

    /// <summary>Handle project deadline marker drag</summary>
    private void HandleProjectDeadlineDrag(MouseEventArgs e)
    {
        if (!isDraggingProjectDeadline || projectTimeline == null) return;

        var newDate = projectTimeline.XToDate(e.OffsetX);
        if (newDate >= projectTimeline.ViewStartDate)
        {
            projectDeadlineDate = newDate;
            StateHasChanged();
        }
    }

    /// <summary>Stop dragging the project deadline marker</summary>
    private void StopProjectDeadlineDrag()
    {
        isDraggingProjectDeadline = false;
    }

    /// <summary>Toggle project compression mode</summary>
    private void ToggleProjectCompressionMode()
    {
        projectCompressionMode = projectCompressionMode == ProjectCompressionMode.Earliest
            ? ProjectCompressionMode.Latest
            : ProjectCompressionMode.Earliest;
        StateHasChanged();
    }

    /// <summary>Get ordered task nodes for rendering (respects collapsed groups)</summary>
    private List<Node> GetOrderedprojectNodes()
    {
        // Filter out children of collapsed groups
        var visibleNodes = GetprojectNodes().Where(n =>
        {
            // SuperNodes (groups) are always visible
            if (n.IsSuperNode) return true;

            // If node has no parent group, it's visible
            if (!n.ParentSuperNodeId.HasValue) return true;

            // If node is in a group, check if the group is collapsed
            var parentGroup = nodes.FirstOrDefault(p => p.Id == n.ParentSuperNodeId.Value);
            if (parentGroup == null) return true;

            // Show children only if group is expanded (not collapsed)
            return !parentGroup.IsCollapsed;
        });

        return visibleNodes
            .OrderBy(n => n.ProjectRowIndex)
            .ThenBy(n => n.ProjectStartDate ?? DateTime.MaxValue)
            .ThenBy(n => n.Id)
            .ToList();
    }

    /// <summary>Get task row index for a node</summary>
    private int GetProjectTaskRowIndex(int nodeId)
    {
        var nodes = GetOrderedprojectNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Id == nodeId) return i;
        }
        return 0;
    }

    /// <summary>
    /// Get WBS (Work Breakdown Structure) ID for a Project node.
    /// Groups get simple numbers (1, 2, 3...).
    /// Children get parent.child format (1.1, 1.2, 2.1...).
    /// Standalone tasks get sequential numbers.
    /// </summary>
    private string GetProjectWbsId(Node node, IEnumerable<Node> orderedNodes)
    {
        var nodeList = orderedNodes.ToList();

        // Count groups and standalone tasks to assign top-level numbers
        int groupCounter = 0;
        int standaloneCounter = 0;
        var groupNumbers = new Dictionary<int, int>(); // groupId -> WBS number

        foreach (var n in nodeList)
        {
            if (n.IsSuperNode)
            {
                groupCounter++;
                groupNumbers[n.Id] = groupCounter;
            }
            else if (!n.ParentSuperNodeId.HasValue)
            {
                // Standalone task (not in a group)
                standaloneCounter++;
            }
        }

        // If this is a group, return its number
        if (node.IsSuperNode && groupNumbers.TryGetValue(node.Id, out var groupNum))
        {
            return groupNum.ToString();
        }

        // If this is a child of a group, return parent.childIndex
        if (node.ParentSuperNodeId.HasValue && groupNumbers.TryGetValue(node.ParentSuperNodeId.Value, out var parentNum))
        {
            var parent = nodes.FirstOrDefault(n => n.Id == node.ParentSuperNodeId.Value);
            if (parent != null)
            {
                var childIndex = parent.ContainedNodeIds.IndexOf(node.Id) + 1;
                return $"{parentNum}.{childIndex}";
            }
        }

        // Standalone task - count position among non-grouped tasks
        int position = 0;
        foreach (var n in nodeList)
        {
            if (!n.IsSuperNode && !n.ParentSuperNodeId.HasValue)
            {
                position++;
                if (n.Id == node.Id)
                {
                    // Offset by number of groups to avoid duplicate numbers
                    return (groupCounter + position).ToString();
                }
            }
        }

        return "-";
    }

    /// <summary>Get Project node by ID</summary>
    private Node? GetProjectNode(int nodeId)
    {
        return nodes.FirstOrDefault(n => n.Id == nodeId && n.TemplateId == "project");
    }

    /// <summary>
    /// Get Kendall notation for a queueing node (e.g., M/M/1, M/D/c/K)
    /// Format: A/S/c/K/N/D where:
    /// A = arrival distribution, S = service distribution, c = servers, K = capacity
    /// </summary>
    private string GetKendallNotation(Node node)
    {
        if (node.TemplateId != "qmaker")
            return "";

        // Get distribution codes
        string arrivalCode = node.QueueArrivalDistribution switch
        {
            QueueDistribution.Exponential => "M",
            QueueDistribution.Deterministic => "D",
            QueueDistribution.General => "G",
            QueueDistribution.Erlang => "Ek",
            _ => "M"
        };

        string serviceCode = node.QueueServiceDistribution switch
        {
            QueueDistribution.Exponential => "M",
            QueueDistribution.Deterministic => "D",
            QueueDistribution.General => "G",
            QueueDistribution.Erlang => "Ek",
            _ => "M"
        };

        int servers = node.QueueServerCount > 0 ? node.QueueServerCount : 1;
        string serverStr = servers > 1 ? "c" : "1";

        // Basic notation: A/S/c
        string notation = $"{arrivalCode}/{serviceCode}/{serverStr}";

        // Add capacity if finite (K)
        if (node.QueueCapacity.HasValue && node.QueueCapacity.Value > 0)
        {
            notation += $"/{node.QueueCapacity.Value}";
        }

        return notation;
    }

    /// <summary>Add a Project dependency between two nodes</summary>
    private void AddProjectDependency(int sourceNodeId, int targetNodeId,
        ProjectDependencyType type = ProjectDependencyType.FinishToStart, int lagDays = 0)
    {
        // Check if dependency already exists
        if (edges.Any(e => e.IsProjectDependency &&
            e.From == sourceNodeId && e.To == targetNodeId))
        {
            return;
        }

        var newEdge = new Edge
        {
            Id = edges.Count > 0 ? edges.Max(e => e.Id) + 1 : 1,
            From = sourceNodeId,
            To = targetNodeId,
            IsProjectDependency = true,
            ProjectDepType = type,
            ProjectLagDays = lagDays,
            StrokeColor = "#64748b",
            Style = EdgeStyle.Ortho
        };

        edges.Add(newEdge);
        UpdateProjectView();
    }

    /// <summary>Remove a Project dependency</summary>
    private void RemoveProjectDependency(int sourceNodeId, int targetNodeId)
    {
        edges.RemoveAll(e => e.IsProjectDependency &&
            e.From == sourceNodeId && e.To == targetNodeId);
        UpdateProjectView();
    }

    /// <summary>Assign a resource to a task</summary>
    private void AssignResourceToTask(int resourceNodeId, int taskNodeId)
    {
        var resource = nodes.FirstOrDefault(n => n.Id == resourceNodeId && n.IsProjectResource);
        var task = nodes.FirstOrDefault(n => n.Id == taskNodeId && n.TemplateId == "project" && !n.IsProjectResource);

        if (resource == null || task == null) return;

        // Add task to resource's assigned list
        resource.ProjectAssignedTaskIds ??= new List<int>();
        if (!resource.ProjectAssignedTaskIds.Contains(taskNodeId))
        {
            resource.ProjectAssignedTaskIds.Add(taskNodeId);
        }

        // Set assigned resource on task
        task.ProjectAssignedTo = resource.Text;

        StateHasChanged();
    }

    /// <summary>Unassign a resource from a task</summary>
    private void UnassignResourceFromTask(int resourceNodeId, int taskNodeId)
    {
        var resource = nodes.FirstOrDefault(n => n.Id == resourceNodeId && n.IsProjectResource);
        var task = nodes.FirstOrDefault(n => n.Id == taskNodeId && n.TemplateId == "project" && !n.IsProjectResource);

        if (resource == null || task == null) return;

        resource.ProjectAssignedTaskIds?.Remove(taskNodeId);

        // Clear assigned resource if this was the assigned resource
        if (task.ProjectAssignedTo == resource.Text)
        {
            task.ProjectAssignedTo = null;
        }

        StateHasChanged();
    }

    /// <summary>Unassign a resource from a task (Node overload)</summary>
    private void UnassignResourceFromTask(Node resource, Node task)
    {
        if (resource == null || task == null) return;
        UnassignResourceFromTask(resource.Id, task.Id);
    }

    /// <summary>Delete the resource being edited in the dialog</summary>
    private void DeleteeditingProjectResource()
    {
        if (editingProjectResource == null) return;

        // Remove assignments from all tasks
        if (editingProjectResource.ProjectAssignedTaskIds != null)
        {
            foreach (var taskId in editingProjectResource.ProjectAssignedTaskIds.ToList())
            {
                var task = nodes.FirstOrDefault(n => n.Id == taskId);
                if (task != null && task.ProjectAssignedTo == editingProjectResource.Text)
                {
                    task.ProjectAssignedTo = null;
                }
            }
        }

        // Remove the resource node
        nodes.RemoveAll(n => n.Id == editingProjectResource.Id);

        showProjectResourceDialog = false;
        editingProjectResource = null;
        StateHasChanged();
    }

    // ============================================
    // HELP SYSTEM
    // ============================================
    private bool showHelpModal = false;
    private string activeHelpSection = "getting-started";

    private static readonly Dictionary<string, (string Title, string Icon)> HelpSections = new()
    {
        ["getting-started"] = ("Getting Started", "??"),
        ["shapes"] = ("Shapes & Meanings", "?"),
        ["connections"] = ("Connections & Edges", "?"),
        ["icons"] = ("Icons Library", "??"),
        ["dfd"] = ("Data Flow Diagrams", "??"),
        ["flowcharts"] = ("Flowcharts", "??"),
        ["swimlanes"] = ("Swimlane Diagrams", "??"),
        ["architecture"] = ("Architecture Diagrams", "??"),
        ["keyboard"] = ("Keyboard Shortcuts", "?"),
        ["export"] = ("Export Options", "??"),
        ["tips"] = ("Tips & Tricks", "??"),
    };

    private void OpenHelp() => showHelpModal = true;
    private void CloseHelp() => showHelpModal = false;
    private void SetHelpSection(string section) => activeHelpSection = section;

    private void ToggleSelectTool()
    {
        selectToolActive = !selectToolActive;
        StateHasChanged();
    }

    // ============================================
    // EXAMPLE GENERATOR - Password Protected
    // ============================================
    private bool showExampleGenerator = false;
    private string examplePassword = "";
    private bool examplePasswordVerified = false;
    private string generatedExampleCode = "";
    private string exampleName = "MyDiagram";
    private string newExampleKey = "myexample";
    private string newExampleName = "My Example";
    private string newExampleDescription = "Description here";
    private const string EXAMPLE_PASSWORD = "dfd2025";
    private const string EXAMPLE_GENERATOR_PASSWORD = "dfd2025";

    private void ToggleExampleGenerator()
    {
        showExampleGenerator = !showExampleGenerator;
        if (!showExampleGenerator)
        {
            examplePasswordVerified = false;
            examplePassword = "";
            generatedExampleCode = "";
        }
    }

    private void CloseExampleGenerator()
    {
        showExampleGenerator = false;
        examplePasswordVerified = false;
        examplePassword = "";
        generatedExampleCode = "";
    }

    private void CheckExampleGeneratorShortcut(KeyboardEventArgs e)
    {
        if (e.CtrlKey && e.ShiftKey && e.Key == "E")
        {
            showExampleGenerator = true;
            StateHasChanged();
        }
    }

    private void VerifyExamplePassword()
    {
        examplePasswordVerified = (examplePassword == EXAMPLE_GENERATOR_PASSWORD);
        if (examplePasswordVerified)
        {
            GenerateExampleCode();
        }
    }

    private void GenerateExampleCode()
    {
        var sb = new System.Text.StringBuilder();

        // Generate method signature
        string methodName = ToPascalCase(newExampleKey);
        sb.AppendLine($"    // Add this to the Examples dictionary:");
        sb.AppendLine($"    // [\"{newExampleKey}\"] = (\"{newExampleName}\", \"{newExampleDescription}\", Load{methodName}),");
        sb.AppendLine();
        sb.AppendLine($"    private static void Load{methodName}(DFDEditor editor)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Helper function");
        sb.AppendLine("        ConnectionPoint CP(string side, int pos) => new ConnectionPoint { Side = side, Position = pos };");
        sb.AppendLine();

        // Generate nodes
        sb.AppendLine("        // Nodes");
        foreach (var node in nodes.OrderBy(n => n.Id))
        {
            sb.Append($"        editor.nodes.Add(new Node {{ ");
            sb.Append($"Id = {node.Id}, ");
            sb.Append($"Text = \"{EscapeString(node.Text)}\", ");
            sb.Append($"X = {node.X}, ");
            sb.Append($"Y = {node.Y}, ");
            sb.Append($"Width = {node.Width}, ");
            sb.Append($"Height = {node.Height}, ");
            sb.Append($"Shape = NodeShape.{node.Shape}, ");
            sb.Append($"StrokeColor = \"{node.StrokeColor}\"");
            if (!string.IsNullOrEmpty(node.Icon))
            {
                sb.Append($", Icon = \"{node.Icon}\"");
            }
            sb.AppendLine(" });");
        }

        if (nodes.Any())
        {
            sb.AppendLine($"        editor.nextId = {nodes.Max(n => n.Id) + 1};");
        }

        sb.AppendLine();

        // Generate edges
        if (edges.Any())
        {
            sb.AppendLine("        // Edges");
            foreach (var edge in edges.OrderBy(e => e.Id))
            {
                sb.Append($"        editor.edges.Add(new Edge {{ ");
                sb.Append($"Id = {edge.Id}, ");
                sb.Append($"From = {edge.From}, ");
                sb.Append($"To = {edge.To}, ");
                sb.Append($"FromConnection = CP(\"{edge.FromConnection?.Side ?? "right"}\", {edge.FromConnection?.Position ?? 0}), ");
                sb.Append($"ToConnection = CP(\"{edge.ToConnection?.Side ?? "left"}\", {edge.ToConnection?.Position ?? 0})");

                if (edge.IsDoubleLine)
                    sb.Append(", IsDoubleLine = true");
                if (edge.IsOrthogonal)
                    sb.Append(", IsOrthogonal = true");
                if (!string.IsNullOrEmpty(edge.StrokeColor))
                    sb.Append($", StrokeColor = \"{edge.StrokeColor}\"");
                if (edge.StrokeWidth.HasValue)
                    sb.Append($", StrokeWidth = {edge.StrokeWidth}");
                if (!string.IsNullOrEmpty(edge.StrokeDashArray))
                    sb.Append($", StrokeDashArray = \"{edge.StrokeDashArray}\"");

                sb.AppendLine(" });");
            }
            sb.AppendLine($"        editor.nextEdgeId = {edges.Max(e => e.Id) + 1};");
        }

        // Generate edge labels
        var labelsForEdges = edgeLabels.Where(l => !string.IsNullOrEmpty(l.Text)).ToList();
        if (labelsForEdges.Any())
        {
            sb.AppendLine();
            sb.AppendLine("        // Labels");
            int labelId = 1;
            foreach (var label in labelsForEdges.OrderBy(l => l.EdgeId))
            {
                sb.AppendLine($"        editor.edgeLabels.Add(new EdgeLabel {{ Id = {labelId++}, EdgeId = {label.EdgeId}, Text = \"{EscapeString(label.Text)}\" }});");
            }
        }

        sb.AppendLine("    }");

        generatedExampleCode = sb.ToString();
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return "Example";

        var words = input.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w =>
            char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1).ToLower() : "")
        ));
    }

    private string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private async Task CopyGeneratedCode()
    {
        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", generatedExampleCode);
    }

    // ============================================
    // LIFECYCLE METHODS
    // ============================================
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Console.WriteLine("=== DFDEditor OnInitializedAsync START ===");

            // Initialize theme service (loads themes.json)
            await dfdTheme.InitializeAsync();

            // Initialize template configuration (reads URL params and config file)
            await TemplateConfig.InitializeAsync();

            Console.WriteLine($"Nodes count: {nodes.Count}");
            Console.WriteLine($"Edges count: {edges.Count}");
            Console.WriteLine($"Mode: {mode}");
            Console.WriteLine($"Enabled templates: {string.Join(", ", TemplateConfig.GetEnabledTemplateIds())}");

            // Apply default mode from config if set
            // Note: We just set the flags here - actual initialization happens in OnAfterRenderAsync
            var defaultMode = TemplateConfig.GetDefaultMode();
            Console.WriteLine($"======================================");
            Console.WriteLine($"DEFAULT MODE FROM CONFIG: '{defaultMode ?? "(null)"}'");
            Console.WriteLine($"======================================");
            if (!string.IsNullOrEmpty(defaultMode))
            {
                Console.WriteLine($"Applying default mode: {defaultMode}");
                if (defaultMode.Equals("project", StringComparison.OrdinalIgnoreCase))
                {
                    // Set flags for Project mode - InitializeProjectView will be called in OnAfterRenderAsync
                    Console.WriteLine("Setting isProjectMode = true and selectedTemplateId = project");
                    isProjectMode = true;
                    selectedTemplateId = "project";
                    selectedTemplateShapeId = "task";  // Default shape for adding tasks
                    mode = EditorMode.AddNode;  // Ready to add tasks
                    _pendingProjectInitialization = true;
                }
                else
                {
                    // Select the specified template
                    selectedTemplateId = defaultMode;
                    var shapes = GetShapesForTemplate(selectedTemplateId);
                    selectedTemplateShapeId = shapes.FirstOrDefault()?.Id;
                    mode = EditorMode.AddNode;
                    chainMode = false;
                    ClearMultiConnectState();
                }
            }

            Console.WriteLine("=== DFDEditor OnInitializedAsync END ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION in OnInitializedAsync: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load saved template styles and terminal configs from localStorage
            await LoadTemplateStylesFromLocalStorage();
            await LoadTerminalConfigsFromLocalStorage();

            // Initialize Project view if pending (from defaultMode = "project")
            if (_pendingProjectInitialization)
            {
                Console.WriteLine("OnAfterRenderAsync: Completing pending Project initialization");
                _pendingProjectInitialization = false;
                InitializeProjectView();
                StateHasChanged();
            }

            // Initialize viewport size for minimap
            await Task.Delay(100);
            await HandleCanvasScroll();
        }
    }

    // ============================================
    // HELPER METHODS
    // ============================================
    private (double X, double Y) GetLastMousePosition() => (lastMouseX, lastMouseY);

    private Rectangle GetSelectionRectangle()
    {
        if (!selectionStart.HasValue) return new Rectangle { X = 0, Y = 0, Width = 0, Height = 0 };

        var startX = Math.Min(selectionStart.Value.X, currentMousePosition.X);
        var startY = Math.Min(selectionStart.Value.Y, currentMousePosition.Y);
        var width = Math.Abs(currentMousePosition.X - selectionStart.Value.X);
        var height = Math.Abs(currentMousePosition.Y - selectionStart.Value.Y);

        return new Rectangle { X = startX, Y = startY, Width = width, Height = height };
    }

    // Get selection rectangle for 1:N Area mode
    private Rectangle GetOneToNAreaRectangle()
    {
        if (!oneToNAreaStart.HasValue) return new Rectangle { X = 0, Y = 0, Width = 0, Height = 0 };

        var startX = Math.Min(oneToNAreaStart.Value.X, currentMousePosition.X);
        var startY = Math.Min(oneToNAreaStart.Value.Y, currentMousePosition.Y);
        var width = Math.Abs(currentMousePosition.X - oneToNAreaStart.Value.X);
        var height = Math.Abs(currentMousePosition.Y - oneToNAreaStart.Value.Y);

        return new Rectangle { X = startX, Y = startY, Width = width, Height = height };
    }

    private (double X, double Y) GetEdgeMidpoint(Edge edge)
    {
        if (edge.Waypoints.Count > 0)
        {
            var midIndex = edge.Waypoints.Count / 2;
            return (edge.Waypoints[midIndex].X, edge.Waypoints[midIndex].Y);
        }

        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);

        if (fromNode == null || toNode == null)
            return (0, 0);

        return (
            (fromNode.X + fromNode.Width / 2 + toNode.X + toNode.Width / 2) / 2,
            (fromNode.Y + fromNode.Height / 2 + toNode.Y + toNode.Height / 2) / 2
        );
    }

    /// <summary>
    /// Get 3 control points along an edge at 25%, 50%, 75% positions
    /// If waypoints exist, return them; otherwise compute from endpoints
    /// </summary>
    private List<(double X, double Y, int Index)> GetEdgeControlPoints(Edge edge)
    {
        var points = new List<(double X, double Y, int Index)>();
        
        // If edge has existing waypoints, return those with their indices
        if (edge.Waypoints.Count > 0)
        {
            for (int i = 0; i < edge.Waypoints.Count; i++)
            {
                points.Add((edge.Waypoints[i].X, edge.Waypoints[i].Y, i));
            }
            return points;
        }
        
        // Otherwise, compute 3 points along the straight line
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
        
        if (fromNode == null || toNode == null)
            return points;
        
        // Get connection point coordinates
        double x1, y1, x2, y2;
        
        if (edge.FromConnection != null)
        {
            var fromCoords = GeometryService.GetConnectionPointCoordinates(fromNode, edge.FromConnection.Side, edge.FromConnection.Position);
            x1 = fromCoords.X;
            y1 = fromCoords.Y;
        }
        else
        {
            x1 = fromNode.X + fromNode.Width / 2;
            y1 = fromNode.Y + fromNode.Height / 2;
        }
        
        if (edge.ToConnection != null)
        {
            var toCoords = GeometryService.GetConnectionPointCoordinates(toNode, edge.ToConnection.Side, edge.ToConnection.Position);
            x2 = toCoords.X;
            y2 = toCoords.Y;
        }
        else
        {
            x2 = toNode.X + toNode.Width / 2;
            y2 = toNode.Y + toNode.Height / 2;
        }
        
        // Calculate 3 points at 25%, 50%, 75%
        points.Add((x1 + (x2 - x1) * 0.25, y1 + (y2 - y1) * 0.25, -1)); // -1 means "needs to be added"
        points.Add((x1 + (x2 - x1) * 0.50, y1 + (y2 - y1) * 0.50, -2));
        points.Add((x1 + (x2 - x1) * 0.75, y1 + (y2 - y1) * 0.75, -3));
        
        return points;
    }

    private void RecalculateEdgePaths(int? movedNodeId = null)
    {
        foreach (var edge in edges)
        {
            if (movedNodeId == null || edge.From == movedNodeId || edge.To == movedNodeId)
            {
                edge.PathData = PathService.GetEdgePath(edge, nodes);
            }
        }
    }

    private void UpdateEdgePath(Edge edge)
    {
        edge.PathData = PathService.GetEdgePath(edge, nodes);
        StateHasChanged();
    }

    /// <summary>
    /// Re-routes selected edges (or all edges if none selected) with the specified style.
    /// This applies the routing algorithm to existing edges, useful for converting
    /// direct edges to orthogonal after initial placement.
    /// </summary>
    private void RerouteEdges(EdgeStyle style)
    {
        UndoService.SaveState(nodes, edges, edgeLabels);

        var edgesToReroute = selectedEdges.Count > 0
            ? edges.Where(e => selectedEdges.Contains(e.Id)).ToList()
            : edges.ToList();

        foreach (var edge in edgesToReroute)
        {
            edge.Style = style;
            edge.Waypoints.Clear(); // Clear any manual waypoints
            edge.PathData = PathService.GetEdgePath(edge, nodes);
        }

        StateHasChanged();
    }

    /// <summary>
    /// Re-routes selected edges to SmartL (simple L-shape based on terminal directions)
    /// </summary>
    private void RerouteToSmartL() => RerouteEdges(EdgeStyle.SmartL);

    /// <summary>
    /// Re-routes selected edges to Ortho (orthogonal with obstacle avoidance)
    /// </summary>
    private void RerouteToOrtho() => RerouteEdges(EdgeStyle.Ortho);

    /// <summary>
    /// Re-routes selected edges to Direct (straight lines)
    /// </summary>
    private void RerouteToDirect() => RerouteEdges(EdgeStyle.Direct);

    private EventCallback<(string side, int position, MouseEventArgs e)> CreateConnectionPointHandler(int nodeId)
    {
        return new EventCallback<(string side, int position, MouseEventArgs e)>(
            this,
            (Action<(string side, int position, MouseEventArgs e)>)(args => HandleConnectionPointClick(nodeId, args.side, args.position, args.e))
        );
    }

    // Helper class for selection rectangle
    private class Rectangle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private void DeleteSelected()
    {
        if (selectedNodes.Count == 0 && selectedEdges.Count == 0 && selectedLabels.Count == 0)
            return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        // Delete selected nodes and their connected edges
        foreach (var nodeId in selectedNodes.ToList())
        {
            // Remove edges connected to this node
            edges.RemoveAll(e => e.From == nodeId || e.To == nodeId);
            // Remove the node
            nodes.RemoveAll(n => n.Id == nodeId);
        }

        // Delete selected edges
        foreach (var edgeId in selectedEdges.ToList())
        {
            edges.RemoveAll(e => e.Id == edgeId);
        }

        // Delete selected labels
        foreach (var labelId in selectedLabels.ToList())
        {
            edgeLabels.RemoveAll(l => l.Id == labelId);
        }

        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();
        StateHasChanged();
    }

    /// <summary>Delete only selected edges (used for Gantt/Project edge deletion)</summary>
    private void DeleteSelectedEdges()
    {
        if (selectedEdges.Count == 0) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        foreach (var edgeId in selectedEdges.ToList())
        {
            edges.RemoveAll(e => e.Id == edgeId);
        }

        selectedEdges.Clear();
        StateHasChanged();
    }

    private void OnForceLayout()
{
    UndoService.SaveState(nodes, edges, edgeLabels);
    _layoutService.ApplyForceDirectedLayout(nodes, edges);
    RecalculateEdgePaths();
    StateHasChanged();
}
private void HandleOptimizationComplete((List<Node>, List<Edge>) result)
{
    nodes = result.Item1;
    edges = result.Item2;
    RecalculateEdgePaths();
    StateHasChanged();
}
private void ToggleChainMode()
{
    chainMode = !chainMode;
    
    if (chainMode)
    {
        connectionMode = ConnectionModeType.Normal;
        ClearOneToNState();
        multiConnectMode = false;
        ClearMultiConnectState();
    }
    
    lastChainedNodeId = null;
    mode = EditorMode.Select;
    StateHasChanged();
}
// Apply stroke color to selected nodes
    private void ApplyStrokeToSelectedNodes()
    {
        if (selectedNodes.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        foreach (var nodeId in selectedNodes)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                node.StrokeColor = defaultStrokeColor;
                node.StrokeWidth = defaultStrokeWidth;
                node.StrokeDashArray = defaultStrokeDashArray;
            }
        }
        StateHasChanged();
    }

    // Apply stroke when clicking color swatch (auto-apply to selection)
    private void ApplyStrokeToSelection()
    {
        if (selectedNodes.Count > 0)
        {
            ApplyStrokeToSelectedNodes();
        }
        else if (selectedEdges.Count > 0)
        {
            ApplyEdgeStylesToSelected();
        }
    }
       /// <summary>
    /// Arranges selected nodes in a grid within their current bounding area.
    /// </summary>
    /// <param name="forceRows">0 = auto (square-ish), 1 = single row, 999 = single column</param>
    private void ArrangeInGrid(int forceRows)
    {
        if (selectedNodes.Count < 2) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        var selected = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        if (selected.Count < 2) return;
        
        // Get bounding box of selection
        var minX = selected.Min(n => n.X);
        var minY = selected.Min(n => n.Y);
        var maxX = selected.Max(n => n.X + n.Width);
        var maxY = selected.Max(n => n.Y + n.Height);
        
        var areaWidth = maxX - minX;
        var areaHeight = maxY - minY;
        
        // Calculate grid dimensions
        int count = selected.Count;
        int cols, rows;
        
        if (forceRows == 1)
        {
            // Single row
            rows = 1;
            cols = count;
        }
        else if (forceRows >= count)
        {
            // Single column
            rows = count;
            cols = 1;
        }
        else if (forceRows > 0)
        {
            // Force specific number of rows
            rows = forceRows;
            cols = (int)Math.Ceiling((double)count / rows);
        }
        else
        {
            // Auto: try to make it square-ish
            cols = (int)Math.Ceiling(Math.Sqrt(count));
            rows = (int)Math.Ceiling((double)count / cols);
        }
        
        // Get average node size for spacing calculation
        var avgWidth = selected.Average(n => n.Width);
        var avgHeight = selected.Average(n => n.Height);
        
        // Calculate cell size (ensure minimum spacing)
        var cellWidth = Math.Max(areaWidth / cols, avgWidth + 20);
        var cellHeight = Math.Max(areaHeight / rows, avgHeight + 20);
        
        // If area is too small, expand it
        if (areaWidth < cellWidth * cols) areaWidth = cellWidth * cols;
        if (areaHeight < cellHeight * rows) areaHeight = cellHeight * rows;
        
        // Recalculate cell size with potentially expanded area
        cellWidth = areaWidth / cols;
        cellHeight = areaHeight / rows;
        
        // Sort nodes by their current position (top-left to bottom-right)
        var sortedNodes = selected
            .OrderBy(n => (int)(n.Y / 100))
            .ThenBy(n => n.X)
            .ToList();
        
        // Position nodes in grid
        for (int i = 0; i < sortedNodes.Count; i++)
        {
            var node = sortedNodes[i];
            int row = i / cols;
            int col = i % cols;
            
            // Center node in its cell
            var cellX = minX + col * cellWidth;
            var cellY = minY + row * cellHeight;
            
            node.X = cellX + (cellWidth - node.Width) / 2;
            node.Y = cellY + (cellHeight - node.Height) / 2;
        }
        
        // Recalculate edge paths for affected nodes
        foreach (var node in selected)
        {
            RecalculateEdgePaths(node.Id);
        }
        
        StateHasChanged();
    }
}

/// <summary>
/// Editable theme data for the theme configurator UI
/// </summary>
public class EditableThemeData
{
    public string? Name { get; set; }
    public string? Description { get; set; }

    // DFD Node Colors
    public string? ProcessFill { get; set; }
    public string? ProcessStroke { get; set; }
    public string? DecisionFill { get; set; }
    public string? DecisionStroke { get; set; }
    public string? ExternalEntityFill { get; set; }
    public string? ExternalEntityStroke { get; set; }
    public string? DataStoreFill { get; set; }
    public string? DataStoreStroke { get; set; }
    public string? NodeFill { get; set; }
    public string? NodeStroke { get; set; }

    // Edge Colors
    public string? EdgeStroke { get; set; }
    public string? EdgeArrow { get; set; }

    // Canvas Colors
    public string? CanvasBackground { get; set; }
    public string? CanvasGrid { get; set; }

    // Project colors
    public string? TaskFill { get; set; }
    public string? TaskStroke { get; set; }
    public string? MilestoneFill { get; set; }
    public string? CriticalFill { get; set; }
    public string? GroupFill { get; set; }
    public string? SelectionStroke { get; set; }

    // Calendar Colors
    public string? WeekendFill { get; set; }
    public string? SaturdayFill { get; set; }
    public string? SundayFill { get; set; }
    public string? HolidayFill { get; set; }
    public string? ChristmasFill { get; set; }
    public string? NewYearFill { get; set; }
    public string? CompanyEventFill { get; set; }
    public string? VacationFill { get; set; }

    // Project Row Colors
    public string? RowColor1 { get; set; }
    public string? RowColor2 { get; set; }
    public string? RowColor3 { get; set; }
    public string? RowColor4 { get; set; }
    public string? RowColor5 { get; set; }
    public string? RowColor6 { get; set; }
    public string? RowColor7 { get; set; }
    public string? RowColor8 { get; set; }
    public string? RowColor9 { get; set; }
    public string? RowColor10 { get; set; }
    public string? GroupRowColor { get; set; }
}

