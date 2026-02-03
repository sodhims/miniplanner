using System.Text.Json;
using System.Text.Json.Serialization;

namespace dfd2wasm.Models;

/// <summary>
/// Types of actions that can be recorded
/// </summary>
public enum RecordedActionType
{
    // Node operations
    NodeCreated,
    NodeMoved,
    NodeResized,
    NodeDeleted,
    NodeTextChanged,
    NodeStyleChanged,

    // Edge operations
    EdgeCreated,
    EdgeDeleted,
    EdgeStyleChanged,
    EdgeWaypointAdded,
    EdgeWaypointMoved,

    // Label operations
    LabelCreated,
    LabelMoved,
    LabelDeleted,
    LabelTextChanged,

    // Freehand stroke operations
    StrokeCreated,
    StrokeDeleted,

    // Drawing shape operations
    ShapeCreated,
    ShapeMoved,
    ShapeDeleted,

    // Selection and mode
    SelectionChanged,
    ModeChanged,

    // Bulk operations
    FullStateRestore
}

/// <summary>
/// A single recorded action with timestamp and data
/// </summary>
public class RecordedAction
{
    /// <summary>
    /// Sequence number of this action (1-based)
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Milliseconds from recording start when this action occurred
    /// </summary>
    public long TimestampMs { get; set; }

    /// <summary>
    /// Type of action performed
    /// </summary>
    public RecordedActionType Type { get; set; }

    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Action-specific payload data (JSON element for flexibility)
    /// </summary>
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Complete editor state after this action (for accurate replay)
    /// </summary>
    public EditorState? StateAfter { get; set; }
}

/// <summary>
/// A complete recording session
/// </summary>
public class Recording
{
    /// <summary>
    /// Recording format version
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Unique identifier for this recording
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// User-provided name for the recording
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// When the recording was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total duration of the recording in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// State of the editor when recording started
    /// </summary>
    public EditorState InitialState { get; set; } = new();

    /// <summary>
    /// List of recorded actions in chronological order
    /// </summary>
    public List<RecordedAction> Actions { get; set; } = new();
}

/// <summary>
/// Options for JSON serialization of recordings
/// </summary>
public static class RecordingJsonOptions
{
    public static JsonSerializerOptions Default => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
