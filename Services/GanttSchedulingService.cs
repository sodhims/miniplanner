using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for Gantt machine scheduling algorithms and precedence constraint validation.
/// Implements SPT (Shortest Processing Time) and other scheduling rules.
/// </summary>
public class GanttSchedulingService
{
    /// <summary>
    /// Result of a scheduling operation
    /// </summary>
    public class SchedulingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TasksScheduled { get; set; }
        public int ViolationsFound { get; set; }
        public TimeSpan Makespan { get; set; }
        public List<PrecedenceViolation> Violations { get; set; } = new();
    }

    /// <summary>
    /// Represents a precedence constraint violation
    /// </summary>
    public class PrecedenceViolation
    {
        public int PredecessorTaskId { get; set; }
        public int SuccessorTaskId { get; set; }
        public string PredecessorName { get; set; } = string.Empty;
        public string SuccessorName { get; set; } = string.Empty;
        public TimeSpan PredecessorEndTime { get; set; }
        public TimeSpan SuccessorStartTime { get; set; }
        public TimeSpan RequiredShift { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // ============================================
    // PRECEDENCE VALIDATION
    // ============================================

    /// <summary>
    /// Validates all precedence constraints in a schedule and marks violations.
    /// </summary>
    public SchedulingResult ValidatePrecedences(GanttSchedule schedule)
    {
        var result = new SchedulingResult { Success = true };

        // Clear all violation flags first
        foreach (var task in schedule.Tasks)
        {
            task.IsViolation = false;
        }

        foreach (var precedence in schedule.Precedences)
        {
            precedence.IsViolated = false;
            precedence.ViolationMessage = null;
        }

        // Check each precedence constraint
        foreach (var precedence in schedule.Precedences)
        {
            var predecessor = schedule.GetTask(precedence.PredecessorTaskId);
            var successor = schedule.GetTask(precedence.SuccessorTaskId);

            if (predecessor == null || successor == null)
                continue;

            var isViolated = !precedence.IsSatisfied(predecessor.EndTime, successor.StartTime);

            if (isViolated)
            {
                precedence.IsViolated = true;
                successor.IsViolation = true;

                var requiredShift = precedence.GetRequiredShift(predecessor.EndTime, successor.StartTime);
                var violation = new PrecedenceViolation
                {
                    PredecessorTaskId = predecessor.Id,
                    SuccessorTaskId = successor.Id,
                    PredecessorName = predecessor.Name,
                    SuccessorName = successor.Name,
                    PredecessorEndTime = predecessor.EndTime,
                    SuccessorStartTime = successor.StartTime,
                    RequiredShift = requiredShift,
                    Message = $"Task '{successor.Name}' starts at {FormatTime(successor.StartTime)} but predecessor '{predecessor.Name}' ends at {FormatTime(predecessor.EndTime)}"
                };

                precedence.ViolationMessage = violation.Message;
                result.Violations.Add(violation);
            }
        }

        result.ViolationsFound = result.Violations.Count;
        result.Success = result.Violations.Count == 0;
        result.Message = result.Success
            ? "All precedence constraints satisfied"
            : $"{result.Violations.Count} precedence violation(s) found";

        return result;
    }

    /// <summary>
    /// Validates precedences for Node-based Gantt tasks
    /// </summary>
    public SchedulingResult ValidateNodePrecedences(IEnumerable<Node> ganttNodes, IEnumerable<Edge> edges)
    {
        var result = new SchedulingResult { Success = true };
        var nodeList = ganttNodes.Where(n => n.IsGanttTask).ToList();

        // Clear all violation flags first
        foreach (var node in nodeList)
        {
            node.GanttIsViolation = false;
        }

        // Each edge represents a precedence: From -> To means From must finish before To starts
        foreach (var edge in edges)
        {
            var predecessor = nodeList.FirstOrDefault(n => n.Id == edge.From);
            var successor = nodeList.FirstOrDefault(n => n.Id == edge.To);

            if (predecessor == null || successor == null)
                continue;

            if (!predecessor.GanttEndTime.HasValue || !successor.GanttStartTime.HasValue)
                continue;

            // Check if successor starts before predecessor ends
            if (successor.GanttStartTime.Value < predecessor.GanttEndTime.Value)
            {
                successor.GanttIsViolation = true;

                var requiredShift = predecessor.GanttEndTime.Value - successor.GanttStartTime.Value;
                var violation = new PrecedenceViolation
                {
                    PredecessorTaskId = predecessor.Id,
                    SuccessorTaskId = successor.Id,
                    PredecessorName = predecessor.Text,
                    SuccessorName = successor.Text,
                    PredecessorEndTime = predecessor.GanttEndTime.Value,
                    SuccessorStartTime = successor.GanttStartTime.Value,
                    RequiredShift = requiredShift,
                    Message = $"Task '{successor.Text}' starts at {FormatTime(successor.GanttStartTime.Value)} but predecessor '{predecessor.Text}' ends at {FormatTime(predecessor.GanttEndTime.Value)}"
                };

                result.Violations.Add(violation);
            }
        }

        result.ViolationsFound = result.Violations.Count;
        result.Success = result.Violations.Count == 0;
        result.Message = result.Success
            ? "All precedence constraints satisfied"
            : $"{result.Violations.Count} precedence violation(s) found";

        return result;
    }

    // ============================================
    // SCHEDULING ALGORITHMS
    // ============================================

    /// <summary>
    /// Schedules tasks using SPT (Shortest Processing Time) rule.
    /// Tasks are sorted by duration and scheduled in order.
    /// </summary>
    public SchedulingResult ScheduleSPT(GanttSchedule schedule)
    {
        var result = new SchedulingResult();

        // Get unscheduled tasks (those at time 0 or need rescheduling)
        var tasksToSchedule = schedule.Tasks
            .OrderBy(t => t.Duration.TotalMinutes) // SPT: shortest first
            .ThenBy(t => t.Priority) // Tie-breaker: priority
            .ToList();

        // Track machine availability (when each machine becomes free)
        var machineAvailability = schedule.Machines.ToDictionary(m => m.Id, m => TimeSpan.Zero);

        foreach (var task in tasksToSchedule)
        {
            if (!machineAvailability.ContainsKey(task.MachineId))
                continue;

            // Get earliest start time based on:
            // 1. Machine availability
            // 2. Predecessor constraints
            var machineReady = machineAvailability[task.MachineId];
            var predecessorReady = GetEarliestStartAfterPredecessors(schedule, task.Id);
            var earliestStart = machineReady > predecessorReady ? machineReady : predecessorReady;

            // Schedule the task
            task.StartTime = earliestStart;
            task.RowIndex = schedule.GetMachine(task.MachineId)?.RowIndex ?? 0;

            // Update machine availability
            machineAvailability[task.MachineId] = task.EndTime;

            result.TasksScheduled++;
        }

        // Validate the result
        var validation = ValidatePrecedences(schedule);
        result.Violations = validation.Violations;
        result.ViolationsFound = validation.ViolationsFound;
        result.Makespan = schedule.GetMakespan();
        result.Success = validation.Success;
        result.Message = $"SPT scheduling complete. {result.TasksScheduled} tasks scheduled. Makespan: {FormatTime(result.Makespan)}";

        if (!result.Success)
        {
            result.Message += $" ({result.ViolationsFound} violations - may need manual adjustment)";
        }

        return result;
    }

    /// <summary>
    /// Schedules Node-based Gantt tasks using SPT rule.
    /// </summary>
    public SchedulingResult ScheduleNodesSPT(List<Node> ganttNodes, IEnumerable<Edge> edges)
    {
        var result = new SchedulingResult();

        // Get task nodes
        var taskNodes = ganttNodes.Where(n => n.IsGanttTask && n.GanttDuration.HasValue).ToList();

        // Get machine nodes
        var machineNodes = ganttNodes.Where(n => n.IsGanttMachine).ToList();

        // Sort by processing time (SPT)
        var sortedTasks = taskNodes
            .OrderBy(t => t.GanttDuration?.TotalMinutes ?? 0)
            .ThenBy(t => t.GanttPriority)
            .ToList();

        // Track machine availability
        var machineAvailability = machineNodes.ToDictionary(m => m.Id, m => TimeSpan.Zero);

        foreach (var task in sortedTasks)
        {
            var machineId = task.GanttMachineId ?? machineNodes.FirstOrDefault()?.Id ?? 0;

            if (!machineAvailability.ContainsKey(machineId))
            {
                // Assign to first available machine
                machineId = machineNodes.FirstOrDefault()?.Id ?? 0;
                task.GanttMachineId = machineId;
            }

            // Get earliest start based on predecessors
            var predecessorReady = GetEarliestNodeStartAfterPredecessors(task.Id, ganttNodes, edges);
            var machineReady = machineAvailability.GetValueOrDefault(machineId, TimeSpan.Zero);
            var earliestStart = machineReady > predecessorReady ? machineReady : predecessorReady;

            // Schedule the task
            task.GanttStartTime = earliestStart;

            // Set row index based on machine
            var machine = machineNodes.FirstOrDefault(m => m.Id == machineId);
            if (machine != null)
            {
                task.GanttRowIndex = machine.GanttRowIndex;
            }

            // Update machine availability
            if (task.GanttEndTime.HasValue)
            {
                machineAvailability[machineId] = task.GanttEndTime.Value;
            }

            result.TasksScheduled++;
        }

        // Validate
        var validation = ValidateNodePrecedences(ganttNodes, edges);
        result.Violations = validation.Violations;
        result.ViolationsFound = validation.ViolationsFound;
        result.Makespan = taskNodes.Any() && taskNodes.All(t => t.GanttEndTime.HasValue)
            ? taskNodes.Max(t => t.GanttEndTime!.Value)
            : TimeSpan.Zero;
        result.Success = validation.Success;
        result.Message = $"SPT scheduling complete. {result.TasksScheduled} tasks scheduled. Makespan: {FormatTime(result.Makespan)}";

        return result;
    }

    /// <summary>
    /// Schedules tasks using LPT (Longest Processing Time) rule.
    /// Tasks are sorted by duration (longest first) and scheduled in order.
    /// </summary>
    public SchedulingResult ScheduleLPT(GanttSchedule schedule)
    {
        var result = new SchedulingResult();

        var tasksToSchedule = schedule.Tasks
            .OrderByDescending(t => t.Duration.TotalMinutes) // LPT: longest first
            .ThenBy(t => t.Priority)
            .ToList();

        var machineAvailability = schedule.Machines.ToDictionary(m => m.Id, m => TimeSpan.Zero);

        foreach (var task in tasksToSchedule)
        {
            if (!machineAvailability.ContainsKey(task.MachineId))
                continue;

            var machineReady = machineAvailability[task.MachineId];
            var predecessorReady = GetEarliestStartAfterPredecessors(schedule, task.Id);
            var earliestStart = machineReady > predecessorReady ? machineReady : predecessorReady;

            task.StartTime = earliestStart;
            task.RowIndex = schedule.GetMachine(task.MachineId)?.RowIndex ?? 0;
            machineAvailability[task.MachineId] = task.EndTime;

            result.TasksScheduled++;
        }

        var validation = ValidatePrecedences(schedule);
        result.Violations = validation.Violations;
        result.ViolationsFound = validation.ViolationsFound;
        result.Makespan = schedule.GetMakespan();
        result.Success = validation.Success;
        result.Message = $"LPT scheduling complete. {result.TasksScheduled} tasks scheduled. Makespan: {FormatTime(result.Makespan)}";

        return result;
    }

    /// <summary>
    /// Schedules tasks using FIFO (First In First Out) rule.
    /// Tasks are scheduled in the order they appear.
    /// </summary>
    public SchedulingResult ScheduleFIFO(GanttSchedule schedule)
    {
        var result = new SchedulingResult();

        var tasksToSchedule = schedule.Tasks.ToList(); // Keep original order

        var machineAvailability = schedule.Machines.ToDictionary(m => m.Id, m => TimeSpan.Zero);

        foreach (var task in tasksToSchedule)
        {
            if (!machineAvailability.ContainsKey(task.MachineId))
                continue;

            var machineReady = machineAvailability[task.MachineId];
            var predecessorReady = GetEarliestStartAfterPredecessors(schedule, task.Id);
            var earliestStart = machineReady > predecessorReady ? machineReady : predecessorReady;

            task.StartTime = earliestStart;
            task.RowIndex = schedule.GetMachine(task.MachineId)?.RowIndex ?? 0;
            machineAvailability[task.MachineId] = task.EndTime;

            result.TasksScheduled++;
        }

        var validation = ValidatePrecedences(schedule);
        result.Violations = validation.Violations;
        result.ViolationsFound = validation.ViolationsFound;
        result.Makespan = schedule.GetMakespan();
        result.Success = validation.Success;
        result.Message = $"FIFO scheduling complete. {result.TasksScheduled} tasks scheduled. Makespan: {FormatTime(result.Makespan)}";

        return result;
    }

    // ============================================
    // AUTO-FIX METHODS
    // ============================================

    /// <summary>
    /// Automatically fixes precedence violations by shifting successor tasks forward.
    /// </summary>
    public SchedulingResult AutoFixViolations(GanttSchedule schedule)
    {
        var result = new SchedulingResult();
        var maxIterations = 100; // Prevent infinite loops
        var iteration = 0;

        while (iteration < maxIterations)
        {
            var validation = ValidatePrecedences(schedule);
            if (validation.Success)
            {
                result.Success = true;
                result.Message = $"All violations fixed after {iteration} iteration(s)";
                result.Makespan = schedule.GetMakespan();
                return result;
            }

            // Fix the first violation
            var violation = validation.Violations.First();
            var successor = schedule.GetTask(violation.SuccessorTaskId);
            if (successor != null)
            {
                successor.ShiftBy(violation.RequiredShift);
            }

            iteration++;
        }

        result.Success = false;
        result.Message = "Could not fix all violations (possible circular dependencies)";
        return result;
    }

    /// <summary>
    /// Auto-fixes precedence violations for Node-based Gantt tasks.
    /// </summary>
    public SchedulingResult AutoFixNodeViolations(List<Node> ganttNodes, IEnumerable<Edge> edges)
    {
        var result = new SchedulingResult();
        var maxIterations = 100;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            var validation = ValidateNodePrecedences(ganttNodes, edges);
            if (validation.Success)
            {
                result.Success = true;
                result.Message = $"All violations fixed after {iteration} iteration(s)";
                return result;
            }

            // Fix the first violation
            var violation = validation.Violations.First();
            var successor = ganttNodes.FirstOrDefault(n => n.Id == violation.SuccessorTaskId);
            if (successor != null && successor.GanttStartTime.HasValue)
            {
                successor.GanttStartTime = successor.GanttStartTime.Value + violation.RequiredShift;
            }

            iteration++;
        }

        result.Success = false;
        result.Message = "Could not fix all violations (possible circular dependencies)";
        return result;
    }

    // ============================================
    // HELPER METHODS
    // ============================================

    /// <summary>
    /// Gets the earliest time a task can start based on its predecessors.
    /// </summary>
    private TimeSpan GetEarliestStartAfterPredecessors(GanttSchedule schedule, int taskId)
    {
        var earliestStart = TimeSpan.Zero;

        foreach (var precedence in schedule.Precedences.Where(p => p.SuccessorTaskId == taskId))
        {
            var predecessor = schedule.GetTask(precedence.PredecessorTaskId);
            if (predecessor != null)
            {
                var requiredStart = predecessor.EndTime + precedence.LagTime;
                if (requiredStart > earliestStart)
                {
                    earliestStart = requiredStart;
                }
            }
        }

        return earliestStart;
    }

    /// <summary>
    /// Gets the earliest time a node task can start based on its predecessors (edges).
    /// </summary>
    private TimeSpan GetEarliestNodeStartAfterPredecessors(int nodeId, IEnumerable<Node> nodes, IEnumerable<Edge> edges)
    {
        var earliestStart = TimeSpan.Zero;

        // Find edges where this node is the target (successor)
        foreach (var edge in edges.Where(e => e.To == nodeId))
        {
            var predecessor = nodes.FirstOrDefault(n => n.Id == edge.From);
            if (predecessor?.GanttEndTime != null)
            {
                var requiredStart = predecessor.GanttEndTime.Value;
                if (requiredStart > earliestStart)
                {
                    earliestStart = requiredStart;
                }
            }
        }

        return earliestStart;
    }

    /// <summary>
    /// Compresses a task to its earliest possible start time on the machine,
    /// respecting both precedence constraints and machine conflicts.
    /// </summary>
    /// <param name="taskNode">The task node to compress</param>
    /// <param name="allNodes">All nodes in the diagram</param>
    /// <param name="allEdges">All edges (precedence constraints)</param>
    /// <returns>The new start time for the task, or null if it cannot be moved</returns>
    public TimeSpan? CompressTask(Node taskNode, IEnumerable<Node> allNodes, IEnumerable<Edge> allEdges)
    {
        if (!taskNode.IsGanttTask || !taskNode.GanttStartTime.HasValue ||
            !taskNode.GanttDuration.HasValue || !taskNode.GanttMachineId.HasValue)
        {
            return null;
        }

        var nodesList = allNodes.ToList();
        var edgesList = allEdges.ToList();
        var taskDuration = taskNode.GanttDuration.Value;
        var taskMachineId = taskNode.GanttMachineId.Value;

        // 1. Find earliest start based on precedence constraints (predecessors must finish first)
        var earliestPrecedenceStart = GetEarliestNodeStartAfterPredecessors(taskNode.Id, nodesList, edgesList);

        // 2. Get all other tasks on the same machine (excluding this task)
        var sameMachineTasks = nodesList
            .Where(n => n.IsGanttTask && n.Id != taskNode.Id &&
                       n.GanttMachineId == taskMachineId &&
                       n.GanttStartTime.HasValue && n.GanttDuration.HasValue)
            .OrderBy(n => n.GanttStartTime!.Value)
            .ToList();

        // 3. Find the earliest slot that can fit the task without overlapping other tasks
        var earliestMachineStart = FindEarliestSlotOnMachine(
            earliestPrecedenceStart,
            taskDuration,
            sameMachineTasks,
            taskNode.GanttStartTime.Value);

        // The earliest possible start is the maximum of precedence and machine constraints
        var newStart = earliestMachineStart;

        // Only move if it's actually earlier than current position
        if (newStart < taskNode.GanttStartTime.Value)
        {
            return newStart;
        }

        return null; // Already at earliest position
    }

    /// <summary>
    /// Finds the earliest time slot on a machine where a task can fit,
    /// starting from the given earliest possible time.
    /// </summary>
    private TimeSpan FindEarliestSlotOnMachine(
        TimeSpan earliestStart,
        TimeSpan taskDuration,
        List<Node> sameMachineTasks,
        TimeSpan currentTaskStart)
    {
        // Try starting at the earliest precedence-allowed time
        var candidateStart = earliestStart;

        // Check if we can fit in this slot
        foreach (var otherTask in sameMachineTasks)
        {
            var otherStart = otherTask.GanttStartTime!.Value;
            var otherEnd = otherTask.GanttEndTime!.Value;

            // Skip tasks that start at or after our current position (we're moving back, not forward)
            if (otherStart >= currentTaskStart)
                continue;

            // Check if our candidate slot would overlap with this task
            var candidateEnd = candidateStart + taskDuration;

            if (candidateStart < otherEnd && candidateEnd > otherStart)
            {
                // Overlap detected - move our candidate to after this task ends
                candidateStart = otherEnd;
            }
        }

        return candidateStart;
    }

    /// <summary>
    /// Formats a TimeSpan as hours:minutes.
    /// </summary>
    private static string FormatTime(TimeSpan time)
    {
        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        return $"{hours}:{minutes:D2}";
    }

    // ============================================
    // METRICS
    // ============================================

    /// <summary>
    /// Calculates scheduling metrics for a schedule.
    /// </summary>
    public ScheduleMetrics CalculateMetrics(GanttSchedule schedule)
    {
        var metrics = new ScheduleMetrics();

        if (schedule.Tasks.Count == 0)
            return metrics;

        metrics.TaskCount = schedule.Tasks.Count;
        metrics.MachineCount = schedule.Machines.Count;
        metrics.JobCount = schedule.Jobs.Count;
        metrics.Makespan = schedule.GetMakespan();

        // Calculate total processing time
        metrics.TotalProcessingTime = schedule.Tasks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration);

        // Calculate average flow time per job
        foreach (var job in schedule.Jobs)
        {
            var jobTasks = schedule.GetTasksForJob(job.Id).ToList();
            if (jobTasks.Count > 0)
            {
                job.CompletionTime = jobTasks.Max(t => t.EndTime);
            }
        }

        var completedJobs = schedule.Jobs.Where(j => j.CompletionTime.HasValue).ToList();
        if (completedJobs.Count > 0)
        {
            metrics.AverageFlowTime = TimeSpan.FromMinutes(
                completedJobs.Average(j => j.FlowTime?.TotalMinutes ?? 0));
        }

        // Calculate machine utilization
        if (schedule.Machines.Count > 0 && metrics.Makespan > TimeSpan.Zero)
        {
            var totalCapacity = schedule.Machines.Count * metrics.Makespan.TotalMinutes;
            metrics.MachineUtilization = (metrics.TotalProcessingTime.TotalMinutes / totalCapacity) * 100;
        }

        // Count violations
        var validation = ValidatePrecedences(schedule);
        metrics.ViolationCount = validation.ViolationsFound;

        // Count late jobs
        metrics.LateJobCount = schedule.Jobs.Count(j => j.IsLate);

        return metrics;
    }
}

/// <summary>
/// Metrics for a Gantt schedule.
/// </summary>
public class ScheduleMetrics
{
    public int TaskCount { get; set; }
    public int MachineCount { get; set; }
    public int JobCount { get; set; }
    public TimeSpan Makespan { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public TimeSpan AverageFlowTime { get; set; }
    public double MachineUtilization { get; set; }
    public int ViolationCount { get; set; }
    public int LateJobCount { get; set; }
}
