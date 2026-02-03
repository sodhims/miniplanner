using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

/// <summary>
/// Partial class for Project chart mouse handlers and interactions (Node-based)
/// </summary>
public partial class DFDEditor
{
    // ============================================
    // GANTT MOUSE HANDLERS
    // ============================================

    /// <summary>Handle mouse down on Project canvas</summary>
    private void HandleProjectMouseDown(MouseEventArgs e)
    {
        if (projectTimeline == null) return;

        // Check if clicking on a task bar
        var clickedNode = GetProjectNodeAtPosition(e.OffsetX, e.OffsetY);

        if (clickedNode != null)
        {
            if (isCreatingProjectDependency)
            {
                // Dependency creation mode
                HandleProjectDependencyClick(clickedNode.Id);
            }
            else if (chainMode || connectionMode == ConnectionModeType.Chain)
            {
                // Chain mode - create dependencies in sequence (click-click-click)
                SelectProjectTask(clickedNode.Id);
            }
            else if (mode == EditorMode.AddNode && selectedTemplateId == "project" &&
                     (selectedTemplateShapeId == "resource" || selectedTemplateShapeId?.StartsWith("resource-") == true) &&
                     !clickedNode.IsProjectResource)
            {
                // Resource assignment mode: clicking on a task with a resource shape selected
                // Assign the resource to this task
                selectedProjectTaskId = clickedNode.Id;
                AssignSelectedResourceToSelectedTask();
            }
            else
            {
                // Start dragging the task
                selectedProjectTaskId = clickedNode.Id;
                draggingProjectTaskId = clickedNode.Id;
                projectDragStartX = e.OffsetX;
                projectDragOriginalStart = clickedNode.ProjectStartDate;
                projectDragOriginalDuration = clickedNode.ProjectDurationDays;

                // Check if we're clicking on resize handles
                var bounds = projectTimeline.GetNodeBarBounds(clickedNode);
                var rowIndex = clickedNode.ProjectRowIndex >= 0 ? clickedNode.ProjectRowIndex : GetProjectTaskRowIndex(clickedNode.Id);
                var barY = projectTimeline.RowToY(rowIndex) + 6;
                var barHeight = projectTimeline.RowHeight - 12;

                // Left resize handle region (8px wide)
                if (e.OffsetX >= bounds.x - 4 && e.OffsetX <= bounds.x + 4 &&
                    e.OffsetY >= barY && e.OffsetY <= barY + barHeight)
                {
                    isResizingProjectTask = true;
                    isResizingProjectFromLeft = true;
                }
                // Right resize handle region (8px wide)
                else if (e.OffsetX >= bounds.x + bounds.width - 4 && e.OffsetX <= bounds.x + bounds.width + 4 &&
                         e.OffsetY >= barY && e.OffsetY <= barY + barHeight)
                {
                    isResizingProjectTask = true;
                    isResizingProjectFromLeft = false;
                }
                else
                {
                    isResizingProjectTask = false;
                }
            }
        }
        else
        {
            // Clicked on empty space
            if (isCreatingProjectDependency)
            {
                // Don't deselect if creating dependency
            }
            else if (mode == EditorMode.AddNode && selectedTemplateId == "project" && !string.IsNullOrEmpty(selectedTemplateShapeId))
            {
                // Resource shapes: clicking on task assigns resource, clicking empty space creates resource
                if (selectedTemplateShapeId == "resource" || selectedTemplateShapeId.StartsWith("resource-"))
                {
                    // Must have a valid projectTimeline
                    if (projectTimeline == null) return;

                    // Try to find task at click position
                    var clickedTask = GetProjectTaskNodeAtPosition(e.OffsetX, e.OffsetY);
                    if (clickedTask != null && !clickedTask.IsProjectResource)
                    {
                        // Clicked on a task with a resource shape selected
                        // Create new resource and assign to task
                        selectedProjectTaskId = clickedTask.Id;
                        AssignSelectedResourceToSelectedTask();
                    }
                    else if (e.OffsetX >= projectTimeline.LabelWidth)
                    {
                        // Clicked on empty space with resource shape - create the resource
                        CreateProjectNodeAtPosition(e.OffsetX, e.OffsetY, selectedTemplateShapeId);
                    }
                    return;
                }

                // Non-resource shapes (task, milestone, summary)
                // Create at clicked position, but only in timeline area
                if (projectTimeline != null && e.OffsetX >= projectTimeline.LabelWidth)
                {
                    Console.WriteLine($"Project AddNode mode: creating {selectedTemplateShapeId} at ({e.OffsetX}, {e.OffsetY})");
                    CreateProjectNodeAtPosition(e.OffsetX, e.OffsetY, selectedTemplateShapeId);
                }
            }
            else
            {
                selectedProjectTaskId = null;
            }
        }

        // Update rubberband position for dependency creation
        if (isCreatingProjectDependency)
        {
            projectRubberbandX = e.OffsetX;
            projectRubberbandY = e.OffsetY;
        }

        StateHasChanged();
    }

    /// <summary>Start dragging a specific task bar (called directly from the task bar element)</summary>
    private void StartProjectTaskDrag(int nodeId, MouseEventArgs e)
    {
        Console.WriteLine($"StartProjectTaskDrag: nodeId={nodeId}, X={e.OffsetX}, shift={e.ShiftKey}, ctrl={e.CtrlKey}");

        // If Shift or Ctrl is held, handle multi-selection instead of dragging
        if (e.ShiftKey || e.CtrlKey)
        {
            Console.WriteLine($"StartProjectTaskDrag: modifier key held, delegating to SelectProjectTask");
            SelectProjectTask(nodeId, e);
            return;
        }

        if (projectTimeline == null)
        {
            Console.WriteLine("StartProjectTaskDrag: projectTimeline is null!");
            return;
        }

        var node = GetProjectNode(nodeId);
        if (node == null)
        {
            Console.WriteLine($"StartProjectTaskDrag: node {nodeId} not found!");
            return;
        }

        // Select and start dragging
        selectedProjectTaskId = nodeId;
        draggingProjectTaskId = nodeId;
        projectDragStartX = e.OffsetX;
        projectDragOriginalStart = node.ProjectStartDate;
        projectDragOriginalDuration = node.ProjectDurationDays;
        isResizingProjectTask = false;

        Console.WriteLine($"StartProjectTaskDrag: drag started for '{node.Text}'");
        StateHasChanged();
    }

    /// <summary>Handle mouse move on Project canvas</summary>
    private void HandleProjectMouseMove(MouseEventArgs e)
    {
        if (projectTimeline == null) return;

        // Handle deadline marker dragging
        if (isDraggingProjectDeadline)
        {
            HandleProjectDeadlineDrag(e);
            return;
        }

        // Handle resource dragging
        if (draggingResourceId.HasValue)
        {
            HandleResourceDragMove(e);
            return;
        }

        // Log when dragging is active
        if (draggingProjectTaskId.HasValue)
        {
            Console.WriteLine($"HandleProjectMouseMove: dragging task {draggingProjectTaskId.Value}, X={e.OffsetX}");
        }

        // Update rubberband for dependency creation
        if (isCreatingProjectDependency && ProjectDependencyFromTaskId.HasValue)
        {
            projectRubberbandX = e.OffsetX;
            projectRubberbandY = e.OffsetY;
            StateHasChanged();
            return;
        }

        // Handle task dragging/resizing
        if (draggingProjectTaskId.HasValue)
        {
            var node = GetProjectNode(draggingProjectTaskId.Value);
            if (node == null || projectDragOriginalStart == null) return;

            var newDate = projectTimeline.XToDateTime(e.OffsetX);
            newDate = projectTimeline.SnapDateTimeToWorkingDay(newDate);

            if (isResizingProjectTask)
            {
                if (isResizingProjectFromLeft)
                {
                    // Resize from left - change start date, adjust duration
                    if (node.ProjectEndDate != null && newDate < node.ProjectEndDate.Value)
                    {
                        var originalEnd = node.ProjectEndDate.Value;
                        node.ProjectStartDate = newDate;
                        node.ProjectEndDate = originalEnd;
                        ProjectTimelineService.SetNodeDurationFromDates(node);
                    }
                }
                else
                {
                    // Resize from right - keep start date, change end date
                    if (node.ProjectStartDate != null && newDate > node.ProjectStartDate.Value)
                    {
                        node.ProjectEndDate = newDate;
                        ProjectTimelineService.SetNodeDurationFromDates(node);
                    }
                }
            }
            else
            {
                // Moving the task - calculate day offset from drag start
                var dragStartDate = projectTimeline.XToDateTime(projectDragStartX);
                var daysDiff = (int)(newDate - dragStartDate).TotalDays;

                if (daysDiff != 0)
                {
                    node.ProjectStartDate = projectDragOriginalStart.Value.AddDays(daysDiff);
                    node.ProjectDurationDays = projectDragOriginalDuration ?? node.ProjectDurationDays;
                    ProjectTimelineService.SetNodeEndDateFromDuration(node);
                }
            }

            // Update node position on timeline
            projectTimeline.PositionNodeForTimeline(node);
            StateHasChanged();
        }
    }

    /// <summary>Handle mouse up on Project canvas</summary>
    private void HandleProjectMouseUp(MouseEventArgs e)
    {
        // Stop deadline dragging
        if (isDraggingProjectDeadline)
        {
            StopProjectDeadlineDrag();
        }

        // Handle resource drag end
        if (draggingResourceId.HasValue)
        {
            HandleResourceDragEnd(e);
            return;
        }

        if (draggingProjectTaskId.HasValue)
        {
            // Snap to working day on release
            if (projectTimeline != null)
            {
                var node = GetProjectNode(draggingProjectTaskId.Value);
                if (node != null && node.ProjectStartDate != null)
                {
                    node.ProjectStartDate = projectTimeline.SnapDateTimeToWorkingDay(node.ProjectStartDate.Value);
                    ProjectTimelineService.SetNodeEndDateFromDuration(node);
                    projectTimeline.PositionNodeForTimeline(node);
                }
            }

            UpdateProjectView();
        }

        // Always reset drag state to prevent stuck states
        draggingProjectTaskId = null;
        isResizingProjectTask = false;
        projectDragOriginalStart = null;
        projectDragOriginalDuration = null;
        StateHasChanged();
    }

    /// <summary>Handle double-click on Project canvas</summary>
    private void HandleProjectDoubleClick(MouseEventArgs e)
    {
        if (projectTimeline == null) return;

        var clickedNode = GetProjectNodeAtPosition(e.OffsetX, e.OffsetY);
        if (clickedNode != null)
        {
            EditProjectTask(clickedNode.Id);
        }
        else
        {
            // Double-click on empty space - add new task at that date
            // But only if clicking in the timeline area, not the label column
            if (e.OffsetX >= projectTimeline.LabelWidth)
            {
                var clickDate = projectTimeline.XToDateTime(e.OffsetX);
                clickDate = projectTimeline.SnapDateTimeToWorkingDay(clickDate);

                var projectNodes = GetprojectNodes().ToList();
                var newId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;

                // Find the highest task number to ensure unique naming
                var taskNodes = projectNodes.Where(n => !n.ProjectIsMilestone && !n.IsSuperNode && !n.IsProjectResource).ToList();
                int nextTaskNum = 1;
                foreach (var t in taskNodes)
                {
                    if (t.Text.StartsWith("Task ") && int.TryParse(t.Text.Substring(5), out int num))
                    {
                        nextTaskNum = Math.Max(nextTaskNum, num + 1);
                    }
                }

                var newNode = ProjectTimelineService.CreateProjectTaskNode(
                    newId,
                    $"Task {nextTaskNum}",
                    clickDate,
                    5);

                // Calculate row index from Y position
                var rowIndex = projectTimeline.YToRow(e.OffsetY);
                rowIndex = Math.Max(0, rowIndex);

                // Each task gets its own row - find the first empty row at or after clicked position
                var existingNodes = GetprojectNodes().ToList();
                while (IsRowOccupied(existingNodes, rowIndex))
                {
                    rowIndex++;
                }

                newNode.ProjectRowIndex = rowIndex;

                nodes.Add(newNode);
                selectedProjectTaskId = newNode.Id;
                UpdateProjectView();
                StateHasChanged();
            }
        }
    }

    /// <summary>Get the Project node at a given pixel position</summary>
    private Node? GetProjectNodeAtPosition(double x, double y)
    {
        if (projectTimeline == null) return null;

        var orderedNodes = GetOrderedprojectNodes();
        for (int i = 0; i < orderedNodes.Count; i++)
        {
            var node = orderedNodes[i];
            var rowIndex = node.ProjectRowIndex >= 0 ? node.ProjectRowIndex : i;
            var rowY = projectTimeline.RowToY(rowIndex);
            var barY = rowY + 6;
            var barHeight = projectTimeline.RowHeight - 12;

            // Check if Y is in this row
            if (y < barY || y > barY + barHeight) continue;

            if (node.ProjectIsMilestone)
            {
                // Milestone diamond hit test
                if (node.ProjectStartDate == null) continue;
                var mx = projectTimeline.DateTimeToX(node.ProjectStartDate.Value) + projectTimeline.DayWidth / 2;
                var my = rowY + projectTimeline.RowHeight / 2;
                var dist = Math.Sqrt(Math.Pow(x - mx, 2) + Math.Pow(y - my, 2));
                if (dist <= 15) return node;
            }
            else
            {
                // Task bar hit test
                var bounds = projectTimeline.GetNodeBarBounds(node);
                if (x >= bounds.x && x <= bounds.x + bounds.width)
                {
                    return node;
                }
            }
        }

        return null;
    }

    /// <summary>Check if a row already has a task assigned to it</summary>
    private bool IsRowOccupied(List<Node> projectNodes, int rowIndex)
    {
        return projectNodes.Any(n =>
            (n.ProjectRowIndex >= 0 ? n.ProjectRowIndex : GetProjectTaskRowIndex(n.Id)) == rowIndex);
    }

    // ============================================
    // DEPENDENCY CREATION
    // ============================================

    /// <summary>Handle clicking a node during dependency creation</summary>
    private void HandleProjectDependencyClick(int nodeId)
    {
        if (!isCreatingProjectDependency) return;

        if (ProjectDependencyFromTaskId == null)
        {
            // First click - set source node
            ProjectDependencyFromTaskId = nodeId;
        }
        else if (ProjectDependencyFromTaskId.Value != nodeId)
        {
            // Second click - create dependency
            AddProjectDependency(ProjectDependencyFromTaskId.Value, nodeId);

            // Chain mode: keep the target as the new source for continuous linking
            // User can click Cancel Link or press Escape to exit
            ProjectDependencyFromTaskId = nodeId;

            UpdateProjectView();
        }
        // If clicking the same node, do nothing

        StateHasChanged();
    }

    /// <summary>Start dependency creation mode</summary>
    private void StartProjectDependencyCreation()
    {
        isCreatingProjectDependency = true;
        ProjectDependencyFromTaskId = null;
        StateHasChanged();
    }

    /// <summary>Cancel dependency creation mode</summary>
    private void CancelProjectDependencyCreation()
    {
        isCreatingProjectDependency = false;
        ProjectDependencyFromTaskId = null;
        StateHasChanged();
    }

    /// <summary>Delete a dependency between two nodes</summary>
    private void DeleteProjectDependency(int predecessorId, int successorId)
    {
        RemoveProjectDependency(predecessorId, successorId);
        StateHasChanged();
    }

    // ============================================
    // AUTO-SCHEDULING
    // ============================================

    /// <summary>
    /// Auto-schedule a task and all its successors to start as early as possible.
    /// Called on double-click of a task bar.
    /// </summary>
    private void AutoScheduleTaskAndSuccessors(int nodeId)
    {
        if (projectTimeline == null) return;

        var node = GetProjectNode(nodeId);
        if (node == null) return;

        Console.WriteLine($"AutoScheduleTaskAndSuccessors: starting from '{node.Text}'");

        // Get all Project dependencies
        var projectDeps = GetProjectDependencies().ToList();

        // Build successor lookup: nodeId -> list of (successorId, dependency)
        var successorLookup = projectDeps
            .GroupBy(e => e.From)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build predecessor lookup: nodeId -> list of (predecessorId, dependency)
        var predecessorLookup = projectDeps
            .GroupBy(e => e.To)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Get all tasks that need to be scheduled (this task + all successors recursively)
        var tasksToSchedule = new HashSet<int> { nodeId };
        var queue = new Queue<int>();
        queue.Enqueue(nodeId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (successorLookup.TryGetValue(currentId, out var successors))
            {
                foreach (var dep in successors)
                {
                    if (tasksToSchedule.Add(dep.To))
                    {
                        queue.Enqueue(dep.To);
                    }
                }
            }
        }

        Console.WriteLine($"AutoScheduleTaskAndSuccessors: scheduling {tasksToSchedule.Count} tasks");

        // Schedule tasks in topological order (process predecessors first)
        var scheduled = new HashSet<int>();
        var toProcess = tasksToSchedule.ToList();

        // Keep processing until all are scheduled
        int maxIterations = toProcess.Count * 2; // Safety limit
        int iteration = 0;
        while (scheduled.Count < tasksToSchedule.Count && iteration++ < maxIterations)
        {
            foreach (var taskId in toProcess)
            {
                if (scheduled.Contains(taskId)) continue;

                var task = GetProjectNode(taskId);
                if (task == null)
                {
                    scheduled.Add(taskId);
                    continue;
                }

                // Check if all predecessors (that are in our set) are scheduled
                var preds = predecessorLookup.TryGetValue(taskId, out var predList) ? predList : new List<Edge>();
                var predsInSet = preds.Where(p => tasksToSchedule.Contains(p.From)).ToList();

                if (predsInSet.All(p => scheduled.Contains(p.From)))
                {
                    // Skip milestones - they are externally set fixed dates
                    if (task.ProjectIsMilestone)
                    {
                        Console.WriteLine($"  Skipping milestone '{task.Text}' (fixed date)");
                        scheduled.Add(taskId);
                        continue;
                    }

                    // Calculate earliest start based on all predecessors
                    DateTime earliestStart = task.ProjectStartDate ?? DateTime.Today;

                    foreach (var predDep in preds)
                    {
                        var predNode = GetProjectNode(predDep.From);
                        if (predNode?.ProjectEndDate == null) continue;

                        // Calculate constrained start based on dependency type
                        var constrainedStart = CalculateConstrainedStartFromPredecessor(
                            predNode, predDep.ProjectDepType, predDep.ProjectLagDays);

                        if (constrainedStart > earliestStart)
                        {
                            earliestStart = constrainedStart;
                        }
                    }

                    // Snap to working day
                    earliestStart = projectTimeline.SnapDateTimeToWorkingDay(earliestStart);

                    // Update task dates
                    if (task.ProjectStartDate != earliestStart)
                    {
                        Console.WriteLine($"  Moving '{task.Text}' from {task.ProjectStartDate:d} to {earliestStart:d}");
                        task.ProjectStartDate = earliestStart;
                        ProjectTimelineService.SetNodeEndDateFromDuration(task);
                    }

                    scheduled.Add(taskId);
                }
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// Calculate the earliest start date for a successor based on predecessor and dependency type
    /// </summary>
    private DateTime CalculateConstrainedStartFromPredecessor(Node predecessor, ProjectDependencyType depType, int lagDays)
    {
        var predStart = predecessor.ProjectStartDate ?? DateTime.Today;
        var predEnd = predecessor.ProjectEndDate ?? DateTime.Today;

        DateTime baseDate = depType switch
        {
            ProjectDependencyType.FinishToStart => predEnd.AddDays(1), // Start after predecessor finishes
            ProjectDependencyType.StartToStart => predStart,           // Start when predecessor starts
            ProjectDependencyType.FinishToFinish => predEnd,           // Finish when predecessor finishes (handled differently)
            ProjectDependencyType.StartToFinish => predStart,          // Finish when predecessor starts (rare)
            _ => predEnd.AddDays(1)
        };

        return baseDate.AddDays(lagDays);
    }

    // ============================================
    // TASK EDITING
    // ============================================

    /// <summary>Open task edit dialog</summary>
    private void EditProjectTask(int nodeId)
    {
        var node = GetProjectNode(nodeId);
        if (node == null) return;

        // Check if this is a resource - show resource dialog instead
        if (node.IsProjectResource)
        {
            EditProjectResource(nodeId);
            return;
        }

        editingProjectTask = node;
        // Create a copy of the task data for editing
        projectEditTaskName = node.Text;
        projectEditStartDate = node.ProjectStartDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
        projectEditDuration = node.ProjectDurationDays;
        projectEditProgress = node.ProjectPercentComplete;
        projectEditIsMilestone = node.ProjectIsMilestone;
        projectEditNotes = node.ProjectNotes ?? "";

        showProjectTaskDialog = true;
        StateHasChanged();
    }

    /// <summary>Open resource edit dialog</summary>
    private void EditProjectResource(int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId && n.IsProjectResource);
        if (node == null) return;

        editingProjectResource = node;
        projectEditResourceName = node.Text;
        projectEditResourceType = node.ProjectResourceType;
        projectEditResourceCapacity = node.ProjectResourceCapacity;
        projectEditResourceRate = node.ProjectResourceRate;
        projectEditResourceEmail = node.ProjectResourceEmail ?? "";

        showProjectResourceDialog = true;
        StateHasChanged();
    }

    /// <summary>Save changes from resource edit dialog</summary>
    private void SaveProjectResourceEdit()
    {
        if (editingProjectResource == null) return;

        editingProjectResource.Text = projectEditResourceName;
        editingProjectResource.ProjectResourceType = projectEditResourceType;
        editingProjectResource.ProjectResourceCapacity = Math.Clamp(projectEditResourceCapacity, 0, 100);
        editingProjectResource.ProjectResourceRate = projectEditResourceRate;
        editingProjectResource.ProjectResourceEmail = string.IsNullOrWhiteSpace(projectEditResourceEmail) ? null : projectEditResourceEmail;

        // Update the template shape ID based on resource type
        editingProjectResource.TemplateShapeId = $"resource-{projectEditResourceType.ToString().ToLower()}";

        showProjectResourceDialog = false;
        editingProjectResource = null;
        StateHasChanged();
    }

    /// <summary>Cancel resource edit dialog</summary>
    private void CancelProjectResourceEdit()
    {
        showProjectResourceDialog = false;
        editingProjectResource = null;
        StateHasChanged();
    }

    /// <summary>Save changes from task edit dialog</summary>
    private void SaveProjectTaskEdit()
    {
        if (editingProjectTask == null) return;

        // Parse and apply changes
        editingProjectTask.Text = projectEditTaskName;

        if (DateTime.TryParseExact(projectEditStartDate, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsedDate))
        {
            editingProjectTask.ProjectStartDate = parsedDate;
        }

        editingProjectTask.ProjectDurationDays = Math.Max(0, projectEditDuration);
        editingProjectTask.ProjectPercentComplete = Math.Clamp(projectEditProgress, 0, 100);
        editingProjectTask.ProjectIsMilestone = projectEditIsMilestone;
        editingProjectTask.ProjectNotes = projectEditNotes;

        if (editingProjectTask.ProjectIsMilestone)
        {
            editingProjectTask.ProjectDurationDays = 0;
            editingProjectTask.ProjectEndDate = editingProjectTask.ProjectStartDate;
            editingProjectTask.TemplateShapeId = "milestone";
        }
        else
        {
            ProjectTimelineService.SetNodeEndDateFromDuration(editingProjectTask);
            editingProjectTask.TemplateShapeId = "task";
        }

        showProjectTaskDialog = false;
        editingProjectTask = null;

        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>Cancel task edit dialog</summary>
    private void CancelProjectTaskEdit()
    {
        showProjectTaskDialog = false;
        editingProjectTask = null;
        StateHasChanged();
    }

    /// <summary>Delete the task being edited</summary>
    private void DeleteEditingProjectTask()
    {
        if (editingProjectTask == null) return;

        nodes.Remove(editingProjectTask);
        // Remove related dependencies
        edges.RemoveAll(e => e.IsProjectDependency &&
            (e.From == editingProjectTask.Id || e.To == editingProjectTask.Id));

        showProjectTaskDialog = false;
        editingProjectTask = null;
        selectedProjectTaskId = null;

        UpdateProjectView();
        StateHasChanged();
    }

    // Task edit dialog form fields
    private string projectEditTaskName = "";
    private string projectEditStartDate = "";
    private int projectEditDuration = 5;
    private int projectEditProgress = 0;
    private bool projectEditIsMilestone = false;
    private string projectEditNotes = "";

    // ============================================
    // KEYBOARD HANDLERS
    // ============================================

    /// <summary>Handle keyboard input in Project mode</summary>
    private void HandleProjectKeyDown(KeyboardEventArgs e)
    {
        if (!isProjectMode) return;

        switch (e.Key)
        {
            case "Delete":
            case "Backspace":
                // Delete selected edges/dependencies first
                if (selectedEdges.Any())
                {
                    DeleteSelectedEdges();
                }
                // Bulk delete if multiple tasks are selected
                else if (selectedNodes.Count > 1)
                {
                    DeleteSelectedProjectTasks();
                }
                else if (selectedProjectTaskId.HasValue)
                {
                    DeleteSelectedProjectTask();
                }
                break;

            case "Escape":
                if (isCreatingProjectDependency)
                {
                    CancelProjectDependencyCreation();
                }
                else if (showProjectTaskDialog)
                {
                    CancelProjectTaskEdit();
                }
                else
                {
                    selectedNodes.Clear();
                    selectedProjectTaskId = null;
                    StateHasChanged();
                }
                break;

            case "Enter":
                if (selectedProjectTaskId.HasValue && !showProjectTaskDialog)
                {
                    EditProjectTask(selectedProjectTaskId.Value);
                }
                break;

            case "+":
            case "=":
                ProjectZoomIn();
                break;

            case "-":
                ProjectZoomOut();
                break;
        }
    }

    // ============================================
    // HELPER METHODS
    // ============================================

    /// <summary>Start resizing a Project node from resize handle</summary>
    private void StartProjectResize(int nodeId, bool fromLeft)
    {
        draggingProjectTaskId = nodeId;
        isResizingProjectTask = true;
        isResizingProjectFromLeft = fromLeft;

        var node = GetProjectNode(nodeId);
        if (node != null)
        {
            projectDragOriginalStart = node.ProjectStartDate;
            projectDragOriginalDuration = node.ProjectDurationDays;
        }
    }

    /// <summary>Select a Project node - handles connection modes like regular nodes</summary>
    private void SelectProjectTask(int nodeId) => SelectProjectTask(nodeId, null);

    /// <summary>Select a Project node with optional multi-select support via MouseEventArgs</summary>
    private void SelectProjectTask(int nodeId, MouseEventArgs? e)
    {
        Console.WriteLine($"SelectProjectTask - nodeId={nodeId}, connectionMode={connectionMode}, chainMode={chainMode}, ctrl={e?.CtrlKey}, shift={e?.ShiftKey}");

        // Handle Shift+Click for range selection
        if (e != null && e.ShiftKey)
        {
            Console.WriteLine($"  Shift+Click detected! selectedProjectTaskId={selectedProjectTaskId}");

            if (!selectedProjectTaskId.HasValue)
            {
                // No anchor - just select this task as the anchor
                selectedNodes.Clear();
                selectedNodes.Add(nodeId);
                selectedProjectTaskId = nodeId;
                Console.WriteLine($"  No anchor, setting anchor to {nodeId}");
                StateHasChanged();
                return;
            }

            // Get all visible Project tasks sorted by row index
            var ProjectTasks = nodes
                .Where(n => n.TemplateId == "project" && !n.IsProjectResource && !n.IsSuperNode)
                .OrderBy(n => n.ProjectRowIndex)
                .ToList();

            Console.WriteLine($"  Found {ProjectTasks.Count} Project tasks");

            var anchorNode = ProjectTasks.FirstOrDefault(n => n.Id == selectedProjectTaskId.Value);
            var targetNode = ProjectTasks.FirstOrDefault(n => n.Id == nodeId);

            Console.WriteLine($"  anchorNode={anchorNode?.Text} (row {anchorNode?.ProjectRowIndex}), targetNode={targetNode?.Text} (row {targetNode?.ProjectRowIndex})");

            if (anchorNode != null && targetNode != null)
            {
                var startRow = Math.Min(anchorNode.ProjectRowIndex, targetNode.ProjectRowIndex);
                var endRow = Math.Max(anchorNode.ProjectRowIndex, targetNode.ProjectRowIndex);

                // Select all tasks in the range
                foreach (var task in ProjectTasks.Where(t => t.ProjectRowIndex >= startRow && t.ProjectRowIndex <= endRow))
                {
                    selectedNodes.Add(task.Id);
                    Console.WriteLine($"    Adding task {task.Id} ({task.Text}) to selection");
                }

                Console.WriteLine($"  Shift+Click range select: rows {startRow}-{endRow}, selectedNodes count = {selectedNodes.Count}");
                StateHasChanged();
                return;
            }
            else
            {
                Console.WriteLine($"  Could not find anchor or target in Project tasks list");
            }
        }

        // Handle Ctrl+Click for toggle individual selection
        if (e != null && e.CtrlKey)
        {
            if (selectedNodes.Contains(nodeId))
            {
                // Toggle off if already selected
                selectedNodes.Remove(nodeId);
            }
            else
            {
                // Add to selection
                selectedNodes.Add(nodeId);
            }
            selectedProjectTaskId = nodeId;
            Console.WriteLine($"  Ctrl+Click toggle: selectedNodes count = {selectedNodes.Count}");
            StateHasChanged();
            return;
        }

        // Handle old-style Project dependency creation (separate from standard connection modes)
        if (isCreatingProjectDependency)
        {
            HandleProjectDependencyClick(nodeId);
            return;
        }

        var clickedNode = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (clickedNode == null) return;

        // CHAIN MODE - connect nodes in sequence
        if (connectionMode == ConnectionModeType.Chain || chainMode)
        {
            Console.WriteLine($"  CHAIN MODE: lastChainedNodeId={lastChainedNodeId}");

            if (lastChainedNodeId.HasValue && lastChainedNodeId.Value != nodeId)
            {
                Console.WriteLine($"  CREATING Project dependency: {lastChainedNodeId.Value} -> {nodeId}");
                UndoService.SaveState(nodes, edges, edgeLabels);

                // Create a Project dependency edge
                AddProjectDependency(lastChainedNodeId.Value, nodeId, ProjectDependencyType.FinishToStart);
                Console.WriteLine($"  Project dependency created: {lastChainedNodeId.Value} -> {nodeId}");
            }

            lastChainedNodeId = nodeId;
            selectedProjectTaskId = nodeId;
            StateHasChanged();
            return;
        }

        // TWO-CLICK MODE - first click selects source, second creates connection
        if (connectionMode == ConnectionModeType.TwoClick)
        {
            Console.WriteLine($"  TWO-CLICK MODE: twoClickSourceNode={twoClickSourceNode}");

            if (twoClickSourceNode == null)
            {
                // First click - set source
                twoClickSourceNode = nodeId;
                selectedProjectTaskId = nodeId;
                Console.WriteLine($"  Set source node: {nodeId}");
            }
            else if (twoClickSourceNode.Value != nodeId)
            {
                // Second click - create connection
                Console.WriteLine($"  CREATING Project dependency: {twoClickSourceNode.Value} -> {nodeId}");
                UndoService.SaveState(nodes, edges, edgeLabels);

                AddProjectDependency(twoClickSourceNode.Value, nodeId, ProjectDependencyType.FinishToStart);
                Console.WriteLine($"  Project dependency created: {twoClickSourceNode.Value} -> {nodeId}");

                twoClickSourceNode = null;
                selectedProjectTaskId = nodeId;
            }
            else
            {
                // Clicked same node - deselect source
                twoClickSourceNode = null;
            }

            StateHasChanged();
            return;
        }

        // ONE-TO-N MODE - first click sets source, subsequent clicks connect to it
        if (connectionMode == ConnectionModeType.OneToN)
        {
            Console.WriteLine($"  ONE-TO-N MODE: oneToNSourceNode={oneToNSourceNode}");

            if (oneToNSourceNode == null)
            {
                // First click - set source
                oneToNSourceNode = nodeId;
                selectedProjectTaskId = nodeId;
                Console.WriteLine($"  Set 1:N source node: {nodeId}");
            }
            else if (oneToNSourceNode.Value != nodeId)
            {
                // Subsequent click - create connection from source to this node
                Console.WriteLine($"  CREATING Project dependency: {oneToNSourceNode.Value} -> {nodeId}");
                UndoService.SaveState(nodes, edges, edgeLabels);

                AddProjectDependency(oneToNSourceNode.Value, nodeId, ProjectDependencyType.FinishToStart);
                Console.WriteLine($"  Project dependency created: {oneToNSourceNode.Value} -> {nodeId}");
                selectedProjectTaskId = nodeId;
            }

            StateHasChanged();
            return;
        }

        // Normal selection - clear multi-select and select just this task
        selectedNodes.Clear();
        selectedNodes.Add(nodeId);
        selectedProjectTaskId = nodeId;
        Console.WriteLine($"  Normal select: selectedNodes count = {selectedNodes.Count}");
        StateHasChanged();
    }

    /// <summary>Get CSS class for task bar based on state</summary>
    private string GetProjectTaskBarClass(int nodeId)
    {
        var classes = new List<string> { "project-task-bar" };

        if (selectedProjectTaskId == nodeId)
            classes.Add("selected");

        if (IsTaskCritical(nodeId) && showProjectCriticalPath)
            classes.Add("critical");

        if (draggingProjectTaskId == nodeId)
            classes.Add("dragging");

        return string.Join(" ", classes);
    }

    /// <summary>Get connection point for dependency arrow based on type</summary>
    private (double x, double y) GetProjectDependencyPoint(Node node, bool isStart, ProjectDependencyType depType)
    {
        if (projectTimeline == null) return (0, 0);

        return projectTimeline.GetNodeDependencyPoint(node, isStart, depType);
    }

    /// <summary>
    /// Handle node click in Project node view mode.
    /// Supports connection modes (chain, two-click, 1:N) and selection.
    /// </summary>
    private void HandleProjectNodeClick(int nodeId, MouseEventArgs e)
    {
        Console.WriteLine($"HandleProjectNodeClick - nodeId={nodeId}, connectionMode={connectionMode}");

        // Use SelectProjectTask which already handles all connection modes
        SelectProjectTask(nodeId);

        // Also add to regular node selection for consistency
        if (!selectedNodes.Contains(nodeId))
        {
            if (!e.CtrlKey && !e.ShiftKey)
            {
                selectedNodes.Clear();
            }
            selectedNodes.Add(nodeId);
        }
    }

    /// <summary>
    /// Layout Project nodes for node view mode.
    /// Preserves relative positions from timeline (date → X, row → Y) but with compact node sizes.
    /// In node view, grouped tasks are hidden and only the group node is shown.
    /// </summary>
    private void LayoutprojectNodesForNodeView()
    {
        var allprojectNodes = GetprojectNodes().ToList();
        if (allprojectNodes.Count == 0) return;

        // In node view, show groups + top-level nodes + children of expanded groups
        var visibleNodes = allprojectNodes
            .Where(n =>
            {
                // Groups are always visible
                if (n.IsSuperNode) return true;

                // Top-level nodes (not in any group) are always visible
                if (!n.ParentSuperNodeId.HasValue) return true;

                // Children of groups: visible only if group is expanded
                var parent = nodes.FirstOrDefault(p => p.Id == n.ParentSuperNodeId.Value);
                var isVisible = parent == null || !parent.IsCollapsed;
                Console.WriteLine($"LayoutprojectNodesForNodeView: Node {n.Id} '{n.Text}' ParentId={n.ParentSuperNodeId} ParentCollapsed={parent?.IsCollapsed} => visible={isVisible}");
                return isVisible;
            })
            .ToList();

        Console.WriteLine($"LayoutprojectNodesForNodeView: {visibleNodes.Count} visible nodes out of {allprojectNodes.Count} total");

        // Compact node size
        const double nodeWidth = 80;
        const double nodeHeight = 40;
        const double groupNodeWidth = 100;
        const double groupNodeHeight = 50;
        const double verticalSpacing = 60;
        const double startY = 50;
        const double startX = 50;

        // Find date range to calculate horizontal scaling (use all nodes for date range)
        var nodesWithDates = allprojectNodes.Where(n => n.ProjectStartDate.HasValue).ToList();
        if (nodesWithDates.Count == 0)
        {
            // No dates, use simple grid layout
            int col = 0, row = 0;
            foreach (var node in visibleNodes)
            {
                bool isGroup = node.IsSuperNode;
                node.X = startX + col * (nodeWidth + 40);
                node.Y = startY + row * (nodeHeight + 20);
                node.Width = isGroup ? groupNodeWidth : nodeWidth;
                node.Height = isGroup ? groupNodeHeight : nodeHeight;
                col++;
                if (col > 6) { col = 0; row++; }
            }
            return;
        }

        var minDate = nodesWithDates.Min(n => n.ProjectStartDate!.Value);
        var maxDate = nodesWithDates.Max(n => n.ProjectEndDate ?? n.ProjectStartDate!.Value);
        var dateRange = (maxDate - minDate).TotalDays;
        if (dateRange < 1) dateRange = 1;

        // Calculate horizontal scale: fit date range into reasonable width
        // Use ~800px for the date range, plus margins
        const double availableWidth = 800;
        double dayWidth = availableWidth / dateRange;
        // Ensure minimum spacing between nodes
        if (dayWidth < 15) dayWidth = 15;

        // Position visible nodes, reassigning row indices for compact layout
        int currentRow = 0;
        foreach (var node in visibleNodes.OrderBy(n => n.ProjectRowIndex))
        {
            bool isGroup = node.IsSuperNode;

            if (node.ProjectStartDate.HasValue)
            {
                // X position based on start date relative to min date
                double daysFromStart = (node.ProjectStartDate.Value - minDate).TotalDays;
                node.X = startX + daysFromStart * dayWidth;
            }
            else
            {
                // No date, position at start
                node.X = startX;
            }

            // Y position based on current row (compact layout without gaps from hidden children)
            node.Y = startY + currentRow * verticalSpacing;

            // Set size (groups are slightly larger)
            node.Width = isGroup ? groupNodeWidth : nodeWidth;
            node.Height = isGroup ? groupNodeHeight : nodeHeight;

            currentRow++;
        }

        // Note: Don't call StateHasChanged here - let the caller handle it
    }

    /// <summary>
    /// Create a Project node at the clicked position.
    /// Called when user clicks on the canvas in AddNode mode with a Project shape selected.
    /// Works in both timeline and node view modes.
    /// </summary>
    private void CreateProjectNodeAtPosition(double clickX, double clickY, string shapeId)
    {
        if (projectTimeline == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        var existingNodes = GetprojectNodes().ToList();
        int maxRowIndex = existingNodes.Count > 0
            ? existingNodes.Max(n => n.ProjectRowIndex >= 0 ? n.ProjectRowIndex : GetProjectTaskRowIndex(n.Id))
            : -1;

        int rowIndex;
        DateTime startDateTime;

        if (projectViewMode == "node")
        {
            // In node view mode, calculate row from Y position using node view spacing
            const double verticalSpacing = 60;
            const double startY = 50;
            rowIndex = Math.Max(0, (int)((clickY - startY + verticalSpacing / 2) / verticalSpacing));

            // For date, use today or relative to existing nodes
            if (existingNodes.Count > 0 && existingNodes.Any(n => n.ProjectStartDate.HasValue))
            {
                var minDate = existingNodes.Where(n => n.ProjectStartDate.HasValue).Min(n => n.ProjectStartDate!.Value);
                var maxDate = existingNodes.Where(n => n.ProjectEndDate.HasValue || n.ProjectStartDate.HasValue)
                    .Max(n => n.ProjectEndDate ?? n.ProjectStartDate!.Value);

                // Calculate date based on X position relative to date range
                var dateRange = (maxDate - minDate).TotalDays;
                if (dateRange < 1) dateRange = 30; // Default 30 day range

                const double availableWidth = 800;
                const double startX = 50;
                double dayWidth = availableWidth / dateRange;
                if (dayWidth < 15) dayWidth = 15;

                double daysFromStart = (clickX - startX) / dayWidth;
                startDateTime = minDate.AddDays(Math.Max(0, daysFromStart));
            }
            else
            {
                // No existing nodes, use today
                startDateTime = DateTime.Today;
            }
        }
        else
        {
            // Timeline view - calculate from timeline position
            var startDate = projectTimeline.XToDate(clickX);
            startDateTime = startDate.ToDateTimeUnspecified();

            // New tasks always go at the next available row (no gaps)
            // This ensures consecutive row indices
            rowIndex = maxRowIndex + 1;
        }

        Node newNode;
        var nodeDefaults = GetCurrentNodeDefaults();

        switch (shapeId)
        {
            case "milestone":
                newNode = ProjectTimelineService.CreateProjectMilestoneNode(
                    nextId++,
                    $"Milestone {existingNodes.Count(n => n.ProjectIsMilestone) + 1}",
                    startDateTime
                );
                newNode.FillColor = nodeDefaults.FillColor ?? "#8b5cf6";
                newNode.StrokeColor = nodeDefaults.StrokeColor ?? "#6d28d9";
                break;

            case "summary":
                newNode = ProjectTimelineService.CreateProjectSummaryNode(
                    nextId++,
                    $"Summary {existingNodes.Count(n => n.IsSuperNode) + 1}",
                    startDateTime,
                    startDateTime.AddDays(6)  // Default 1 week
                );
                newNode.FillColor = nodeDefaults.FillColor ?? "#1f2937";
                break;

            // Resource types - handle all variations
            case "resource":
            case "resource-person":
            case "resource-team":
            case "resource-equipment":
            case "resource-vehicle":
            case "resource-machine":
            case "resource-tool":
            case "resource-material":
            case "resource-room":
            case "resource-computer":
            case "resource-custom":
                var resourceType = shapeId switch
                {
                    "resource-person" => ProjectResourceType.Person,
                    "resource-team" => ProjectResourceType.Team,
                    "resource-equipment" => ProjectResourceType.Equipment,
                    "resource-vehicle" => ProjectResourceType.Vehicle,
                    "resource-machine" => ProjectResourceType.Machine,
                    "resource-tool" => ProjectResourceType.Tool,
                    "resource-material" => ProjectResourceType.Material,
                    "resource-room" => ProjectResourceType.Room,
                    "resource-computer" => ProjectResourceType.Computer,
                    "resource-custom" => ProjectResourceType.Custom,
                    _ => ProjectResourceType.Person
                };
                var resourceTypeName = ProjectTimelineService.GetResourceTypeName(resourceType);
                var resourceCount = existingNodes.Count(n => n.IsProjectResource && n.ProjectResourceType == resourceType) + 1;
                newNode = ProjectTimelineService.CreateProjectResourceNode(
                    nextId++,
                    $"{resourceTypeName} {resourceCount}",
                    resourceType
                );
                // Override colors if user has set defaults
                if (nodeDefaults.FillColor != null) newNode.FillColor = nodeDefaults.FillColor;
                if (nodeDefaults.StrokeColor != null) newNode.StrokeColor = nodeDefaults.StrokeColor;
                break;

            case "task":
            default:
                // Find the highest task number to ensure unique naming
                var taskNodes = existingNodes.Where(n => !n.ProjectIsMilestone && !n.IsSuperNode && !n.IsProjectResource).ToList();
                int nextTaskNum = 1;
                foreach (var t in taskNodes)
                {
                    if (t.Text.StartsWith("Task ") && int.TryParse(t.Text.Substring(5), out int num))
                    {
                        nextTaskNum = Math.Max(nextTaskNum, num + 1);
                    }
                }
                newNode = ProjectTimelineService.CreateProjectTaskNode(
                    nextId++,
                    $"Task {nextTaskNum}",
                    startDateTime,
                    5  // Default 5 days duration
                );
                newNode.FillColor = nodeDefaults.FillColor ?? "#3b82f6";
                newNode.StrokeColor = nodeDefaults.StrokeColor ?? "#1d4ed8";
                break;
        }

        // Set the row index
        newNode.ProjectRowIndex = rowIndex;

        // In node view mode, position at click location; otherwise use timeline positioning
        if (projectViewMode == "node")
        {
            newNode.X = clickX - 40; // Center on click
            newNode.Y = clickY - 20;
            newNode.Width = 80;
            newNode.Height = 40;
        }
        else
        {
            projectTimeline.PositionNodeForTimeline(newNode);
        }

        // Add to nodes list
        nodes.Add(newNode);

        // Select the new node
        selectedProjectTaskId = newNode.Id;

        Console.WriteLine($"Created Project {shapeId} node ID={newNode.Id} at row {rowIndex}, date {startDateTime:yyyy-MM-dd}, viewMode={projectViewMode}");

        UpdateProjectView();
    }

    // ============================================
    // Project group TASK / MINI-PROJECT OPERATIONS
    // ============================================

    /// <summary>
    /// Creates a Project group Task (Summary/Mini-Project) from selected Project tasks.
    /// The group task acts as a SuperNode that can be collapsed/expanded.
    /// Its dates are automatically calculated from child tasks.
    /// </summary>
    private void CreateProjectGroupFromSelection()
    {
        // Get selected Project tasks (excluding resources and existing groups)
        var selectedProjectTasks = nodes
            .Where(n => selectedNodes.Contains(n.Id) &&
                       n.TemplateId == "project" &&
                       !n.IsProjectResource &&
                       !n.IsSuperNode)
            .ToList();

        if (selectedProjectTasks.Count < 2)
        {
            Console.WriteLine("Need at least 2 Project tasks to create a group");
            return;
        }

        // Check for nested groups (don't allow grouping tasks that are already in a group)
        if (selectedProjectTasks.Any(n => n.ParentSuperNodeId.HasValue))
        {
            Console.WriteLine("Cannot group tasks that are already in a group");
            return;
        }

        UndoService.SaveState(nodes, edges, edgeLabels);

        var containedIds = selectedProjectTasks.Select(n => n.Id).ToList();

        // Calculate date range from children
        var (minDate, maxDate) = CalculateProjectGroupDateRange(selectedProjectTasks);

        // Calculate row position (use minimum row of children)
        var minRowIndex = selectedProjectTasks.Min(n =>
            n.ProjectRowIndex >= 0 ? n.ProjectRowIndex : GetProjectTaskRowIndex(n.Id));

        // Create the Group Task (Summary node with SuperNode behavior)
        var groupNode = ProjectTimelineService.CreateProjectSummaryNode(
            nextId++,
            $"Group ({containedIds.Count} tasks)",
            minDate,
            maxDate
        );

        // Set SuperNode properties
        groupNode.IsSuperNode = true;
        groupNode.IsCollapsed = true; // Start collapsed - consistent philosophy with regular groups
        groupNode.ContainedNodeIds = containedIds;
        groupNode.ProjectRowIndex = minRowIndex;

        // Set visual styling
        groupNode.FillColor = "#1f2937";
        groupNode.StrokeColor = "#f59e0b"; // Amber border for group tasks

        // Calculate X/Y position for node view (based on start date and row index)
        // Use layout similar to LayoutprojectNodesForNodeView
        const double nodeViewStartX = 50;
        const double nodeViewStartY = 50;
        const double nodeViewSpacing = 60;
        const double dayWidth = 15; // pixels per day

        // Calculate X based on start date relative to earliest task
        var allProjectTasks = nodes.Where(n => n.TemplateId == "project" && n.ProjectStartDate.HasValue && !n.IsProjectResource).ToList();
        var earliestDate = allProjectTasks.Count > 0
            ? allProjectTasks.Min(n => n.ProjectStartDate!.Value)
            : (groupNode.ProjectStartDate ?? DateTime.Today);

        if (groupNode.ProjectStartDate.HasValue)
        {
            var dayOffset = (groupNode.ProjectStartDate.Value - earliestDate).TotalDays;
            groupNode.X = nodeViewStartX + dayOffset * dayWidth;
        }
        else
        {
            groupNode.X = nodeViewStartX;
        }

        // Calculate Y based on row index
        groupNode.Y = nodeViewStartY + minRowIndex * nodeViewSpacing;

        // Set proper dimensions for node view rendering
        groupNode.Width = 100;
        groupNode.Height = 50;

        nodes.Add(groupNode);

        // Mark contained tasks with parent reference
        foreach (var task in selectedProjectTasks)
        {
            task.ParentSuperNodeId = groupNode.Id;
        }

        // Recalculate all row indices to properly position group and children
        ReassignProjectRowIndices();

        // Remap boundary edges (dependencies crossing the group boundary)
        RemapProjectDependenciesToGroup(groupNode, containedIds);

        // Select the new group
        selectedNodes.Clear();
        selectedNodes.Add(groupNode.Id);
        selectedProjectTaskId = groupNode.Id;

        UpdateProjectView();
        StateHasChanged();

        Console.WriteLine($"Created Project group {groupNode.Id} containing {containedIds.Count} tasks, dates: {minDate:yyyy-MM-dd} to {maxDate:yyyy-MM-dd}");
    }

    /// <summary>
    /// Collapses a Project group to show only the summary bar
    /// </summary>
    private void CollapseProjectGroup(int groupNodeId)
    {
        var groupNode = nodes.FirstOrDefault(n => n.Id == groupNodeId && n.IsSuperNode && n.TemplateId == "project");
        if (groupNode == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        groupNode.IsCollapsed = true;

        // Recalculate group dates from children before collapsing
        UpdateProjectGroupDates(groupNode);

        // Hide internal dependencies
        foreach (var edge in edges.Where(e => e.IsProjectDependency))
        {
            var fromInside = groupNode.ContainedNodeIds.Contains(edge.From);
            var toInside = groupNode.ContainedNodeIds.Contains(edge.To);

            if (fromInside && toInside)
            {
                edge.IsHiddenInternal = true;
            }
            else if (fromInside && !toInside)
            {
                // Remap outgoing dependency to group
                if (edge.OriginalFrom == null)
                    edge.OriginalFrom = edge.From;
                edge.From = groupNode.Id;
            }
            else if (!fromInside && toInside)
            {
                // Remap incoming dependency to group
                if (edge.OriginalTo == null)
                    edge.OriginalTo = edge.To;
                edge.To = groupNode.Id;
            }
        }

        // Compact row indices (remove gaps left by hidden children)
        CompactProjectRowIndices();

        selectedProjectTaskId = groupNodeId;
        UpdateProjectView();
        StateHasChanged();

        Console.WriteLine($"Collapsed Project group {groupNodeId}");
    }

    /// <summary>
    /// Expands a Project group to show child tasks
    /// </summary>
    private void ExpandProjectGroup(int groupNodeId)
    {
        var groupNode = nodes.FirstOrDefault(n => n.Id == groupNodeId && n.IsSuperNode && n.TemplateId == "project");
        if (groupNode == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        groupNode.IsCollapsed = false;

        // Restore edge mappings
        foreach (var edge in edges.Where(e => e.IsProjectDependency))
        {
            if (edge.IsHiddenInternal)
            {
                var fromInside = groupNode.ContainedNodeIds.Contains(edge.From);
                var toInside = groupNode.ContainedNodeIds.Contains(edge.To);
                if (fromInside && toInside)
                {
                    edge.IsHiddenInternal = false;
                }
            }

            if (edge.OriginalFrom.HasValue && edge.From == groupNode.Id)
            {
                edge.From = edge.OriginalFrom.Value;
                edge.OriginalFrom = null;
            }

            if (edge.OriginalTo.HasValue && edge.To == groupNode.Id)
            {
                edge.To = edge.OriginalTo.Value;
                edge.OriginalTo = null;
            }
        }

        // Recalculate row indices to show children
        ReassignProjectRowIndices();

        selectedProjectTaskId = groupNodeId;
        UpdateProjectView();
        StateHasChanged();

        Console.WriteLine($"Expanded Project group {groupNodeId}");
    }

    /// <summary>
    /// Ungroups a Project group, converting it back to individual tasks
    /// </summary>
    private void UngroupProjectGroup(int groupNodeId)
    {
        var groupNode = nodes.FirstOrDefault(n => n.Id == groupNodeId && n.IsSuperNode && n.TemplateId == "project");
        if (groupNode == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        // If collapsed, expand first
        if (groupNode.IsCollapsed)
        {
            ExpandProjectGroup(groupNodeId);
        }

        // Clear parent references from contained tasks
        foreach (var nodeId in groupNode.ContainedNodeIds)
        {
            var task = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (task != null)
            {
                task.ParentSuperNodeId = null;
            }
        }

        // Remove dependencies that were connected to the group
        edges.RemoveAll(e => e.IsProjectDependency &&
            (e.From == groupNode.Id || e.To == groupNode.Id));

        // Select the formerly contained tasks
        selectedNodes.Clear();
        selectedNodes.AddRange(groupNode.ContainedNodeIds.Where(id => nodes.Any(n => n.Id == id)));

        // Remove the group node
        nodes.Remove(groupNode);
        selectedProjectTaskId = selectedNodes.FirstOrDefault();

        // Compact row indices
        CompactProjectRowIndices();

        UpdateProjectView();
        StateHasChanged();

        Console.WriteLine($"Ungrouped Project group {groupNodeId}, {groupNode.ContainedNodeIds.Count} tasks released");
    }

    /// <summary>
    /// Calculates the date range for a group from its child tasks
    /// </summary>
    private (DateTime minDate, DateTime maxDate) CalculateProjectGroupDateRange(IEnumerable<Node> childTasks)
    {
        var tasksWithDates = childTasks.Where(t => t.ProjectStartDate.HasValue).ToList();

        if (tasksWithDates.Count == 0)
        {
            return (DateTime.Today, DateTime.Today.AddDays(7));
        }

        var minDate = tasksWithDates.Min(t => t.ProjectStartDate!.Value);
        var maxDate = tasksWithDates.Max(t => t.ProjectEndDate ?? t.ProjectStartDate!.Value);

        return (minDate, maxDate);
    }

    /// <summary>
    /// Updates a group's dates based on its child tasks
    /// </summary>
    private void UpdateProjectGroupDates(Node groupNode)
    {
        if (!groupNode.IsSuperNode || groupNode.TemplateId != "project") return;

        var childTasks = nodes.Where(n => groupNode.ContainedNodeIds.Contains(n.Id)).ToList();
        var (minDate, maxDate) = CalculateProjectGroupDateRange(childTasks);

        groupNode.ProjectStartDate = minDate;
        groupNode.ProjectEndDate = maxDate;
        groupNode.ProjectDurationDays = (int)(maxDate - minDate).TotalDays + 1;

        // Update percent complete based on children
        if (childTasks.Count > 0)
        {
            groupNode.ProjectPercentComplete = (int)childTasks.Average(t => t.ProjectPercentComplete);
        }
    }

    /// <summary>
    /// Updates all Project group dates from their children
    /// </summary>
    private void UpdateAllProjectGroupDates()
    {
        var groups = nodes.Where(n => n.IsSuperNode && n.TemplateId == "project").ToList();
        foreach (var group in groups)
        {
            UpdateProjectGroupDates(group);
        }
    }

    /// <summary>
    /// Remaps Project dependencies that cross the group boundary
    /// </summary>
    private void RemapProjectDependenciesToGroup(Node groupNode, List<int> containedIds)
    {
        var containedSet = containedIds.ToHashSet();

        foreach (var edge in edges.Where(e => e.IsProjectDependency))
        {
            var fromInside = containedSet.Contains(edge.From);
            var toInside = containedSet.Contains(edge.To);

            if (fromInside && toInside)
            {
                // Internal dependency - keep visible when expanded
                // Will be hidden when collapsed
            }
            // Don't remap on creation - only when collapsing
        }
    }

    /// <summary>
    /// Compacts Project row indices to remove gaps
    /// </summary>
    private void CompactProjectRowIndices()
    {
        var visibleTasks = nodes
            .Where(n => n.TemplateId == "project" && !n.IsProjectResource && ShouldRenderNode(n))
            .OrderBy(n => n.ProjectRowIndex)
            .ToList();

        for (int i = 0; i < visibleTasks.Count; i++)
        {
            visibleTasks[i].ProjectRowIndex = i;
        }
    }

    /// <summary>
    /// Reassigns row indices to show groups and their children properly.
    /// Simple approach: collect all visible items in order, then assign consecutive row indices.
    /// </summary>
    private void ReassignProjectRowIndices()
    {
        var allTasks = nodes
            .Where(n => n.TemplateId == "project" && !n.IsProjectResource)
            .ToList();

        // Get all groups
        var groups = allTasks.Where(n => n.IsSuperNode).ToList();

        // Build set of all child IDs (tasks that belong to a group)
        var childIds = new HashSet<int>();
        foreach (var group in groups)
        {
            foreach (var childId in group.ContainedNodeIds)
            {
                childIds.Add(childId);
            }
        }

        // Get standalone tasks (not in any group, not a group itself), sorted by current row index
        var standaloneTasks = allTasks
            .Where(n => !n.IsSuperNode && !childIds.Contains(n.Id))
            .OrderBy(n => n.ProjectRowIndex >= 0 ? n.ProjectRowIndex : int.MaxValue)
            .ThenBy(n => n.ProjectStartDate ?? DateTime.MaxValue)
            .ToList();

        // For each group, calculate its insert position based on its children's original positions
        var groupPositions = new Dictionary<int, int>();
        foreach (var group in groups)
        {
            var children = allTasks.Where(n => group.ContainedNodeIds.Contains(n.Id)).ToList();
            if (children.Count > 0)
            {
                var minChildRow = children.Min(n => n.ProjectRowIndex >= 0 ? n.ProjectRowIndex : int.MaxValue);
                groupPositions[group.Id] = minChildRow;
            }
            else
            {
                groupPositions[group.Id] = int.MaxValue;
            }
        }

        // Sort groups by their position
        var sortedGroups = groups.OrderBy(g => groupPositions[g.Id]).ToList();

        // Now build the final order: interleave standalone tasks and groups
        var finalOrder = new List<Node>();
        int standaloneIdx = 0;
        int groupIdx = 0;

        while (standaloneIdx < standaloneTasks.Count || groupIdx < sortedGroups.Count)
        {
            // Determine which comes next: standalone task or group
            int nextStandaloneRow = standaloneIdx < standaloneTasks.Count
                ? (standaloneTasks[standaloneIdx].ProjectRowIndex >= 0 ? standaloneTasks[standaloneIdx].ProjectRowIndex : int.MaxValue)
                : int.MaxValue;
            int nextGroupRow = groupIdx < sortedGroups.Count
                ? groupPositions[sortedGroups[groupIdx].Id]
                : int.MaxValue;

            if (nextGroupRow <= nextStandaloneRow && groupIdx < sortedGroups.Count)
            {
                // Insert the group
                var group = sortedGroups[groupIdx++];
                finalOrder.Add(group);

                // Insert children immediately after (if expanded)
                if (!group.IsCollapsed)
                {
                    var children = allTasks
                        .Where(n => group.ContainedNodeIds.Contains(n.Id))
                        .OrderBy(n => n.ProjectRowIndex >= 0 ? n.ProjectRowIndex : int.MaxValue)
                        .ThenBy(n => n.ProjectStartDate ?? DateTime.MaxValue)
                        .ToList();
                    finalOrder.AddRange(children);
                }
            }
            else if (standaloneIdx < standaloneTasks.Count)
            {
                // Insert standalone task
                finalOrder.Add(standaloneTasks[standaloneIdx++]);
            }
            else
            {
                break; // Safety exit
            }
        }

        // Assign consecutive row indices
        for (int i = 0; i < finalOrder.Count; i++)
        {
            finalOrder[i].ProjectRowIndex = i;
        }

        Console.WriteLine($"ReassignProjectRowIndices: Assigned {finalOrder.Count} tasks/groups. Groups={groups.Count}, Standalone={standaloneTasks.Count}");
    }

    /// <summary>
    /// Toggle collapse/expand for a Project group
    /// </summary>
    private void ToggleProjectGroup(int groupNodeId)
    {
        var groupNode = nodes.FirstOrDefault(n => n.Id == groupNodeId && n.IsSuperNode && n.TemplateId == "project");
        if (groupNode == null) return;

        if (groupNode.IsCollapsed)
        {
            ExpandProjectGroup(groupNodeId);
        }
        else
        {
            CollapseProjectGroup(groupNodeId);
        }
    }

    /// <summary>
    /// Checks if we can create a group from current selection
    /// </summary>
    private bool CanCreateProjectGroup()
    {
        var selectedProjectTasks = nodes
            .Where(n => selectedNodes.Contains(n.Id) &&
                       n.TemplateId == "project" &&
                       !n.IsProjectResource &&
                       !n.IsSuperNode &&
                       !n.ParentSuperNodeId.HasValue)
            .ToList();

        return selectedProjectTasks.Count >= 2;
    }

    /// <summary>
    /// Gets the selected Project group node, if any
    /// </summary>
    private Node? GetSelectedProjectGroup()
    {
        if (!selectedProjectTaskId.HasValue) return null;

        return nodes.FirstOrDefault(n =>
            n.Id == selectedProjectTaskId.Value &&
            n.IsSuperNode &&
            n.TemplateId == "project");
    }

    // ============================================
    // Project import/EXPORT
    // ============================================

    private ProjectImportExportService projectImportExport = new();

    /// <summary>
    /// Import Project data from the import dialog text
    /// </summary>
    private void ImportProjectData()
    {
        projectImportError = "";

        if (string.IsNullOrWhiteSpace(projectImportText))
        {
            projectImportError = "Please paste data or load a file first.";
            return;
        }

        UndoService.SaveState(nodes, edges, edgeLabels);

        // Get starting IDs
        int startNodeId = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1;
        int startEdgeId = edges.Count > 0 ? edges.Max(e => e.Id) + 1 : 1;

        // Auto-detect format and import
        var result = projectImportExport.Import(projectImportText, startNodeId, startEdgeId);

        if (result.Success)
        {
            // Add imported nodes and edges
            nodes.AddRange(result.Nodes);
            edges.AddRange(result.Edges);

            // Update nextId
            if (result.Nodes.Count > 0)
            {
                nextId = result.Nodes.Max(n => n.Id) + 1;
            }

            // Refresh the view
            projectTimeline?.SetViewRangeFromNodes(result.Nodes, 7);
            UpdateProjectView();

            Console.WriteLine($"Imported {result.Nodes.Count} tasks, {result.Edges.Count} dependencies");

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"Import warning: {warning}");
            }

            // Close dialog on success
            showProjectImportDialog = false;
            projectImportText = "";
            projectImportError = "";
        }
        else
        {
            // Show errors in dialog, don't close
            projectImportError = string.Join("\n", result.Errors);
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"Import error: {error}");
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// Handle file selection for Project import
    /// </summary>
    private async Task OnProjectFileSelected(InputFileChangeEventArgs e)
    {
        projectImportError = "";

        try
        {
            var file = e.File;
            if (file == null) return;

            // Limit file size to 5MB
            const long maxSize = 5 * 1024 * 1024;
            if (file.Size > maxSize)
            {
                projectImportError = $"File too large. Maximum size is 5MB, but file is {file.Size / 1024 / 1024}MB.";
                return;
            }

            using var stream = file.OpenReadStream(maxSize);
            using var reader = new StreamReader(stream);
            projectImportText = await reader.ReadToEndAsync();

            Console.WriteLine($"Loaded file: {file.Name} ({file.Size} bytes)");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            projectImportError = $"Error reading file: {ex.Message}";
            Console.WriteLine($"File load error: {ex.Message}");
        }
    }

    /// <summary>
    /// Export Project data to CSV and copy to clipboard
    /// </summary>
    private async Task ExportProjectToCsv()
    {
        var csv = projectImportExport.ExportToCsv(nodes, edges);
        await CopyToClipboard(csv);
        Console.WriteLine("Project CSV copied to clipboard");
    }

    /// <summary>
    /// Export Project data to JSON and copy to clipboard
    /// </summary>
    private async Task ExportProjectToJson()
    {
        var json = projectImportExport.ExportToJson(nodes, edges, "Project Chart");
        await CopyToClipboard(json);
        Console.WriteLine("Project JSON copied to clipboard");
    }

    /// <summary>
    /// Copy text to clipboard using JS interop
    /// </summary>
    private async Task CopyToClipboard(string text)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to copy to clipboard: {ex.Message}");
        }
    }

    // ============================================
    // SOLVER METHODS
    // ============================================

    /// <summary>
    /// Run the solver with current configuration
    /// </summary>
    private async Task RunSolver()
    {
        if (solverRunning) return;

        solverRunning = true;
        solverResult = null;
        StateHasChanged();

        try
        {
            // Run solver on a background thread to avoid blocking UI
            await Task.Run(() =>
            {
                var solver = new ProjectSolverService();
                solverResult = solver.Solve(nodes, edges, solverConfig);
            });
        }
        catch (Exception ex)
        {
            solverResult = new SolverResult
            {
                Status = SolverStatus.Error,
                Message = $"Solver failed: {ex.Message}"
            };
        }
        finally
        {
            solverRunning = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Apply the solver result to update task schedules
    /// </summary>
    private void ApplySolverResult()
    {
        if (solverResult?.OptimizedSchedule == null || !solverResult.OptimizedSchedule.Any())
            return;

        foreach (var (taskId, newStart) in solverResult.OptimizedSchedule)
        {
            var task = nodes.FirstOrDefault(n => n.Id == taskId);
            if (task != null && task.ProjectStartDate.HasValue)
            {
                task.ProjectStartDate = newStart;

                // Apply optimized duration if task was compressed
                if (solverResult.OptimizedDurations.TryGetValue(taskId, out var newDuration))
                {
                    task.ProjectDurationDays = newDuration;
                }

                // Use ProjectTimelineService to properly calculate end date from duration
                ProjectTimelineService.SetNodeEndDateFromDuration(task);
            }
        }

        // Recalculate timeline bounds using existing method
        if (projectTimeline != null)
        {
            var projectNodes = GetprojectNodes().ToList();
            if (projectNodes.Any())
            {
                projectTimeline.SetViewRangeFromNodes(projectNodes, paddingDays: 14);
            }
        }

        // Close dialog after applying
        showProjectSolverDialog = false;
        solverResult = null;
        StateHasChanged();
    }

    /// <summary>
    /// Open the solver dialog
    /// </summary>
    private void OpenSolverDialog()
    {
        solverResult = null;
        showProjectSolverDialog = true;
    }

    // ============================================
    // RESOURCE DRAG-DROP ASSIGNMENT
    // ============================================

    /// <summary>
    /// Start dragging a resource node for assignment to a task.
    /// Called when mousedown on a resource in the resource panel.
    /// </summary>
    private void StartResourceDrag(int resourceNodeId, MouseEventArgs e)
    {
        var resourceNode = nodes.FirstOrDefault(n => n.Id == resourceNodeId && n.IsProjectResource);
        if (resourceNode == null) return;

        draggingResourceId = resourceNodeId;
        resourceDragTargetTaskId = null;

        Console.WriteLine($"Started resource drag: {resourceNode.Text} (ID={resourceNodeId})");
        StateHasChanged();
    }

    /// <summary>
    /// Handle mouse move during resource drag.
    /// Updates the target task based on mouse position.
    /// </summary>
    private void HandleResourceDragMove(MouseEventArgs e)
    {
        if (!draggingResourceId.HasValue || projectTimeline == null) return;

        // Find task under the cursor
        var targetTask = GetProjectTaskNodeAtPosition(e.OffsetX, e.OffsetY);

        // Update target (only non-resource, non-group tasks)
        if (targetTask != null && !targetTask.IsProjectResource && !targetTask.IsSuperNode)
        {
            resourceDragTargetTaskId = targetTask.Id;
        }
        else
        {
            resourceDragTargetTaskId = null;
        }

        StateHasChanged();
    }

    /// <summary>
    /// Complete resource drag - assign to task if dropped on valid target.
    /// </summary>
    private void HandleResourceDragEnd(MouseEventArgs e)
    {
        if (!draggingResourceId.HasValue) return;

        var resourceId = draggingResourceId.Value;
        var targetTaskId = resourceDragTargetTaskId;

        // Reset drag state
        draggingResourceId = null;
        resourceDragTargetTaskId = null;

        if (targetTaskId.HasValue)
        {
            // Show assignment dialog to configure quantity/percentage
            var resource = nodes.FirstOrDefault(n => n.Id == resourceId);
            var task = nodes.FirstOrDefault(n => n.Id == targetTaskId.Value);

            if (resource != null && task != null)
            {
                assigningResource = resource;
                assigningToTask = task;
                resourceAssignmentQuantity = 1;
                resourceAssignmentPercentage = 100;
                showResourceAssignmentDialog = true;

                Console.WriteLine($"Resource drag completed: {resource.Text} -> {task.Text}");
            }
        }
        else
        {
            Console.WriteLine("Resource drag cancelled - no valid target");
        }

        StateHasChanged();
    }

    /// <summary>
    /// Get a Project task node at the given position (excludes resources).
    /// </summary>
    private Node? GetProjectTaskNodeAtPosition(double x, double y)
    {
        if (projectTimeline == null) return null;

        var orderedNodes = GetOrderedprojectNodes();
        foreach (var node in orderedNodes)
        {
            // Skip resources - we're looking for tasks
            if (node.IsProjectResource) continue;

            var rowIndex = node.ProjectRowIndex >= 0 ? node.ProjectRowIndex : GetProjectTaskRowIndex(node.Id);
            var rowY = projectTimeline.RowToY(rowIndex);
            var barY = rowY + 6;
            var barHeight = projectTimeline.RowHeight - 12;

            // Check if Y is in this row
            if (y < barY || y > barY + barHeight) continue;

            if (node.ProjectIsMilestone)
            {
                // Milestone diamond hit test
                if (node.ProjectStartDate == null) continue;
                var mx = projectTimeline.DateTimeToX(node.ProjectStartDate.Value) + projectTimeline.DayWidth / 2;
                var my = rowY + projectTimeline.RowHeight / 2;
                var dist = Math.Sqrt(Math.Pow(x - mx, 2) + Math.Pow(y - my, 2));
                if (dist <= 15) return node;
            }
            else
            {
                // Task bar hit test
                var bounds = projectTimeline.GetNodeBarBounds(node);
                if (x >= bounds.x && x <= bounds.x + bounds.width)
                {
                    return node;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Confirm resource assignment from the dialog.
    /// </summary>
    private void ConfirmResourceAssignment()
    {
        if (assigningToTask == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        Node resourceToAssign;

        if (assigningResource == null)
        {
            // Create a new resource based on the selected shape
            var resourceType = GetResourceTypeFromShapeId(selectedTemplateShapeId);
            string resourceName;
            if (!string.IsNullOrWhiteSpace(newResourceName))
            {
                resourceName = newResourceName.Trim();
            }
            else
            {
                resourceName = ProjectTimelineService.GetResourceTypeName(resourceType);
                var existingCount = nodes.Count(n => n.IsProjectResource && n.ProjectResourceType == resourceType);
                if (existingCount > 0)
                {
                    resourceName = $"{resourceName} {existingCount + 1}";
                }
            }

            resourceToAssign = new Node
            {
                Id = nodes.Count > 0 ? nodes.Max(n => n.Id) + 1 : 1,
                Text = resourceName,
                TemplateId = "project",
                TemplateShapeId = selectedTemplateShapeId ?? "resource",
                IsProjectResource = true,
                ProjectResourceType = resourceType,
                ProjectAssignedTaskIds = new List<int>(),
                X = 0,
                Y = 0,
                Width = 120,
                Height = 40
            };
            nodes.Add(resourceToAssign);
            Console.WriteLine($"Created new resource: {resourceToAssign.Text} (type={resourceType})");
        }
        else
        {
            resourceToAssign = assigningResource;
        }

        // Use existing assignment method
        AssignResourceToTask(resourceToAssign.Id, assigningToTask.Id);

        // Store the quantity and percentage in the assignment
        // (For now we track this in the resource's assigned tasks list)
        // TODO: Create ResourceAssignment model for detailed tracking

        Console.WriteLine($"Assigned {resourceToAssign.Text} to {assigningToTask.Text} " +
                         $"(Qty: {resourceAssignmentQuantity}, {resourceAssignmentPercentage}%)");

        // Close dialog and reset
        showResourceAssignmentDialog = false;
        assigningResource = null;
        assigningToTask = null;
        newResourceName = "";

        UpdateProjectView();
        StateHasChanged();
    }

    /// <summary>
    /// Get ProjectResourceType from shape ID
    /// </summary>
    private ProjectResourceType GetResourceTypeFromShapeId(string? shapeId)
    {
        return shapeId switch
        {
            "resource-person" => ProjectResourceType.Person,
            "resource-team" => ProjectResourceType.Team,
            "resource-equipment" => ProjectResourceType.Equipment,
            "resource-vehicle" => ProjectResourceType.Vehicle,
            "resource-machine" => ProjectResourceType.Machine,
            "resource-tool" => ProjectResourceType.Tool,
            "resource-material" => ProjectResourceType.Material,
            "resource-room" => ProjectResourceType.Room,
            "resource-computer" => ProjectResourceType.Computer,
            "resource-custom" => ProjectResourceType.Custom,
            _ => ProjectResourceType.Person
        };
    }

    /// <summary>
    /// Cancel resource assignment dialog.
    /// </summary>
    private void CancelResourceAssignment()
    {
        showResourceAssignmentDialog = false;
        assigningResource = null;
        assigningToTask = null;
        newResourceName = "";
        StateHasChanged();
    }

    /// <summary>
    /// Handle "Add Shape" button when a resource is selected and a task is also selected.
    /// Assigns the selected resource to the selected task.
    /// </summary>
    private void AssignSelectedResourceToSelectedTask()
    {
        if (!selectedProjectTaskId.HasValue) return;

        // Find the selected resource from shape dropdown selection
        if (selectedTemplateId != "project" || string.IsNullOrEmpty(selectedTemplateShapeId)) return;
        if (!selectedTemplateShapeId.StartsWith("resource")) return;

        // Check if there's a resource node selected
        var selectedResource = nodes.FirstOrDefault(n =>
            selectedNodes.Contains(n.Id) && n.IsProjectResource);

        if (selectedResource == null)
        {
            // No resource selected - maybe we're in "add new resource to task" mode
            // Get the selected task
            var selectedTask = nodes.FirstOrDefault(n => n.Id == selectedProjectTaskId.Value);
            if (selectedTask == null || selectedTask.IsProjectResource) return;

            // Show dialog to create new resource and assign
            assigningToTask = selectedTask;
            assigningResource = null; // Will create new
            resourceAssignmentQuantity = 1;
            resourceAssignmentPercentage = 100;
            showResourceAssignmentDialog = true;

            StateHasChanged();
            return;
        }

        // Resource is selected - show assignment dialog
        var task = nodes.FirstOrDefault(n => n.Id == selectedProjectTaskId.Value && !n.IsProjectResource);
        if (task == null) return;

        assigningResource = selectedResource;
        assigningToTask = task;
        resourceAssignmentQuantity = 1;
        resourceAssignmentPercentage = 100;
        showResourceAssignmentDialog = true;

        StateHasChanged();
    }

    /// <summary>
    /// Check if the current selection allows resource assignment.
    /// </summary>
    private bool CanAssignResourceToTask()
    {
        // Need a task selected in Project mode
        if (!isProjectMode || !selectedProjectTaskId.HasValue) return false;

        var selectedTask = nodes.FirstOrDefault(n => n.Id == selectedProjectTaskId.Value);
        if (selectedTask == null) return false;

        // Can't assign to a resource node
        if (selectedTask.IsProjectResource) return false;

        // Check if we have a resource shape selected in the dropdown
        if (selectedTemplateId == "project" &&
            !string.IsNullOrEmpty(selectedTemplateShapeId) &&
            selectedTemplateShapeId.StartsWith("resource"))
        {
            return true;
        }

        // Or check if we have a resource node selected
        return nodes.Any(n => selectedNodes.Contains(n.Id) && n.IsProjectResource);
    }

    /// <summary>
    /// Check if the task bar should show as a drop target for resource assignment.
    /// </summary>
    private bool IsResourceDropTarget(int taskNodeId)
    {
        return draggingResourceId.HasValue && resourceDragTargetTaskId == taskNodeId;
    }

    /// <summary>
    /// Get CSS class for resource assignment drop target highlighting.
    /// </summary>
    private string GetResourceDropTargetClass(int taskNodeId)
    {
        if (IsResourceDropTarget(taskNodeId))
        {
            return "resource-drop-target";
        }
        return "";
    }


    /// <summary>Handle mouse wheel on Project canvas for horizontal scrolling</summary>
    private async Task HandleProjectWheel(WheelEventArgs e)
    {
        if (canvasRef.Id == null) return;

        // Ctrl+Wheel = zoom
        if (e.CtrlKey)
        {
            if (e.DeltaY < 0)
                ProjectZoomIn();
            else
                ProjectZoomOut();
            return;
        }

        // Regular wheel or Shift+Wheel = horizontal scroll
        // Use deltaX if available (horizontal scroll wheel), otherwise use deltaY
        var horizontalDelta = e.ShiftKey || Math.Abs(e.DeltaX) > Math.Abs(e.DeltaY)
            ? (e.DeltaX != 0 ? e.DeltaX : e.DeltaY)
            : e.DeltaY;

        await JSRuntime.InvokeVoidAsync("scrollProjectHorizontal", canvasRef, horizontalDelta);

        // Update minimap viewport position after scroll
        var scrollInfo = await JSRuntime.InvokeAsync<double[]>("getScrollInfo", canvasRef);
        if (scrollInfo != null && scrollInfo.Length >= 4)
        {
            scrollX = scrollInfo[0];
            scrollY = scrollInfo[1];
            viewportWidth = scrollInfo[2];
            viewportHeight = scrollInfo[3];
            StateHasChanged();
        }
    }
}
