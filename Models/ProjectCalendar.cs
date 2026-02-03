using NodaTime;

namespace dfd2wasm.Models;

/// <summary>
/// Calendar for Project chart working day calculations
/// </summary>
public class ProjectCalendar
{
    /// <summary>
    /// Days of the week that are working days
    /// </summary>
    public HashSet<IsoDayOfWeek> WorkingDays { get; set; } = new()
    {
        IsoDayOfWeek.Monday,
        IsoDayOfWeek.Tuesday,
        IsoDayOfWeek.Wednesday,
        IsoDayOfWeek.Thursday,
        IsoDayOfWeek.Friday
    };

    /// <summary>
    /// Specific dates that are holidays (non-working)
    /// </summary>
    public HashSet<LocalDate> Holidays { get; set; } = new();

    /// <summary>
    /// Specific dates that are working days (overrides weekends)
    /// </summary>
    public HashSet<LocalDate> ExtraWorkingDays { get; set; } = new();

    /// <summary>
    /// Checks if a specific date is a working day
    /// </summary>
    public bool IsWorkingDay(LocalDate date)
    {
        // Check if it's an extra working day (overrides everything)
        if (ExtraWorkingDays.Contains(date))
            return true;

        // Check if it's a holiday
        if (Holidays.Contains(date))
            return false;

        // Check if it's a normal working day of the week
        return WorkingDays.Contains(date.DayOfWeek);
    }

    /// <summary>
    /// Calculates the end date given a start date and working day duration
    /// </summary>
    public LocalDate AddWorkingDays(LocalDate startDate, int workingDays)
    {
        if (workingDays <= 0)
            return startDate;

        var currentDate = startDate;
        var daysAdded = 0;

        // First, ensure we start on a working day
        while (!IsWorkingDay(currentDate))
        {
            currentDate = currentDate.PlusDays(1);
        }

        // Count working days (we count the start day as day 1)
        daysAdded = 1;

        while (daysAdded < workingDays)
        {
            currentDate = currentDate.PlusDays(1);
            if (IsWorkingDay(currentDate))
            {
                daysAdded++;
            }
        }

        return currentDate;
    }

    /// <summary>
    /// Calculates the number of working days between two dates (inclusive)
    /// </summary>
    public int CountWorkingDays(LocalDate startDate, LocalDate endDate)
    {
        if (endDate < startDate)
            return 0;

        var count = 0;
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            if (IsWorkingDay(currentDate))
            {
                count++;
            }
            currentDate = currentDate.PlusDays(1);
        }

        return count;
    }

    /// <summary>
    /// Gets the next working day on or after the given date
    /// </summary>
    public LocalDate GetNextWorkingDay(LocalDate date)
    {
        while (!IsWorkingDay(date))
        {
            date = date.PlusDays(1);
        }
        return date;
    }

    /// <summary>
    /// Gets the previous working day on or before the given date
    /// </summary>
    public LocalDate GetPreviousWorkingDay(LocalDate date)
    {
        while (!IsWorkingDay(date))
        {
            date = date.PlusDays(-1);
        }
        return date;
    }

    /// <summary>
    /// Subtracts working days from a date
    /// </summary>
    public LocalDate SubtractWorkingDays(LocalDate startDate, int workingDays)
    {
        if (workingDays <= 0)
            return startDate;

        var currentDate = startDate;
        var daysSubtracted = 0;

        // Ensure we start on a working day
        while (!IsWorkingDay(currentDate))
        {
            currentDate = currentDate.PlusDays(-1);
        }

        // Count working days backward
        daysSubtracted = 1;

        while (daysSubtracted < workingDays)
        {
            currentDate = currentDate.PlusDays(-1);
            if (IsWorkingDay(currentDate))
            {
                daysSubtracted++;
            }
        }

        return currentDate;
    }

    /// <summary>
    /// Adds a holiday to the calendar
    /// </summary>
    public void AddHoliday(LocalDate date)
    {
        Holidays.Add(date);
    }

    /// <summary>
    /// Adds a range of holidays
    /// </summary>
    public void AddHolidayRange(LocalDate startDate, LocalDate endDate)
    {
        var current = startDate;
        while (current <= endDate)
        {
            Holidays.Add(current);
            current = current.PlusDays(1);
        }
    }

    /// <summary>
    /// Creates a calendar with weekends only (no holidays)
    /// </summary>
    public static ProjectCalendar CreateStandardCalendar()
    {
        return new ProjectCalendar();
    }

    /// <summary>
    /// Creates a calendar where all days are working days
    /// </summary>
    public static ProjectCalendar CreateContinuousCalendar()
    {
        return new ProjectCalendar
        {
            WorkingDays = new HashSet<IsoDayOfWeek>
            {
                IsoDayOfWeek.Monday,
                IsoDayOfWeek.Tuesday,
                IsoDayOfWeek.Wednesday,
                IsoDayOfWeek.Thursday,
                IsoDayOfWeek.Friday,
                IsoDayOfWeek.Saturday,
                IsoDayOfWeek.Sunday
            }
        };
    }
}
