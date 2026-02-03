namespace dfd2wasm.Models;

/// <summary>
/// Types of task dependencies in a Project chart
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// Finish-to-Start: Successor starts after predecessor finishes
    /// </summary>
    FinishToStart,

    /// <summary>
    /// Start-to-Start: Successor starts when predecessor starts
    /// </summary>
    StartToStart,

    /// <summary>
    /// Finish-to-Finish: Successor finishes when predecessor finishes
    /// </summary>
    FinishToFinish,

    /// <summary>
    /// Start-to-Finish: Successor finishes when predecessor starts
    /// </summary>
    StartToFinish
}

/// <summary>
/// Represents a dependency between two Project tasks
/// </summary>
public class ProjectDependency
{
    public int Id { get; set; }

    /// <summary>
    /// The predecessor task ID (the task that must happen first)
    /// </summary>
    public int PredecessorTaskId { get; set; }

    /// <summary>
    /// The successor task ID (the task that depends on the predecessor)
    /// </summary>
    public int SuccessorTaskId { get; set; }

    /// <summary>
    /// Type of dependency relationship
    /// </summary>
    public DependencyType Type { get; set; } = DependencyType.FinishToStart;

    /// <summary>
    /// Lag time in days (positive = delay, negative = overlap)
    /// </summary>
    public int LagDays { get; set; }

    /// <summary>
    /// Gets the short code for the dependency type
    /// </summary>
    public string TypeCode => Type switch
    {
        DependencyType.FinishToStart => "FS",
        DependencyType.StartToStart => "SS",
        DependencyType.FinishToFinish => "FF",
        DependencyType.StartToFinish => "SF",
        _ => "FS"
    };
}
