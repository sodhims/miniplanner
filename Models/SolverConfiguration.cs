namespace dfd2wasm.Models;

/// <summary>
/// Configuration for the Project chart solver optimization
/// </summary>
public class SolverConfiguration
{
    // === GOALS (Hard Constraints) ===

    /// <summary>
    /// Milestones must be completed on or before their target dates
    /// </summary>
    public bool GoalMeetMilestones { get; set; } = true;

    /// <summary>
    /// Project must complete by a specific deadline
    /// </summary>
    public bool GoalMeetDeadline { get; set; } = false;

    /// <summary>
    /// The deadline date (used when GoalMeetDeadline is true)
    /// </summary>
    public DateTime? DeadlineDate { get; set; }

    /// <summary>
    /// All dependency relationships must be respected (predecessor before successor)
    /// </summary>
    public bool GoalRespectDependencies { get; set; } = true;

    /// <summary>
    /// No resource can be assigned to overlapping tasks
    /// </summary>
    public bool GoalNoOverallocation { get; set; } = false;

    /// <summary>
    /// Tasks marked as fixed/locked cannot be moved
    /// </summary>
    public bool GoalFixedTasksLocked { get; set; } = true;

    /// <summary>
    /// Completed tasks (100%) cannot be moved
    /// </summary>
    public bool GoalCompletedTasksLocked { get; set; } = true;

    // === COSTS (Soft Objectives to Minimize) ===

    /// <summary>
    /// Minimize total project cost (sum of duration * cost/day)
    /// </summary>
    public bool CostMinimizeTotalCost { get; set; } = true;

    /// <summary>
    /// Weight for total cost objective (1-10)
    /// </summary>
    public int CostTotalCostWeight { get; set; } = 5;

    /// <summary>
    /// Minimize overall project duration
    /// </summary>
    public bool CostMinimizeDuration { get; set; } = false;

    /// <summary>
    /// Weight for duration objective (1-10)
    /// </summary>
    public int CostDurationWeight { get; set; } = 5;

    /// <summary>
    /// Minimize resource conflicts (overlapping assignments)
    /// </summary>
    public bool CostMinimizeResourceConflicts { get; set; } = false;

    /// <summary>
    /// Weight for resource conflicts objective (1-10)
    /// </summary>
    public int CostResourceConflictsWeight { get; set; } = 5;

    /// <summary>
    /// Minimize milestone slippage (days past target)
    /// </summary>
    public bool CostMinimizeMilestoneSlippage { get; set; } = false;

    /// <summary>
    /// Weight for milestone slippage objective (1-10)
    /// </summary>
    public int CostMilestoneSlippageWeight { get; set; } = 5;

    /// <summary>
    /// Minimize critical path length
    /// </summary>
    public bool CostMinimizeCriticalPath { get; set; } = false;

    /// <summary>
    /// Weight for critical path objective (1-10)
    /// </summary>
    public int CostCriticalPathWeight { get; set; } = 5;

    /// <summary>
    /// Minimize peak daily cost (level the cost curve)
    /// </summary>
    public bool CostMinimizePeakDailyCost { get; set; } = false;

    /// <summary>
    /// Weight for peak daily cost objective (1-10)
    /// </summary>
    public int CostPeakDailyCostWeight { get; set; } = 5;

    /// <summary>
    /// Minimize idle time between tasks for resources
    /// </summary>
    public bool CostMinimizeIdleTime { get; set; } = false;

    /// <summary>
    /// Weight for idle time objective (1-10)
    /// </summary>
    public int CostIdleTimeWeight { get; set; } = 5;

    // === TASK COMPRESSION OPTIONS ===

    /// <summary>
    /// Allow task duration compression (crashing) to meet deadlines
    /// </summary>
    public bool AllowTaskCompression { get; set; } = false;

    /// <summary>
    /// Cost model for task compression
    /// </summary>
    public CompressionCostModel CompressionCostModel { get; set; } = CompressionCostModel.Linear;

    /// <summary>
    /// Minimize the cost of compressing tasks
    /// </summary>
    public bool CostMinimizeCompressionCost { get; set; } = true;

    /// <summary>
    /// Weight for compression cost objective (1-10)
    /// </summary>
    public int CostCompressionCostWeight { get; set; } = 5;
}

/// <summary>
/// Cost model for task compression/crashing
/// </summary>
public enum CompressionCostModel
{
    /// <summary>
    /// Fixed cost per day reduced: cost = crash_cost_per_day * days_reduced
    /// </summary>
    Linear,

    /// <summary>
    /// Quadratic cost (diminishing returns): cost = crash_cost_per_day * days_reduced^2
    /// Each additional day of compression costs more than the previous
    /// </summary>
    Quadratic
}

/// <summary>
/// Result of running the solver
/// </summary>
public class SolverResult
{
    /// <summary>
    /// Whether the solver found a solution
    /// </summary>
    public SolverStatus Status { get; set; } = SolverStatus.NotRun;

    /// <summary>
    /// Human-readable status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to solve in milliseconds
    /// </summary>
    public long SolveTimeMs { get; set; }

    /// <summary>
    /// List of constraint violations (for infeasible solutions)
    /// </summary>
    public List<ConstraintViolation> Violations { get; set; } = new();

    /// <summary>
    /// Suggested fixes for infeasibility
    /// </summary>
    public List<string> SuggestedFixes { get; set; } = new();

    /// <summary>
    /// Metrics before optimization
    /// </summary>
    public SolverMetrics? BeforeMetrics { get; set; }

    /// <summary>
    /// Metrics after optimization
    /// </summary>
    public SolverMetrics? AfterMetrics { get; set; }

    /// <summary>
    /// The optimized task schedule (node ID -> new start date)
    /// </summary>
    public Dictionary<int, DateTime> OptimizedSchedule { get; set; } = new();

    /// <summary>
    /// Optimized task durations (node ID -> new duration in days)
    /// Only contains entries for tasks that were compressed
    /// </summary>
    public Dictionary<int, int> OptimizedDurations { get; set; } = new();

    /// <summary>
    /// Total compression cost incurred
    /// </summary>
    public decimal TotalCompressionCost { get; set; } = 0;
}

public enum SolverStatus
{
    NotRun,
    Optimal,
    Feasible,
    Infeasible,
    Timeout,
    Error
}

/// <summary>
/// Describes a constraint violation
/// </summary>
public class ConstraintViolation
{
    public string ConstraintName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<int> AffectedTaskIds { get; set; } = new();
    public ViolationSeverity Severity { get; set; } = ViolationSeverity.Error;
}

public enum ViolationSeverity
{
    Warning,
    Error
}

/// <summary>
/// Metrics for comparing before/after optimization
/// </summary>
public class SolverMetrics
{
    public decimal TotalProjectCost { get; set; }
    public int ProjectDurationDays { get; set; }
    public DateTime ProjectStartDate { get; set; }
    public DateTime ProjectEndDate { get; set; }
    public int ResourceConflictCount { get; set; }
    public int MilestoneSlippageDays { get; set; }
    public int CriticalPathLength { get; set; }
    public decimal PeakDailyCost { get; set; }
    public int TotalIdleDays { get; set; }
}
