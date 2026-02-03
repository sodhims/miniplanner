using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for Gantt machine scheduling timeline time-to-pixel conversions.
/// Works with hours/minutes timescale (not days like ProjectTimelineService).
/// </summary>
public class GanttTimelineService
{
    /// <summary>
    /// Width of one minute in pixels (default 2px = 120px per hour)
    /// </summary>
    public double MinuteWidth { get; set; } = 2.0;

    /// <summary>
    /// Height of each machine row in pixels
    /// </summary>
    public double RowHeight { get; set; } = 36;

    /// <summary>
    /// Height of the timeline header in pixels
    /// </summary>
    public double HeaderHeight { get; set; } = 50;

    /// <summary>
    /// Height of the job filter row in pixels (0 to hide)
    /// </summary>
    public double JobFilterRowHeight { get; set; } = 32;

    /// <summary>
    /// Total header height including job filter row
    /// </summary>
    public double TotalHeaderHeight => HeaderHeight + JobFilterRowHeight;

    /// <summary>
    /// Width of the machine name column
    /// </summary>
    public double LabelWidth { get; set; } = 120;

    /// <summary>
    /// The visible start time of the timeline
    /// </summary>
    public TimeSpan ViewStartTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// The visible end time of the timeline
    /// </summary>
    public TimeSpan ViewEndTime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Grid snap interval in minutes (default 5 minutes)
    /// </summary>
    public int SnapMinutes { get; set; } = 5;

    /// <summary>
    /// Width of one hour in pixels (derived from MinuteWidth)
    /// </summary>
    public double HourWidth => MinuteWidth * 60;

    /// <summary>
    /// Converts a TimeSpan to an X pixel position
    /// </summary>
    public double TimeToX(TimeSpan time)
    {
        var minutesDiff = (time - ViewStartTime).TotalMinutes;
        return LabelWidth + minutesDiff * MinuteWidth;
    }

    /// <summary>
    /// Converts an X pixel position to a TimeSpan
    /// </summary>
    public TimeSpan XToTime(double x)
    {
        var adjustedX = x - LabelWidth;
        var minutes = adjustedX / MinuteWidth;
        return ViewStartTime + TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Converts a machine row index to a Y pixel position
    /// </summary>
    public double RowToY(int rowIndex)
    {
        return TotalHeaderHeight + rowIndex * RowHeight;
    }

    /// <summary>
    /// Converts a Y pixel position to a row index
    /// </summary>
    public int YToRow(double y)
    {
        var adjustedY = y - TotalHeaderHeight;
        return (int)(adjustedY / RowHeight);
    }

    /// <summary>
    /// Snaps a time to the nearest snap interval
    /// </summary>
    public TimeSpan SnapToGrid(TimeSpan time)
    {
        var totalMinutes = time.TotalMinutes;
        var snappedMinutes = Math.Round(totalMinutes / SnapMinutes) * SnapMinutes;
        return TimeSpan.FromMinutes(Math.Max(0, snappedMinutes));
    }

    // ============================================
    // NODE-BASED METHODS
    // ============================================

    /// <summary>
    /// Gets the X position and width for a Gantt node's task bar
    /// </summary>
    public (double x, double width) GetNodeBarBounds(Node node)
    {
        if (node.GanttStartTime == null || node.GanttDuration == null)
            return (0, 0);

        var x = TimeToX(node.GanttStartTime.Value);
        var width = node.GanttDuration.Value.TotalMinutes * MinuteWidth;
        return (x, Math.Max(width, MinuteWidth * 5)); // Minimum 5 minutes visible
    }

    /// <summary>
    /// Positions a node based on its Gantt times and row index.
    /// Updates node.X, node.Y, and node.Width from time data.
    /// </summary>
    public void PositionNodeForTimeline(Node node)
    {
        if (node.GanttStartTime == null || node.GanttDuration == null)
            return;

        var (x, width) = GetNodeBarBounds(node);
        var rowIndex = node.GanttRowIndex >= 0 ? node.GanttRowIndex : 0;

        node.X = x;
        node.Y = RowToY(rowIndex) + 6; // 6px padding from row top
        node.Width = width;
        node.Height = RowHeight - 12; // Leave padding
    }

    /// <summary>
    /// Updates a node's Gantt times from its X/Y/Width position.
    /// Call this after dragging/resizing a node.
    /// </summary>
    public void UpdateNodeFromPosition(Node node)
    {
        // Calculate start time from X position
        var startTime = XToTime(node.X);
        startTime = SnapToGrid(startTime);
        node.GanttStartTime = startTime;

        // Calculate duration from width
        var durationMinutes = node.Width / MinuteWidth;
        node.GanttDuration = TimeSpan.FromMinutes(Math.Max(SnapMinutes, durationMinutes));

        // Calculate row index from Y position
        node.GanttRowIndex = YToRow(node.Y + (RowHeight / 2) - 6);
    }

    /// <summary>
    /// Gets the center point of a node's task bar
    /// </summary>
    public (double x, double y) GetNodeBarCenter(Node node)
    {
        var (x, width) = GetNodeBarBounds(node);
        var rowIndex = node.GanttRowIndex >= 0 ? node.GanttRowIndex : 0;
        var y = RowToY(rowIndex) + RowHeight / 2;
        return (x + width / 2, y);
    }

    /// <summary>
    /// Gets the right edge connection point of a node's task bar
    /// </summary>
    public (double x, double y) GetNodeBarRightEdge(Node node)
    {
        var (barX, width) = GetNodeBarBounds(node);
        var rowIndex = node.GanttRowIndex >= 0 ? node.GanttRowIndex : 0;
        var y = RowToY(rowIndex) + RowHeight / 2;
        return (barX + width, y);
    }

    /// <summary>
    /// Gets the left edge connection point of a node's task bar
    /// </summary>
    public (double x, double y) GetNodeBarLeftEdge(Node node)
    {
        if (node.GanttStartTime == null)
            return (0, 0);

        var x = TimeToX(node.GanttStartTime.Value);
        var rowIndex = node.GanttRowIndex >= 0 ? node.GanttRowIndex : 0;
        var y = RowToY(rowIndex) + RowHeight / 2;
        return (x, y);
    }

    /// <summary>
    /// Gets the total width of the timeline in pixels
    /// </summary>
    public double GetTotalWidth()
    {
        var totalMinutes = (ViewEndTime - ViewStartTime).TotalMinutes;
        return LabelWidth + totalMinutes * MinuteWidth;
    }

    /// <summary>
    /// Alias for GetTotalWidth for razor compatibility
    /// </summary>
    public double GetTimelineWidth() => GetTotalWidth();

    /// <summary>
    /// Gets the total height of the timeline in pixels
    /// </summary>
    public double GetTotalHeight(int machineCount)
    {
        return TotalHeaderHeight + machineCount * RowHeight;
    }

    /// <summary>
    /// Alias for GetTotalHeight for razor compatibility
    /// </summary>
    public double GetTimelineHeight(int machineCount) => GetTotalHeight(machineCount);

    /// <summary>
    /// Alias for RowToY for razor compatibility
    /// </summary>
    public double GetRowY(int rowIndex) => RowToY(rowIndex);

    /// <summary>
    /// Converts a duration to pixel width
    /// </summary>
    public double DurationToWidth(TimeSpan duration)
    {
        return duration.TotalMinutes * MinuteWidth;
    }

    /// <summary>
    /// Gets the current time within the timeline (defaults to start for demo)
    /// </summary>
    public TimeSpan CurrentTimeInTimeline => TimeSpan.FromHours(DateTime.Now.Hour) + TimeSpan.FromMinutes(DateTime.Now.Minute);

    /// <summary>
    /// Sets the view range to encompass all Gantt nodes with padding
    /// </summary>
    public void SetViewRangeFromNodes(IEnumerable<Node> ganttNodes, TimeSpan padding)
    {
        var nodesList = ganttNodes.Where(n => n.GanttStartTime != null && n.GanttDuration != null).ToList();

        if (nodesList.Count == 0)
        {
            ViewStartTime = TimeSpan.Zero;
            ViewEndTime = TimeSpan.FromHours(8);
            return;
        }

        var minStart = nodesList.Min(n => n.GanttStartTime!.Value);
        var maxEnd = nodesList.Max(n => n.GanttEndTime!.Value);

        ViewStartTime = minStart - padding;
        if (ViewStartTime < TimeSpan.Zero) ViewStartTime = TimeSpan.Zero;

        ViewEndTime = maxEnd + padding;
    }

    // ============================================
    // TIMELINE HEADER METHODS
    // ============================================

    /// <summary>
    /// Gets hour labels for the timeline header
    /// </summary>
    public IEnumerable<(TimeSpan time, double x, string label)> GetHourLabels()
    {
        // Start at the next whole hour from ViewStartTime
        var startHour = (int)Math.Floor(ViewStartTime.TotalHours);
        var current = TimeSpan.FromHours(startHour);

        while (current <= ViewEndTime)
        {
            if (current >= ViewStartTime)
            {
                var x = TimeToX(current);
                var hours = (int)current.TotalHours;
                var label = $"{hours}:00";
                yield return (current, x, label);
            }
            current = current.Add(TimeSpan.FromHours(1));
        }
    }

    /// <summary>
    /// Gets half-hour labels for the timeline header (when zoomed in)
    /// </summary>
    public IEnumerable<(TimeSpan time, double x, string label)> GetHalfHourLabels()
    {
        var startMinutes = (int)Math.Floor(ViewStartTime.TotalMinutes / 30) * 30;
        var current = TimeSpan.FromMinutes(startMinutes);

        while (current <= ViewEndTime)
        {
            if (current >= ViewStartTime)
            {
                var x = TimeToX(current);
                var hours = (int)current.TotalHours;
                var minutes = current.Minutes;
                var label = $"{hours}:{minutes:D2}";
                yield return (current, x, label);
            }
            current = current.Add(TimeSpan.FromMinutes(30));
        }
    }

    /// <summary>
    /// Gets minute grid lines (15-minute intervals)
    /// </summary>
    public IEnumerable<(double x, bool isMajor)> GetMinuteGridLines()
    {
        var startMinutes = (int)Math.Floor(ViewStartTime.TotalMinutes / 15) * 15;
        var current = TimeSpan.FromMinutes(startMinutes);

        while (current <= ViewEndTime)
        {
            if (current >= ViewStartTime)
            {
                var x = TimeToX(current);
                var isMajor = current.Minutes == 0; // Hour marks are major
                yield return (x, isMajor);
            }
            current = current.Add(TimeSpan.FromMinutes(15));
        }
    }

    /// <summary>
    /// Gets the "now" position for the current time indicator
    /// </summary>
    public double? GetNowX(TimeSpan currentTime)
    {
        if (currentTime >= ViewStartTime && currentTime <= ViewEndTime)
        {
            return TimeToX(currentTime);
        }
        return null;
    }

    // ============================================
    // ZOOM METHODS
    // ============================================

    /// <summary>
    /// Zooms in (increases minute width)
    /// </summary>
    public void ZoomIn()
    {
        MinuteWidth = Math.Min(10, MinuteWidth * 1.25); // Max 10px per minute = 600px per hour
    }

    /// <summary>
    /// Zooms out (decreases minute width)
    /// </summary>
    public void ZoomOut()
    {
        MinuteWidth = Math.Max(0.5, MinuteWidth / 1.25); // Min 0.5px per minute = 30px per hour
    }

    /// <summary>
    /// Sets zoom level to show all tasks
    /// </summary>
    public void ZoomToFit(IEnumerable<Node> ganttNodes, double availableWidth)
    {
        var nodesList = ganttNodes.Where(n => n.GanttStartTime != null).ToList();
        if (nodesList.Count == 0) return;

        var totalMinutes = (ViewEndTime - ViewStartTime).TotalMinutes;
        var availableForTimeline = availableWidth - LabelWidth;
        MinuteWidth = Math.Max(0.5, availableForTimeline / totalMinutes);
    }

    // ============================================
    // NODE FACTORY METHODS
    // ============================================

    /// <summary>
    /// Creates a new Gantt task node with default properties
    /// </summary>
    public static Node CreateGanttTaskNode(int id, string name, TimeSpan startTime, TimeSpan duration, int? jobId = null, int? machineId = null)
    {
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "gantt",
            TemplateShapeId = "task",
            GanttStartTime = startTime,
            GanttDuration = duration,
            IsGanttTask = true,
            GanttJobId = jobId,
            GanttMachineId = machineId,
            GanttRowIndex = machineId ?? 0,
            Width = 120,
            Height = 24,
            FillColor = "#3b82f6",
            StrokeColor = "#1d4ed8"
        };
    }

    /// <summary>
    /// Creates a new Gantt job node
    /// </summary>
    public static Node CreateGanttJobNode(int id, string name, string color)
    {
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "gantt",
            TemplateShapeId = "job",
            IsGanttJob = true,
            GanttJobColor = color,
            Width = 80,
            Height = 40,
            FillColor = color,
            StrokeColor = DarkenColor(color)
        };
    }

    /// <summary>
    /// Creates a new Gantt machine node
    /// </summary>
    public static Node CreateGanttMachineNode(int id, string name, int rowIndex, GanttMachineType machineType = GanttMachineType.Machine)
    {
        return new Node
        {
            Id = id,
            Text = name,
            TemplateId = "gantt",
            TemplateShapeId = "machine",
            IsGanttMachine = true,
            GanttMachineType = machineType,
            GanttRowIndex = rowIndex,
            Width = 100,
            Height = 40,
            FillColor = GetMachineTypeColor(machineType),
            StrokeColor = GetMachineTypeStrokeColor(machineType)
        };
    }

    /// <summary>
    /// Gets the icon for a machine type
    /// </summary>
    public static string GetMachineTypeIcon(GanttMachineType type) => type switch
    {
        GanttMachineType.Machine => "⚙️",
        GanttMachineType.Workstation => "🖥️",
        GanttMachineType.Robot => "🤖",
        GanttMachineType.Conveyor => "➡️",
        GanttMachineType.Assembly => "🔧",
        GanttMachineType.Inspection => "🔍",
        GanttMachineType.Packaging => "📦",
        GanttMachineType.Storage => "🏭",
        GanttMachineType.Custom => "⭐",
        _ => "⚙️"
    };

    /// <summary>
    /// Gets the SVG path for a machine type icon
    /// </summary>
    public static string GetMachineTypeSvgIcon(GanttMachineType type) => type switch
    {
        // Gear icon for generic machine
        GanttMachineType.Machine => "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z",
        // Monitor for workstation
        GanttMachineType.Workstation => "M21,16H3V4H21M21,2H3C1.89,2 1,2.89 1,4V16A2,2 0 0,0 3,18H10V20H8V22H16V20H14V18H21A2,2 0 0,0 23,16V4C23,2.89 22.1,2 21,2Z",
        // Robot arm
        GanttMachineType.Robot => "M12,2A2,2 0 0,1 14,4C14,4.74 13.6,5.39 13,5.73V7H14A7,7 0 0,1 21,14H22A1,1 0 0,1 23,15V18A1,1 0 0,1 22,19H21V20A2,2 0 0,1 19,22H5A2,2 0 0,1 3,20V19H2A1,1 0 0,1 1,18V15A1,1 0 0,1 2,14H3A7,7 0 0,1 10,7H11V5.73C10.4,5.39 10,4.74 10,4A2,2 0 0,1 12,2M7.5,13A2.5,2.5 0 0,0 5,15.5A2.5,2.5 0 0,0 7.5,18A2.5,2.5 0 0,0 10,15.5A2.5,2.5 0 0,0 7.5,13M16.5,13A2.5,2.5 0 0,0 14,15.5A2.5,2.5 0 0,0 16.5,18A2.5,2.5 0 0,0 19,15.5A2.5,2.5 0 0,0 16.5,13Z",
        // Arrow for conveyor
        GanttMachineType.Conveyor => "M4,15V9H12V4.16L19.84,12L12,19.84V15H4Z",
        // Wrench for assembly
        GanttMachineType.Assembly => "M22.7,19L13.6,9.9C14.5,7.6 14,4.9 12.1,3C10.1,1 7.1,0.6 4.7,1.7L9,6L6,9L1.6,4.7C0.4,7.1 0.9,10.1 2.9,12.1C4.8,14 7.5,14.5 9.8,13.6L18.9,22.7C19.3,23.1 19.9,23.1 20.3,22.7L22.6,20.4C23.1,20 23.1,19.3 22.7,19Z",
        // Magnifying glass for inspection
        GanttMachineType.Inspection => "M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z",
        // Box for packaging
        GanttMachineType.Packaging => "M5,4H19A2,2 0 0,1 21,6V18A2,2 0 0,1 19,20H5A2,2 0 0,1 3,18V6A2,2 0 0,1 5,4M5,6V18H19V6H5M7,8H17V10H7V8M7,12H17V14H7V12Z",
        // Factory for storage
        GanttMachineType.Storage => "M4,18V20H8V18H4M4,14V16H14V14H4M10,18V20H14V18H10M16,14V16H20V14H16M16,18V20H20V18H16M2,22V8L7,12V8L12,12V8L17,12H20A2,2 0 0,1 22,14V20A2,2 0 0,1 20,22H2Z",
        // Star for custom
        GanttMachineType.Custom => "M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z",
        _ => "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12Z"
    };

    /// <summary>
    /// Gets the default fill color for a machine type
    /// </summary>
    public static string GetMachineTypeColor(GanttMachineType type) => type switch
    {
        GanttMachineType.Machine => "#64748b",      // Slate
        GanttMachineType.Workstation => "#6366f1",  // Indigo
        GanttMachineType.Robot => "#8b5cf6",        // Purple
        GanttMachineType.Conveyor => "#0ea5e9",     // Sky blue
        GanttMachineType.Assembly => "#f97316",     // Orange
        GanttMachineType.Inspection => "#eab308",   // Yellow
        GanttMachineType.Packaging => "#22c55e",    // Green
        GanttMachineType.Storage => "#ec4899",      // Pink
        GanttMachineType.Custom => "#a855f7",       // Violet
        _ => "#64748b"
    };

    /// <summary>
    /// Gets the stroke color for a machine type
    /// </summary>
    public static string GetMachineTypeStrokeColor(GanttMachineType type) => type switch
    {
        GanttMachineType.Machine => "#475569",
        GanttMachineType.Workstation => "#4f46e5",
        GanttMachineType.Robot => "#7c3aed",
        GanttMachineType.Conveyor => "#0284c7",
        GanttMachineType.Assembly => "#ea580c",
        GanttMachineType.Inspection => "#ca8a04",
        GanttMachineType.Packaging => "#16a34a",
        GanttMachineType.Storage => "#db2777",
        GanttMachineType.Custom => "#9333ea",
        _ => "#475569"
    };

    /// <summary>
    /// Gets a display name for a machine type
    /// </summary>
    public static string GetMachineTypeName(GanttMachineType type) => type switch
    {
        GanttMachineType.Machine => "Machine",
        GanttMachineType.Workstation => "Workstation",
        GanttMachineType.Robot => "Robot",
        GanttMachineType.Conveyor => "Conveyor",
        GanttMachineType.Assembly => "Assembly",
        GanttMachineType.Inspection => "Inspection",
        GanttMachineType.Packaging => "Packaging",
        GanttMachineType.Storage => "Storage",
        GanttMachineType.Custom => "Custom",
        _ => "Machine"
    };

    /// <summary>
    /// Formats a TimeSpan as hours:minutes
    /// </summary>
    public static string FormatTime(TimeSpan time)
    {
        var hours = (int)time.TotalHours;
        var minutes = time.Minutes;
        return $"{hours}:{minutes:D2}";
    }

    /// <summary>
    /// Formats a duration as a readable string
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }
        return $"{(int)duration.TotalMinutes}m";
    }

    /// <summary>
    /// Darkens a hex color for stroke
    /// </summary>
    private static string DarkenColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            return "#000000";

        try
        {
            var hex = hexColor.TrimStart('#');
            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);

            // Darken by 20%
            r = (int)(r * 0.8);
            g = (int)(g * 0.8);
            b = (int)(b * 0.8);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return "#000000";
        }
    }

    /// <summary>
    /// Gets dependency connection point for a node based on precedence type
    /// </summary>
    public (double x, double y) GetNodePrecedencePoint(Node node, bool isStart, GanttPrecedenceType precType)
    {
        return precType switch
        {
            GanttPrecedenceType.FinishToStart when !isStart => GetNodeBarRightEdge(node),
            GanttPrecedenceType.FinishToStart when isStart => GetNodeBarLeftEdge(node),
            GanttPrecedenceType.StartToStart => GetNodeBarLeftEdge(node),
            GanttPrecedenceType.FinishToFinish => GetNodeBarRightEdge(node),
            GanttPrecedenceType.StartToFinish when !isStart => GetNodeBarLeftEdge(node),
            GanttPrecedenceType.StartToFinish when isStart => GetNodeBarRightEdge(node),
            _ => GetNodeBarRightEdge(node)
        };
    }
}
