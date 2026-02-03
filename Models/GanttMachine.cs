namespace dfd2wasm.Models;

/// <summary>
/// Represents a machine/resource in a Gantt machine scheduling chart.
/// Each machine has its own row in the timeline view.
/// </summary>
public class GanttMachine
{
    public int Id { get; set; }
    public string Name { get; set; } = "Machine";
    public string? Description { get; set; }

    /// <summary>
    /// IDs of tasks assigned to this machine.
    /// </summary>
    public List<int> TaskIds { get; set; } = new();

    /// <summary>
    /// Row index in the timeline view.
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Machine type for grouping/display.
    /// </summary>
    public GanttMachineType MachineType { get; set; } = GanttMachineType.Machine;

    /// <summary>
    /// Processing speed multiplier (1.0 = normal, 2.0 = twice as fast).
    /// </summary>
    public double SpeedFactor { get; set; } = 1.0;

    /// <summary>
    /// Cost per hour of operation.
    /// </summary>
    public decimal HourlyCost { get; set; }

    /// <summary>
    /// Whether the machine is available for scheduling.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Display color for the machine row background.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Availability windows - times when machine is available.
    /// If empty, machine is available all the time.
    /// </summary>
    public List<GanttTimeWindow> AvailabilityWindows { get; set; } = new();

    /// <summary>
    /// Maintenance/downtime windows - times when machine is unavailable.
    /// </summary>
    public List<GanttTimeWindow> DowntimeWindows { get; set; } = new();

    /// <summary>
    /// Gets the total processing time of all tasks on this machine.
    /// </summary>
    public TimeSpan GetTotalProcessingTime(IEnumerable<GanttTask> allTasks)
    {
        return allTasks
            .Where(t => TaskIds.Contains(t.Id))
            .Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration);
    }

    /// <summary>
    /// Gets the makespan (end time of last task) on this machine.
    /// </summary>
    public TimeSpan GetMakespan(IEnumerable<GanttTask> allTasks)
    {
        var machineTasks = allTasks.Where(t => TaskIds.Contains(t.Id)).ToList();
        return machineTasks.Count > 0 ? machineTasks.Max(t => t.EndTime) : TimeSpan.Zero;
    }

    /// <summary>
    /// Checks if a time slot is available on this machine.
    /// </summary>
    public bool IsTimeSlotAvailable(TimeSpan start, TimeSpan end, IEnumerable<GanttTask> allTasks)
    {
        // Check downtime windows
        foreach (var downtime in DowntimeWindows)
        {
            if (start < downtime.End && end > downtime.Start)
            {
                return false; // Overlaps with downtime
            }
        }

        // Check existing tasks
        var machineTasks = allTasks.Where(t => TaskIds.Contains(t.Id));
        foreach (var task in machineTasks)
        {
            if (start < task.EndTime && end > task.StartTime)
            {
                return false; // Overlaps with existing task
            }
        }

        return true;
    }

    /// <summary>
    /// Adds a task ID to this machine.
    /// </summary>
    public void AddTask(int taskId)
    {
        if (!TaskIds.Contains(taskId))
        {
            TaskIds.Add(taskId);
        }
    }

    /// <summary>
    /// Removes a task ID from this machine.
    /// </summary>
    public void RemoveTask(int taskId)
    {
        TaskIds.Remove(taskId);
    }
}

/// <summary>
/// Types of machines/resources for visual differentiation.
/// </summary>
public enum GanttMachineType
{
    Machine,
    Workstation,
    Robot,
    Conveyor,
    Assembly,
    Inspection,
    Packaging,
    Storage,
    Custom
}

/// <summary>
/// Represents a time window (availability or downtime period).
/// </summary>
public class GanttTimeWindow
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }

    public TimeSpan Duration => End - Start;

    public bool Contains(TimeSpan time) => time >= Start && time < End;

    public bool Overlaps(TimeSpan start, TimeSpan end) => start < End && end > Start;
}
