using NodaTime;

namespace dfd2wasm.Models;

/// <summary>
/// Represents a task in a Project chart
/// </summary>
public class ProjectTask
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Start date of the task (using NodaTime for proper date handling)
    /// </summary>
    public LocalDate StartDate { get; set; }

    /// <summary>
    /// End date of the task (inclusive)
    /// </summary>
    public LocalDate EndDate { get; set; }

    /// <summary>
    /// Duration in working days
    /// </summary>
    public int DurationDays { get; set; }

    /// <summary>
    /// Percentage complete (0-100)
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Resource assigned to this task
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Parent task ID for hierarchical task structures
    /// </summary>
    public int? ParentTaskId { get; set; }

    /// <summary>
    /// Whether this is a milestone (zero-duration marker)
    /// </summary>
    public bool IsMilestone { get; set; }

    /// <summary>
    /// Priority level (higher = more important)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Color for display in the Project chart
    /// </summary>
    public string Color { get; set; } = "#3b82f6";

    /// <summary>
    /// Notes or comments about the task
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Calculates duration based on start and end dates
    /// </summary>
    public int CalculateDurationDays()
    {
        return Period.Between(StartDate, EndDate, PeriodUnits.Days).Days + 1;
    }

    /// <summary>
    /// Sets end date based on start date and duration
    /// </summary>
    public void SetEndDateFromDuration(int durationDays)
    {
        DurationDays = durationDays;
        EndDate = StartDate.PlusDays(durationDays - 1);
    }

    /// <summary>
    /// Sets duration based on start and end dates
    /// </summary>
    public void SetDurationFromDates()
    {
        DurationDays = CalculateDurationDays();
    }
}
