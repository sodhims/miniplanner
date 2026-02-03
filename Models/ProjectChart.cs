using NodaTime;

namespace dfd2wasm.Models;

/// <summary>
/// Container for a complete Project chart
/// </summary>
public class ProjectChart
{
    public int Id { get; set; }
    public string Name { get; set; } = "New Project";
    public string? Description { get; set; }

    /// <summary>
    /// Project start date
    /// </summary>
    public LocalDate StartDate { get; set; }

    /// <summary>
    /// Project end date (calculated from tasks)
    /// </summary>
    public LocalDate EndDate { get; set; }

    /// <summary>
    /// All tasks in the project
    /// </summary>
    public List<ProjectTask> Tasks { get; set; } = new();

    /// <summary>
    /// All dependencies between tasks
    /// </summary>
    public List<ProjectDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Calendar defining working days
    /// </summary>
    public ProjectCalendar Calendar { get; set; } = new();

    /// <summary>
    /// Next available task ID
    /// </summary>
    private int _nextTaskId = 1;

    /// <summary>
    /// Gets or sets the next task ID (used for import)
    /// </summary>
    public int NextTaskId
    {
        get => _nextTaskId;
        set => _nextTaskId = value;
    }

    /// <summary>
    /// Next available dependency ID
    /// </summary>
    private int _nextDependencyId = 1;

    /// <summary>
    /// Adds a new task to the project
    /// </summary>
    public ProjectTask AddTask(string name, LocalDate startDate, int durationDays)
    {
        var task = new ProjectTask
        {
            Id = _nextTaskId++,
            Name = name,
            StartDate = startDate,
            DurationDays = durationDays
        };
        task.SetEndDateFromDuration(durationDays);
        Tasks.Add(task);
        RecalculateProjectDates();
        return task;
    }

    /// <summary>
    /// Adds a milestone to the project
    /// </summary>
    public ProjectTask AddMilestone(string name, LocalDate date)
    {
        var task = new ProjectTask
        {
            Id = _nextTaskId++,
            Name = name,
            StartDate = date,
            EndDate = date,
            DurationDays = 0,
            IsMilestone = true
        };
        Tasks.Add(task);
        RecalculateProjectDates();
        return task;
    }

    /// <summary>
    /// Adds a dependency between tasks
    /// </summary>
    public ProjectDependency AddDependency(int predecessorId, int successorId,
        DependencyType type = DependencyType.FinishToStart, int lagDays = 0)
    {
        var dependency = new ProjectDependency
        {
            Id = _nextDependencyId++,
            PredecessorTaskId = predecessorId,
            SuccessorTaskId = successorId,
            Type = type,
            LagDays = lagDays
        };
        Dependencies.Add(dependency);
        return dependency;
    }

    /// <summary>
    /// Gets all dependencies where the specified task is the successor
    /// </summary>
    public IEnumerable<ProjectDependency> GetPredecessors(int taskId)
    {
        return Dependencies.Where(d => d.SuccessorTaskId == taskId);
    }

    /// <summary>
    /// Gets all dependencies where the specified task is the predecessor
    /// </summary>
    public IEnumerable<ProjectDependency> GetSuccessors(int taskId)
    {
        return Dependencies.Where(d => d.PredecessorTaskId == taskId);
    }

    /// <summary>
    /// Gets a task by ID
    /// </summary>
    public ProjectTask? GetTask(int taskId)
    {
        return Tasks.FirstOrDefault(t => t.Id == taskId);
    }

    /// <summary>
    /// Removes a task and its related dependencies
    /// </summary>
    public void RemoveTask(int taskId)
    {
        Tasks.RemoveAll(t => t.Id == taskId);
        Dependencies.RemoveAll(d => d.PredecessorTaskId == taskId || d.SuccessorTaskId == taskId);
        RecalculateProjectDates();
    }

    /// <summary>
    /// Recalculates project start and end dates based on tasks
    /// </summary>
    public void RecalculateProjectDates()
    {
        if (Tasks.Count == 0) return;

        StartDate = Tasks.Min(t => t.StartDate);
        EndDate = Tasks.Max(t => t.EndDate);
    }

    /// <summary>
    /// Gets tasks sorted by start date
    /// </summary>
    public IEnumerable<ProjectTask> GetTasksInOrder()
    {
        return Tasks.OrderBy(t => t.StartDate).ThenBy(t => t.Id);
    }

    /// <summary>
    /// Gets root-level tasks (no parent)
    /// </summary>
    public IEnumerable<ProjectTask> GetRootTasks()
    {
        return Tasks.Where(t => t.ParentTaskId == null);
    }

    /// <summary>
    /// Gets child tasks of a parent task
    /// </summary>
    public IEnumerable<ProjectTask> GetChildTasks(int parentId)
    {
        return Tasks.Where(t => t.ParentTaskId == parentId);
    }
}
