using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for managing solution layers - creating, editing, and applying
/// schedule/layout overrides on top of base diagram data.
/// </summary>
public class SolutionLayerService
{
    private readonly LayerState _state = new();

    // Layer colors for auto-assignment
    private static readonly string[] LayerColors = new[]
    {
        "#3b82f6", // Blue
        "#22c55e", // Green
        "#f59e0b", // Amber
        "#ef4444", // Red
        "#8b5cf6", // Purple
        "#06b6d4", // Cyan
        "#ec4899", // Pink
        "#84cc16", // Lime
        "#f97316", // Orange
        "#6366f1"  // Indigo
    };

    /// <summary>Event raised when layers change</summary>
    public event Action? OnLayersChanged;

    /// <summary>Event raised when active layer changes</summary>
    public event Action<string?>? OnActiveLayerChanged;

    // ========================================
    // LAYER CRUD OPERATIONS
    // ========================================

    /// <summary>Get all layers</summary>
    public IReadOnlyList<SolutionLayer> GetLayers() => _state.Layers.AsReadOnly();

    /// <summary>Get visible layers ordered by creation time</summary>
    public IEnumerable<SolutionLayer> GetVisibleLayers() =>
        _state.Layers.Where(l => l.IsVisible).OrderBy(l => l.CreatedAt);

    /// <summary>Get the currently active layer (null if editing base)</summary>
    public SolutionLayer? GetActiveLayer() =>
        _state.ActiveLayerId != null
            ? _state.Layers.FirstOrDefault(l => l.Id == _state.ActiveLayerId)
            : null;

    /// <summary>Get active layer ID</summary>
    public string? ActiveLayerId => _state.ActiveLayerId;

    /// <summary>Get a layer by ID</summary>
    public SolutionLayer? GetLayer(string layerId) =>
        _state.Layers.FirstOrDefault(l => l.Id == layerId);

    /// <summary>Create a new layer with optional overrides</summary>
    public SolutionLayer CreateLayer(
        string name,
        string solverType,
        string templateMode,
        Dictionary<int, NodeOverride>? overrides = null)
    {
        var layer = new SolutionLayer
        {
            Name = name,
            SolverType = solverType,
            TemplateMode = templateMode,
            LayerColor = GetNextLayerColor(),
            NodeOverrides = overrides ?? new Dictionary<int, NodeOverride>()
        };

        // Record operation for undo
        RecordLayerOperation(new LayerOperation
        {
            Type = LayerOperationType.Create,
            LayerId = layer.Id
        });

        _state.Layers.Add(layer);

        // Initialize undo stack for this layer
        _state.UndoStacks[layer.Id] = new Stack<LayerSnapshot>();
        _state.RedoStacks[layer.Id] = new Stack<LayerSnapshot>();

        OnLayersChanged?.Invoke();
        return layer;
    }

    /// <summary>Delete a layer by ID</summary>
    public bool DeleteLayer(string layerId)
    {
        var layer = _state.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null) return false;

        // Record operation for undo (backup the layer)
        RecordLayerOperation(new LayerOperation
        {
            Type = LayerOperationType.Delete,
            LayerId = layerId,
            LayerBackup = CloneLayer(layer),
            PreviousActiveLayerId = _state.ActiveLayerId
        });

        _state.Layers.Remove(layer);

        // Clean up undo stacks
        _state.UndoStacks.Remove(layerId);
        _state.RedoStacks.Remove(layerId);

        // If deleted layer was active, deactivate
        if (_state.ActiveLayerId == layerId)
        {
            _state.ActiveLayerId = null;
            OnActiveLayerChanged?.Invoke(null);
        }

        OnLayersChanged?.Invoke();
        return true;
    }

    /// <summary>Set the active layer for editing</summary>
    public void SetActiveLayer(string? layerId)
    {
        var previousActiveId = _state.ActiveLayerId;

        // Deactivate current active layer
        var currentActive = GetActiveLayer();
        if (currentActive != null)
        {
            currentActive.IsActive = false;
        }

        // Activate new layer
        if (layerId != null)
        {
            var newActive = _state.Layers.FirstOrDefault(l => l.Id == layerId);
            if (newActive != null)
            {
                newActive.IsActive = true;
                _state.ActiveLayerId = layerId;
            }
            else
            {
                _state.ActiveLayerId = null;
            }
        }
        else
        {
            _state.ActiveLayerId = null;
        }

        // Record operation for undo
        if (previousActiveId != _state.ActiveLayerId)
        {
            RecordLayerOperation(new LayerOperation
            {
                Type = LayerOperationType.Switch,
                LayerId = _state.ActiveLayerId,
                PreviousActiveLayerId = previousActiveId
            });

            OnActiveLayerChanged?.Invoke(_state.ActiveLayerId);
            OnLayersChanged?.Invoke();
        }
    }

    /// <summary>Toggle layer visibility</summary>
    public void ToggleLayerVisibility(string layerId)
    {
        var layer = _state.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer != null)
        {
            layer.IsVisible = !layer.IsVisible;

            RecordLayerOperation(new LayerOperation
            {
                Type = LayerOperationType.ToggleVisibility,
                LayerId = layerId
            });

            OnLayersChanged?.Invoke();
        }
    }

    /// <summary>Rename a layer</summary>
    public void RenameLayer(string layerId, string newName)
    {
        var layer = _state.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer != null && layer.Name != newName)
        {
            var oldName = layer.Name;
            layer.Name = newName;
            layer.ModifiedAt = DateTime.UtcNow;

            OnLayersChanged?.Invoke();
        }
    }

    /// <summary>Update layer color</summary>
    public void SetLayerColor(string layerId, string color)
    {
        var layer = _state.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer != null)
        {
            layer.LayerColor = color;
            layer.ModifiedAt = DateTime.UtcNow;
            OnLayersChanged?.Invoke();
        }
    }

    // ========================================
    // DELTA APPLICATION
    // ========================================

    /// <summary>
    /// Apply layer overrides to a base node, returning effective values.
    /// Does NOT modify the original node.
    /// </summary>
    public Node GetEffectiveNode(Node baseNode, IEnumerable<SolutionLayer>? visibleLayers = null)
    {
        var layers = visibleLayers ?? GetVisibleLayers();

        // Create a shallow copy with base values
        var effective = ShallowCopyNode(baseNode);

        // Apply each visible layer's overrides in order
        foreach (var layer in layers.OrderBy(l => l.CreatedAt))
        {
            if (layer.NodeOverrides.TryGetValue(baseNode.Id, out var nodeOverride))
            {
                ApplyOverrideToNode(effective, nodeOverride);
            }
        }

        return effective;
    }

    /// <summary>Get the override for a specific node in the active layer</summary>
    public NodeOverride? GetActiveLayerOverride(int nodeId)
    {
        var activeLayer = GetActiveLayer();
        if (activeLayer == null) return null;

        activeLayer.NodeOverrides.TryGetValue(nodeId, out var nodeOverride);
        return nodeOverride;
    }

    /// <summary>Set or update an override in the active layer</summary>
    public void SetOverride(int nodeId, NodeOverride nodeOverride)
    {
        var activeLayer = GetActiveLayer();
        if (activeLayer == null) return;

        // Save state for undo
        SaveLayerState(activeLayer.Id);

        nodeOverride.NodeId = nodeId;
        activeLayer.NodeOverrides[nodeId] = nodeOverride;
        activeLayer.ModifiedAt = DateTime.UtcNow;

        OnLayersChanged?.Invoke();
    }

    /// <summary>Update specific Gantt properties in the active layer</summary>
    public void SetGanttOverride(int nodeId, TimeSpan? startTime = null, TimeSpan? duration = null, int? machineId = null)
    {
        var activeLayer = GetActiveLayer();
        if (activeLayer == null) return;

        SaveLayerState(activeLayer.Id);

        if (!activeLayer.NodeOverrides.TryGetValue(nodeId, out var nodeOverride))
        {
            nodeOverride = new NodeOverride { NodeId = nodeId };
            activeLayer.NodeOverrides[nodeId] = nodeOverride;
        }

        if (startTime.HasValue) nodeOverride.GanttStartTime = startTime;
        if (duration.HasValue) nodeOverride.GanttDuration = duration;
        if (machineId.HasValue) nodeOverride.GanttMachineId = machineId;

        activeLayer.ModifiedAt = DateTime.UtcNow;
        OnLayersChanged?.Invoke();
    }

    /// <summary>Update specific Project properties in the active layer</summary>
    public void SetProjectOverride(int nodeId, DateTime? startDate = null, DateTime? endDate = null, int? durationDays = null)
    {
        var activeLayer = GetActiveLayer();
        if (activeLayer == null) return;

        SaveLayerState(activeLayer.Id);

        if (!activeLayer.NodeOverrides.TryGetValue(nodeId, out var nodeOverride))
        {
            nodeOverride = new NodeOverride { NodeId = nodeId };
            activeLayer.NodeOverrides[nodeId] = nodeOverride;
        }

        if (startDate.HasValue) nodeOverride.ProjectStartDate = startDate;
        if (endDate.HasValue) nodeOverride.ProjectEndDate = endDate;
        if (durationDays.HasValue) nodeOverride.ProjectDurationDays = durationDays;

        activeLayer.ModifiedAt = DateTime.UtcNow;
        OnLayersChanged?.Invoke();
    }

    /// <summary>Remove an override from the active layer</summary>
    public void RemoveOverride(int nodeId)
    {
        var activeLayer = GetActiveLayer();
        if (activeLayer == null) return;

        if (activeLayer.NodeOverrides.ContainsKey(nodeId))
        {
            SaveLayerState(activeLayer.Id);
            activeLayer.NodeOverrides.Remove(nodeId);
            activeLayer.ModifiedAt = DateTime.UtcNow;
            OnLayersChanged?.Invoke();
        }
    }

    // ========================================
    // LAYER CREATION FROM SOLVERS
    // ========================================

    /// <summary>Create a layer from Gantt scheduling results</summary>
    public SolutionLayer CreateLayerFromGanttSchedule(
        string name,
        string solverType,
        IEnumerable<Node> originalNodes,
        IEnumerable<Node> scheduledNodes)
    {
        var overrides = new Dictionary<int, NodeOverride>();

        var originalDict = originalNodes.ToDictionary(n => n.Id);
        foreach (var scheduled in scheduledNodes)
        {
            if (!originalDict.TryGetValue(scheduled.Id, out var original))
                continue;

            var nodeOverride = new NodeOverride { NodeId = scheduled.Id };
            var hasChanges = false;

            // Compare Gantt properties
            if (scheduled.GanttStartTime != original.GanttStartTime)
            {
                nodeOverride.GanttStartTime = scheduled.GanttStartTime;
                hasChanges = true;
            }
            if (scheduled.GanttDuration != original.GanttDuration)
            {
                nodeOverride.GanttDuration = scheduled.GanttDuration;
                hasChanges = true;
            }
            if (scheduled.GanttMachineId != original.GanttMachineId)
            {
                nodeOverride.GanttMachineId = scheduled.GanttMachineId;
                hasChanges = true;
            }
            if (scheduled.GanttRowIndex != original.GanttRowIndex)
            {
                nodeOverride.GanttRowIndex = scheduled.GanttRowIndex;
                hasChanges = true;
            }

            if (hasChanges)
            {
                overrides[scheduled.Id] = nodeOverride;
            }
        }

        return CreateLayer(name, solverType, "gantt", overrides);
    }

    /// <summary>Create a layer from Project solver results</summary>
    public SolutionLayer CreateLayerFromProjectSchedule(
        string name,
        string solverType,
        IEnumerable<Node> originalNodes,
        Dictionary<int, DateTime> optimizedSchedule,
        Dictionary<int, int>? optimizedDurations = null)
    {
        var overrides = new Dictionary<int, NodeOverride>();

        var originalDict = originalNodes.ToDictionary(n => n.Id);
        foreach (var (nodeId, newStartDate) in optimizedSchedule)
        {
            if (!originalDict.TryGetValue(nodeId, out var original))
                continue;

            var nodeOverride = new NodeOverride { NodeId = nodeId };
            var hasChanges = false;

            if (original.ProjectStartDate != newStartDate)
            {
                nodeOverride.ProjectStartDate = newStartDate;
                hasChanges = true;
            }

            if (optimizedDurations != null && optimizedDurations.TryGetValue(nodeId, out var newDuration))
            {
                if (original.ProjectDurationDays != newDuration)
                {
                    nodeOverride.ProjectDurationDays = newDuration;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                overrides[nodeId] = nodeOverride;
            }
        }

        return CreateLayer(name, solverType, "project", overrides);
    }

    // ========================================
    // UNDO / REDO
    // ========================================

    /// <summary>Save the current state of a layer for undo</summary>
    public void SaveLayerState(string layerId)
    {
        var layer = GetLayer(layerId);
        if (layer == null) return;

        if (!_state.UndoStacks.TryGetValue(layerId, out var undoStack))
        {
            undoStack = new Stack<LayerSnapshot>();
            _state.UndoStacks[layerId] = undoStack;
        }

        // Create snapshot
        var snapshot = new LayerSnapshot(layer.NodeOverrides);
        undoStack.Push(snapshot);

        // Trim to max size
        while (undoStack.Count > _state.MaxUndoSteps)
        {
            var items = undoStack.ToArray();
            undoStack.Clear();
            for (int i = 0; i < _state.MaxUndoSteps; i++)
            {
                undoStack.Push(items[items.Length - 1 - i]);
            }
        }

        // Clear redo stack when new action is taken
        if (_state.RedoStacks.TryGetValue(layerId, out var redoStack))
        {
            redoStack.Clear();
        }
    }

    /// <summary>Undo the last change in a specific layer</summary>
    public bool UndoLayerChange(string layerId)
    {
        if (!_state.UndoStacks.TryGetValue(layerId, out var undoStack) || undoStack.Count == 0)
            return false;

        var layer = GetLayer(layerId);
        if (layer == null) return false;

        // Save current state to redo stack
        if (!_state.RedoStacks.TryGetValue(layerId, out var redoStack))
        {
            redoStack = new Stack<LayerSnapshot>();
            _state.RedoStacks[layerId] = redoStack;
        }
        redoStack.Push(new LayerSnapshot(layer.NodeOverrides));

        // Restore previous state
        var snapshot = undoStack.Pop();
        layer.NodeOverrides = snapshot.NodeOverrides.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Clone()
        );
        layer.ModifiedAt = DateTime.UtcNow;

        OnLayersChanged?.Invoke();
        return true;
    }

    /// <summary>Redo the last undone change in a specific layer</summary>
    public bool RedoLayerChange(string layerId)
    {
        if (!_state.RedoStacks.TryGetValue(layerId, out var redoStack) || redoStack.Count == 0)
            return false;

        var layer = GetLayer(layerId);
        if (layer == null) return false;

        // Save current state to undo stack
        if (!_state.UndoStacks.TryGetValue(layerId, out var undoStack))
        {
            undoStack = new Stack<LayerSnapshot>();
            _state.UndoStacks[layerId] = undoStack;
        }
        undoStack.Push(new LayerSnapshot(layer.NodeOverrides));

        // Restore redo state
        var snapshot = redoStack.Pop();
        layer.NodeOverrides = snapshot.NodeOverrides.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Clone()
        );
        layer.ModifiedAt = DateTime.UtcNow;

        OnLayersChanged?.Invoke();
        return true;
    }

    /// <summary>Undo a layer operation (create, delete, switch)</summary>
    public bool UndoLayerOperation()
    {
        if (_state.LayerOperationHistory.Count == 0)
            return false;

        var operation = _state.LayerOperationHistory.Pop();
        _state.LayerOperationRedoHistory.Push(operation);

        switch (operation.Type)
        {
            case LayerOperationType.Create:
                // Undo create = delete (without recording)
                var layerToDelete = _state.Layers.FirstOrDefault(l => l.Id == operation.LayerId);
                if (layerToDelete != null)
                {
                    _state.Layers.Remove(layerToDelete);
                    if (_state.ActiveLayerId == operation.LayerId)
                    {
                        _state.ActiveLayerId = null;
                        OnActiveLayerChanged?.Invoke(null);
                    }
                }
                break;

            case LayerOperationType.Delete:
                // Undo delete = restore
                if (operation.LayerBackup != null)
                {
                    _state.Layers.Add(operation.LayerBackup);
                    _state.UndoStacks[operation.LayerBackup.Id] = new Stack<LayerSnapshot>();
                    _state.RedoStacks[operation.LayerBackup.Id] = new Stack<LayerSnapshot>();

                    if (operation.PreviousActiveLayerId == operation.LayerBackup.Id)
                    {
                        SetActiveLayer(operation.LayerBackup.Id);
                    }
                }
                break;

            case LayerOperationType.Switch:
                // Undo switch = switch back
                SetActiveLayerWithoutRecording(operation.PreviousActiveLayerId);
                break;

            case LayerOperationType.ToggleVisibility:
                // Undo toggle = toggle again
                var layer = _state.Layers.FirstOrDefault(l => l.Id == operation.LayerId);
                if (layer != null)
                {
                    layer.IsVisible = !layer.IsVisible;
                }
                break;
        }

        OnLayersChanged?.Invoke();
        return true;
    }

    /// <summary>Check if there are undo operations available</summary>
    public bool CanUndoLayerOperation() => _state.LayerOperationHistory.Count > 0;

    /// <summary>Check if there are undo operations available for a specific layer</summary>
    public bool CanUndoLayerChange(string layerId) =>
        _state.UndoStacks.TryGetValue(layerId, out var stack) && stack.Count > 0;

    // ========================================
    // METRICS
    // ========================================

    /// <summary>Update computed metrics for a layer</summary>
    public void UpdateLayerMetrics(string layerId, Dictionary<string, decimal> metrics)
    {
        var layer = GetLayer(layerId);
        if (layer != null)
        {
            layer.ComputedMetrics = metrics;
            OnLayersChanged?.Invoke();
        }
    }

    /// <summary>Get metric comparison across all visible layers</summary>
    public Dictionary<string, Dictionary<string, decimal>> GetMetricComparison(params string[] metricNames)
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>();

        foreach (var layer in GetVisibleLayers())
        {
            var layerMetrics = new Dictionary<string, decimal>();
            foreach (var metricName in metricNames)
            {
                if (layer.ComputedMetrics.TryGetValue(metricName, out var value))
                {
                    layerMetrics[metricName] = value;
                }
            }
            result[layer.Id] = layerMetrics;
        }

        return result;
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    private string GetNextLayerColor()
    {
        var usedColors = _state.Layers.Select(l => l.LayerColor).ToHashSet();
        return LayerColors.FirstOrDefault(c => !usedColors.Contains(c)) ?? LayerColors[_state.Layers.Count % LayerColors.Length];
    }

    private void RecordLayerOperation(LayerOperation operation)
    {
        _state.LayerOperationHistory.Push(operation);
        _state.LayerOperationRedoHistory.Clear();

        // Trim to reasonable size
        while (_state.LayerOperationHistory.Count > 100)
        {
            var items = _state.LayerOperationHistory.ToArray();
            _state.LayerOperationHistory.Clear();
            for (int i = 0; i < 50; i++)
            {
                _state.LayerOperationHistory.Push(items[items.Length - 1 - i]);
            }
        }
    }

    private void SetActiveLayerWithoutRecording(string? layerId)
    {
        var currentActive = GetActiveLayer();
        if (currentActive != null)
        {
            currentActive.IsActive = false;
        }

        if (layerId != null)
        {
            var newActive = _state.Layers.FirstOrDefault(l => l.Id == layerId);
            if (newActive != null)
            {
                newActive.IsActive = true;
                _state.ActiveLayerId = layerId;
            }
            else
            {
                _state.ActiveLayerId = null;
            }
        }
        else
        {
            _state.ActiveLayerId = null;
        }

        OnActiveLayerChanged?.Invoke(_state.ActiveLayerId);
    }

    private static SolutionLayer CloneLayer(SolutionLayer source)
    {
        return new SolutionLayer
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            SolverType = source.SolverType,
            CreatedAt = source.CreatedAt,
            ModifiedAt = source.ModifiedAt,
            IsVisible = source.IsVisible,
            IsActive = source.IsActive,
            LayerColor = source.LayerColor,
            LayerOpacity = source.LayerOpacity,
            TemplateMode = source.TemplateMode,
            NodeOverrides = source.NodeOverrides.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Clone()
            ),
            ComputedMetrics = new Dictionary<string, decimal>(source.ComputedMetrics)
        };
    }

    private static Node ShallowCopyNode(Node source)
    {
        // Create a new node with the same property values
        // This is a shallow copy - doesn't deep copy lists/references
        return new Node
        {
            Id = source.Id,
            Text = source.Text,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            Shape = source.Shape,
            FillColor = source.FillColor,
            StrokeColor = source.StrokeColor,
            StrokeWidth = source.StrokeWidth,
            TemplateId = source.TemplateId,
            TemplateShapeId = source.TemplateShapeId,
            Icon = source.Icon,
            // Gantt properties
            GanttStartTime = source.GanttStartTime,
            GanttDuration = source.GanttDuration,
            GanttMachineId = source.GanttMachineId,
            GanttJobId = source.GanttJobId,
            GanttRowIndex = source.GanttRowIndex,
            GanttIsViolation = source.GanttIsViolation,
            GanttPercentComplete = source.GanttPercentComplete,
            GanttPriority = source.GanttPriority,
            IsGanttTask = source.IsGanttTask,
            IsGanttJob = source.IsGanttJob,
            IsGanttMachine = source.IsGanttMachine,
            // Project properties
            ProjectStartDate = source.ProjectStartDate,
            ProjectEndDate = source.ProjectEndDate,
            ProjectDurationDays = source.ProjectDurationDays,
            ProjectRowIndex = source.ProjectRowIndex,
            ProjectPercentComplete = source.ProjectPercentComplete,
            ProjectIsMilestone = source.ProjectIsMilestone,
            IsProjectResource = source.IsProjectResource,
            // Other properties...
            IsSuperNode = source.IsSuperNode,
            IsCollapsed = source.IsCollapsed,
            ParentSuperNodeId = source.ParentSuperNodeId
        };
    }

    private static void ApplyOverrideToNode(Node node, NodeOverride nodeOverride)
    {
        // Apply Gantt overrides
        if (nodeOverride.GanttStartTime.HasValue)
            node.GanttStartTime = nodeOverride.GanttStartTime.Value;
        if (nodeOverride.GanttDuration.HasValue)
            node.GanttDuration = nodeOverride.GanttDuration.Value;
        if (nodeOverride.GanttMachineId.HasValue)
            node.GanttMachineId = nodeOverride.GanttMachineId.Value;
        if (nodeOverride.GanttRowIndex.HasValue)
            node.GanttRowIndex = nodeOverride.GanttRowIndex.Value;
        if (nodeOverride.GanttJobId.HasValue)
            node.GanttJobId = nodeOverride.GanttJobId.Value;

        // Apply Project overrides
        if (nodeOverride.ProjectStartDate.HasValue)
            node.ProjectStartDate = nodeOverride.ProjectStartDate.Value;
        if (nodeOverride.ProjectEndDate.HasValue)
            node.ProjectEndDate = nodeOverride.ProjectEndDate.Value;
        if (nodeOverride.ProjectDurationDays.HasValue)
            node.ProjectDurationDays = nodeOverride.ProjectDurationDays.Value;
        if (nodeOverride.ProjectRowIndex.HasValue)
            node.ProjectRowIndex = nodeOverride.ProjectRowIndex.Value;

        // Apply Layout overrides
        if (nodeOverride.X.HasValue)
            node.X = nodeOverride.X.Value;
        if (nodeOverride.Y.HasValue)
            node.Y = nodeOverride.Y.Value;
        if (nodeOverride.Width.HasValue)
            node.Width = nodeOverride.Width.Value;
        if (nodeOverride.Height.HasValue)
            node.Height = nodeOverride.Height.Value;

        // Apply Visual overrides
        if (nodeOverride.FillColor != null)
            node.FillColor = nodeOverride.FillColor;
        if (nodeOverride.StrokeColor != null)
            node.StrokeColor = nodeOverride.StrokeColor;
    }
}
