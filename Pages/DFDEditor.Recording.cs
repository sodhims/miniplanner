using dfd2wasm.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Text.Json;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    /// <summary>
    /// Deep copy helper using JSON serialization
    /// </summary>
    private static T DeepCopy<T>(T obj)
    {
        if (obj == null) return default!;
        var json = JsonSerializer.Serialize(obj, RecordingJsonOptions.Default);
        return JsonSerializer.Deserialize<T>(json, RecordingJsonOptions.Default)!;
    }

    /// <summary>
    /// Get the current editor state for recording
    /// </summary>
    private EditorState GetCurrentEditorState()
    {
        return new EditorState
        {
            Nodes = DeepCopy(nodes.ToList()),
            Edges = DeepCopy(edges.ToList()),
            EdgeLabels = DeepCopy(edgeLabels.ToList()),
            FreehandStrokes = DeepCopy(freehandStrokes.ToList()),
            DrawingShapes = DeepCopy(drawingShapes.ToList())
        };
    }

    /// <summary>
    /// Apply an editor state (for playback)
    /// </summary>
    private void ApplyEditorState(EditorState state)
    {
        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        freehandStrokes.Clear();
        drawingShapes.Clear();

        nodes.AddRange(DeepCopy(state.Nodes));
        edges.AddRange(DeepCopy(state.Edges));
        edgeLabels.AddRange(DeepCopy(state.EdgeLabels));
        freehandStrokes.AddRange(DeepCopy(state.FreehandStrokes));
        drawingShapes.AddRange(DeepCopy(state.DrawingShapes));

        // Update ID counters
        if (nodes.Any()) nextId = nodes.Max(n => n.Id) + 1;
        if (edges.Any()) nextEdgeId = edges.Max(e => e.Id) + 1;
        if (edgeLabels.Any()) nextLabelId = edgeLabels.Max(l => l.Id) + 1;
        if (freehandStrokes.Any()) nextStrokeId = freehandStrokes.Max(s => s.Id) + 1;
        if (drawingShapes.Any()) nextShapeId = drawingShapes.Max(s => s.Id) + 1;

        // Clear selections
        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();
        selectedStrokes.Clear();
        selectedDrawingShapes.Clear();

        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Start a new recording session (simplified like vecsketch)
    /// </summary>
    private void StartRecording()
    {
        currentRecording = new Recording
        {
            Name = $"Recording {DateTime.Now:yyyy-MM-dd HH:mm}",
            InitialState = GetCurrentEditorState()
        };
        recordingStopwatch = Stopwatch.StartNew();
        isRecording = true;
        playbackIndex = 0;
        Console.WriteLine("Recording started");
        StateHasChanged();
    }

    /// <summary>
    /// Stop the current recording session
    /// </summary>
    private void StopRecording()
    {
        if (!isRecording || currentRecording == null) return;

        recordingStopwatch?.Stop();
        currentRecording.DurationMs = recordingStopwatch?.ElapsedMilliseconds ?? 0;
        isRecording = false;
        recordingName = currentRecording.Name ?? "Recording";
        Console.WriteLine($"Recording stopped. Actions: {currentRecording.Actions.Count}");
        StateHasChanged();
    }

    /// <summary>
    /// Record an action if recording is active (simplified inline approach)
    /// </summary>
    private void RecordAction(RecordedActionType type, string? description, object? data = null)
    {
        if (!isRecording || currentRecording == null || recordingStopwatch == null) return;

        try
        {
            var action = new RecordedAction
            {
                Sequence = currentRecording.Actions.Count + 1,
                TimestampMs = recordingStopwatch.ElapsedMilliseconds,
                Type = type,
                Description = description,
                StateAfter = GetCurrentEditorState()
            };

            currentRecording.Actions.Add(action);
            Console.WriteLine($"Recorded: [{action.Sequence}] {type} - {description} @ {action.TimestampMs}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error recording action: {ex.Message}");
        }
    }

    /// <summary>
    /// Open the recording dialog
    /// </summary>
    private void OpenRecordingDialog()
    {
        showRecordingDialog = true;
        StateHasChanged();
    }

    /// <summary>
    /// Close the recording dialog
    /// </summary>
    private void CloseRecordingDialog()
    {
        showRecordingDialog = false;
        StateHasChanged();
    }

    /// <summary>
    /// Save the current recording as JSON file
    /// </summary>
    private async Task SaveRecording()
    {
        if (currentRecording == null) return;

        currentRecording.Name = recordingName;
        var json = RecordingService.ExportToJson(currentRecording);
        var filename = $"{SanitizeFilename(recordingName)}.recording.json";

        await JS.InvokeVoidAsync("downloadFile", filename, json);
    }

    /// <summary>
    /// Load a recording from file
    /// </summary>
    private async Task LoadRecording(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB max
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            currentRecording = RecordingService.ImportFromJson(json);
            recordingName = currentRecording.Name ?? "Loaded Recording";
            playbackIndex = 0;
            isPlayingRecording = false;
            isRecording = false;

            Console.WriteLine($"Loaded recording: {currentRecording.Actions.Count} actions, {currentRecording.DurationMs}ms duration");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load recording: {ex.Message}");
        }
    }

    /// <summary>
    /// Start auto-playback from the beginning
    /// </summary>
    private async Task PlayFromStart()
    {
        if (currentRecording == null || isPlayingRecording) return;

        Console.WriteLine($"PlayFromStart: speed={playbackSpeed}, actions={currentRecording.Actions.Count}");

        isPlayingRecording = true;
        playbackIndex = 0;
        playbackCts = new CancellationTokenSource();

        // Apply initial state
        ApplyEditorState(currentRecording.InitialState);

        try
        {
            long lastTimestamp = 0;

            for (int i = 0; i < currentRecording.Actions.Count; i++)
            {
                if (playbackCts.Token.IsCancellationRequested) break;

                var action = currentRecording.Actions[i];

                // Wait for time delta between actions (use current speed value for dynamic adjustment)
                var currentSpeed = playbackSpeed > 0 ? playbackSpeed : 1.0;
                var delay = (int)((action.TimestampMs - lastTimestamp) / currentSpeed);
                if (delay > 0)
                {
                    await Task.Delay(Math.Min(delay, 2000), playbackCts.Token); // Cap at 2s
                }

                // Apply the state
                if (action.StateAfter != null)
                {
                    ApplyEditorState(action.StateAfter);
                }

                lastTimestamp = action.TimestampMs;
                playbackIndex = i + 1;
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // Playback was cancelled
        }
        finally
        {
            isPlayingRecording = false;
            playbackCts = null;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Pause current playback
    /// </summary>
    private void PausePlayback()
    {
        playbackCts?.Cancel();
        isPlayingRecording = false;
        StateHasChanged();
    }

    /// <summary>
    /// Stop playback and reset to initial state
    /// </summary>
    private void ResetPlayback()
    {
        playbackCts?.Cancel();
        isPlayingRecording = false;

        if (currentRecording != null)
        {
            ApplyEditorState(currentRecording.InitialState);
            playbackIndex = 0;
        }
        StateHasChanged();
    }

    /// <summary>
    /// Step forward one action (step mode)
    /// </summary>
    private void PlaybackStepForward()
    {
        if (currentRecording == null) return;
        if (playbackIndex >= currentRecording.Actions.Count) return;

        var action = currentRecording.Actions[playbackIndex];
        if (action.StateAfter != null)
        {
            ApplyEditorState(action.StateAfter);
        }
        playbackIndex++;
        StateHasChanged();
    }

    /// <summary>
    /// Step backward one action (step mode)
    /// </summary>
    private void PlaybackStepBackward()
    {
        if (currentRecording == null) return;
        if (playbackIndex <= 0)
        {
            ApplyEditorState(currentRecording.InitialState);
            playbackIndex = 0;
            StateHasChanged();
            return;
        }

        playbackIndex--;
        if (playbackIndex == 0)
        {
            ApplyEditorState(currentRecording.InitialState);
        }
        else
        {
            var action = currentRecording.Actions[playbackIndex - 1];
            if (action.StateAfter != null)
            {
                ApplyEditorState(action.StateAfter);
            }
        }
        StateHasChanged();
    }

    /// <summary>
    /// Jump to start of recording
    /// </summary>
    private void PlaybackJumpToStart()
    {
        if (currentRecording == null) return;

        ApplyEditorState(currentRecording.InitialState);
        playbackIndex = 0;
        StateHasChanged();
    }

    /// <summary>
    /// Jump to end of recording
    /// </summary>
    private void PlaybackJumpToEnd()
    {
        if (currentRecording == null || currentRecording.Actions.Count == 0) return;

        var lastAction = currentRecording.Actions[^1];
        if (lastAction.StateAfter != null)
        {
            ApplyEditorState(lastAction.StateAfter);
        }
        playbackIndex = currentRecording.Actions.Count;
        StateHasChanged();
    }

    /// <summary>
    /// Clear the current recording
    /// </summary>
    private void ClearCurrentRecording()
    {
        currentRecording = null;
        playbackIndex = 0;
        isPlayingRecording = false;
        isRecording = false;
        recordingName = "";
        recordingStopwatch = null;
        StateHasChanged();
    }

    /// <summary>
    /// Sanitize a string for use as a filename
    /// </summary>
    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Clear all elements from the canvas
    /// </summary>
    private void ClearCanvas()
    {
        UndoService.SaveState(nodes, edges, edgeLabels, freehandStrokes);

        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        freehandStrokes.Clear();
        drawingShapes.Clear();

        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();
        selectedStrokes.Clear();
        selectedDrawingShapes.Clear();

        // Reset ID counters
        nextId = 1;
        nextEdgeId = 1;
        nextLabelId = 1;
        nextStrokeId = 1;
        nextShapeId = 1;

        Console.WriteLine("Canvas cleared");
        StateHasChanged();
    }

    /// <summary>
    /// Jump to a specific action in the recording
    /// </summary>
    private void JumpToAction(int actionIndex)
    {
        if (currentRecording == null) return;
        if (actionIndex < 0 || actionIndex >= currentRecording.Actions.Count) return;

        // Apply the state at this action
        var action = currentRecording.Actions[actionIndex];
        if (action.StateAfter != null)
        {
            ApplyEditorState(action.StateAfter);
        }
        playbackIndex = actionIndex + 1;
        StateHasChanged();
    }

    /// <summary>
    /// Get an SVG icon for the action type
    /// </summary>
    private static string GetActionIcon(RecordedActionType type)
    {
        const string svgStart = "<svg width=\"14\" height=\"14\" viewBox=\"0 0 14 14\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\">";
        const string svgEnd = "</svg>";

        var iconPath = type switch
        {
            // Node icons - rectangle shape
            RecordedActionType.NodeCreated =>
                "<rect x=\"2\" y=\"2\" width=\"10\" height=\"10\" rx=\"1\" stroke=\"#3b82f6\" stroke-width=\"1.5\" fill=\"none\"/><line x1=\"7\" y1=\"4\" x2=\"7\" y2=\"10\" stroke=\"#22c55e\" stroke-width=\"1.5\"/><line x1=\"4\" y1=\"7\" x2=\"10\" y2=\"7\" stroke=\"#22c55e\" stroke-width=\"1.5\"/>",
            RecordedActionType.NodeMoved =>
                "<rect x=\"3\" y=\"3\" width=\"8\" height=\"8\" rx=\"1\" stroke=\"#3b82f6\" stroke-width=\"1.5\" fill=\"none\"/><path d=\"M7 2L7 1M7 13L7 12M2 7L1 7M13 7L12 7\" stroke=\"#6b7280\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>",
            RecordedActionType.NodeResized =>
                "<rect x=\"2\" y=\"2\" width=\"10\" height=\"10\" rx=\"1\" stroke=\"#3b82f6\" stroke-width=\"1.5\" fill=\"none\" stroke-dasharray=\"2 1\"/><path d=\"M9 9L12 12M12 9L12 12L9 12\" stroke=\"#f59e0b\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>",
            RecordedActionType.NodeDeleted =>
                "<rect x=\"2\" y=\"2\" width=\"10\" height=\"10\" rx=\"1\" stroke=\"#ef4444\" stroke-width=\"1.5\" fill=\"none\"/><line x1=\"5\" y1=\"5\" x2=\"9\" y2=\"9\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><line x1=\"9\" y1=\"5\" x2=\"5\" y2=\"9\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>",
            RecordedActionType.NodeTextChanged =>
                "<rect x=\"2\" y=\"2\" width=\"10\" height=\"10\" rx=\"1\" stroke=\"#3b82f6\" stroke-width=\"1.5\" fill=\"none\"/><text x=\"7\" y=\"10\" font-size=\"8\" fill=\"#6b7280\" text-anchor=\"middle\" font-family=\"sans-serif\">T</text>",
            RecordedActionType.NodeStyleChanged =>
                "<rect x=\"2\" y=\"2\" width=\"10\" height=\"10\" rx=\"1\" stroke=\"#a855f7\" stroke-width=\"1.5\" fill=\"#f3e8ff\"/>",

            // Edge icons - arrow/line shape
            RecordedActionType.EdgeCreated =>
                "<line x1=\"2\" y1=\"7\" x2=\"10\" y2=\"7\" stroke=\"#3b82f6\" stroke-width=\"1.5\"/><path d=\"M8 4L11 7L8 10\" stroke=\"#22c55e\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/>",
            RecordedActionType.EdgeDeleted =>
                "<line x1=\"2\" y1=\"7\" x2=\"10\" y2=\"7\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-dasharray=\"2 1\"/><line x1=\"5\" y1=\"5\" x2=\"9\" y2=\"9\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><line x1=\"9\" y1=\"5\" x2=\"5\" y2=\"9\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>",
            RecordedActionType.EdgeStyleChanged =>
                "<line x1=\"2\" y1=\"7\" x2=\"10\" y2=\"7\" stroke=\"#a855f7\" stroke-width=\"2\"/><path d=\"M8 4L11 7L8 10\" stroke=\"#a855f7\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/>",
            RecordedActionType.EdgeWaypointAdded =>
                "<polyline points=\"2,10 7,4 12,10\" stroke=\"#3b82f6\" stroke-width=\"1.5\" fill=\"none\"/><circle cx=\"7\" cy=\"4\" r=\"2\" fill=\"#22c55e\" stroke=\"none\"/>",
            RecordedActionType.EdgeWaypointMoved =>
                "<polyline points=\"2,10 7,4 12,10\" stroke=\"#3b82f6\" stroke-width=\"1.5\" fill=\"none\"/><circle cx=\"7\" cy=\"4\" r=\"2\" fill=\"#f59e0b\" stroke=\"none\"/>",

            // Label icons - tag shape
            RecordedActionType.LabelCreated =>
                "<rect x=\"2\" y=\"4\" width=\"10\" height=\"6\" rx=\"1\" stroke=\"#6b7280\" stroke-width=\"1\" fill=\"#fef3c7\"/><text x=\"7\" y=\"9\" font-size=\"6\" fill=\"#6b7280\" text-anchor=\"middle\" font-family=\"sans-serif\">A</text>",
            RecordedActionType.LabelMoved =>
                "<rect x=\"3\" y=\"5\" width=\"8\" height=\"4\" rx=\"1\" stroke=\"#6b7280\" stroke-width=\"1\" fill=\"#fef3c7\"/><path d=\"M7 2L7 1M7 13L7 12\" stroke=\"#6b7280\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>",
            RecordedActionType.LabelDeleted =>
                "<rect x=\"2\" y=\"4\" width=\"10\" height=\"6\" rx=\"1\" stroke=\"#ef4444\" stroke-width=\"1\" fill=\"none\"/><line x1=\"5\" y1=\"5\" x2=\"9\" y2=\"9\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>",
            RecordedActionType.LabelTextChanged =>
                "<rect x=\"2\" y=\"4\" width=\"10\" height=\"6\" rx=\"1\" stroke=\"#6b7280\" stroke-width=\"1\" fill=\"#fef3c7\"/><text x=\"7\" y=\"9\" font-size=\"6\" fill=\"#3b82f6\" text-anchor=\"middle\" font-family=\"sans-serif\">T</text>",

            // Stroke icons - freehand line
            RecordedActionType.StrokeCreated =>
                "<path d=\"M2 10Q5 2 7 7Q9 12 12 4\" stroke=\"#10b981\" stroke-width=\"1.5\" fill=\"none\" stroke-linecap=\"round\"/>",
            RecordedActionType.StrokeDeleted =>
                "<path d=\"M2 10Q5 2 7 7Q9 12 12 4\" stroke=\"#ef4444\" stroke-width=\"1.5\" fill=\"none\" stroke-linecap=\"round\" stroke-dasharray=\"2 1\"/>",

            // Shape icons - geometric shape
            RecordedActionType.ShapeCreated =>
                "<rect x=\"3\" y=\"3\" width=\"8\" height=\"8\" stroke=\"#8b5cf6\" stroke-width=\"1.5\" fill=\"#ede9fe\"/>",
            RecordedActionType.ShapeMoved =>
                "<rect x=\"4\" y=\"4\" width=\"6\" height=\"6\" stroke=\"#8b5cf6\" stroke-width=\"1.5\" fill=\"none\"/><path d=\"M7 1L7 2M7 12L7 13M1 7L2 7M12 7L13 7\" stroke=\"#6b7280\" stroke-width=\"1\" stroke-linecap=\"round\"/>",
            RecordedActionType.ShapeDeleted =>
                "<rect x=\"3\" y=\"3\" width=\"8\" height=\"8\" stroke=\"#ef4444\" stroke-width=\"1.5\" fill=\"none\"/><line x1=\"5\" y1=\"5\" x2=\"9\" y2=\"9\" stroke=\"#ef4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/>",

            // Selection icon - checkbox
            RecordedActionType.SelectionChanged =>
                "<rect x=\"2\" y=\"2\" width=\"10\" height=\"10\" rx=\"1\" stroke=\"#6b7280\" stroke-width=\"1.5\" fill=\"none\"/><path d=\"M4 7L6 9L10 5\" stroke=\"#3b82f6\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/>",

            // Mode icon - toggle/switch
            RecordedActionType.ModeChanged =>
                "<rect x=\"1\" y=\"4\" width=\"12\" height=\"6\" rx=\"3\" stroke=\"#6b7280\" stroke-width=\"1\" fill=\"#e5e7eb\"/><circle cx=\"10\" cy=\"7\" r=\"2\" fill=\"#3b82f6\"/>",

            // Restore icon - refresh arrows
            RecordedActionType.FullStateRestore =>
                "<path d=\"M2 7a5 5 0 0 1 8.5-3.5M12 7a5 5 0 0 1-8.5 3.5\" stroke=\"#10b981\" stroke-width=\"1.5\" stroke-linecap=\"round\" fill=\"none\"/><path d=\"M10.5 2v2h2M3.5 12v-2h-2\" stroke=\"#10b981\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/>",

            _ => "<circle cx=\"7\" cy=\"7\" r=\"2\" fill=\"#6b7280\"/>"
        };

        return svgStart + iconPath + svgEnd;
    }

    /// <summary>
    /// Get a short display name for the action type
    /// </summary>
    private static string GetActionTypeName(RecordedActionType type)
    {
        return type switch
        {
            RecordedActionType.NodeCreated => "Node +",
            RecordedActionType.NodeMoved => "Move",
            RecordedActionType.NodeResized => "Resize",
            RecordedActionType.NodeDeleted => "Delete",
            RecordedActionType.NodeTextChanged => "Edit",
            RecordedActionType.NodeStyleChanged => "Style",
            RecordedActionType.EdgeCreated => "Edge +",
            RecordedActionType.EdgeDeleted => "Delete",
            RecordedActionType.EdgeStyleChanged => "Style",
            RecordedActionType.EdgeWaypointAdded => "Waypoint",
            RecordedActionType.EdgeWaypointMoved => "Waypoint",
            RecordedActionType.LabelCreated => "Label +",
            RecordedActionType.LabelMoved => "Move",
            RecordedActionType.LabelDeleted => "Delete",
            RecordedActionType.LabelTextChanged => "Edit",
            RecordedActionType.StrokeCreated => "Stroke +",
            RecordedActionType.StrokeDeleted => "Delete",
            RecordedActionType.ShapeCreated => "Shape +",
            RecordedActionType.ShapeMoved => "Move",
            RecordedActionType.ShapeDeleted => "Delete",
            RecordedActionType.SelectionChanged => "Select",
            RecordedActionType.ModeChanged => "Mode",
            RecordedActionType.FullStateRestore => "Restore",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Format time in milliseconds to a readable string
    /// </summary>
    private static string FormatTime(long ms)
    {
        if (ms < 1000)
            return $"{ms}ms";
        return $"{ms / 1000.0:F1}s";
    }
}
