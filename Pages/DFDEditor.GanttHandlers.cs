using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

/// <summary>
/// Partial class for Gantt machine scheduling mouse handlers and interactions
/// </summary>
public partial class DFDEditor
{
    // ============================================
    // GANTT STATE FIELDS
    // ============================================

    private bool isGanttMode = false;
    private bool ganttTimelineView = true; // true = timeline view, false = node view
    private string ganttViewMode = "timeline"; // "timeline" or "node"
    private GanttTimelineService ganttTimeline = new();
    private GanttSchedulingService ganttScheduling = new();
    private GanttColorService ganttColors = new();
    private GanttColorService ganttColorService = new();

    // Selection state
    private int? selectedGanttTaskId;
    private int? selectedGanttJobId;
    private int? selectedGanttMachineId;

    // Hover state for task tooltip
    private int? hoveredGanttTaskId;

    // Drag state
    private int? draggingGanttTaskId;
    private double ganttDragStartX;
    private TimeSpan? ganttDragOriginalStart;
    private TimeSpan? ganttDragOriginalDuration;
    private bool isResizingGanttTask;
    private bool isResizingGanttFromLeft;

    // Dependency creation state
    private bool isCreatingGanttPrecedence;
    private int? ganttPrecedenceFromTaskId;
    private double ganttRubberbandX;
    private double ganttRubberbandY;

    // Job visibility state - stores IDs of hidden jobs
    private HashSet<int> hiddenGanttJobIds = new();
    private bool showGanttJobFilter = false;

    // Job filter popup position and drag state
    private double ganttFilterX = 350;  // Initial X position
    private double ganttFilterY = 50;   // Initial Y position
    private bool isDraggingFilter = false;
    private double filterDragOffsetX;
    private double filterDragOffsetY;

    // Dashboard state
    private bool showGanttDashboard = false;
    private double ganttDashboardX = 800; // Initial X position
    private double ganttDashboardY = 80;  // Initial Y position
    private bool isDraggingDashboard = false;
    private double dashboardDragOffsetX;
    private double dashboardDragOffsetY;

    // Zoom debouncing state
    private System.Timers.Timer? _ganttZoomDebounceTimer;
    private bool _ganttZoomPending = false;
    private const int ZoomDebounceMs = 50; // Debounce interval in milliseconds

    // Deadline marker state
    private TimeSpan? ganttDeadlineTime = null; // The deadline/max makespan marker time
    private bool isDraggingGanttDeadline = false;
    private double ganttDeadlineDragStartX;

    // Compression mode: "earliest" moves tasks left, "latest" moves tasks right (toward deadline)
    public enum GanttCompressionMode { Earliest, Latest }
    private GanttCompressionMode ganttCompressionMode = GanttCompressionMode.Earliest;

    // ============================================
    // GANTT MODE TOGGLE
    // ============================================

    /// <summary>Toggle Gantt mode on/off or switch between timeline/node view</summary>
    private async Task ToggleGanttMode()
    {
        if (!isGanttMode)
        {
            // Enter Gantt mode
            isGanttMode = true;
            isProjectMode = false; // Ensure Project mode is off
            ganttViewMode = "timeline";
            InitializeGanttView();
            selectedTemplateId = "gantt";
            selectedTemplateShapeId = "task";
            mode = EditorMode.Select; // Start in Select mode for easier task manipulation

            // Auto zoom-fit if there are tasks
            var hasGanttTasks = nodes.Any(n => n.TemplateId == "gantt" && n.IsGanttTask);
            if (hasGanttTasks)
            {
                await GanttZoomToFit();
            }
        }
        else
        {
            // Toggle between timeline and node view
            ganttViewMode = ganttViewMode == "timeline" ? "node" : "timeline";

            if (ganttViewMode == "node")
            {
                LayoutGanttNodesForNodeView();
            }
            else
            {
                UpdateGanttView();
                // Auto zoom-fit when switching to timeline view
                await GanttZoomToFit();
            }
        }
    }

    /// <summary>Exit Gantt mode and return to DFD</summary>
    private void ExitGanttMode()
    {
        isGanttMode = false;
        ganttTimeline = new GanttTimelineService();
        selectedGanttTaskId = null;
        selectedGanttJobId = null;
        selectedGanttMachineId = null;
        draggingGanttTaskId = null;

        // Clean up zoom debounce timer
        _ganttZoomDebounceTimer?.Stop();
        _ganttZoomDebounceTimer?.Dispose();
        _ganttZoomDebounceTimer = null;
    }

    // ============================================
    // GANTT INITIALIZATION
    // ============================================

    /// <summary>Initialize the Gantt timeline view</summary>
    private void InitializeGanttView()
    {
        ganttTimeline = new GanttTimelineService();

        // Set view range based on existing Gantt nodes
        var ganttNodes = nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttTask).ToList();
        if (ganttNodes.Count > 0)
        {
            ganttTimeline.SetViewRangeFromNodes(ganttNodes, TimeSpan.FromMinutes(30));
        }
        else
        {
            // Default 8-hour view
            ganttTimeline.ViewStartTime = TimeSpan.Zero;
            ganttTimeline.ViewEndTime = TimeSpan.FromHours(8);
        }

        UpdateGanttView();
    }

    /// <summary>Update Gantt node positions based on their times</summary>
    private void UpdateGanttView()
    {
        if (ganttTimeline == null) return;

        foreach (var node in nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttTask))
        {
            ganttTimeline.PositionNodeForTimeline(node);

            // Inherit job color if assigned to a job
            if (node.GanttJobId.HasValue)
            {
                var jobNode = nodes.FirstOrDefault(n => n.Id == node.GanttJobId.Value && n.IsGanttJob);
                if (jobNode != null)
                {
                    node.FillColor = jobNode.FillColor ?? jobNode.GanttJobColor;
                    node.StrokeColor = jobNode.StrokeColor ?? GanttColorService.DarkenColor(node.FillColor ?? "#3b82f6");
                }
            }
        }

        // Validate precedences
        var ganttEdges = edges.Where(e =>
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
            return fromNode?.IsGanttTask == true && toNode?.IsGanttTask == true;
        });

        ganttScheduling.ValidateNodePrecedences(nodes.Where(n => n.IsGanttTask), ganttEdges);
    }

    /// <summary>Layout Gantt nodes for node view (compact graph)</summary>
    private void LayoutGanttNodesForNodeView()
    {
        var ganttNodes = nodes.Where(n => n.TemplateId == "gantt").ToList();
        var machineNodes = ganttNodes.Where(n => n.IsGanttMachine).ToList();
        var jobNodes = ganttNodes.Where(n => n.IsGanttJob).ToList();
        var taskNodes = ganttNodes.Where(n => n.IsGanttTask).ToList();

        // Position machines on the left
        double y = 100;
        foreach (var machine in machineNodes)
        {
            machine.X = 50;
            machine.Y = y;
            machine.Width = 100;
            machine.Height = 40;
            y += 60;
        }

        // Position jobs at the top
        double x = 200;
        foreach (var job in jobNodes)
        {
            job.X = x;
            job.Y = 20;
            job.Width = 80;
            job.Height = 40;
            x += 100;
        }

        // Position tasks in a grid based on time
        x = 200;
        y = 100;
        var tasksPerRow = 5;
        var taskIndex = 0;

        foreach (var task in taskNodes.OrderBy(t => t.GanttStartTime ?? TimeSpan.Zero))
        {
            task.X = x + (taskIndex % tasksPerRow) * 120;
            task.Y = y + (taskIndex / tasksPerRow) * 50;
            task.Width = 100;
            task.Height = 30;
            taskIndex++;
        }
    }

    // ============================================
    // GANTT MOUSE HANDLERS
    // ============================================

    /// <summary>Handle mouse down on Gantt canvas</summary>
    private void HandleGanttMouseDown(MouseEventArgs e)
    {
        if (ganttTimeline == null) return;

        var clickedNode = GetGanttNodeAtPosition(e.OffsetX, e.OffsetY);

        if (clickedNode != null)
        {
            if (isCreatingGanttPrecedence)
            {
                // Precedence creation mode
                HandleGanttPrecedenceClick(clickedNode.Id);
            }
            else if (clickedNode.IsGanttTask)
            {
                // Select and potentially drag the task
                selectedGanttTaskId = clickedNode.Id;
                draggingGanttTaskId = clickedNode.Id;
                ganttDragStartX = e.OffsetX;
                ganttDragOriginalStart = clickedNode.GanttStartTime;
                ganttDragOriginalDuration = clickedNode.GanttDuration;

                // Check resize handles
                var bounds = ganttTimeline.GetNodeBarBounds(clickedNode);
                var rowIndex = clickedNode.GanttRowIndex >= 0 ? clickedNode.GanttRowIndex : 0;
                var barY = ganttTimeline.RowToY(rowIndex) + 6;
                var barHeight = ganttTimeline.RowHeight - 12;

                // Left resize handle (4px region)
                if (e.OffsetX >= bounds.x - 2 && e.OffsetX <= bounds.x + 4 &&
                    e.OffsetY >= barY && e.OffsetY <= barY + barHeight)
                {
                    isResizingGanttTask = true;
                    isResizingGanttFromLeft = true;
                }
                // Right resize handle (4px region)
                else if (e.OffsetX >= bounds.x + bounds.width - 4 && e.OffsetX <= bounds.x + bounds.width + 2 &&
                         e.OffsetY >= barY && e.OffsetY <= barY + barHeight)
                {
                    isResizingGanttTask = true;
                    isResizingGanttFromLeft = false;
                }
                else
                {
                    isResizingGanttTask = false;
                }
            }
            else if (clickedNode.IsGanttJob)
            {
                selectedGanttJobId = clickedNode.Id;
            }
            else if (clickedNode.IsGanttMachine)
            {
                selectedGanttMachineId = clickedNode.Id;
            }
        }
        else
        {
            // Clicked on empty space
            if (mode == EditorMode.AddNode && selectedTemplateId == "gantt")
            {
                // Create new node at click position
                CreateGanttNodeAtPosition(e.OffsetX, e.OffsetY);
            }
            else
            {
                // Deselect
                selectedGanttTaskId = null;
                selectedGanttJobId = null;
                selectedGanttMachineId = null;
            }
        }
    }

    /// <summary>
    /// Handle double-click on Gantt canvas - compresses task based on current compression mode
    /// </summary>
    private void HandleGanttDoubleClick(MouseEventArgs e)
    {
        if (ganttTimeline == null) return;

        var clickedNode = GetGanttNodeAtPosition(e.OffsetX, e.OffsetY);

        if (clickedNode?.IsGanttTask == true)
        {
            if (ganttCompressionMode == GanttCompressionMode.Earliest)
            {
                CompressGanttTaskEarliest(clickedNode);
            }
            else
            {
                CompressGanttTaskLatest(clickedNode);
            }
        }
    }

    /// <summary>
    /// Toggle between earliest and latest compression modes
    /// </summary>
    private void ToggleGanttCompressionMode()
    {
        ganttCompressionMode = ganttCompressionMode == GanttCompressionMode.Earliest
            ? GanttCompressionMode.Latest
            : GanttCompressionMode.Earliest;
        StateHasChanged();
    }

    /// <summary>
    /// Compresses a task to its earliest possible start time on the machine,
    /// respecting both precedence constraints and machine conflicts.
    /// </summary>
    private void CompressGanttTaskEarliest(Node taskNode)
    {
        var newStart = ganttScheduling.CompressTask(taskNode, nodes, edges);

        if (newStart.HasValue)
        {
            // Save state for undo
            UndoService.SaveState(nodes, edges, edgeLabels);

            // Update the task's start time
            taskNode.GanttStartTime = newStart.Value;

            // Reposition the task visually
            if (ganttTimeline != null)
            {
                ganttTimeline.PositionNodeForTimeline(taskNode);
            }

            // Re-validate precedences
            ValidateGanttPrecedences();

            StateHasChanged();
        }
    }

    /// <summary>
    /// Compresses a task to its latest possible start time (as close to deadline as possible),
    /// respecting precedence constraints (successors), machine conflicts, and the deadline marker.
    /// </summary>
    private void CompressGanttTaskLatest(Node taskNode)
    {
        if (!taskNode.GanttDuration.HasValue) return;

        var duration = taskNode.GanttDuration.Value;

        // Determine the earliest we can start (from predecessors)
        var earliestStart = TimeSpan.Zero;
        var predecessorEdges = edges.Where(e => e.To == taskNode.Id).ToList();
        foreach (var edge in predecessorEdges)
        {
            var predecessor = nodes.FirstOrDefault(n => n.Id == edge.From && n.IsGanttTask);
            if (predecessor?.GanttStartTime.HasValue == true && predecessor.GanttDuration.HasValue)
            {
                var predecessorEnd = predecessor.GanttStartTime.Value + predecessor.GanttDuration.Value;
                if (predecessorEnd > earliestStart)
                {
                    earliestStart = predecessorEnd;
                }
            }
        }

        // Determine the latest end time (from deadline and successors)
        var latestEnd = ganttDeadlineTime ?? GetGanttMakespan();

        // Find all successor tasks (tasks that depend on this one)
        var successorEdges = edges.Where(e => e.From == taskNode.Id).ToList();
        foreach (var edge in successorEdges)
        {
            var successor = nodes.FirstOrDefault(n => n.Id == edge.To && n.IsGanttTask);
            if (successor?.GanttStartTime.HasValue == true)
            {
                // This task must end before successor starts
                if (successor.GanttStartTime.Value < latestEnd)
                {
                    latestEnd = successor.GanttStartTime.Value;
                }
            }
        }

        // Find other tasks on the same machine
        var machineId = taskNode.GanttMachineId;
        var otherTasksOnMachine = nodes
            .Where(n => n.Id != taskNode.Id && n.IsGanttTask && n.GanttMachineId == machineId &&
                       n.GanttStartTime.HasValue && n.GanttDuration.HasValue)
            .OrderBy(n => n.GanttStartTime!.Value)
            .ToList();

        // Find all valid gaps on the machine where we could place this task
        // A gap is valid if we can fit our task and respect earliestStart/latestEnd constraints
        var validPositions = new List<TimeSpan>();

        // Build a list of occupied intervals on this machine
        var occupied = otherTasksOnMachine
            .Select(n => (Start: n.GanttStartTime!.Value, End: n.GanttStartTime!.Value + n.GanttDuration!.Value))
            .OrderBy(x => x.Start)
            .ToList();

        if (occupied.Count == 0)
        {
            // No other tasks on this machine - use the latest possible position
            var idealStart = latestEnd - duration;
            if (idealStart >= earliestStart)
            {
                validPositions.Add(idealStart);
            }
            else if (earliestStart + duration <= latestEnd)
            {
                validPositions.Add(earliestStart);
            }
        }
        else
        {
            // Check gap before first occupied interval (from time 0 or earliestStart)
            var firstOccupiedStart = occupied[0].Start;
            if (firstOccupiedStart > TimeSpan.Zero)
            {
                // Gap from 0 (or earliestStart) to first task
                var gapStart = earliestStart > TimeSpan.Zero ? earliestStart : TimeSpan.Zero;
                var gapEnd = firstOccupiedStart;
                if (gapEnd > gapStart && gapEnd - gapStart >= duration)
                {
                    // We can fit here - the rightmost position in this gap
                    var candidateStart = gapEnd - duration;
                    if (candidateStart < gapStart) candidateStart = gapStart;
                    if (candidateStart >= earliestStart && candidateStart + duration <= latestEnd)
                    {
                        validPositions.Add(candidateStart);
                    }
                }
            }

            // Check gaps between occupied intervals
            for (int i = 0; i < occupied.Count - 1; i++)
            {
                var gapStart = occupied[i].End;
                var gapEnd = occupied[i + 1].Start;

                if (gapEnd - gapStart >= duration)
                {
                    // We can fit in this gap - find the rightmost position
                    var candidateStart = gapEnd - duration;
                    if (candidateStart < gapStart) candidateStart = gapStart;
                    // Also respect earliestStart constraint
                    if (candidateStart < earliestStart) candidateStart = earliestStart;
                    if (candidateStart >= earliestStart && candidateStart + duration <= latestEnd && candidateStart + duration <= gapEnd)
                    {
                        validPositions.Add(candidateStart);
                    }
                }
            }

            // Check gap after last occupied interval - THIS IS THE KEY FOR "JUMPING PAST"
            // If the task can start after all other tasks on this machine and still meet the deadline, do so
            var lastEnd = occupied[^1].End;
            if (lastEnd < latestEnd)
            {
                var gapStart = lastEnd;
                var gapEnd = latestEnd;

                // The effective start of this gap must respect earliestStart
                var effectiveGapStart = gapStart > earliestStart ? gapStart : earliestStart;

                if (gapEnd - effectiveGapStart >= duration)
                {
                    // We can fit here - the rightmost position in this gap
                    var candidateStart = gapEnd - duration;
                    if (candidateStart < effectiveGapStart) candidateStart = effectiveGapStart;
                    if (candidateStart >= earliestStart && candidateStart + duration <= latestEnd)
                    {
                        validPositions.Add(candidateStart);
                    }
                }
            }
        }

        // Pick the rightmost (latest) valid position
        if (validPositions.Count == 0)
        {
            // No valid position found - keep current position or use earliest possible
            return;
        }

        var latestStart = validPositions.Max();

        // Only update if position changed
        if (taskNode.GanttStartTime != latestStart)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            taskNode.GanttStartTime = latestStart;

            if (ganttTimeline != null)
            {
                ganttTimeline.PositionNodeForTimeline(taskNode);
            }

            ValidateGanttPrecedences();
            StateHasChanged();
        }
    }

    /// <summary>
    /// Get the current makespan (latest task end time)
    /// </summary>
    private TimeSpan GetGanttMakespan()
    {
        var maxEnd = TimeSpan.Zero;
        foreach (var task in nodes.Where(n => n.IsGanttTask && n.GanttStartTime.HasValue && n.GanttDuration.HasValue))
        {
            var end = task.GanttStartTime!.Value + task.GanttDuration!.Value;
            if (end > maxEnd) maxEnd = end;
        }
        return maxEnd;
    }

    /// <summary>
    /// Set the deadline marker to the current makespan
    /// </summary>
    private void SetGanttDeadlineToMakespan()
    {
        ganttDeadlineTime = GetGanttMakespan();
        StateHasChanged();
    }

    /// <summary>
    /// Clear the deadline marker
    /// </summary>
    private void ClearGanttDeadline()
    {
        ganttDeadlineTime = null;
        StateHasChanged();
    }

    /// <summary>
    /// Start dragging the deadline marker
    /// </summary>
    private void StartGanttDeadlineDrag(MouseEventArgs e)
    {
        isDraggingGanttDeadline = true;
        ganttDeadlineDragStartX = e.OffsetX;
    }

    /// <summary>
    /// Handle deadline marker drag
    /// </summary>
    private void HandleGanttDeadlineDrag(MouseEventArgs e)
    {
        if (!isDraggingGanttDeadline || ganttTimeline == null) return;

        var newTime = ganttTimeline.XToTime(e.OffsetX);
        newTime = ganttTimeline.SnapToGrid(newTime);
        if (newTime >= TimeSpan.Zero)
        {
            ganttDeadlineTime = newTime;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Stop dragging the deadline marker
    /// </summary>
    private void StopGanttDeadlineDrag()
    {
        isDraggingGanttDeadline = false;
    }

    /// <summary>
    /// Legacy method for backward compatibility
    /// </summary>
    private void CompressGanttTask(Node taskNode)
    {
        CompressGanttTaskEarliest(taskNode);
    }

    /// <summary>Handle mouse move on Gantt canvas</summary>
    private void HandleGanttMouseMove(MouseEventArgs e)
    {
        if (ganttTimeline == null) return;

        // Handle deadline marker dragging
        if (isDraggingGanttDeadline)
        {
            HandleGanttDeadlineDrag(e);
            return;
        }

        // Update rubberband line for precedence creation
        if (isCreatingGanttPrecedence && ganttPrecedenceFromTaskId.HasValue)
        {
            ganttRubberbandX = e.OffsetX;
            ganttRubberbandY = e.OffsetY;
            StateHasChanged();
            return;
        }

        // Handle task dragging
        if (draggingGanttTaskId.HasValue)
        {
            var node = nodes.FirstOrDefault(n => n.Id == draggingGanttTaskId.Value);
            if (node == null) return;

            var deltaX = e.OffsetX - ganttDragStartX;
            var deltaMinutes = deltaX / ganttTimeline.MinuteWidth;

            if (isResizingGanttTask)
            {
                if (isResizingGanttFromLeft && ganttDragOriginalStart.HasValue)
                {
                    // Resize from left - change start time, keep end time
                    var newStart = ganttDragOriginalStart.Value + TimeSpan.FromMinutes(deltaMinutes);
                    newStart = ganttTimeline.SnapToGrid(newStart);

                    if (newStart >= TimeSpan.Zero && node.GanttEndTime.HasValue && newStart < node.GanttEndTime.Value)
                    {
                        var oldEnd = node.GanttEndTime.Value;
                        node.GanttStartTime = newStart;
                        node.GanttDuration = oldEnd - newStart;
                    }
                }
                else if (ganttDragOriginalStart.HasValue && ganttDragOriginalDuration.HasValue)
                {
                    // Resize from right - keep start time, change duration
                    var newDuration = ganttDragOriginalDuration.Value + TimeSpan.FromMinutes(deltaMinutes);
                    var snappedEnd = ganttTimeline.SnapToGrid(ganttDragOriginalStart.Value + newDuration);
                    newDuration = snappedEnd - ganttDragOriginalStart.Value;

                    if (newDuration >= TimeSpan.FromMinutes(ganttTimeline.SnapMinutes))
                    {
                        node.GanttDuration = newDuration;
                    }
                }
            }
            else
            {
                // Move task
                if (ganttDragOriginalStart.HasValue)
                {
                    var newStart = ganttDragOriginalStart.Value + TimeSpan.FromMinutes(deltaMinutes);
                    newStart = ganttTimeline.SnapToGrid(newStart);

                    if (newStart >= TimeSpan.Zero)
                    {
                        node.GanttStartTime = newStart;
                    }
                }
            }

            ganttTimeline.PositionNodeForTimeline(node);
            ValidateGanttPrecedences();
            StateHasChanged();
        }
    }

    /// <summary>Handle mouse up on Gantt canvas</summary>
    private void HandleGanttMouseUp(MouseEventArgs e)
    {
        // Stop deadline dragging
        if (isDraggingGanttDeadline)
        {
            StopGanttDeadlineDrag();
        }

        if (draggingGanttTaskId.HasValue)
        {
            var node = nodes.FirstOrDefault(n => n.Id == draggingGanttTaskId.Value);
            if (node != null && ganttTimeline != null)
            {
                // Final snap to grid
                if (node.GanttStartTime.HasValue)
                {
                    node.GanttStartTime = ganttTimeline.SnapToGrid(node.GanttStartTime.Value);
                    ganttTimeline.PositionNodeForTimeline(node);
                }
            }
        }

        draggingGanttTaskId = null;
        isResizingGanttTask = false;
        ganttDragOriginalStart = null;
        ganttDragOriginalDuration = null;

        ValidateGanttPrecedences();
        StateHasChanged();
    }

    // ============================================
    // GANTT NODE HELPERS
    // ============================================

    /// <summary>Get the Gantt node at a given position</summary>
    private Node? GetGanttNodeAtPosition(double x, double y)
    {
        if (ganttTimeline == null) return null;

        foreach (var node in nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttTask))
        {
            var bounds = ganttTimeline.GetNodeBarBounds(node);
            var rowIndex = node.GanttRowIndex >= 0 ? node.GanttRowIndex : 0;
            var barY = ganttTimeline.RowToY(rowIndex) + 6;
            var barHeight = ganttTimeline.RowHeight - 12;

            if (x >= bounds.x && x <= bounds.x + bounds.width &&
                y >= barY && y <= barY + barHeight)
            {
                return node;
            }
        }

        // Check job and machine nodes (in node view)
        if (ganttViewMode == "node")
        {
            foreach (var node in nodes.Where(n => n.TemplateId == "gantt" && (n.IsGanttJob || n.IsGanttMachine)))
            {
                if (x >= node.X && x <= node.X + node.Width &&
                    y >= node.Y && y <= node.Y + node.Height)
                {
                    return node;
                }
            }
        }

        return null;
    }

    /// <summary>Create a new Gantt node at the given position</summary>
    private void CreateGanttNodeAtPosition(double x, double y)
    {
        if (ganttTimeline == null) return;

        var id = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;

        if (selectedTemplateShapeId == "task")
        {
            // Create task at clicked time
            var startTime = ganttTimeline.XToTime(x);
            startTime = ganttTimeline.SnapToGrid(startTime);
            var rowIndex = ganttTimeline.YToRow(y);

            // Get existing machines
            var machineNodes = nodes.Where(n => n.IsGanttMachine).OrderBy(n => n.GanttRowIndex).ToList();

            // Auto-create a machine if none exist
            if (machineNodes.Count == 0)
            {
                var machineId = id;
                id++;
                var machine = GanttTimelineService.CreateGanttMachineNode(machineId, "Machine 1", 0);
                nodes.Add(machine);
                machineNodes.Add(machine);
            }

            // Find or create the machine for the clicked row
            Node? targetMachine = null;
            if (rowIndex >= 0 && rowIndex < machineNodes.Count)
            {
                targetMachine = machineNodes[rowIndex];
            }
            else
            {
                // Row is beyond existing machines - create new machine or use last one
                targetMachine = machineNodes.Last();
                rowIndex = machineNodes.IndexOf(targetMachine);
            }

            var node = GanttTimelineService.CreateGanttTaskNode(
                id,
                $"Task {id}",
                startTime,
                TimeSpan.FromMinutes(30),
                null,
                targetMachine?.Id  // Assign the machine
            );
            node.GanttRowIndex = rowIndex;

            // Assign job color if a job is selected
            if (selectedGanttJobId.HasValue)
            {
                node.GanttJobId = selectedGanttJobId.Value;
                var jobNode = nodes.FirstOrDefault(n => n.Id == selectedGanttJobId.Value);
                if (jobNode != null)
                {
                    node.FillColor = jobNode.FillColor;
                    node.StrokeColor = jobNode.StrokeColor;
                }
            }
            else
            {
                // Each new task gets a unique color (cycles through palette)
                // Use the task's own ID as the job ID so it gets a unique color
                node.GanttJobId = id;
                var color = ganttColorService.AssignJobColor(id);
                node.FillColor = color.Fill;
                node.StrokeColor = color.Stroke;
            }

            ganttTimeline.PositionNodeForTimeline(node);
            nodes.Add(node);
            selectedGanttTaskId = node.Id;
        }
        else if (selectedTemplateShapeId == "job")
        {
            // Create job node
            var color = ganttColorService.AssignJobColor(id);
            var node = GanttTimelineService.CreateGanttJobNode(id, $"Job {id}", color.Fill);
            node.X = x;
            node.Y = y;
            nodes.Add(node);
            selectedGanttJobId = node.Id;
        }
        else if (selectedTemplateShapeId == "machine")
        {
            // Create machine node
            var rowIndex = nodes.Count(n => n.IsGanttMachine);
            var node = GanttTimelineService.CreateGanttMachineNode(id, $"Machine {rowIndex + 1}", rowIndex);
            node.X = x;
            node.Y = y;
            nodes.Add(node);
            selectedGanttMachineId = node.Id;
        }

        StateHasChanged();
    }

    /// <summary>Add a new machine to the Gantt chart</summary>
    private void AddGanttMachine()
    {
        var id = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        var rowIndex = nodes.Count(n => n.IsGanttMachine);
        var machine = GanttTimelineService.CreateGanttMachineNode(id, $"Machine {rowIndex + 1}", rowIndex);
        nodes.Add(machine);
        selectedGanttMachineId = machine.Id;
        StateHasChanged();
    }

    /// <summary>Delete the currently selected Gantt task</summary>
    private void DeleteSelectedGanttTask()
    {
        if (!selectedGanttTaskId.HasValue) return;

        var taskId = selectedGanttTaskId.Value;
        var task = nodes.FirstOrDefault(n => n.Id == taskId);
        if (task == null || !task.IsGanttTask) return;

        // Remove any edges connected to this task
        var connectedEdges = edges.Where(e => e.From == taskId || e.To == taskId).ToList();
        foreach (var edge in connectedEdges)
        {
            edges.Remove(edge);
        }

        // Remove the task node
        nodes.Remove(task);

        // Clear selection
        selectedGanttTaskId = null;

        // Update the view
        UpdateGanttView();
        StateHasChanged();
    }

    /// <summary>Delete a specific Gantt task by ID</summary>
    private void DeleteGanttTask(int taskId)
    {
        var task = nodes.FirstOrDefault(n => n.Id == taskId);
        if (task == null || !task.IsGanttTask) return;

        // Remove any edges connected to this task
        var connectedEdges = edges.Where(e => e.From == taskId || e.To == taskId).ToList();
        foreach (var edge in connectedEdges)
        {
            edges.Remove(edge);
        }

        // Remove the task node
        nodes.Remove(task);

        // Clear selection if this was the selected task
        if (selectedGanttTaskId == taskId)
        {
            selectedGanttTaskId = null;
        }

        // Update the view
        UpdateGanttView();
        StateHasChanged();
    }

    // ============================================
    // GANTT PRECEDENCE (DEPENDENCY) CREATION
    // ============================================

    /// <summary>Start precedence creation mode</summary>
    private void StartGanttPrecedenceCreation()
    {
        isCreatingGanttPrecedence = true;
        ganttPrecedenceFromTaskId = null;
    }

    /// <summary>Cancel precedence creation</summary>
    private void CancelGanttPrecedenceCreation()
    {
        isCreatingGanttPrecedence = false;
        ganttPrecedenceFromTaskId = null;
    }

    /// <summary>Handle click during precedence creation</summary>
    private void HandleGanttPrecedenceClick(int taskId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == taskId && n.IsGanttTask);
        if (node == null) return;

        if (!ganttPrecedenceFromTaskId.HasValue)
        {
            // First click - set source
            ganttPrecedenceFromTaskId = taskId;
            var bounds = ganttTimeline?.GetNodeBarRightEdge(node) ?? (0, 0);
            ganttRubberbandX = bounds.x;
            ganttRubberbandY = bounds.y;
        }
        else if (ganttPrecedenceFromTaskId.Value != taskId)
        {
            // Second click - create precedence
            AddGanttPrecedence(ganttPrecedenceFromTaskId.Value, taskId);
            ganttPrecedenceFromTaskId = null;
            isCreatingGanttPrecedence = false;
        }
    }

    /// <summary>Handle click on a Gantt task terminal (input/output)</summary>
    private void HandleGanttTerminalClick(int taskId, string terminalType, Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        Console.WriteLine($"HandleGanttTerminalClick: taskId={taskId}, terminalType={terminalType}");

        var node = nodes.FirstOrDefault(n => n.Id == taskId && n.IsGanttTask);
        if (node == null) return;

        if (terminalType == "output")
        {
            // Output terminal clicked - start creating precedence FROM this task
            if (!ganttPrecedenceFromTaskId.HasValue)
            {
                ganttPrecedenceFromTaskId = taskId;
                isCreatingGanttPrecedence = true;

                // Set rubberband start position at the output terminal
                if (node.GanttStartTime.HasValue && node.GanttDuration.HasValue && ganttTimeline != null)
                {
                    var machineNodes = GetGanttMachineNodes().ToList();
                    var machineIndex = machineNodes.FindIndex(m => m.Id == node.GanttMachineId);
                    if (machineIndex >= 0)
                    {
                        ganttRubberbandX = ganttTimeline.TimeToX(node.GanttStartTime.Value + node.GanttDuration.Value) + 10;
                        ganttRubberbandY = ganttTimeline.GetRowY(machineIndex) + ganttTimeline.RowHeight / 2;
                    }
                }
                Console.WriteLine($"Started precedence from task {taskId}");
            }
        }
        else if (terminalType == "input")
        {
            // Input terminal clicked - complete precedence TO this task
            if (ganttPrecedenceFromTaskId.HasValue && ganttPrecedenceFromTaskId.Value != taskId)
            {
                AddGanttPrecedence(ganttPrecedenceFromTaskId.Value, taskId);
                ganttPrecedenceFromTaskId = null;
                isCreatingGanttPrecedence = false;
                Console.WriteLine($"Created precedence to task {taskId}");
            }
        }

        StateHasChanged();
    }

    /// <summary>Add a precedence constraint (create edge)</summary>
    private void AddGanttPrecedence(int fromTaskId, int toTaskId)
    {
        // Check if edge already exists
        if (edges.Any(e => e.From == fromTaskId && e.To == toTaskId))
            return;

        var edgeId = edges.Count > 0 ? edges.Max(e => e.Id) + 1 : 1;
        var edge = new Edge
        {
            Id = edgeId,
            From = fromTaskId,
            To = toTaskId,
            StrokeColor = "#0F172A", // Dark navy
            StrokeWidth = 2
        };

        edges.Add(edge);

        // When tasks are connected, they should share the same color (job)
        // Propagate color from source task to target task
        var fromTask = nodes.FirstOrDefault(n => n.Id == fromTaskId && n.IsGanttTask);
        var toTask = nodes.FirstOrDefault(n => n.Id == toTaskId && n.IsGanttTask);
        if (fromTask != null && toTask != null)
        {
            Console.WriteLine($"Color sync: fromTask {fromTaskId} (JobId={fromTask.GanttJobId}, Fill={fromTask.FillColor}) -> toTask {toTaskId} (JobId={toTask.GanttJobId}, Fill={toTask.FillColor})");

            // Get the source task's color - use the color service if FillColor is not set
            var fillColor = fromTask.FillColor;
            var strokeColor = fromTask.StrokeColor;
            var jobId = fromTask.GanttJobId;

            // If source has no color, assign one
            if (string.IsNullOrEmpty(fillColor) && jobId.HasValue)
            {
                var color = ganttColorService.AssignJobColor(jobId.Value);
                fillColor = color.Fill;
                strokeColor = color.Stroke;
                fromTask.FillColor = fillColor;
                fromTask.StrokeColor = strokeColor;
            }

            // Target task adopts the source task's job/color
            toTask.GanttJobId = jobId;
            toTask.FillColor = fillColor;
            toTask.StrokeColor = strokeColor;

            Console.WriteLine($"After sync: toTask {toTaskId} now has JobId={toTask.GanttJobId}, Fill={toTask.FillColor}");

            // Also propagate to any tasks already connected to the target (downstream)
            PropagateJobColorToConnectedTasks(toTaskId, jobId, fillColor, strokeColor);
        }

        ValidateGanttPrecedences();
        StateHasChanged();
    }

    /// <summary>Propagate job color to all tasks connected (downstream) from a given task</summary>
    private void PropagateJobColorToConnectedTasks(int taskId, int? jobId, string? fillColor, string? strokeColor)
    {
        // Find all edges where this task is the source
        var downstreamEdges = edges.Where(e => e.From == taskId).ToList();
        foreach (var edge in downstreamEdges)
        {
            var targetTask = nodes.FirstOrDefault(n => n.Id == edge.To && n.IsGanttTask);
            if (targetTask != null && targetTask.GanttJobId != jobId)
            {
                targetTask.GanttJobId = jobId;
                targetTask.FillColor = fillColor;
                targetTask.StrokeColor = strokeColor;
                // Recursively propagate to downstream tasks
                PropagateJobColorToConnectedTasks(targetTask.Id, jobId, fillColor, strokeColor);
            }
        }
    }

    /// <summary>Validate all Gantt precedences and mark violations</summary>
    private void ValidateGanttPrecedences()
    {
        var ganttTasks = nodes.Where(n => n.IsGanttTask);
        var ganttEdges = edges.Where(e =>
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
            return fromNode?.IsGanttTask == true && toNode?.IsGanttTask == true;
        });

        ganttScheduling.ValidateNodePrecedences(ganttTasks, ganttEdges);
    }

    // ============================================
    // GANTT SCHEDULING ACTIONS
    // ============================================

    /// <summary>Apply SPT scheduling to all tasks, creating a solution layer</summary>
    private void ApplyGanttSPT()
    {
        ApplyGanttScheduling("SPT");
    }

    /// <summary>Apply a scheduling algorithm and create a solution layer</summary>
    private void ApplyGanttScheduling(string algorithm)
    {
        // Pass ALL Gantt nodes (tasks + machines) so scheduling can track machine availability
        var ganttNodes = nodes.Where(n => n.TemplateId == "gantt" && (n.IsGanttTask || n.IsGanttMachine)).ToList();
        var ganttEdges = edges.Where(e =>
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
            return fromNode?.IsGanttTask == true && toNode?.IsGanttTask == true;
        }).ToList();

        // Capture original task states BEFORE scheduling
        var taskNodes = ganttNodes.Where(n => n.IsGanttTask).ToList();
        var originalStates = taskNodes.ToDictionary(
            n => n.Id,
            n => (StartTime: n.GanttStartTime, Duration: n.GanttDuration, MachineId: n.GanttMachineId, RowIndex: n.GanttRowIndex)
        );

        // Run the scheduling algorithm (modifies nodes in place)
        var result = algorithm switch
        {
            "SPT" => ganttScheduling.ScheduleNodesSPT(ganttNodes, ganttEdges),
            // Add other algorithms here: "LPT", "CPM", etc.
            _ => ganttScheduling.ScheduleNodesSPT(ganttNodes, ganttEdges)
        };

        // Create a solution layer with the scheduling results
        // The layer captures the delta between original and scheduled
        var layerName = $"{algorithm} Solution #{layerService.GetLayers().Count + 1}";

        // Create layer from the scheduled nodes (comparing against captured original)
        var overrides = new Dictionary<int, NodeOverride>();
        foreach (var node in taskNodes)
        {
            var orig = originalStates[node.Id];
            var nodeOverride = new NodeOverride { NodeId = node.Id };
            bool hasChanges = false;

            if (node.GanttStartTime != orig.StartTime)
            {
                nodeOverride.GanttStartTime = node.GanttStartTime;
                hasChanges = true;
            }
            if (node.GanttDuration != orig.Duration)
            {
                nodeOverride.GanttDuration = node.GanttDuration;
                hasChanges = true;
            }
            if (node.GanttMachineId != orig.MachineId)
            {
                nodeOverride.GanttMachineId = node.GanttMachineId;
                hasChanges = true;
            }
            if (node.GanttRowIndex != orig.RowIndex)
            {
                nodeOverride.GanttRowIndex = node.GanttRowIndex;
                hasChanges = true;
            }

            if (hasChanges)
            {
                overrides[node.Id] = nodeOverride;
            }
        }

        // Create the layer with the computed overrides
        var layer = layerService.CreateLayer(layerName, algorithm, "gantt", overrides);

        // Calculate and store metrics for this layer
        var metrics = CalculateGanttMetrics(taskNodes);
        layerService.UpdateLayerMetrics(layer.Id, metrics);

        // Restore original node values (base layer is unchanged)
        foreach (var node in taskNodes)
        {
            var orig = originalStates[node.Id];
            node.GanttStartTime = orig.StartTime;
            node.GanttDuration = orig.Duration;
            node.GanttMachineId = orig.MachineId;
            node.GanttRowIndex = orig.RowIndex;
        }

        // Make the new layer active so user sees the scheduled result
        layerService.SetActiveLayer(layer.Id);

        // Update view
        UpdateGanttView();

        Console.WriteLine($"Created layer '{layerName}' with {overrides.Count} task overrides. {result.Message}");
        StateHasChanged();
    }

    /// <summary>Auto-fix all precedence violations</summary>
    private void AutoFixGanttViolations()
    {
        var ganttNodes = nodes.Where(n => n.TemplateId == "gantt" && (n.IsGanttTask || n.IsGanttMachine)).ToList();
        var ganttEdges = edges.Where(e =>
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
            return fromNode?.IsGanttTask == true && toNode?.IsGanttTask == true;
        }).ToList();

        var result = ganttScheduling.AutoFixNodeViolations(ganttNodes, ganttEdges);

        UpdateGanttView();
        Console.WriteLine(result.Message);
        StateHasChanged();
    }

    // ============================================
    // GANTT ZOOM CONTROLS
    // ============================================

    private void GanttZoomIn()
    {
        ganttTimeline?.ZoomIn();
        ScheduleGanttZoomUpdate();
    }

    private void GanttZoomOut()
    {
        ganttTimeline?.ZoomOut();
        ScheduleGanttZoomUpdate();
    }

    private async Task GanttZoomToFit()
    {
        if (ganttTimeline == null) return;

        // Get actual viewport width from JS
        var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
        if (scrollInfo != null && scrollInfo.Length >= 4)
        {
            viewportWidth = scrollInfo[2];
        }

        // Use viewport width (minimum 600px to avoid too-small calculations)
        var availableWidth = Math.Max(600, viewportWidth);

        var ganttNodes = nodes.Where(n => n.IsGanttTask);
        ganttTimeline.ZoomToFit(ganttNodes, availableWidth);
        UpdateGanttView();
        StateHasChanged();
    }

    /// <summary>Schedule a debounced zoom update - batches rapid zoom events</summary>
    private void ScheduleGanttZoomUpdate()
    {
        _ganttZoomPending = true;

        if (_ganttZoomDebounceTimer == null)
        {
            _ganttZoomDebounceTimer = new System.Timers.Timer(ZoomDebounceMs);
            _ganttZoomDebounceTimer.AutoReset = false;
            _ganttZoomDebounceTimer.Elapsed += async (s, e) =>
            {
                if (_ganttZoomPending)
                {
                    _ganttZoomPending = false;
                    await InvokeAsync(() =>
                    {
                        UpdateGanttView();
                        StateHasChanged();
                    });
                }
            };
        }

        // Reset the timer on each zoom event
        _ganttZoomDebounceTimer.Stop();
        _ganttZoomDebounceTimer.Start();
    }

    // ============================================
    // GANTT WHEEL/SCROLL HANDLING
    // ============================================

    /// <summary>Handle mouse wheel on Gantt canvas for horizontal scrolling and zoom</summary>
    private async Task HandleGanttWheel(WheelEventArgs e)
    {
        if (canvasRef.Id == null) return;

        // Ctrl+Wheel = zoom
        if (e.CtrlKey)
        {
            if (e.DeltaY < 0)
                GanttZoomIn();
            else
                GanttZoomOut();
            return;
        }

        // Regular wheel scrolls horizontally on Gantt timeline
        // Use deltaX if available (horizontal scroll wheel/trackpad), otherwise use deltaY
        var horizontalDelta = e.ShiftKey || Math.Abs(e.DeltaX) > Math.Abs(e.DeltaY)
            ? (e.DeltaX != 0 ? e.DeltaX : e.DeltaY)
            : e.DeltaY;

        await JS.InvokeVoidAsync("scrollCanvasHorizontal", canvasRef, horizontalDelta);

        // Update minimap viewport position after scroll
        var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
        if (scrollInfo != null && scrollInfo.Length >= 4)
        {
            scrollX = scrollInfo[0];
            scrollY = scrollInfo[1];
            viewportWidth = scrollInfo[2];
            viewportHeight = scrollInfo[3];
            StateHasChanged();
        }
    }

    // ============================================
    // GANTT NODE ACCESSORS
    // ============================================

    /// <summary>Get all machine nodes for Gantt mode</summary>
    private IEnumerable<Node> GetGanttMachineNodes()
    {
        return nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttMachine)
                    .OrderBy(n => n.GanttRowIndex);
    }

    /// <summary>Get all task nodes for Gantt mode (filtered by job visibility)</summary>
    private IEnumerable<Node> GetGanttTaskNodes()
    {
        return nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttTask && IsGanttTaskVisible(n));
    }

    /// <summary>Get all job nodes for Gantt mode (ordered by text for filter display)</summary>
    private IEnumerable<Node> GetGanttJobNodes()
    {
        return nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttJob)
                    .OrderBy(n => n.Text);
    }

    /// <summary>
    /// Get all filterable job entries for the job filter row.
    /// This includes both actual Job nodes AND unique GanttJobId values from tasks
    /// (for tasks created without a Job node).
    /// </summary>
    private IEnumerable<(int Id, string Name, string Color)> GetGanttJobFilterEntries()
    {
        var entries = new Dictionary<int, (int Id, string Name, string Color)>();

        // Add actual Job nodes
        foreach (var job in nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttJob))
        {
            var color = !string.IsNullOrEmpty(job.FillColor) ? job.FillColor : (job.GanttJobColor ?? "#3b82f6");
            entries[job.Id] = (job.Id, job.Text ?? "Job", color);
        }

        // Add unique GanttJobId values from tasks that don't correspond to Job nodes
        foreach (var task in nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttTask && n.GanttJobId.HasValue))
        {
            var jobId = task.GanttJobId!.Value;
            if (!entries.ContainsKey(jobId))
            {
                // This task has a GanttJobId that's not a Job node - use its own color
                var color = !string.IsNullOrEmpty(task.FillColor) ? task.FillColor : "#3b82f6";
                entries[jobId] = (jobId, task.Text ?? "Task", color);
            }
        }

        return entries.Values.OrderBy(e => e.Name);
    }

    /// <summary>Select a Gantt task (handles Ctrl/Shift for multi-select in node view)</summary>
    private void SelectGanttTask(int taskId, MouseEventArgs e)
    {
        if (ganttTimelineView)
        {
            // In timeline view, single selection only
            selectedGanttTaskId = taskId;
        }
        else
        {
            // In node view, use standard node selection
            var node = nodes.FirstOrDefault(n => n.Id == taskId);
            if (node != null)
            {
                if (e.CtrlKey)
                {
                    // Toggle selection
                    if (selectedNodes.Contains(taskId))
                        selectedNodes.Remove(taskId);
                    else
                        selectedNodes.Add(taskId);
                }
                else if (e.ShiftKey && selectedNodes.Count > 0)
                {
                    // Range select (simplified - just add to selection)
                    selectedNodes.Add(taskId);
                }
                else
                {
                    // Single select
                    selectedNodes.Clear();
                    selectedNodes.Add(taskId);
                }
            }
        }
        StateHasChanged();
    }

    /// <summary>Toggle between Gantt timeline and node view</summary>
    private void ToggleGanttView()
    {
        ganttTimelineView = !ganttTimelineView;
        ganttViewMode = ganttTimelineView ? "timeline" : "node";

        if (!ganttTimelineView)
        {
            LayoutGanttNodesForNodeView();
        }
        else
        {
            UpdateGanttView();
        }

        StateHasChanged();
    }

    // ============================================
    // GANTT JOB FILTER
    // ============================================

    /// <summary>Toggle job filter dropdown visibility</summary>
    private void ToggleGanttJobFilter()
    {
        showGanttJobFilter = !showGanttJobFilter;
        StateHasChanged();
    }

    /// <summary>Toggle visibility of a specific job's tasks</summary>
    private void ToggleGanttJobVisibility(int jobId)
    {
        if (hiddenGanttJobIds.Contains(jobId))
        {
            hiddenGanttJobIds.Remove(jobId);
        }
        else
        {
            hiddenGanttJobIds.Add(jobId);
        }
        StateHasChanged();
    }

    /// <summary>Show all jobs</summary>
    private void ShowAllGanttJobs()
    {
        hiddenGanttJobIds.Clear();
        StateHasChanged();
    }

    /// <summary>Hide all jobs (including standalone task colors)</summary>
    private void HideAllGanttJobs()
    {
        foreach (var entry in GetGanttJobFilterEntries())
        {
            hiddenGanttJobIds.Add(entry.Id);
        }
        StateHasChanged();
    }

    /// <summary>Invert job visibility - show hidden, hide shown</summary>
    private void InvertGanttJobVisibility()
    {
        var allJobIds = GetGanttJobFilterEntries()
                             .Select(e => e.Id)
                             .ToHashSet();

        // Jobs currently visible become hidden, hidden become visible
        var newHiddenSet = allJobIds.Except(hiddenGanttJobIds).ToHashSet();
        hiddenGanttJobIds = newHiddenSet;
        StateHasChanged();
    }

    /// <summary>Check if a job is visible</summary>
    private bool IsGanttJobVisible(int jobId)
    {
        return !hiddenGanttJobIds.Contains(jobId);
    }

    /// <summary>Check if a task should be visible based on its job visibility</summary>
    private bool IsGanttTaskVisible(Node task)
    {
        if (!task.IsGanttTask) return true;
        if (!task.GanttJobId.HasValue) return true; // Tasks without a job are always visible
        return !hiddenGanttJobIds.Contains(task.GanttJobId.Value);
    }

    // ============================================
    // GANTT VIEW MODE TOGGLE
    // ============================================

    /// <summary>Toggle between timeline and node view</summary>
    private void ToggleGanttViewMode()
    {
        ganttViewMode = ganttViewMode == "timeline" ? "nodes" : "timeline";
        ganttTimelineView = ganttViewMode == "timeline";
        UpdateGanttView();
        StateHasChanged();
    }

    // ============================================
    // GANTT DASHBOARD
    // ============================================

    /// <summary>Toggle dashboard visibility</summary>
    private void ToggleGanttDashboard()
    {
        showGanttDashboard = !showGanttDashboard;
        StateHasChanged();
    }

    /// <summary>Start dragging the dashboard</summary>
    private void StartDashboardDrag(MouseEventArgs e)
    {
        isDraggingDashboard = true;
        dashboardDragOffsetX = e.ClientX - ganttDashboardX;
        dashboardDragOffsetY = e.ClientY - ganttDashboardY;
    }

    /// <summary>Handle dashboard drag move (called from global mouse move)</summary>
    private void HandleDashboardDragMove(MouseEventArgs e)
    {
        if (!isDraggingDashboard) return;

        ganttDashboardX = e.ClientX - dashboardDragOffsetX;
        ganttDashboardY = e.ClientY - dashboardDragOffsetY;

        // Keep dashboard within viewport bounds
        ganttDashboardX = Math.Max(0, ganttDashboardX);
        ganttDashboardY = Math.Max(50, ganttDashboardY); // Keep below toolbar

        StateHasChanged();
    }

    /// <summary>Stop dragging the dashboard</summary>
    private void StopDashboardDrag()
    {
        isDraggingDashboard = false;
    }

    // ============================================
    // GANTT FILTER POPUP DRAG
    // ============================================

    /// <summary>Start dragging the filter popup</summary>
    private void StartFilterDrag(MouseEventArgs e)
    {
        isDraggingFilter = true;
        filterDragOffsetX = e.ClientX - ganttFilterX;
        filterDragOffsetY = e.ClientY - ganttFilterY;
    }

    /// <summary>Handle filter popup drag move</summary>
    private void HandleFilterDragMove(MouseEventArgs e)
    {
        if (!isDraggingFilter) return;

        ganttFilterX = e.ClientX - filterDragOffsetX;
        ganttFilterY = e.ClientY - filterDragOffsetY;

        // Keep filter within viewport bounds
        ganttFilterX = Math.Max(0, ganttFilterX);
        ganttFilterY = Math.Max(50, ganttFilterY); // Keep below toolbar

        StateHasChanged();
    }

    /// <summary>Stop dragging the filter popup</summary>
    private void StopFilterDrag()
    {
        isDraggingFilter = false;
    }

    /// <summary>Get dashboard statistics</summary>
    private GanttDashboardStats GetGanttDashboardStats()
    {
        var machines = nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttMachine).ToList();
        var tasks = nodes.Where(n => n.TemplateId == "gantt" && n.IsGanttTask).ToList();

        var stats = new GanttDashboardStats
        {
            TotalTasks = tasks.Count,
            MachineStats = new List<MachineStat>()
        };

        if (tasks.Count == 0 || machines.Count == 0)
        {
            stats.Makespan = TimeSpan.Zero;
            stats.AverageUtilization = 0;
            return stats;
        }

        // Calculate makespan (latest task end time)
        TimeSpan makespan = TimeSpan.Zero;
        foreach (var task in tasks)
        {
            if (task.GanttStartTime.HasValue && task.GanttDuration.HasValue)
            {
                var endTime = task.GanttStartTime.Value + task.GanttDuration.Value;
                if (endTime > makespan)
                    makespan = endTime;
            }
        }
        stats.Makespan = makespan;

        // Calculate per-machine utilization (accounting for overlaps)
        foreach (var machine in machines)
        {
            var machineTasks = tasks.Where(t => t.GanttMachineId == machine.Id).ToList();

            // Calculate actual busy time by merging overlapping intervals
            var intervals = machineTasks
                .Where(t => t.GanttStartTime.HasValue && t.GanttDuration.HasValue)
                .Select(t => (Start: t.GanttStartTime!.Value, End: t.GanttStartTime!.Value + t.GanttDuration!.Value))
                .OrderBy(i => i.Start)
                .ToList();

            var totalBusyTime = TimeSpan.Zero;
            if (intervals.Count > 0)
            {
                // Merge overlapping intervals to get actual busy time
                var mergedStart = intervals[0].Start;
                var mergedEnd = intervals[0].End;

                foreach (var interval in intervals.Skip(1))
                {
                    if (interval.Start <= mergedEnd)
                    {
                        // Overlapping - extend the merged interval
                        mergedEnd = interval.End > mergedEnd ? interval.End : mergedEnd;
                    }
                    else
                    {
                        // Non-overlapping - add the previous merged interval
                        totalBusyTime += mergedEnd - mergedStart;
                        mergedStart = interval.Start;
                        mergedEnd = interval.End;
                    }
                }
                // Add the last merged interval
                totalBusyTime += mergedEnd - mergedStart;
            }

            double utilization = makespan.TotalMinutes > 0
                ? (totalBusyTime.TotalMinutes / makespan.TotalMinutes) * 100
                : 0;

            // Cap at 100% (shouldn't exceed after proper scheduling)
            utilization = Math.Min(utilization, 100.0);

            stats.MachineStats.Add(new MachineStat
            {
                Name = machine.Text ?? $"Machine {machine.Id}",
                Utilization = utilization,
                TaskCount = machineTasks.Count
            });
        }

        // Sort by utilization descending
        stats.MachineStats = stats.MachineStats.OrderByDescending(m => m.Utilization).ToList();

        // Calculate average utilization
        stats.AverageUtilization = stats.MachineStats.Count > 0
            ? stats.MachineStats.Average(m => m.Utilization)
            : 0;

        return stats;
    }

    /// <summary>Get color based on utilization percentage</summary>
    private string GetUtilizationColor(double utilization)
    {
        if (utilization >= 80) return "#22c55e"; // Green - high utilization
        if (utilization >= 50) return "#eab308"; // Yellow - medium
        if (utilization >= 25) return "#f97316"; // Orange - low
        return "#ef4444"; // Red - very low
    }

    /// <summary>Dashboard statistics data</summary>
    private class GanttDashboardStats
    {
        public TimeSpan Makespan { get; set; }
        public int TotalTasks { get; set; }
        public double AverageUtilization { get; set; }
        public List<MachineStat> MachineStats { get; set; } = new();
    }

    /// <summary>Per-machine statistics</summary>
    private class MachineStat
    {
        public string Name { get; set; } = "";
        public double Utilization { get; set; }
        public int TaskCount { get; set; }
    }

    // ============================================
    // GANTT IMPORT/EXPORT
    // ============================================

    /// <summary>Whether to import solution as a layer (vs replacing base)</summary>
    private bool ganttImportAsLayer = false;

    /// <summary>Detected solution info from import text</summary>
    private bool ganttImportIsSolution = false;

    /// <summary>
    /// Import Gantt job-shop data from the import dialog text
    /// </summary>
    private async Task ImportGanttData()
    {
        if (string.IsNullOrWhiteSpace(ganttImportText))
        {
            ganttImportError = "Please paste JSON data or load a file.";
            return;
        }

        // Check if importing as layer to existing Gantt data
        if (ganttImportAsLayer && ganttImportIsSolution)
        {
            await ImportGanttSolutionAsLayer();
            return;
        }

        var startingNodeId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        var startingEdgeId = edges.Count > 0 ? edges.Max(e => e.Id) + 1 : 1;

        var result = ganttImportExport.ImportFromJson(ganttImportText, startingNodeId, startingEdgeId);

        if (result.Success)
        {
            // Clear existing Gantt data
            nodes.RemoveAll(n => n.TemplateId == "gantt");
            edges.RemoveAll(e =>
            {
                var fromNode = nodes.FirstOrDefault(n => n.Id == e.From);
                var toNode = nodes.FirstOrDefault(n => n.Id == e.To);
                return fromNode?.TemplateId == "gantt" || toNode?.TemplateId == "gantt";
            });

            // Add imported nodes and edges
            nodes.AddRange(result.Nodes);
            edges.AddRange(result.Edges);

            // Update nextId
            nextId = result.Nodes.Count > 0 ? result.Nodes.Max(n => n.Id) + 1 : nextId;

            // Enter Gantt mode and initialize view
            isGanttMode = true;
            isProjectMode = false;
            selectedTemplateId = "gantt";
            ganttViewMode = "timeline";

            ganttTimeline = new GanttTimelineService();
            var ganttNodes = nodes.Where(n => n.IsGanttTask).ToList();
            if (ganttNodes.Count > 0)
            {
                ganttTimeline.SetViewRangeFromNodes(ganttNodes, TimeSpan.FromMinutes(30));
            }

            // Position all task nodes
            foreach (var node in ganttNodes)
            {
                ganttTimeline.PositionNodeForTimeline(node);
            }

            // Validate precedences
            ValidateGanttPrecedences();

            // Log any warnings
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"Import warning: {warning}");
            }

            // Show solution info if imported
            if (result.IsSolution && result.Makespan.HasValue)
            {
                Console.WriteLine($"Imported solution: Makespan = {result.Makespan} min, Status = {result.Status}");
            }

            // Close dialog on success
            showGanttImportDialog = false;
            ganttImportText = "";
            ganttImportError = "";
            ganttImportAsLayer = false;
            ganttImportIsSolution = false;

            // Set mode and zoom to fit
            mode = EditorMode.Select;
            StateHasChanged();
            await GanttZoomToFit();
        }
        else
        {
            ganttImportError = string.Join("; ", result.Errors);
            if (result.Warnings.Count > 0)
            {
                ganttImportError += " Warnings: " + string.Join("; ", result.Warnings);
            }
            StateHasChanged();
        }
    }

    /// <summary>
    /// Import a solution JSON as a new layer (applies to existing Gantt tasks)
    /// </summary>
    private async Task ImportGanttSolutionAsLayer()
    {
        var result = ganttImportExport.ImportSolutionAsLayer(ganttImportText, nodes);

        if (result.Success)
        {
            // Create a new layer with the imported overrides
            var layer = layerService.CreateLayer(
                result.LayerName,
                result.SolverType,
                "gantt",
                result.NodeOverrides
            );

            // Update description and metrics
            layer.Description = result.Description;
            foreach (var metric in result.ComputedMetrics)
            {
                layer.ComputedMetrics[metric.Key] = metric.Value;
            }

            // Log success
            Console.WriteLine($"Imported solution as layer '{layer.Name}' with {result.NodeOverrides.Count} task overrides");
            if (result.Makespan.HasValue)
            {
                Console.WriteLine($"Makespan: {result.Makespan} min, Status: {result.Status}");
            }

            // Log any warnings
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"Import warning: {warning}");
            }

            // Close dialog on success
            showGanttImportDialog = false;
            ganttImportText = "";
            ganttImportError = "";
            ganttImportAsLayer = false;
            ganttImportIsSolution = false;

            // Update view to show new layer and zoom to fit
            UpdateGanttView();
            StateHasChanged();
            await GanttZoomToFit();
        }
        else
        {
            ganttImportError = string.Join("; ", result.Errors);
            if (result.Warnings.Count > 0)
            {
                ganttImportError += " Warnings: " + string.Join("; ", result.Warnings);
            }
            StateHasChanged();
        }
    }

    /// <summary>
    /// Gets the next available layer color
    /// </summary>
    private string GetNextLayerColor()
    {
        var layers = layerService.GetLayers();
        var usedColors = layers.Select(l => l.LayerColor).ToHashSet();
        var palette = new[] { "#3b82f6", "#22c55e", "#f59e0b", "#ef4444", "#8b5cf6", "#ec4899", "#06b6d4", "#f97316" };

        foreach (var color in palette)
        {
            if (!usedColors.Contains(color))
                return color;
        }

        return palette[layers.Count % palette.Length];
    }

    /// <summary>
    /// Called when import text changes to detect solution format
    /// </summary>
    private void OnGanttImportTextChanged(string text)
    {
        ganttImportText = text;
        ganttImportIsSolution = ganttImportExport.IsSolutionJson(text);

        // Auto-enable layer import if it's a solution and we have existing Gantt tasks
        var hasExistingTasks = nodes.Any(n => n.IsGanttTask);
        if (ganttImportIsSolution && hasExistingTasks)
        {
            ganttImportAsLayer = true;
        }
    }

    /// <summary>
    /// Handle file selection for Gantt import
    /// </summary>
    private async Task OnGanttFileSelected(Microsoft.AspNetCore.Components.Forms.InputFileChangeEventArgs e)
    {
        ganttImportError = "";

        try
        {
            var file = e.File;
            if (file == null) return;

            // Check file size (limit to 5MB)
            if (file.Size > 5 * 1024 * 1024)
            {
                ganttImportError = "File too large. Maximum size is 5MB.";
                return;
            }

            using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            using var reader = new System.IO.StreamReader(stream);
            var text = await reader.ReadToEndAsync();

            // Use the change handler to also detect solution format
            OnGanttImportTextChanged(text);

            StateHasChanged();
        }
        catch (Exception ex)
        {
            ganttImportError = $"Error reading file: {ex.Message}";
        }
    }

    /// <summary>
    /// Export Gantt job-shop data to JSON
    /// </summary>
    private async Task ExportGanttToJson()
    {
        try
        {
            var json = ganttImportExport.ExportToJson(nodes, edges, "Job Shop Schedule");

            // Download the file using JavaScript interop
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var base64 = Convert.ToBase64String(bytes);
            await JS.InvokeVoidAsync("downloadFile", "jobshop.json", "application/json", base64);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Export error: {ex.Message}");
        }
    }

    // ============================================
    // JOB SHOP SOLVER & VALIDATOR
    // ============================================

    private JobShopSolverService jobShopSolver = new();
    private JobShopValidatorService jobShopValidator = new();
    private bool showJobShopSolverDialog = false;
    private string selectedSolverAlgorithm = "SPT";
    private bool isSolving = false;
    private string? solverResultMessage = "";
    private ValidationResult? lastValidationResult;

    /// <summary>
    /// Opens the solver dialog
    /// </summary>
    private void ShowSolverDialog()
    {
        showJobShopSolverDialog = true;
        solverResultMessage = "";
        lastValidationResult = null;
        StateHasChanged();
    }

    /// <summary>
    /// Closes the solver dialog
    /// </summary>
    private void CloseSolverDialog()
    {
        showJobShopSolverDialog = false;
        StateHasChanged();
    }

    /// <summary>
    /// Runs the selected scheduling algorithm on the current Gantt data
    /// </summary>
    private void RunJobShopSolver()
    {
        isSolving = true;
        solverResultMessage = "Solving...";
        StateHasChanged();

        try
        {
            // Convert current Gantt nodes to a JobShopInstance
            var instance = ConvertGanttToJobShopInstance();
            if (instance == null)
            {
                solverResultMessage = "Error: Could not create job shop instance from current data";
                isSolving = false;
                return;
            }

            // Validate instance
            var instanceErrors = instance.Validate();
            if (instanceErrors.Count > 0)
            {
                solverResultMessage = "Instance errors: " + string.Join("; ", instanceErrors);
                isSolving = false;
                return;
            }

            // Parse algorithm
            var algorithm = selectedSolverAlgorithm switch
            {
                "SPT" => JobShopSolverService.Algorithm.SPT,
                "LPT" => JobShopSolverService.Algorithm.LPT,
                "FCFS" => JobShopSolverService.Algorithm.FCFS,
                "MWR" => JobShopSolverService.Algorithm.MWR,
                "LWR" => JobShopSolverService.Algorithm.LWR,
                "Best" => JobShopSolverService.Algorithm.SPT, // Will use SolveMultiple
                _ => JobShopSolverService.Algorithm.SPT
            };

            // Solve
            JobShopSolverResult result;
            if (selectedSolverAlgorithm == "Best")
            {
                result = jobShopSolver.SolveMultiple(instance);
            }
            else
            {
                result = jobShopSolver.Solve(instance, algorithm);
            }

            if (result.Success && result.Solution != null)
            {
                // Apply solution to current Gantt nodes
                ApplyJobShopSolution(result.Solution);

                lastValidationResult = result.Validation;
                solverResultMessage = $"Solved with {result.Solution.Solver}: Makespan = {result.Solution.Makespan} min ({TimeSpan.FromMinutes(result.Solution.Makespan):h\\:mm})";

                if (result.Validation?.Metrics != null)
                {
                    solverResultMessage += $"\nUtilization: {result.Validation.Metrics.AverageMachineUtilization:F1}%";
                    solverResultMessage += $", Lower Bound Gap: {result.Validation.Metrics.GapPercentage:F1}%";
                }
            }
            else
            {
                solverResultMessage = $"Solver failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            solverResultMessage = $"Error: {ex.Message}";
        }

        isSolving = false;
        StateHasChanged();
    }

    /// <summary>
    /// Runs the solver and creates a new layer instead of modifying the base
    /// </summary>
    private void RunJobShopSolverAsLayer()
    {
        isSolving = true;
        solverResultMessage = "Solving...";
        StateHasChanged();

        try
        {
            var instance = ConvertGanttToJobShopInstance();
            if (instance == null)
            {
                solverResultMessage = "Error: Could not create job shop instance from current data";
                isSolving = false;
                return;
            }

            var algorithm = selectedSolverAlgorithm switch
            {
                "SPT" => JobShopSolverService.Algorithm.SPT,
                "LPT" => JobShopSolverService.Algorithm.LPT,
                "FCFS" => JobShopSolverService.Algorithm.FCFS,
                "MWR" => JobShopSolverService.Algorithm.MWR,
                "LWR" => JobShopSolverService.Algorithm.LWR,
                "Best" => JobShopSolverService.Algorithm.SPT,
                _ => JobShopSolverService.Algorithm.SPT
            };

            JobShopSolverResult result;
            if (selectedSolverAlgorithm == "Best")
            {
                result = jobShopSolver.SolveMultiple(instance);
            }
            else
            {
                result = jobShopSolver.Solve(instance, algorithm);
            }

            if (result.Success && result.Solution != null)
            {
                // Create layer overrides from solution
                var overrides = CreateOverridesFromSolution(result.Solution);

                // Create a new layer
                var layer = layerService.CreateLayer(
                    $"{selectedSolverAlgorithm} Solution",
                    result.Solution.Solver ?? selectedSolverAlgorithm,
                    "gantt",
                    overrides
                );

                layer.ComputedMetrics["Makespan"] = result.Solution.Makespan;
                if (result.Validation?.Metrics != null)
                {
                    layer.ComputedMetrics["Utilization"] = (decimal)result.Validation.Metrics.AverageMachineUtilization;
                    layer.ComputedMetrics["LowerBound"] = result.Validation.Metrics.LowerBound;
                }

                solverResultMessage = $"Created layer '{layer.Name}' with makespan {result.Solution.Makespan} min";
                UpdateGanttView();
            }
            else
            {
                solverResultMessage = $"Solver failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            solverResultMessage = $"Error: {ex.Message}";
        }

        isSolving = false;
        StateHasChanged();
    }

    /// <summary>
    /// Validates the current Gantt schedule
    /// </summary>
    private void ValidateJobShopSchedule()
    {
        try
        {
            var instance = ConvertGanttToJobShopInstance();
            var solution = ConvertGanttToJobShopSolution();

            if (instance == null || solution == null)
            {
                solverResultMessage = "Error: Could not create instance/solution from current data";
                return;
            }

            lastValidationResult = jobShopValidator.Validate(instance, solution);

            if (lastValidationResult.IsValid)
            {
                solverResultMessage = $"Valid schedule! Makespan: {lastValidationResult.ComputedMakespan} min";
                if (lastValidationResult.Metrics != null)
                {
                    solverResultMessage += $"\nUtilization: {lastValidationResult.Metrics.AverageMachineUtilization:F1}%";
                    solverResultMessage += $", Lower Bound Gap: {lastValidationResult.Metrics.GapPercentage:F1}%";
                }
            }
            else
            {
                solverResultMessage = $"Invalid schedule: {lastValidationResult.Errors.Count} error(s)\n";
                solverResultMessage += string.Join("\n", lastValidationResult.Errors.Take(5));
                if (lastValidationResult.Errors.Count > 5)
                {
                    solverResultMessage += $"\n... and {lastValidationResult.Errors.Count - 5} more";
                }
            }
        }
        catch (Exception ex)
        {
            solverResultMessage = $"Validation error: {ex.Message}";
        }

        StateHasChanged();
    }

    /// <summary>
    /// Converts current Gantt nodes to a JobShopInstance
    /// </summary>
    private JobShopInstance? ConvertGanttToJobShopInstance()
    {
        var machines = nodes.Where(n => n.IsGanttMachine).OrderBy(n => n.GanttRowIndex).ToList();
        var jobs = nodes.Where(n => n.IsGanttJob).OrderBy(n => n.Id).ToList();
        var tasks = nodes.Where(n => n.IsGanttTask && n.GanttJobId.HasValue).ToList();

        if (machines.Count == 0 || jobs.Count == 0 || tasks.Count == 0)
            return null;

        var instance = new JobShopInstance
        {
            Name = "Current_Schedule",
            MachineCount = machines.Count,
            JobCount = jobs.Count,
            TimeUnit = "minutes",
            MachineNames = machines.Select(m => m.Text ?? $"Machine {m.GanttRowIndex}").ToArray(),
            JobNames = jobs.Select(j => j.Text ?? $"Job {j.Id}").ToArray()
        };

        // Group tasks by job
        var tasksByJob = tasks
            .GroupBy(t => t.GanttJobId!.Value)
            .OrderBy(g => jobs.FindIndex(j => j.Id == g.Key))
            .ToList();

        var data = new List<int[][]>();

        foreach (var jobGroup in tasksByJob)
        {
            // Order operations by precedence (using edges) or by start time
            var jobTasks = OrderTasksByPrecedence(jobGroup.ToList());
            var jobData = new List<int[]>();

            foreach (var task in jobTasks)
            {
                int machineIndex = task.GanttRowIndex >= 0 ? task.GanttRowIndex : 0;
                int duration = (int)(task.GanttDuration?.TotalMinutes ?? 10);
                jobData.Add(new[] { machineIndex, duration });
            }

            data.Add(jobData.ToArray());
        }

        instance.Data = data.ToArray();
        return instance;
    }

    /// <summary>
    /// Converts current Gantt nodes to a JobShopSolution (with start times)
    /// </summary>
    private JobShopSolution? ConvertGanttToJobShopSolution()
    {
        var machines = nodes.Where(n => n.IsGanttMachine).OrderBy(n => n.GanttRowIndex).ToList();
        var jobs = nodes.Where(n => n.IsGanttJob).OrderBy(n => n.Id).ToList();
        var tasks = nodes.Where(n => n.IsGanttTask && n.GanttJobId.HasValue).ToList();

        if (machines.Count == 0 || jobs.Count == 0 || tasks.Count == 0)
            return null;

        var solution = new JobShopSolution
        {
            Name = "Current_Schedule_Solution",
            MachineCount = machines.Count,
            JobCount = jobs.Count,
            TimeUnit = "minutes",
            MachineNames = machines.Select(m => m.Text ?? $"Machine {m.GanttRowIndex}").ToArray(),
            JobNames = jobs.Select(j => j.Text ?? $"Job {j.Id}").ToArray()
        };

        // Group tasks by job
        var tasksByJob = tasks
            .GroupBy(t => t.GanttJobId!.Value)
            .OrderBy(g => jobs.FindIndex(j => j.Id == g.Key))
            .ToList();

        var data = new List<int[][]>();

        foreach (var jobGroup in tasksByJob)
        {
            var jobTasks = OrderTasksByPrecedence(jobGroup.ToList());
            var jobData = new List<int[]>();

            foreach (var task in jobTasks)
            {
                int machineIndex = task.GanttRowIndex >= 0 ? task.GanttRowIndex : 0;
                int duration = (int)(task.GanttDuration?.TotalMinutes ?? 10);
                int startTime = (int)(task.GanttStartTime?.TotalMinutes ?? 0);
                jobData.Add(new[] { machineIndex, duration, startTime });
            }

            data.Add(jobData.ToArray());
        }

        solution.Data = data.ToArray();
        solution.Makespan = solution.CalculateMakespan();
        return solution;
    }

    /// <summary>
    /// Orders tasks by precedence constraints (edges) within a job
    /// </summary>
    private List<Node> OrderTasksByPrecedence(List<Node> jobTasks)
    {
        if (jobTasks.Count <= 1) return jobTasks;

        // Build adjacency from edges
        var taskIds = jobTasks.Select(t => t.Id).ToHashSet();
        var inDegree = jobTasks.ToDictionary(t => t.Id, _ => 0);
        var successors = jobTasks.ToDictionary(t => t.Id, _ => new List<int>());

        foreach (var edge in edges)
        {
            if (taskIds.Contains(edge.From) && taskIds.Contains(edge.To))
            {
                successors[edge.From].Add(edge.To);
                inDegree[edge.To]++;
            }
        }

        // Topological sort
        var result = new List<Node>();
        var queue = new Queue<int>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var task = jobTasks.First(t => t.Id == current);
            result.Add(task);

            foreach (var succ in successors[current])
            {
                inDegree[succ]--;
                if (inDegree[succ] == 0)
                    queue.Enqueue(succ);
            }
        }

        // If topological sort didn't get all tasks, fall back to start time ordering
        if (result.Count < jobTasks.Count)
        {
            return jobTasks.OrderBy(t => t.GanttStartTime ?? TimeSpan.MaxValue).ToList();
        }

        return result;
    }

    /// <summary>
    /// Applies a JobShopSolution to the current Gantt nodes
    /// </summary>
    private void ApplyJobShopSolution(JobShopSolution solution)
    {
        UndoService.SaveState(nodes, edges, edgeLabels);

        var jobs = nodes.Where(n => n.IsGanttJob).OrderBy(n => n.Id).ToList();

        for (int j = 0; j < solution.Data.Length && j < jobs.Count; j++)
        {
            var jobId = jobs[j].Id;
            var jobTasks = nodes
                .Where(n => n.IsGanttTask && n.GanttJobId == jobId)
                .ToList();

            var orderedTasks = OrderTasksByPrecedence(jobTasks);

            for (int o = 0; o < solution.Data[j].Length && o < orderedTasks.Count; o++)
            {
                var task = orderedTasks[o];
                int startTime = solution.GetStartTime(j, o);
                int duration = solution.GetProcessingTime(j, o);
                int machine = solution.GetMachineId(j, o);

                task.GanttStartTime = TimeSpan.FromMinutes(startTime);
                task.GanttDuration = TimeSpan.FromMinutes(duration);
                task.GanttRowIndex = machine;

                // Reposition node
                ganttTimeline?.PositionNodeForTimeline(task);
            }
        }

        ValidateGanttPrecedences();
        UpdateGanttView();
    }

    /// <summary>
    /// Creates NodeOverrides from a JobShopSolution for layer creation
    /// </summary>
    private Dictionary<int, NodeOverride> CreateOverridesFromSolution(JobShopSolution solution)
    {
        var overrides = new Dictionary<int, NodeOverride>();
        var jobs = nodes.Where(n => n.IsGanttJob).OrderBy(n => n.Id).ToList();

        for (int j = 0; j < solution.Data.Length && j < jobs.Count; j++)
        {
            var jobId = jobs[j].Id;
            var jobTasks = nodes
                .Where(n => n.IsGanttTask && n.GanttJobId == jobId)
                .ToList();

            var orderedTasks = OrderTasksByPrecedence(jobTasks);

            for (int o = 0; o < solution.Data[j].Length && o < orderedTasks.Count; o++)
            {
                var task = orderedTasks[o];
                var newStart = TimeSpan.FromMinutes(solution.GetStartTime(j, o));
                var newDuration = TimeSpan.FromMinutes(solution.GetProcessingTime(j, o));
                int newMachine = solution.GetMachineId(j, o);

                var nodeOverride = new NodeOverride { NodeId = task.Id };
                bool hasChanges = false;

                if (task.GanttStartTime != newStart)
                {
                    nodeOverride.GanttStartTime = newStart;
                    hasChanges = true;
                }
                if (task.GanttDuration != newDuration)
                {
                    nodeOverride.GanttDuration = newDuration;
                    hasChanges = true;
                }
                if (task.GanttRowIndex != newMachine)
                {
                    nodeOverride.GanttRowIndex = newMachine;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    overrides[task.Id] = nodeOverride;
                }
            }
        }

        return overrides;
    }
}
