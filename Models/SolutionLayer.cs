using System.Text.Json.Serialization;

namespace dfd2wasm.Models;

/// <summary>
/// Represents a solution layer containing schedule/layout overrides.
/// Layers store only deltas from the base diagram, not full copies.
/// Works across Gantt, Project, and other diagram types.
/// </summary>
public class SolutionLayer
{
    /// <summary>Unique identifier for this layer</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>User-friendly name for this layer</summary>
    public string Name { get; set; } = "Untitled Layer";

    /// <summary>Description of this solution</summary>
    public string Description { get; set; } = "";

    /// <summary>Solver/algorithm that created this layer (SPT, LPT, CPM, ASAP, ALAP, Manual)</summary>
    public string SolverType { get; set; } = "Manual";

    /// <summary>When this layer was created</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this layer was last modified</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether this layer is visible in the timeline</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether this layer is currently active for editing (only one at a time)</summary>
    public bool IsActive { get; set; } = false;

    /// <summary>Color for visual distinction in UI and overlays</summary>
    public string LayerColor { get; set; } = "#3b82f6";

    /// <summary>Opacity for overlay rendering (0.0-1.0)</summary>
    public double LayerOpacity { get; set; } = 0.8;

    /// <summary>
    /// Node property overrides - only stores deltas from base values.
    /// Key is node ID, value contains only the properties that differ.
    /// </summary>
    public Dictionary<int, NodeOverride> NodeOverrides { get; set; } = new();

    /// <summary>
    /// Computed metrics for this layer (e.g., Makespan, Utilization, Cost).
    /// Calculated when layer is created or modified.
    /// </summary>
    public Dictionary<string, decimal> ComputedMetrics { get; set; } = new();

    /// <summary>
    /// Template/mode this layer applies to (gantt, project, dfd, etc.)
    /// </summary>
    public string TemplateMode { get; set; } = "gantt";

    /// <summary>
    /// Solver configuration used to create this layer (for reproducibility)
    /// </summary>
    public Dictionary<string, object>? SolverConfig { get; set; }

    /// <summary>Check if this layer has any overrides</summary>
    [JsonIgnore]
    public bool HasOverrides => NodeOverrides.Count > 0;

    /// <summary>Get the count of nodes with overrides</summary>
    [JsonIgnore]
    public int OverrideCount => NodeOverrides.Count;
}

/// <summary>
/// Stores property overrides for a single node.
/// Only non-null properties are applied; null means "use base value".
/// </summary>
public class NodeOverride
{
    /// <summary>Node ID this override applies to</summary>
    public int NodeId { get; set; }

    // ========================================
    // GANTT MODE OVERRIDES
    // ========================================

    /// <summary>Override for task start time (Gantt)</summary>
    public TimeSpan? GanttStartTime { get; set; }

    /// <summary>Override for task duration (Gantt)</summary>
    public TimeSpan? GanttDuration { get; set; }

    /// <summary>Override for machine assignment (Gantt)</summary>
    public int? GanttMachineId { get; set; }

    /// <summary>Override for row index in timeline (Gantt)</summary>
    public int? GanttRowIndex { get; set; }

    /// <summary>Override for job assignment (Gantt)</summary>
    public int? GanttJobId { get; set; }

    // ========================================
    // PROJECT MODE OVERRIDES
    // ========================================

    /// <summary>Override for task start date (Project)</summary>
    public DateTime? ProjectStartDate { get; set; }

    /// <summary>Override for task end date (Project)</summary>
    public DateTime? ProjectEndDate { get; set; }

    /// <summary>Override for task duration in days (Project)</summary>
    public int? ProjectDurationDays { get; set; }

    /// <summary>Override for row index in timeline (Project)</summary>
    public int? ProjectRowIndex { get; set; }

    // ========================================
    // DFD/LAYOUT MODE OVERRIDES
    // ========================================

    /// <summary>Override for X position (layout)</summary>
    public double? X { get; set; }

    /// <summary>Override for Y position (layout)</summary>
    public double? Y { get; set; }

    /// <summary>Override for width (layout)</summary>
    public double? Width { get; set; }

    /// <summary>Override for height (layout)</summary>
    public double? Height { get; set; }

    // ========================================
    // VISUAL OVERRIDES
    // ========================================

    /// <summary>Override for fill color</summary>
    public string? FillColor { get; set; }

    /// <summary>Override for stroke color</summary>
    public string? StrokeColor { get; set; }

    /// <summary>Check if this override has any Gantt changes</summary>
    [JsonIgnore]
    public bool HasGanttChanges =>
        GanttStartTime.HasValue || GanttDuration.HasValue ||
        GanttMachineId.HasValue || GanttRowIndex.HasValue || GanttJobId.HasValue;

    /// <summary>Check if this override has any Project changes</summary>
    [JsonIgnore]
    public bool HasProjectChanges =>
        ProjectStartDate.HasValue || ProjectEndDate.HasValue ||
        ProjectDurationDays.HasValue || ProjectRowIndex.HasValue;

    /// <summary>Check if this override has any layout changes</summary>
    [JsonIgnore]
    public bool HasLayoutChanges =>
        X.HasValue || Y.HasValue || Width.HasValue || Height.HasValue;

    /// <summary>Check if this override has any changes at all</summary>
    [JsonIgnore]
    public bool HasChanges =>
        HasGanttChanges || HasProjectChanges || HasLayoutChanges ||
        FillColor != null || StrokeColor != null;

    /// <summary>Create a deep copy of this override</summary>
    public NodeOverride Clone()
    {
        return new NodeOverride
        {
            NodeId = NodeId,
            GanttStartTime = GanttStartTime,
            GanttDuration = GanttDuration,
            GanttMachineId = GanttMachineId,
            GanttRowIndex = GanttRowIndex,
            GanttJobId = GanttJobId,
            ProjectStartDate = ProjectStartDate,
            ProjectEndDate = ProjectEndDate,
            ProjectDurationDays = ProjectDurationDays,
            ProjectRowIndex = ProjectRowIndex,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            FillColor = FillColor,
            StrokeColor = StrokeColor
        };
    }
}

/// <summary>
/// Snapshot of layer state for undo/redo within a layer
/// </summary>
public class LayerSnapshot
{
    public Dictionary<int, NodeOverride> NodeOverrides { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public LayerSnapshot() { }

    public LayerSnapshot(Dictionary<int, NodeOverride> overrides)
    {
        NodeOverrides = overrides.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Clone()
        );
    }
}

/// <summary>
/// Records a layer operation for global undo
/// </summary>
public class LayerOperation
{
    public LayerOperationType Type { get; set; }
    public string? LayerId { get; set; }
    public SolutionLayer? LayerBackup { get; set; }
    public string? PreviousActiveLayerId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of layer operations that can be undone
/// </summary>
public enum LayerOperationType
{
    Create,
    Delete,
    Switch,
    Rename,
    ToggleVisibility
}

/// <summary>
/// Container for all layer state management
/// </summary>
public class LayerState
{
    /// <summary>All solution layers</summary>
    public List<SolutionLayer> Layers { get; set; } = new();

    /// <summary>Currently active layer ID (null = editing base)</summary>
    public string? ActiveLayerId { get; set; }

    /// <summary>Per-layer undo stacks</summary>
    public Dictionary<string, Stack<LayerSnapshot>> UndoStacks { get; set; } = new();

    /// <summary>Per-layer redo stacks</summary>
    public Dictionary<string, Stack<LayerSnapshot>> RedoStacks { get; set; } = new();

    /// <summary>Global undo stack for layer operations</summary>
    public Stack<LayerOperation> LayerOperationHistory { get; set; } = new();

    /// <summary>Global redo stack for layer operations</summary>
    public Stack<LayerOperation> LayerOperationRedoHistory { get; set; } = new();

    /// <summary>Maximum undo steps per layer</summary>
    public int MaxUndoSteps { get; set; } = 50;
}
