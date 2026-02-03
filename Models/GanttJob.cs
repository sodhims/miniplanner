namespace dfd2wasm.Models;

/// <summary>
/// Represents a job (order/batch) in a Gantt machine scheduling chart.
/// A job contains multiple tasks that must be processed, often with precedence constraints.
/// Each job has a unique color that all its tasks inherit.
/// </summary>
public class GanttJob
{
    public int Id { get; set; }
    public string Name { get; set; } = "Job";
    public string? Description { get; set; }

    /// <summary>
    /// Unique color for this job. All tasks in this job display this color.
    /// </summary>
    public string Color { get; set; } = "#3b82f6";

    /// <summary>
    /// Stroke/border color (typically darker than fill).
    /// </summary>
    public string StrokeColor { get; set; } = "#1d4ed8";

    /// <summary>
    /// IDs of tasks belonging to this job.
    /// </summary>
    public List<int> TaskIds { get; set; } = new();

    /// <summary>
    /// Priority for scheduling (higher = more important, processed first in priority-based rules).
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Optional due time (deadline) for the entire job.
    /// </summary>
    public TimeSpan? DueTime { get; set; }

    /// <summary>
    /// Release time - earliest time the job can start processing.
    /// </summary>
    public TimeSpan ReleaseTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Weight factor for weighted scheduling algorithms (e.g., weighted shortest job).
    /// </summary>
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// Customer or order reference.
    /// </summary>
    public string? CustomerReference { get; set; }

    /// <summary>
    /// Calculated completion time (end time of the last task in this job).
    /// </summary>
    public TimeSpan? CompletionTime { get; set; }

    /// <summary>
    /// Calculated flow time (CompletionTime - ReleaseTime).
    /// </summary>
    public TimeSpan? FlowTime => CompletionTime.HasValue ? CompletionTime.Value - ReleaseTime : null;

    /// <summary>
    /// Tardiness (max of 0 and CompletionTime - DueTime).
    /// </summary>
    public TimeSpan? Tardiness
    {
        get
        {
            if (!CompletionTime.HasValue || !DueTime.HasValue) return null;
            var diff = CompletionTime.Value - DueTime.Value;
            return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Whether the job is late (CompletionTime > DueTime).
    /// </summary>
    public bool IsLate => Tardiness.HasValue && Tardiness.Value > TimeSpan.Zero;

    /// <summary>
    /// Adds a task ID to this job.
    /// </summary>
    public void AddTask(int taskId)
    {
        if (!TaskIds.Contains(taskId))
        {
            TaskIds.Add(taskId);
        }
    }

    /// <summary>
    /// Removes a task ID from this job.
    /// </summary>
    public void RemoveTask(int taskId)
    {
        TaskIds.Remove(taskId);
    }
}
