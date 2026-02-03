using NodaTime;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Result of CPM calculation for a task
/// </summary>
public class CpmTaskResult
{
    public int TaskId { get; set; }
    public LocalDate EarlyStart { get; set; }
    public LocalDate EarlyFinish { get; set; }
    public LocalDate LateStart { get; set; }
    public LocalDate LateFinish { get; set; }
    public int TotalFloat { get; set; }
    public int FreeFloat { get; set; }
    public bool IsCritical => TotalFloat == 0;
}

/// <summary>
/// Service for Project scheduling using Critical Path Method (CPM)
/// </summary>
public class ProjectSchedulingService
{
    private readonly ProjectCalendar _calendar;

    public ProjectSchedulingService(ProjectCalendar? calendar = null)
    {
        _calendar = calendar ?? new ProjectCalendar();
    }

    /// <summary>
    /// Performs full CPM analysis on a project
    /// </summary>
    public Dictionary<int, CpmTaskResult> CalculateCpm(ProjectChart project)
    {
        var results = new Dictionary<int, CpmTaskResult>();

        // Check for circular dependencies first
        if (HasCircularDependency(project))
        {
            throw new InvalidOperationException("Project contains circular dependencies");
        }

        // Get topologically sorted tasks
        var sortedTasks = TopologicalSort(project);

        // Forward pass - calculate early start/finish
        ForwardPass(project, sortedTasks, results);

        // Backward pass - calculate late start/finish
        BackwardPass(project, sortedTasks, results);

        // Calculate floats
        CalculateFloats(project, results);

        return results;
    }

    /// <summary>
    /// Forward pass: Calculate early start and early finish for all tasks
    /// </summary>
    private void ForwardPass(ProjectChart project, List<ProjectTask> sortedTasks, Dictionary<int, CpmTaskResult> results)
    {
        foreach (var task in sortedTasks)
        {
            var result = new CpmTaskResult { TaskId = task.Id };

            // Find the earliest possible start based on predecessors
            var predecessors = project.GetPredecessors(task.Id).ToList();
            if (predecessors.Count == 0)
            {
                // No predecessors - can start at project start
                result.EarlyStart = project.StartDate;
            }
            else
            {
                result.EarlyStart = project.StartDate;
                foreach (var dep in predecessors)
                {
                    var predTask = project.GetTask(dep.PredecessorTaskId);
                    if (predTask == null) continue;

                    var predResult = results[dep.PredecessorTaskId];
                    var constrainedStart = CalculateConstrainedStart(predResult, predTask, dep);

                    if (constrainedStart > result.EarlyStart)
                    {
                        result.EarlyStart = constrainedStart;
                    }
                }
            }

            // Calculate early finish
            if (task.IsMilestone)
            {
                result.EarlyFinish = result.EarlyStart;
            }
            else
            {
                result.EarlyFinish = _calendar.AddWorkingDays(result.EarlyStart, task.DurationDays);
            }

            results[task.Id] = result;
        }
    }

    /// <summary>
    /// Backward pass: Calculate late start and late finish for all tasks
    /// </summary>
    private void BackwardPass(ProjectChart project, List<ProjectTask> sortedTasks, Dictionary<int, CpmTaskResult> results)
    {
        // Find project end date (latest early finish)
        var projectEnd = results.Values.Max(r => r.EarlyFinish);

        // Process tasks in reverse order
        for (int i = sortedTasks.Count - 1; i >= 0; i--)
        {
            var task = sortedTasks[i];
            var result = results[task.Id];

            // Find successors
            var successors = project.GetSuccessors(task.Id).ToList();
            if (successors.Count == 0)
            {
                // No successors - late finish is project end
                result.LateFinish = projectEnd;
            }
            else
            {
                result.LateFinish = projectEnd;
                foreach (var dep in successors)
                {
                    var succTask = project.GetTask(dep.SuccessorTaskId);
                    if (succTask == null) continue;

                    var succResult = results[dep.SuccessorTaskId];
                    var constrainedFinish = CalculateConstrainedFinish(succResult, succTask, dep);

                    if (constrainedFinish < result.LateFinish)
                    {
                        result.LateFinish = constrainedFinish;
                    }
                }
            }

            // Calculate late start
            if (task.IsMilestone)
            {
                result.LateStart = result.LateFinish;
            }
            else
            {
                result.LateStart = _calendar.SubtractWorkingDays(result.LateFinish, task.DurationDays);
            }
        }
    }

    /// <summary>
    /// Calculate floats for all tasks
    /// </summary>
    private void CalculateFloats(ProjectChart project, Dictionary<int, CpmTaskResult> results)
    {
        foreach (var result in results.Values)
        {
            // Total float = Late Start - Early Start
            result.TotalFloat = _calendar.CountWorkingDays(result.EarlyStart, result.LateStart) - 1;
            if (result.EarlyStart > result.LateStart) result.TotalFloat = 0;

            // Free float = min(ES of successors) - EF of this task
            var task = project.GetTask(result.TaskId);
            if (task == null) continue;

            var successors = project.GetSuccessors(task.Id).ToList();
            if (successors.Count == 0)
            {
                result.FreeFloat = result.TotalFloat;
            }
            else
            {
                var minSuccessorEs = successors
                    .Select(d => results[d.SuccessorTaskId].EarlyStart)
                    .Min();
                result.FreeFloat = _calendar.CountWorkingDays(result.EarlyFinish, minSuccessorEs) - 1;
                if (result.FreeFloat < 0) result.FreeFloat = 0;
            }
        }
    }

    /// <summary>
    /// Calculate the constrained start date based on dependency type
    /// </summary>
    private LocalDate CalculateConstrainedStart(CpmTaskResult predResult, ProjectTask predTask, ProjectDependency dep)
    {
        LocalDate baseDate = dep.Type switch
        {
            // FinishToStart: successor starts the day AFTER predecessor finishes
            DependencyType.FinishToStart => predResult.EarlyFinish.PlusDays(1),
            DependencyType.StartToStart => predResult.EarlyStart,
            DependencyType.FinishToFinish => _calendar.SubtractWorkingDays(predResult.EarlyFinish, predTask.DurationDays),
            DependencyType.StartToFinish => _calendar.SubtractWorkingDays(predResult.EarlyStart, predTask.DurationDays),
            _ => predResult.EarlyFinish.PlusDays(1)
        };

        // Apply lag
        if (dep.LagDays > 0)
        {
            baseDate = _calendar.AddWorkingDays(baseDate, dep.LagDays);
        }
        else if (dep.LagDays < 0)
        {
            baseDate = _calendar.SubtractWorkingDays(baseDate, -dep.LagDays);
        }

        return _calendar.GetNextWorkingDay(baseDate);
    }

    /// <summary>
    /// Calculate the constrained finish date for backward pass
    /// </summary>
    private LocalDate CalculateConstrainedFinish(CpmTaskResult succResult, ProjectTask succTask, ProjectDependency dep)
    {
        LocalDate baseDate = dep.Type switch
        {
            // FinishToStart: predecessor must finish the day BEFORE successor starts
            DependencyType.FinishToStart => succResult.LateStart.PlusDays(-1),
            DependencyType.StartToStart => _calendar.AddWorkingDays(succResult.LateStart, succTask.DurationDays),
            DependencyType.FinishToFinish => succResult.LateFinish,
            DependencyType.StartToFinish => _calendar.AddWorkingDays(succResult.LateFinish, succTask.DurationDays),
            _ => succResult.LateStart.PlusDays(-1)
        };

        // Apply lag (reverse direction)
        if (dep.LagDays > 0)
        {
            baseDate = _calendar.SubtractWorkingDays(baseDate, dep.LagDays);
        }
        else if (dep.LagDays < 0)
        {
            baseDate = _calendar.AddWorkingDays(baseDate, -dep.LagDays);
        }

        return _calendar.GetPreviousWorkingDay(baseDate);
    }

    /// <summary>
    /// Gets the critical path (list of task IDs on the critical path)
    /// </summary>
    public List<int> GetCriticalPath(ProjectChart project)
    {
        var results = CalculateCpm(project);
        return results.Values
            .Where(r => r.IsCritical)
            .Select(r => r.TaskId)
            .ToList();
    }

    /// <summary>
    /// Checks if the project has circular dependencies
    /// </summary>
    public bool HasCircularDependency(ProjectChart project)
    {
        var visited = new HashSet<int>();
        var recursionStack = new HashSet<int>();

        foreach (var task in project.Tasks)
        {
            if (HasCycleFromTask(project, task.Id, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCycleFromTask(ProjectChart project, int taskId, HashSet<int> visited, HashSet<int> recursionStack)
    {
        if (recursionStack.Contains(taskId))
        {
            return true; // Found a cycle
        }

        if (visited.Contains(taskId))
        {
            return false; // Already processed, no cycle from here
        }

        visited.Add(taskId);
        recursionStack.Add(taskId);

        foreach (var dep in project.GetSuccessors(taskId))
        {
            if (HasCycleFromTask(project, dep.SuccessorTaskId, visited, recursionStack))
            {
                return true;
            }
        }

        recursionStack.Remove(taskId);
        return false;
    }

    /// <summary>
    /// Topological sort of tasks based on dependencies
    /// </summary>
    public List<ProjectTask> TopologicalSort(ProjectChart project)
    {
        var result = new List<ProjectTask>();
        var visited = new HashSet<int>();
        var tempMark = new HashSet<int>();

        foreach (var task in project.Tasks)
        {
            if (!visited.Contains(task.Id))
            {
                TopologicalVisit(project, task.Id, visited, tempMark, result);
            }
        }

        result.Reverse();
        return result;
    }

    private void TopologicalVisit(ProjectChart project, int taskId, HashSet<int> visited, HashSet<int> tempMark, List<ProjectTask> result)
    {
        if (tempMark.Contains(taskId))
        {
            throw new InvalidOperationException($"Circular dependency detected involving task {taskId}");
        }

        if (!visited.Contains(taskId))
        {
            tempMark.Add(taskId);

            foreach (var dep in project.GetSuccessors(taskId))
            {
                TopologicalVisit(project, dep.SuccessorTaskId, visited, tempMark, result);
            }

            visited.Add(taskId);
            tempMark.Remove(taskId);

            var task = project.GetTask(taskId);
            if (task != null)
            {
                result.Add(task);
            }
        }
    }

    /// <summary>
    /// Schedules all tasks based on their dependencies (auto-schedule)
    /// </summary>
    public void AutoSchedule(ProjectChart project)
    {
        var results = CalculateCpm(project);

        foreach (var task in project.Tasks)
        {
            if (results.TryGetValue(task.Id, out var result))
            {
                task.StartDate = result.EarlyStart;
                task.EndDate = result.EarlyFinish;
            }
        }

        project.RecalculateProjectDates();
    }

    /// <summary>
    /// Updates a single task and cascades changes to dependent tasks
    /// </summary>
    public void UpdateTaskAndCascade(ProjectChart project, int taskId)
    {
        var task = project.GetTask(taskId);
        if (task == null) return;

        // Ensure end date matches duration
        task.SetEndDateFromDuration(task.DurationDays);

        // Get all tasks that depend on this one (directly or indirectly)
        var affectedTasks = GetDependentTasks(project, taskId);

        // Sort affected tasks topologically and update their dates
        var sortedAffected = affectedTasks
            .OrderBy(t => TopologicalSort(project).IndexOf(t))
            .ToList();

        foreach (var affected in sortedAffected)
        {
            var results = CalculateCpm(project);
            if (results.TryGetValue(affected.Id, out var result))
            {
                affected.StartDate = result.EarlyStart;
                affected.SetEndDateFromDuration(affected.DurationDays);
            }
        }

        project.RecalculateProjectDates();
    }

    /// <summary>
    /// Gets all tasks that depend on the given task (directly or indirectly)
    /// </summary>
    private List<ProjectTask> GetDependentTasks(ProjectChart project, int taskId)
    {
        var result = new List<ProjectTask>();
        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        foreach (var dep in project.GetSuccessors(taskId))
        {
            queue.Enqueue(dep.SuccessorTaskId);
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (visited.Contains(currentId)) continue;

            visited.Add(currentId);
            var task = project.GetTask(currentId);
            if (task != null)
            {
                result.Add(task);
            }

            foreach (var dep in project.GetSuccessors(currentId))
            {
                if (!visited.Contains(dep.SuccessorTaskId))
                {
                    queue.Enqueue(dep.SuccessorTaskId);
                }
            }
        }

        return result;
    }
}
