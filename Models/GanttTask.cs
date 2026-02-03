namespace dfd2wasm.Models;

/// <summary>
/// Represents a task/operation in a Gantt machine scheduling chart.
/// Tasks are assigned to machines and belong to jobs.
/// </summary>
public class GanttTask
{
    public int Id { get; set; }
    public string Name { get; set; } = "Task";
    public string? Description { get; set; }

    /// <summary>
    /// The job this task belongs to. All tasks in a job share the same color.
    /// </summary>
    public int JobId { get; set; }

    /// <summary>
    /// The machine that processes this task.
    /// </summary>
    public int MachineId { get; set; }

    /// <summary>
    /// Start time from the beginning of the schedule (e.g., 0:00 = start, 2:30 = 2.5 hours in).
    /// </summary>
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Duration of the task (processing time).
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Calculated end time (StartTime + Duration).
    /// </summary>
    public TimeSpan EndTime => StartTime + Duration;

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Processing time in minutes (for scheduling algorithms like SPT).
    /// </summary>
    public double ProcessingTimeMinutes => Duration.TotalMinutes;

    /// <summary>
    /// Setup time before processing can begin (optional).
    /// </summary>
    public TimeSpan SetupTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// True if this task has a precedence violation (starts before predecessor ends).
    /// </summary>
    public bool IsViolation { get; set; }

    /// <summary>
    /// True if this task is on the critical path.
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Priority for scheduling (higher = more important).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Row index in the timeline view (typically based on machine).
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Sets the end time by adjusting duration while keeping start time fixed.
    /// </summary>
    public void SetEndTime(TimeSpan endTime)
    {
        if (endTime > StartTime)
        {
            Duration = endTime - StartTime;
        }
    }

    /// <summary>
    /// Sets the start time by adjusting while keeping duration fixed.
    /// </summary>
    public void SetStartTime(TimeSpan startTime)
    {
        StartTime = startTime >= TimeSpan.Zero ? startTime : TimeSpan.Zero;
    }

    /// <summary>
    /// Shifts the task by a time offset (positive = later, negative = earlier).
    /// </summary>
    public void ShiftBy(TimeSpan offset)
    {
        var newStart = StartTime + offset;
        StartTime = newStart >= TimeSpan.Zero ? newStart : TimeSpan.Zero;
    }
}
