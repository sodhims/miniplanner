namespace dfd2wasm.Models;

/// <summary>
/// Represents a precedence constraint between two tasks in a Gantt machine schedule.
/// The predecessor task must complete before the successor task can start.
/// </summary>
public class GanttPrecedence
{
    public int Id { get; set; }

    /// <summary>
    /// The task that must finish first.
    /// </summary>
    public int PredecessorTaskId { get; set; }

    /// <summary>
    /// The task that depends on the predecessor.
    /// </summary>
    public int SuccessorTaskId { get; set; }

    /// <summary>
    /// Type of precedence constraint.
    /// </summary>
    public GanttPrecedenceType Type { get; set; } = GanttPrecedenceType.FinishToStart;

    /// <summary>
    /// Lag time (delay) between predecessor completion and successor start.
    /// Positive = delay, Negative = overlap allowed.
    /// </summary>
    public TimeSpan LagTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Whether this precedence is currently violated.
    /// </summary>
    public bool IsViolated { get; set; }

    /// <summary>
    /// Description of the violation (if any).
    /// </summary>
    public string? ViolationMessage { get; set; }

    /// <summary>
    /// Checks if the precedence constraint is satisfied given task times.
    /// </summary>
    public bool IsSatisfied(TimeSpan predecessorEnd, TimeSpan successorStart)
    {
        return Type switch
        {
            GanttPrecedenceType.FinishToStart => successorStart >= predecessorEnd + LagTime,
            GanttPrecedenceType.StartToStart => successorStart >= predecessorEnd - (predecessorEnd - TimeSpan.Zero) + LagTime, // Simplified
            GanttPrecedenceType.FinishToFinish => true, // Handled differently
            GanttPrecedenceType.StartToFinish => true, // Handled differently
            _ => successorStart >= predecessorEnd + LagTime
        };
    }

    /// <summary>
    /// Calculates the earliest valid start time for the successor.
    /// </summary>
    public TimeSpan GetEarliestSuccessorStart(TimeSpan predecessorStart, TimeSpan predecessorEnd)
    {
        return Type switch
        {
            GanttPrecedenceType.FinishToStart => predecessorEnd + LagTime,
            GanttPrecedenceType.StartToStart => predecessorStart + LagTime,
            GanttPrecedenceType.FinishToFinish => TimeSpan.Zero, // Depends on successor duration
            GanttPrecedenceType.StartToFinish => TimeSpan.Zero, // Depends on successor duration
            _ => predecessorEnd + LagTime
        };
    }

    /// <summary>
    /// Calculates how much the successor needs to be shifted to satisfy the constraint.
    /// Returns positive value if shift needed, zero if already satisfied.
    /// </summary>
    public TimeSpan GetRequiredShift(TimeSpan predecessorEnd, TimeSpan successorStart)
    {
        var earliestStart = predecessorEnd + LagTime;
        var shift = earliestStart - successorStart;
        return shift > TimeSpan.Zero ? shift : TimeSpan.Zero;
    }
}

/// <summary>
/// Types of precedence constraints (matches standard scheduling notation).
/// </summary>
public enum GanttPrecedenceType
{
    /// <summary>
    /// Successor starts after predecessor finishes (most common).
    /// </summary>
    FinishToStart,

    /// <summary>
    /// Successor starts when predecessor starts.
    /// </summary>
    StartToStart,

    /// <summary>
    /// Successor finishes when predecessor finishes.
    /// </summary>
    FinishToFinish,

    /// <summary>
    /// Successor finishes when predecessor starts.
    /// </summary>
    StartToFinish
}
