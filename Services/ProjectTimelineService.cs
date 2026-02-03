using NodaTime;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for Project timeline date-to-pixel conversions.
/// Works with Node-based Project tasks (nodes with TemplateId == "project").
/// </summary>
public class ProjectTimelineService
{
    /// <summary>
    /// Width of each day cell in pixels
    /// </summary>
    public double DayWidth { get; set; } = 30;

    /// <summary>
    /// Height of each task row in pixels
    /// </summary>
    public double RowHeight { get; set; } = 36;

    /// <summary>
    /// Height of the timeline header in pixels
    /// </summary>
    public double HeaderHeight { get; set; } = 50;

    /// <summary>
    /// Width of the outline column (expand/collapse +/- buttons)
    /// </summary>
    public double OutlineColumnWidth { get; set; } = 24;

    /// <summary>
    /// Width of the ID/WBS column
    /// </summary>
    public double IdColumnWidth { get; set; } = 44;

    /// <summary>
    /// Left margin for task labels (includes outline + ID columns)
    /// </summary>
    public double LabelWidth { get; set; } = 220;

    /// <summary>
    /// The visible start date of the timeline
    /// </summary>
    public LocalDate ViewStartDate { get; set; } = LocalDate.FromDateTime(DateTime.Today);

    /// <summary>
    /// The visible end date of the timeline
    /// </summary>
    public LocalDate ViewEndDate { get; set; } = LocalDate.FromDateTime(DateTime.Today.AddDays(30));

    /// <summary>
    /// Calendar for working day calculations
    /// </summary>
    public ProjectCalendar Calendar { get; set; } = new();

    /// <summary>
    /// Converts a date to an X pixel position
    /// </summary>
    public double DateToX(LocalDate date)
    {
        var daysDiff = Period.Between(ViewStartDate, date, PeriodUnits.Days).Days;
        return LabelWidth + daysDiff * DayWidth;
    }

    /// <summary>
    /// Converts a DateTime to an X pixel position
    /// </summary>
    public double DateTimeToX(DateTime date)
    {
        return DateToX(LocalDate.FromDateTime(date));
    }

    /// <summary>
    /// Converts an X pixel position to a date
    /// </summary>
    public LocalDate XToDate(double x)
    {
        var adjustedX = x - LabelWidth;
        var days = (int)Math.Round(adjustedX / DayWidth);
        return ViewStartDate.PlusDays(days);
    }

    /// <summary>
    /// Converts an X pixel position to a DateTime
    /// </summary>
    public DateTime XToDateTime(double x)
    {
        var localDate = XToDate(x);
        return localDate.ToDateTimeUnspecified();
    }

    /// <summary>
    /// Converts a task row index to a Y pixel position
    /// </summary>
    public double RowToY(int rowIndex)
    {
        return HeaderHeight + rowIndex * RowHeight;
    }

    /// <summary>
    /// Converts a Y pixel position to a row index
    /// </summary>
    public int YToRow(double y)
    {
        var adjustedY = y - HeaderHeight;
        return (int)(adjustedY / RowHeight);
    }

    // ============================================
    // NODE-BASED METHODS
    // ============================================

    /// <summary>
    /// Gets the X position and width for a Project node's task bar
    /// </summary>
    public (double x, double width) GetNodeBarBounds(Node node)
    {
        if (node.ProjectStartDate == null || node.ProjectEndDate == null)
            return (0, 0);

        var startDate = LocalDate.FromDateTime(node.ProjectStartDate.Value);
        var endDate = LocalDate.FromDateTime(node.ProjectEndDate.Value);

        var x = DateToX(startDate);
        var endX = DateToX(endDate) + DayWidth; // Include the end day
        var width = endX - x;
        return (x, width);
    }

    /// <summary>
    /// Positions a node based on its Project dates and row index.
    /// Updates node.X, node.Y, and node.Width from date data.
    /// </summary>
    public void PositionNodeForTimeline(Node node)
    {
        if (node.ProjectStartDate == null || node.ProjectEndDate == null)
            return;

        var (x, width) = GetNodeBarBounds(node);
        var rowIndex = node.ProjectRowIndex >= 0 ? node.ProjectRowIndex : 0;

        node.X = x;
        node.Y = RowToY(rowIndex) + 6; // 6px padding from row top
        node.Width = Math.Max(width, DayWidth); // Minimum 1 day width
        node.Height = RowHeight - 12; // Leave padding
    }

    /// <summary>
    /// Updates a node's Project dates from its X/Y/Width position.
    /// Call this after dragging/resizing a node.
    /// </summary>
    public void UpdateNodeFromPosition(Node node)
    {
        // Calculate start date from X position
        var startDate = XToDate(node.X);
        node.ProjectStartDate = startDate.ToDateTimeUnspecified();

        // Calculate end date from width
        var endDate = CalculateNewEndDate(startDate, node.Width);
        node.ProjectEndDate = endDate.ToDateTimeUnspecified();

        // Calculate duration
        node.ProjectDurationDays = Period.Between(startDate, endDate, PeriodUnits.Days).Days + 1;

        // Calculate row index from Y position
        node.ProjectRowIndex = YToRow(node.Y + (RowHeight / 2) - 6);
    }

    /// <summary>
    /// Gets the center point of a node's task bar
    /// </summary>
    public (double x, double y) GetNodeBarCenter(Node node)
    {
        var (x, width) = GetNodeBarBounds(node);
        var rowIndex = node.ProjectRowIndex >= 0 ? node.ProjectRowIndex : 0;
        var y = RowToY(rowIndex) + RowHeight / 2;
        return (x + width / 2, y);
    }

    /// <summary>
    /// Gets the right edge connection point of a node's task bar
    /// </summary>
    public (double x, double y) GetNodeBarRightEdge(Node node)
    {
        var (barX, width) = GetNodeBarBounds(node);
        var rowIndex = node.ProjectRowIndex >= 0 ? node.ProjectRowIndex : 0;
        var y = RowToY(rowIndex) + RowHeight / 2;
        return (barX + width, y);
    }

    /// <summary>
    /// Gets the left edge connection point of a node's task bar
    /// </summary>
    public (double x, double y) GetNodeBarLeftEdge(Node node)
    {
        if (node.ProjectStartDate == null)
            return (0, 0);

        var x = DateToX(LocalDate.FromDateTime(node.ProjectStartDate.Value));
        var rowIndex = node.ProjectRowIndex >= 0 ? node.ProjectRowIndex : 0;
        var y = RowToY(rowIndex) + RowHeight / 2;
        return (x, y);
    }

    /// <summary>
    /// Gets the total width of the timeline in pixels
    /// </summary>
    public double GetTotalWidth()
    {
        var days = Period.Between(ViewStartDate, ViewEndDate, PeriodUnits.Days).Days + 1;
        return LabelWidth + days * DayWidth;
    }

    /// <summary>
    /// Gets the total height of the timeline in pixels
    /// </summary>
    public double GetTotalHeight(int taskCount)
    {
        return HeaderHeight + taskCount * RowHeight;
    }

    /// <summary>
    /// Sets the view range to encompass all Project nodes with padding
    /// </summary>
    public void SetViewRangeFromNodes(IEnumerable<Node> projectNodes, int paddingDays = 7)
    {
        var nodesList = projectNodes.Where(n => n.ProjectStartDate != null && n.ProjectEndDate != null).ToList();

        if (nodesList.Count == 0)
        {
            ViewStartDate = LocalDate.FromDateTime(DateTime.Today);
            ViewEndDate = ViewStartDate.PlusDays(30);
            return;
        }

        var minStart = nodesList.Min(n => n.ProjectStartDate!.Value);
        var maxEnd = nodesList.Max(n => n.ProjectEndDate!.Value);

        ViewStartDate = LocalDate.FromDateTime(minStart).PlusDays(-paddingDays);
        ViewEndDate = LocalDate.FromDateTime(maxEnd).PlusDays(paddingDays);
    }

    /// <summary>
    /// Gets day labels for the timeline header
    /// </summary>
    public IEnumerable<(LocalDate date, double x, string label)> GetDayLabels()
    {
        var current = ViewStartDate;
        while (current <= ViewEndDate)
        {
            var x = DateToX(current);
            var label = current.Day.ToString();
            yield return (current, x, label);
            current = current.PlusDays(1);
        }
    }

    /// <summary>
    /// Gets month labels for the timeline header
    /// </summary>
    public IEnumerable<(LocalDate startDate, double x, double width, string label)> GetMonthLabels()
    {
        var current = new LocalDate(ViewStartDate.Year, ViewStartDate.Month, 1);

        while (current <= ViewEndDate)
        {
            var monthStart = current;
            var monthEnd = current.PlusMonths(1).PlusDays(-1);

            // Clamp to visible range
            var visibleStart = monthStart < ViewStartDate ? ViewStartDate : monthStart;
            var visibleEnd = monthEnd > ViewEndDate ? ViewEndDate : monthEnd;

            var x = DateToX(visibleStart);
            var endX = DateToX(visibleEnd) + DayWidth;
            var width = endX - x;

            var label = current.ToString("MMM yyyy", null);
            yield return (visibleStart, x, width, label);

            current = current.PlusMonths(1);
        }
    }

    /// <summary>
    /// Checks if a date falls on a weekend
    /// </summary>
    public bool IsWeekend(LocalDate date)
    {
        return date.DayOfWeek == IsoDayOfWeek.Saturday ||
               date.DayOfWeek == IsoDayOfWeek.Sunday;
    }

    /// <summary>
    /// Gets weekend day positions for shading
    /// </summary>
    public IEnumerable<(double x, double width)> GetWeekendBands()
    {
        var current = ViewStartDate;
        while (current <= ViewEndDate)
        {
            if (IsWeekend(current))
            {
                yield return (DateToX(current), DayWidth);
            }
            current = current.PlusDays(1);
        }
    }

    /// <summary>
    /// Gets Saturday positions for shading
    /// </summary>
    public IEnumerable<(double x, double width)> GetSaturdayBands()
    {
        var current = ViewStartDate;
        while (current <= ViewEndDate)
        {
            if (current.DayOfWeek == IsoDayOfWeek.Saturday)
            {
                yield return (DateToX(current), DayWidth);
            }
            current = current.PlusDays(1);
        }
    }

    /// <summary>
    /// Gets Sunday positions for shading
    /// </summary>
    public IEnumerable<(double x, double width)> GetSundayBands()
    {
        var current = ViewStartDate;
        while (current <= ViewEndDate)
        {
            if (current.DayOfWeek == IsoDayOfWeek.Sunday)
            {
                yield return (DateToX(current), DayWidth);
            }
            current = current.PlusDays(1);
        }
    }

    /// <summary>
    /// Gets today's position for the "today" line
    /// </summary>
    public double? GetTodayX()
    {
        var today = LocalDate.FromDateTime(DateTime.Today);
        if (today >= ViewStartDate && today <= ViewEndDate)
        {
            return DateToX(today) + DayWidth / 2;
        }
        return null;
    }

    /// <summary>
    /// Snaps a date to the nearest working day
    /// </summary>
    public LocalDate SnapToWorkingDay(LocalDate date)
    {
        return Calendar.GetNextWorkingDay(date);
    }

    /// <summary>
    /// Snaps a DateTime to the nearest working day
    /// </summary>
    public DateTime SnapDateTimeToWorkingDay(DateTime date)
    {
        var localDate = LocalDate.FromDateTime(date);
        return SnapToWorkingDay(localDate).ToDateTimeUnspecified();
    }

    /// <summary>
    /// Calculates a new end date when dragging to resize
    /// </summary>
    public LocalDate CalculateNewEndDate(LocalDate startDate, double newWidth)
    {
        var days = (int)Math.Max(1, Math.Round(newWidth / DayWidth));
        return startDate.PlusDays(days - 1);
    }

    /// <summary>
    /// Zooms in (increases day width)
    /// </summary>
    public void ZoomIn()
    {
        DayWidth = Math.Min(100, DayWidth * 1.25);
    }

    /// <summary>
    /// Zooms out (decreases day width)
    /// </summary>
    public void ZoomOut()
    {
        DayWidth = Math.Max(10, DayWidth / 1.25);
    }

    /// <summary>
    /// Sets zoom level to show all Project nodes
    /// </summary>
    public void ZoomToFit(IEnumerable<Node> projectNodes, double availableWidth)
    {
        var nodesList = projectNodes.Where(n => n.ProjectStartDate != null).ToList();
        if (nodesList.Count == 0) return;

        var days = Period.Between(ViewStartDate, ViewEndDate, PeriodUnits.Days).Days + 1;
        var availableForTimeline = availableWidth - LabelWidth;
        DayWidth = Math.Max(10, availableForTimeline / days);
    }

    // ============================================
    // NODE HELPER METHODS
    // ============================================

    /// <summary>
    /// Creates a new Project task node with default properties
    /// </summary>
    public static Node CreateProjectTaskNode(int id, string name, DateTime startDate, int durationDays)
    {
        var endDate = startDate.AddDays(durationDays - 1);
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "project",
            TemplateShapeId = "task",
            ProjectStartDate = startDate,
            ProjectEndDate = endDate,
            ProjectDurationDays = durationDays,
            ProjectPercentComplete = 0,
            Width = 120,
            Height = 24,
            FillColor = "#3b82f6",
            StrokeColor = "#1d4ed8"
        };
    }

    /// <summary>
    /// Creates a new Project milestone node
    /// </summary>
    public static Node CreateProjectMilestoneNode(int id, string name, DateTime date)
    {
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "project",
            TemplateShapeId = "milestone",
            ProjectStartDate = date,
            ProjectEndDate = date,
            ProjectDurationDays = 0,
            ProjectIsMilestone = true,
            Width = 24,
            Height = 24,
            FillColor = "#8b5cf6",
            StrokeColor = "#6d28d9"
        };
    }

    /// <summary>
    /// Creates a new Project summary (group header) node
    /// </summary>
    public static Node CreateProjectSummaryNode(int id, string name, DateTime startDate, DateTime endDate)
    {
        var duration = (endDate - startDate).Days + 1;
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "project",
            TemplateShapeId = "summary",
            ProjectStartDate = startDate,
            ProjectEndDate = endDate,
            ProjectDurationDays = duration,
            IsSuperNode = true,
            Width = 120,
            Height = 16,
            FillColor = "#374151",
            StrokeColor = "#1f2937"
        };
    }

    /// <summary>
    /// Creates a new Project resource node with a specific type
    /// </summary>
    public static Node CreateProjectResourceNode(int id, string name, ProjectResourceType resourceType = ProjectResourceType.Person, string? color = null)
    {
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "project",
            TemplateShapeId = "resource",
            IsProjectResource = true,
            ProjectResourceType = resourceType,
            ProjectAssignedTaskIds = new List<int>(),
            Width = 32,
            Height = 32,
            FillColor = color ?? GetResourceTypeColor(resourceType),
            StrokeColor = GetResourceTypeStrokeColor(resourceType)
        };
    }

    /// <summary>
    /// Gets the icon/symbol for a resource type
    /// </summary>
    public static string GetResourceTypeIcon(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Person => "👤",
        ProjectResourceType.Team => "👥",
        ProjectResourceType.Equipment => "⚙️",
        ProjectResourceType.Vehicle => "🚗",
        ProjectResourceType.Machine => "🏭",
        ProjectResourceType.Tool => "🔧",
        ProjectResourceType.Material => "📦",
        ProjectResourceType.Room => "🏠",
        ProjectResourceType.Computer => "💻",
        ProjectResourceType.Custom => "⭐",
        _ => "👤"
    };

    /// <summary>
    /// Gets the SVG path for a resource type icon (for rendering in SVG)
    /// </summary>
    public static string GetResourceTypeSvgIcon(ProjectResourceType type) => type switch
    {
        // Person - simple head and shoulders
        ProjectResourceType.Person => "M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z",
        // Team - multiple people
        ProjectResourceType.Team => "M16,13C15.71,13 15.38,13 15.03,13.05C16.19,13.89 17,15 17,16.5V19H23V16.5C23,14.17 18.33,13 16,13M8,13C5.67,13 1,14.17 1,16.5V19H15V16.5C15,14.17 10.33,13 8,13M8,11A3,3 0 0,0 11,8A3,3 0 0,0 8,5A3,3 0 0,0 5,8A3,3 0 0,0 8,11M16,11A3,3 0 0,0 19,8A3,3 0 0,0 16,5A3,3 0 0,0 13,8A3,3 0 0,0 16,11Z",
        // Equipment - gear
        ProjectResourceType.Equipment => "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z",
        // Vehicle - car
        ProjectResourceType.Vehicle => "M5,11L6.5,6.5H17.5L19,11M17.5,16A1.5,1.5 0 0,1 16,14.5A1.5,1.5 0 0,1 17.5,13A1.5,1.5 0 0,1 19,14.5A1.5,1.5 0 0,1 17.5,16M6.5,16A1.5,1.5 0 0,1 5,14.5A1.5,1.5 0 0,1 6.5,13A1.5,1.5 0 0,1 8,14.5A1.5,1.5 0 0,1 6.5,16M18.92,6C18.72,5.42 18.16,5 17.5,5H6.5C5.84,5 5.28,5.42 5.08,6L3,12V20A1,1 0 0,0 4,21H5A1,1 0 0,0 6,20V19H18V20A1,1 0 0,0 19,21H20A1,1 0 0,0 21,20V12L18.92,6Z",
        // Machine - factory/industrial
        ProjectResourceType.Machine => "M4,18V20H8V18H4M4,14V16H14V14H4M10,18V20H14V18H10M16,14V16H20V14H16M16,18V20H20V18H16M2,22V8L7,12V8L12,12V8L17,12H20A2,2 0 0,1 22,14V20A2,2 0 0,1 20,22H2Z",
        // Tool - wrench
        ProjectResourceType.Tool => "M22.7,19L13.6,9.9C14.5,7.6 14,4.9 12.1,3C10.1,1 7.1,0.6 4.7,1.7L9,6L6,9L1.6,4.7C0.4,7.1 0.9,10.1 2.9,12.1C4.8,14 7.5,14.5 9.8,13.6L18.9,22.7C19.3,23.1 19.9,23.1 20.3,22.7L22.6,20.4C23.1,20 23.1,19.3 22.7,19Z",
        // Material - box
        ProjectResourceType.Material => "M5,4H19A2,2 0 0,1 21,6V18A2,2 0 0,1 19,20H5A2,2 0 0,1 3,18V6A2,2 0 0,1 5,4M5,6V18H19V6H5M7,8H17V10H7V8M7,12H17V14H7V12Z",
        // Room - home/building
        ProjectResourceType.Room => "M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z",
        // Computer - laptop
        ProjectResourceType.Computer => "M4,6H20V16H4M20,18A2,2 0 0,0 22,16V6C22,4.89 21.1,4 20,4H4C2.89,4 2,4.89 2,6V16A2,2 0 0,0 4,18H0V20H24V18H20Z",
        // Custom - star
        ProjectResourceType.Custom => "M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z",
        _ => "M12,4A4,4 0 0,1 16,8A4,4 0 0,1 12,12A4,4 0 0,1 8,8A4,4 0 0,1 12,4M12,14C16.42,14 20,15.79 20,18V20H4V18C4,15.79 7.58,14 12,14Z"
    };

    /// <summary>
    /// Gets the default fill color for a resource type
    /// </summary>
    public static string GetResourceTypeColor(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Person => "#6366f1",     // Indigo
        ProjectResourceType.Team => "#8b5cf6",       // Purple
        ProjectResourceType.Equipment => "#64748b",  // Slate
        ProjectResourceType.Vehicle => "#0ea5e9",    // Sky blue
        ProjectResourceType.Machine => "#f97316",    // Orange
        ProjectResourceType.Tool => "#eab308",       // Yellow
        ProjectResourceType.Material => "#22c55e",   // Green
        ProjectResourceType.Room => "#ec4899",       // Pink
        ProjectResourceType.Computer => "#06b6d4",   // Cyan
        ProjectResourceType.Custom => "#a855f7",     // Violet
        _ => "#6366f1"
    };

    /// <summary>
    /// Gets the stroke color for a resource type
    /// </summary>
    public static string GetResourceTypeStrokeColor(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Person => "#4f46e5",
        ProjectResourceType.Team => "#7c3aed",
        ProjectResourceType.Equipment => "#475569",
        ProjectResourceType.Vehicle => "#0284c7",
        ProjectResourceType.Machine => "#ea580c",
        ProjectResourceType.Tool => "#ca8a04",
        ProjectResourceType.Material => "#16a34a",
        ProjectResourceType.Room => "#db2777",
        ProjectResourceType.Computer => "#0891b2",
        ProjectResourceType.Custom => "#9333ea",
        _ => "#4f46e5"
    };

    /// <summary>
    /// Gets a display name for a resource type
    /// </summary>
    public static string GetResourceTypeName(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Person => "Person",
        ProjectResourceType.Team => "Team",
        ProjectResourceType.Equipment => "Equipment",
        ProjectResourceType.Vehicle => "Vehicle",
        ProjectResourceType.Machine => "Machine",
        ProjectResourceType.Tool => "Tool",
        ProjectResourceType.Material => "Material",
        ProjectResourceType.Room => "Room/Space",
        ProjectResourceType.Computer => "Computer/IT",
        ProjectResourceType.Custom => "Custom",
        _ => "Resource"
    };

    /// <summary>
    /// Sets a node's end date from its start date and duration
    /// </summary>
    public static void SetNodeEndDateFromDuration(Node node)
    {
        if (node.ProjectStartDate == null) return;

        var durationDays = Math.Max(1, node.ProjectDurationDays);
        node.ProjectEndDate = node.ProjectStartDate.Value.AddDays(durationDays - 1);
    }

    /// <summary>
    /// Sets a node's duration from its start and end dates
    /// </summary>
    public static void SetNodeDurationFromDates(Node node)
    {
        if (node.ProjectStartDate == null || node.ProjectEndDate == null) return;

        node.ProjectDurationDays = (int)(node.ProjectEndDate.Value - node.ProjectStartDate.Value).TotalDays + 1;
    }

    /// <summary>
    /// Gets dependency connection point for a node based on dependency type
    /// </summary>
    public (double x, double y) GetNodeDependencyPoint(Node node, bool isStart, ProjectDependencyType depType)
    {
        return depType switch
        {
            ProjectDependencyType.FinishToStart when !isStart => GetNodeBarRightEdge(node),
            ProjectDependencyType.FinishToStart when isStart => GetNodeBarLeftEdge(node),
            ProjectDependencyType.StartToStart => GetNodeBarLeftEdge(node),
            ProjectDependencyType.FinishToFinish => GetNodeBarRightEdge(node),
            ProjectDependencyType.StartToFinish when !isStart => GetNodeBarLeftEdge(node),
            ProjectDependencyType.StartToFinish when isStart => GetNodeBarRightEdge(node),
            _ => GetNodeBarRightEdge(node)
        };
    }
}
