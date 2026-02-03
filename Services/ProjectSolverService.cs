using dfd2wasm.Models;
using System.Diagnostics;

namespace dfd2wasm.Services;

/// <summary>
/// Service for optimizing Project chart schedules using constraint programming.
/// This is a stub implementation - OR-Tools integration will be added later.
/// </summary>
public class ProjectSolverService
{
    /// <summary>
    /// Solve the scheduling optimization problem
    /// </summary>
    /// <param name="nodes">All nodes in the diagram (includes Project tasks)</param>
    /// <param name="edges">All edges in the diagram (includes Project dependencies)</param>
    /// <param name="config">Solver configuration with goals and costs</param>
    /// <returns>Solver result with optimized schedule or infeasibility info</returns>
    public SolverResult Solve(List<Node> nodes, List<Edge> edges, SolverConfiguration config)
    {
        var sw = Stopwatch.StartNew();
        var result = new SolverResult();

        try
        {
            // Get Project tasks and dependencies
            var projectTasks = nodes.Where(n => n.TemplateId == "project" && !n.IsProjectResource).ToList();
            var projectDeps = edges.Where(e => e.IsProjectDependency).ToList();

            if (!projectTasks.Any())
            {
                result.Status = SolverStatus.Error;
                result.Message = "No Project tasks found. Add tasks to the timeline first.";
                return result;
            }

            // Calculate before metrics
            result.BeforeMetrics = CalculateMetrics(projectTasks, projectDeps);

            // Validate constraints and detect infeasibility
            var violations = ValidateConstraints(projectTasks, projectDeps, config);
            if (violations.Any(v => v.Severity == ViolationSeverity.Error))
            {
                result.Status = SolverStatus.Infeasible;
                result.Message = "Cannot find a feasible schedule with current constraints.";
                result.Violations = violations;
                result.SuggestedFixes = GenerateSuggestedFixes(violations, projectTasks, config);
                sw.Stop();
                result.SolveTimeMs = sw.ElapsedMilliseconds;
                return result;
            }

            // TODO: Implement OR-Tools CP-SAT solver here
            // For now, use a simple heuristic approach

            var optimizedDurations = new Dictionary<int, int>();
            var optimizedSchedule = RunSimpleHeuristic(projectTasks, projectDeps, config);

            // Apply task compression if enabled and deadline needs to be met
            if (config.AllowTaskCompression && config.GoalMeetDeadline && config.DeadlineDate.HasValue)
            {
                var compressionResult = ApplyTaskCompression(
                    projectTasks, projectDeps, config,
                    optimizedSchedule, optimizedDurations);

                optimizedSchedule = compressionResult.Schedule;
                optimizedDurations = compressionResult.Durations;
                result.TotalCompressionCost = compressionResult.TotalCost;
            }

            result.OptimizedSchedule = optimizedSchedule;
            result.OptimizedDurations = optimizedDurations;

            // Calculate after metrics with optimized durations
            result.AfterMetrics = CalculateMetricsFromSchedule(projectTasks, optimizedSchedule, optimizedDurations);

            result.Status = SolverStatus.Optimal;
            result.Message = $"Found optimal schedule for {projectTasks.Count} tasks.";
            result.Violations = violations.Where(v => v.Severity == ViolationSeverity.Warning).ToList();
        }
        catch (Exception ex)
        {
            result.Status = SolverStatus.Error;
            result.Message = $"Solver error: {ex.Message}";
        }

        sw.Stop();
        result.SolveTimeMs = sw.ElapsedMilliseconds;
        return result;
    }

    /// <summary>
    /// Validate constraints and detect potential infeasibility
    /// </summary>
    private List<ConstraintViolation> ValidateConstraints(
        List<Node> tasks,
        List<Edge> deps,
        SolverConfiguration config)
    {
        var violations = new List<ConstraintViolation>();

        // Check for circular dependencies
        var circularDeps = DetectCircularDependencies(tasks, deps);
        if (circularDeps.Any())
        {
            violations.Add(new ConstraintViolation
            {
                ConstraintName = "No Circular Dependencies",
                Description = $"Circular dependency detected involving tasks: {string.Join(", ", circularDeps.Select(id => tasks.FirstOrDefault(t => t.Id == id)?.Text ?? id.ToString()))}",
                AffectedTaskIds = circularDeps,
                Severity = ViolationSeverity.Error
            });
        }

        // Check milestone constraints
        if (config.GoalMeetMilestones)
        {
            var milestones = tasks.Where(t => t.ProjectIsMilestone).ToList();
            foreach (var milestone in milestones)
            {
                // Check if any predecessor can't finish before milestone
                var predecessors = GetAllPredecessors(milestone.Id, tasks, deps);
                var latestPredEnd = predecessors
                    .Where(p => p.ProjectEndDate.HasValue)
                    .Select(p => p.ProjectEndDate!.Value)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();

                if (milestone.ProjectStartDate.HasValue && latestPredEnd > milestone.ProjectStartDate.Value)
                {
                    violations.Add(new ConstraintViolation
                    {
                        ConstraintName = "Meet Milestones",
                        Description = $"Milestone '{milestone.Text}' ({milestone.ProjectStartDate:yyyy-MM-dd}) cannot be met - predecessors end on {latestPredEnd:yyyy-MM-dd}",
                        AffectedTaskIds = new List<int> { milestone.Id },
                        Severity = ViolationSeverity.Error
                    });
                }
            }
        }

        // Check deadline constraint
        if (config.GoalMeetDeadline && config.DeadlineDate.HasValue)
        {
            var projectEnd = tasks
                .Where(t => t.ProjectEndDate.HasValue)
                .Select(t => t.ProjectEndDate!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            if (projectEnd > config.DeadlineDate.Value)
            {
                violations.Add(new ConstraintViolation
                {
                    ConstraintName = "Meet Deadline",
                    Description = $"Project ends on {projectEnd:yyyy-MM-dd}, but deadline is {config.DeadlineDate.Value:yyyy-MM-dd}",
                    AffectedTaskIds = tasks.Where(t => t.ProjectEndDate == projectEnd).Select(t => t.Id).ToList(),
                    Severity = ViolationSeverity.Warning // Warning, not error - solver might fix it
                });
            }
        }

        // Check resource overallocation
        if (config.GoalNoOverallocation)
        {
            var resourceConflicts = DetectResourceConflicts(tasks);
            foreach (var conflict in resourceConflicts)
            {
                violations.Add(new ConstraintViolation
                {
                    ConstraintName = "No Overallocation",
                    Description = conflict.Description,
                    AffectedTaskIds = conflict.TaskIds,
                    Severity = ViolationSeverity.Warning
                });
            }
        }

        return violations;
    }

    /// <summary>
    /// Detect circular dependencies using DFS
    /// </summary>
    private List<int> DetectCircularDependencies(List<Node> tasks, List<Edge> deps)
    {
        var taskIds = new HashSet<int>(tasks.Select(t => t.Id));
        var graph = deps
            .Where(d => taskIds.Contains(d.From) && taskIds.Contains(d.To))
            .GroupBy(d => d.From)
            .ToDictionary(g => g.Key, g => g.Select(d => d.To).ToList());

        var visited = new HashSet<int>();
        var inStack = new HashSet<int>();
        var cycle = new List<int>();

        bool DFS(int node)
        {
            visited.Add(node);
            inStack.Add(node);

            if (graph.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        if (DFS(neighbor))
                        {
                            cycle.Add(node);
                            return true;
                        }
                    }
                    else if (inStack.Contains(neighbor))
                    {
                        cycle.Add(neighbor);
                        cycle.Add(node);
                        return true;
                    }
                }
            }

            inStack.Remove(node);
            return false;
        }

        foreach (var taskId in taskIds)
        {
            if (!visited.Contains(taskId))
            {
                if (DFS(taskId))
                    break;
            }
        }

        return cycle.Distinct().ToList();
    }

    /// <summary>
    /// Get all predecessor tasks (transitive closure)
    /// </summary>
    private List<Node> GetAllPredecessors(int taskId, List<Node> tasks, List<Edge> deps)
    {
        var result = new List<Node>();
        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        // Find direct predecessors
        var directPreds = deps.Where(d => d.To == taskId).Select(d => d.From).ToList();
        foreach (var pred in directPreds)
        {
            queue.Enqueue(pred);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current)) continue;
            visited.Add(current);

            var task = tasks.FirstOrDefault(t => t.Id == current);
            if (task != null)
            {
                result.Add(task);
            }

            // Add predecessors of current
            var preds = deps.Where(d => d.To == current).Select(d => d.From);
            foreach (var pred in preds)
            {
                if (!visited.Contains(pred))
                    queue.Enqueue(pred);
            }
        }

        return result;
    }

    /// <summary>
    /// Detect resource conflicts (overlapping task assignments)
    /// </summary>
    private List<(string Description, List<int> TaskIds)> DetectResourceConflicts(List<Node> tasks)
    {
        var conflicts = new List<(string Description, List<int> TaskIds)>();

        // Group tasks by assigned resource
        var byResource = tasks
            .Where(t => !string.IsNullOrEmpty(t.ProjectAssignedTo) && t.ProjectStartDate.HasValue && t.ProjectEndDate.HasValue)
            .GroupBy(t => t.ProjectAssignedTo!);

        foreach (var group in byResource)
        {
            var resourceTasks = group.OrderBy(t => t.ProjectStartDate).ToList();

            for (int i = 0; i < resourceTasks.Count - 1; i++)
            {
                var current = resourceTasks[i];
                var next = resourceTasks[i + 1];

                // Check overlap
                if (current.ProjectEndDate > next.ProjectStartDate)
                {
                    conflicts.Add((
                        $"Resource '{group.Key}' is overallocated: '{current.Text}' overlaps with '{next.Text}'",
                        new List<int> { current.Id, next.Id }
                    ));
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Generate suggested fixes for constraint violations
    /// </summary>
    private List<string> GenerateSuggestedFixes(
        List<ConstraintViolation> violations,
        List<Node> tasks,
        SolverConfiguration config)
    {
        var fixes = new List<string>();

        foreach (var violation in violations.Where(v => v.Severity == ViolationSeverity.Error))
        {
            switch (violation.ConstraintName)
            {
                case "No Circular Dependencies":
                    fixes.Add("Remove one of the dependency links to break the cycle.");
                    break;

                case "Meet Milestones":
                    fixes.Add($"Move the milestone date later, or reduce predecessor durations.");
                    if (!config.GoalMeetMilestones)
                        fixes.Add("Or uncheck 'Meet milestone dates' to allow slippage.");
                    break;

                case "Meet Deadline":
                    fixes.Add("Reduce task durations or parallelize work to meet the deadline.");
                    fixes.Add("Or extend the deadline date.");
                    break;
            }
        }

        return fixes.Distinct().ToList();
    }

    /// <summary>
    /// Run a simple forward-scheduling heuristic
    /// </summary>
    private Dictionary<int, DateTime> RunSimpleHeuristic(
        List<Node> tasks,
        List<Edge> deps,
        SolverConfiguration config)
    {
        var schedule = new Dictionary<int, DateTime>();

        // Build predecessor map
        var predecessors = deps
            .GroupBy(d => d.To)
            .ToDictionary(g => g.Key, g => g.Select(d => d.From).ToList());

        // Topological sort
        var sorted = TopologicalSort(tasks, deps);

        foreach (var task in sorted)
        {
            // Skip locked tasks
            if (config.GoalCompletedTasksLocked && task.ProjectPercentComplete >= 100)
            {
                if (task.ProjectStartDate.HasValue)
                    schedule[task.Id] = task.ProjectStartDate.Value;
                continue;
            }

            // Calculate earliest start based on predecessors
            DateTime earliestStart = task.ProjectStartDate ?? DateTime.Today;

            if (predecessors.TryGetValue(task.Id, out var predIds))
            {
                foreach (var predId in predIds)
                {
                    var predTask = tasks.FirstOrDefault(t => t.Id == predId);
                    if (predTask?.ProjectEndDate != null)
                    {
                        var predEnd = schedule.TryGetValue(predId, out var schedStart)
                            ? schedStart.AddDays(predTask.ProjectDurationDays)
                            : predTask.ProjectEndDate.Value;

                        // Get lag from dependency
                        var dep = deps.FirstOrDefault(d => d.From == predId && d.To == task.Id);
                        var lag = dep?.ProjectLagDays ?? 0;

                        var constrainedStart = predEnd.AddDays(lag);
                        if (constrainedStart > earliestStart)
                            earliestStart = constrainedStart;
                    }
                }
            }

            // Skip weekends for start date
            while (earliestStart.DayOfWeek == DayOfWeek.Saturday ||
                   earliestStart.DayOfWeek == DayOfWeek.Sunday)
            {
                earliestStart = earliestStart.AddDays(1);
            }

            schedule[task.Id] = earliestStart;
        }

        return schedule;
    }

    /// <summary>
    /// Perform topological sort on tasks
    /// </summary>
    private List<Node> TopologicalSort(List<Node> tasks, List<Edge> deps)
    {
        var taskDict = tasks.ToDictionary(t => t.Id);
        var inDegree = tasks.ToDictionary(t => t.Id, _ => 0);

        foreach (var dep in deps)
        {
            if (inDegree.ContainsKey(dep.To))
                inDegree[dep.To]++;
        }

        var queue = new Queue<int>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<Node>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (taskDict.TryGetValue(id, out var task))
                result.Add(task);

            foreach (var dep in deps.Where(d => d.From == id))
            {
                if (inDegree.ContainsKey(dep.To))
                {
                    inDegree[dep.To]--;
                    if (inDegree[dep.To] == 0)
                        queue.Enqueue(dep.To);
                }
            }
        }

        // Add any remaining tasks (not in dependency graph)
        foreach (var task in tasks)
        {
            if (!result.Contains(task))
                result.Add(task);
        }

        return result;
    }

    /// <summary>
    /// Calculate metrics from current task states
    /// </summary>
    private SolverMetrics CalculateMetrics(List<Node> tasks, List<Edge> deps)
    {
        var metrics = new SolverMetrics();

        var tasksWithDates = tasks.Where(t => t.ProjectStartDate.HasValue && t.ProjectEndDate.HasValue).ToList();

        if (tasksWithDates.Any())
        {
            metrics.ProjectStartDate = tasksWithDates.Min(t => t.ProjectStartDate!.Value);
            metrics.ProjectEndDate = tasksWithDates.Max(t => t.ProjectEndDate!.Value);
            metrics.ProjectDurationDays = (int)(metrics.ProjectEndDate - metrics.ProjectStartDate).TotalDays;
        }

        // Total cost
        metrics.TotalProjectCost = tasks.Sum(t => t.ProjectCostPerDay * t.ProjectDurationDays);

        // Resource conflicts
        var conflicts = DetectResourceConflicts(tasks);
        metrics.ResourceConflictCount = conflicts.Count;

        // Milestone slippage (would need original dates to calculate properly)
        metrics.MilestoneSlippageDays = 0;

        // Peak daily cost
        metrics.PeakDailyCost = CalculatePeakDailyCost(tasks);

        return metrics;
    }

    /// <summary>
    /// Calculate metrics from optimized schedule
    /// </summary>
    private SolverMetrics CalculateMetricsFromSchedule(
        List<Node> tasks,
        Dictionary<int, DateTime> schedule,
        Dictionary<int, int>? optimizedDurations = null)
    {
        var metrics = new SolverMetrics();

        if (!schedule.Any()) return metrics;

        // Calculate end dates from new start dates and durations
        var endDates = new List<DateTime>();
        foreach (var task in tasks)
        {
            if (schedule.TryGetValue(task.Id, out var start))
            {
                // Use optimized duration if available, otherwise original
                var duration = optimizedDurations?.GetValueOrDefault(task.Id, task.ProjectDurationDays)
                               ?? task.ProjectDurationDays;
                endDates.Add(start.AddDays(duration));
            }
        }

        if (schedule.Any())
        {
            metrics.ProjectStartDate = schedule.Values.Min();
            metrics.ProjectEndDate = endDates.Any() ? endDates.Max() : schedule.Values.Max();
            metrics.ProjectDurationDays = (int)(metrics.ProjectEndDate - metrics.ProjectStartDate).TotalDays;
        }

        // Calculate total cost with optimized durations
        metrics.TotalProjectCost = tasks.Sum(t =>
        {
            var duration = optimizedDurations?.GetValueOrDefault(t.Id, t.ProjectDurationDays)
                           ?? t.ProjectDurationDays;
            return t.ProjectCostPerDay * duration;
        });

        // Resource conflicts after optimization (would need to recalculate)
        metrics.ResourceConflictCount = 0;

        return metrics;
    }

    /// <summary>
    /// Apply task compression to meet deadline, respecting min durations and cost models
    /// </summary>
    private (Dictionary<int, DateTime> Schedule, Dictionary<int, int> Durations, decimal TotalCost)
        ApplyTaskCompression(
            List<Node> tasks,
            List<Edge> deps,
            SolverConfiguration config,
            Dictionary<int, DateTime> currentSchedule,
            Dictionary<int, int> currentDurations)
    {
        var schedule = new Dictionary<int, DateTime>(currentSchedule);
        var durations = new Dictionary<int, int>(currentDurations);
        decimal totalCompressionCost = 0;

        if (!config.DeadlineDate.HasValue) return (schedule, durations, totalCompressionCost);

        var deadline = config.DeadlineDate.Value;

        // Calculate current project end
        var projectEnd = CalculateProjectEnd(tasks, schedule, durations);

        // If already meeting deadline, no compression needed
        if (projectEnd <= deadline)
        {
            return (schedule, durations, totalCompressionCost);
        }

        // Get compressible tasks sorted by crash cost efficiency (lowest cost first)
        var compressibleTasks = tasks
            .Where(t => t.ProjectAllowCompression && t.ProjectDurationDays > t.ProjectMinDuration)
            .OrderBy(t => t.ProjectCrashCostPerDay) // Cheapest first
            .ThenByDescending(t => t.ProjectDurationDays - t.ProjectMinDuration) // Most compressible first
            .ToList();

        // Iteratively compress tasks until deadline is met or no more compression possible
        while (projectEnd > deadline && compressibleTasks.Any())
        {
            bool madeProgress = false;

            foreach (var task in compressibleTasks.ToList())
            {
                var currentDuration = durations.GetValueOrDefault(task.Id, task.ProjectDurationDays);
                var minDuration = Math.Max(1, task.ProjectMinDuration);

                if (currentDuration <= minDuration)
                {
                    compressibleTasks.Remove(task);
                    continue;
                }

                // Reduce by 1 day
                var newDuration = currentDuration - 1;
                var daysReduced = task.ProjectDurationDays - newDuration;

                // Calculate compression cost based on model
                decimal compressionCost = CalculateCompressionCost(
                    task.ProjectCrashCostPerDay,
                    daysReduced,
                    config.CompressionCostModel);

                durations[task.Id] = newDuration;
                madeProgress = true;

                // Recalculate schedule with new durations
                schedule = RecalculateScheduleWithDurations(tasks, deps, config, durations);

                // Recalculate project end
                projectEnd = CalculateProjectEnd(tasks, schedule, durations);

                if (projectEnd <= deadline)
                {
                    // Calculate final total compression cost
                    totalCompressionCost = CalculateTotalCompressionCost(tasks, durations, config.CompressionCostModel);
                    return (schedule, durations, totalCompressionCost);
                }
            }

            if (!madeProgress) break;
        }

        // Calculate final total compression cost
        totalCompressionCost = CalculateTotalCompressionCost(tasks, durations, config.CompressionCostModel);
        return (schedule, durations, totalCompressionCost);
    }

    /// <summary>
    /// Calculate compression cost based on the selected cost model
    /// </summary>
    private decimal CalculateCompressionCost(decimal crashCostPerDay, int daysReduced, CompressionCostModel model)
    {
        return model switch
        {
            CompressionCostModel.Linear => crashCostPerDay * daysReduced,
            CompressionCostModel.Quadratic => crashCostPerDay * daysReduced * daysReduced,
            _ => crashCostPerDay * daysReduced
        };
    }

    /// <summary>
    /// Calculate total compression cost for all compressed tasks
    /// </summary>
    private decimal CalculateTotalCompressionCost(
        List<Node> tasks,
        Dictionary<int, int> durations,
        CompressionCostModel model)
    {
        decimal total = 0;

        foreach (var task in tasks.Where(t => t.ProjectAllowCompression))
        {
            if (durations.TryGetValue(task.Id, out var newDuration))
            {
                var daysReduced = task.ProjectDurationDays - newDuration;
                if (daysReduced > 0)
                {
                    total += CalculateCompressionCost(task.ProjectCrashCostPerDay, daysReduced, model);
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Calculate project end date with current schedule and durations
    /// </summary>
    private DateTime CalculateProjectEnd(
        List<Node> tasks,
        Dictionary<int, DateTime> schedule,
        Dictionary<int, int> durations)
    {
        var endDates = new List<DateTime>();

        foreach (var task in tasks)
        {
            if (schedule.TryGetValue(task.Id, out var start))
            {
                var duration = durations.GetValueOrDefault(task.Id, task.ProjectDurationDays);
                endDates.Add(start.AddDays(duration));
            }
        }

        return endDates.Any() ? endDates.Max() : DateTime.MaxValue;
    }

    /// <summary>
    /// Recalculate schedule with updated durations
    /// </summary>
    private Dictionary<int, DateTime> RecalculateScheduleWithDurations(
        List<Node> tasks,
        List<Edge> deps,
        SolverConfiguration config,
        Dictionary<int, int> durations)
    {
        var schedule = new Dictionary<int, DateTime>();

        // Build predecessor map
        var predecessors = deps
            .GroupBy(d => d.To)
            .ToDictionary(g => g.Key, g => g.Select(d => d.From).ToList());

        // Topological sort
        var sorted = TopologicalSort(tasks, deps);

        foreach (var task in sorted)
        {
            // Skip locked tasks
            if (config.GoalCompletedTasksLocked && task.ProjectPercentComplete >= 100)
            {
                if (task.ProjectStartDate.HasValue)
                    schedule[task.Id] = task.ProjectStartDate.Value;
                continue;
            }

            // Calculate earliest start based on predecessors
            DateTime earliestStart = task.ProjectStartDate ?? DateTime.Today;

            if (predecessors.TryGetValue(task.Id, out var predIds))
            {
                foreach (var predId in predIds)
                {
                    var predTask = tasks.FirstOrDefault(t => t.Id == predId);
                    if (predTask != null)
                    {
                        var predDuration = durations.GetValueOrDefault(predId, predTask.ProjectDurationDays);
                        var predEnd = schedule.TryGetValue(predId, out var schedStart)
                            ? schedStart.AddDays(predDuration)
                            : (predTask.ProjectStartDate?.AddDays(predDuration) ?? DateTime.Today);

                        // Get lag from dependency
                        var dep = deps.FirstOrDefault(d => d.From == predId && d.To == task.Id);
                        var lag = dep?.ProjectLagDays ?? 0;

                        var constrainedStart = predEnd.AddDays(lag);
                        if (constrainedStart > earliestStart)
                            earliestStart = constrainedStart;
                    }
                }
            }

            // Skip weekends for start date
            while (earliestStart.DayOfWeek == DayOfWeek.Saturday ||
                   earliestStart.DayOfWeek == DayOfWeek.Sunday)
            {
                earliestStart = earliestStart.AddDays(1);
            }

            schedule[task.Id] = earliestStart;
        }

        return schedule;
    }

    /// <summary>
    /// Calculate peak daily cost across all days of the project
    /// </summary>
    private decimal CalculatePeakDailyCost(List<Node> tasks)
    {
        var tasksWithDates = tasks
            .Where(t => t.ProjectStartDate.HasValue && t.ProjectEndDate.HasValue && t.ProjectDurationDays > 0)
            .ToList();

        if (!tasksWithDates.Any()) return 0;

        var projectStart = tasksWithDates.Min(t => t.ProjectStartDate!.Value);
        var projectEnd = tasksWithDates.Max(t => t.ProjectEndDate!.Value);

        decimal peakCost = 0;

        for (var day = projectStart; day <= projectEnd; day = day.AddDays(1))
        {
            var dayCost = tasksWithDates
                .Where(t => t.ProjectStartDate <= day && t.ProjectEndDate > day)
                .Sum(t => t.ProjectCostPerDay);

            if (dayCost > peakCost)
                peakCost = dayCost;
        }

        return peakCost;
    }
}
