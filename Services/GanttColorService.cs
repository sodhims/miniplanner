namespace dfd2wasm.Services;

/// <summary>
/// Service for managing job colors in Gantt machine scheduling.
/// Provides automatic color assignment for jobs with distinct, visually appealing colors.
/// </summary>
public class GanttColorService
{
    /// <summary>
    /// Predefined color palette for jobs (12 distinct colors).
    /// </summary>
    private static readonly List<(string Fill, string Stroke)> ColorPalette = new()
    {
        ("#ef4444", "#dc2626"), // Red
        ("#3b82f6", "#2563eb"), // Blue
        ("#22c55e", "#16a34a"), // Green
        ("#f59e0b", "#d97706"), // Amber
        ("#8b5cf6", "#7c3aed"), // Purple
        ("#ec4899", "#db2777"), // Pink
        ("#06b6d4", "#0891b2"), // Cyan
        ("#f97316", "#ea580c"), // Orange
        ("#84cc16", "#65a30d"), // Lime
        ("#6366f1", "#4f46e5"), // Indigo
        ("#14b8a6", "#0d9488"), // Teal
        ("#a855f7", "#9333ea"), // Violet
    };

    /// <summary>
    /// Available pattern types for task differentiation.
    /// </summary>
    public enum PatternType
    {
        Solid,
        DiagonalLines,
        CrossHatch,
        Dots,
        HorizontalLines,
        VerticalLines,
        Checkerboard,
        Zigzag
    }

    /// <summary>
    /// Pattern definitions in order of assignment.
    /// </summary>
    private static readonly PatternType[] PatternOrder = new[]
    {
        PatternType.Solid,
        PatternType.DiagonalLines,
        PatternType.Dots,
        PatternType.HorizontalLines,
        PatternType.CrossHatch,
        PatternType.VerticalLines,
        PatternType.Checkerboard,
        PatternType.Zigzag
    };

    /// <summary>
    /// Extended color palette for when more than 12 jobs are needed.
    /// </summary>
    private static readonly List<(string Fill, string Stroke)> ExtendedPalette = new()
    {
        ("#f43f5e", "#e11d48"), // Rose
        ("#0ea5e9", "#0284c7"), // Sky
        ("#10b981", "#059669"), // Emerald
        ("#eab308", "#ca8a04"), // Yellow
        ("#d946ef", "#c026d3"), // Fuchsia
        ("#64748b", "#475569"), // Slate
        ("#78716c", "#57534e"), // Stone
        ("#71717a", "#52525b"), // Zinc
    };

    /// <summary>
    /// Dictionary mapping job IDs to their assigned colors.
    /// </summary>
    private readonly Dictionary<int, (string Fill, string Stroke)> _jobColors = new();

    /// <summary>
    /// Next index in the color palette to use.
    /// </summary>
    private int _nextColorIndex = 0;

    /// <summary>
    /// Assigns a unique color to a job.
    /// </summary>
    public (string Fill, string Stroke) AssignJobColor(int jobId)
    {
        if (_jobColors.TryGetValue(jobId, out var existingColor))
        {
            return existingColor;
        }

        // Get next color from palette
        var color = GetNextColor();
        _jobColors[jobId] = color;
        return color;
    }

    /// <summary>
    /// Gets the color assigned to a job.
    /// </summary>
    public (string Fill, string Stroke)? GetJobColor(int jobId)
    {
        return _jobColors.TryGetValue(jobId, out var color) ? color : null;
    }

    /// <summary>
    /// Gets the fill color for a job (assigns one if not yet assigned).
    /// </summary>
    public string GetJobFillColor(int jobId)
    {
        var color = AssignJobColor(jobId);
        return color.Fill;
    }

    /// <summary>
    /// Gets the stroke color for a job (assigns one if not yet assigned).
    /// </summary>
    public string GetJobStrokeColor(int jobId)
    {
        var color = AssignJobColor(jobId);
        return color.Stroke;
    }

    /// <summary>
    /// Sets a specific color for a job.
    /// </summary>
    public void SetJobColor(int jobId, string fillColor, string? strokeColor = null)
    {
        var stroke = strokeColor ?? DarkenColor(fillColor);
        _jobColors[jobId] = (fillColor, stroke);
    }

    /// <summary>
    /// Removes the color assignment for a job.
    /// </summary>
    public void RemoveJobColor(int jobId)
    {
        _jobColors.Remove(jobId);
    }

    /// <summary>
    /// Clears all color assignments.
    /// </summary>
    public void Reset()
    {
        _jobColors.Clear();
        _nextColorIndex = 0;
    }

    /// <summary>
    /// Gets the next color from the palette.
    /// </summary>
    private (string Fill, string Stroke) GetNextColor()
    {
        var allColors = ColorPalette.Concat(ExtendedPalette).ToList();
        var color = allColors[_nextColorIndex % allColors.Count];
        _nextColorIndex++;
        return color;
    }

    /// <summary>
    /// Gets a random color from the palette (for variety).
    /// </summary>
    public static (string Fill, string Stroke) GetRandomColor()
    {
        var random = new Random();
        return ColorPalette[random.Next(ColorPalette.Count)];
    }

    /// <summary>
    /// Gets a specific color by index.
    /// </summary>
    public static (string Fill, string Stroke) GetColorByIndex(int index)
    {
        var allColors = ColorPalette.Concat(ExtendedPalette).ToList();
        return allColors[index % allColors.Count];
    }

    /// <summary>
    /// Gets the pattern type for a job based on its index.
    /// Patterns cycle after colors are exhausted to differentiate similar colors.
    /// </summary>
    public PatternType GetPatternForJob(int jobIndex)
    {
        // First cycle through colors with solid pattern
        // Then cycle through colors with patterns
        var patternCycle = jobIndex / ColorPalette.Count;
        return PatternOrder[patternCycle % PatternOrder.Length];
    }

    /// <summary>
    /// Gets the pattern ID for SVG fill reference.
    /// </summary>
    public string GetPatternId(int jobId, string baseColor)
    {
        var jobIndex = GetJobIndex(jobId);
        var pattern = GetPatternForJob(jobIndex);
        if (pattern == PatternType.Solid)
            return ""; // No pattern, use solid fill
        return $"pattern-{pattern}-{baseColor.TrimStart('#')}";
    }

    /// <summary>
    /// Gets the job index (0-based) for pattern calculation.
    /// </summary>
    private int GetJobIndex(int jobId)
    {
        var keys = _jobColors.Keys.ToList();
        var index = keys.IndexOf(jobId);
        return index >= 0 ? index : _jobColors.Count;
    }

    /// <summary>
    /// Generates SVG pattern definitions for all pattern types with a given color.
    /// </summary>
    public static string GeneratePatternDefs(string color, string colorId)
    {
        var darkerColor = DarkenColor(color);
        var lighterColor = LightenColor(color);

        return $@"
            <pattern id=""pattern-DiagonalLines-{colorId}"" patternUnits=""userSpaceOnUse"" width=""8"" height=""8"">
                <rect width=""8"" height=""8"" fill=""{color}""/>
                <path d=""M-2,2 l4,-4 M0,8 l8,-8 M6,10 l4,-4"" stroke=""{darkerColor}"" stroke-width=""2"" opacity=""0.4""/>
            </pattern>
            <pattern id=""pattern-CrossHatch-{colorId}"" patternUnits=""userSpaceOnUse"" width=""8"" height=""8"">
                <rect width=""8"" height=""8"" fill=""{color}""/>
                <path d=""M0,0 l8,8 M8,0 l-8,8"" stroke=""{darkerColor}"" stroke-width=""1.5"" opacity=""0.35""/>
            </pattern>
            <pattern id=""pattern-Dots-{colorId}"" patternUnits=""userSpaceOnUse"" width=""10"" height=""10"">
                <rect width=""10"" height=""10"" fill=""{color}""/>
                <circle cx=""5"" cy=""5"" r=""2"" fill=""{darkerColor}"" opacity=""0.4""/>
            </pattern>
            <pattern id=""pattern-HorizontalLines-{colorId}"" patternUnits=""userSpaceOnUse"" width=""8"" height=""6"">
                <rect width=""8"" height=""6"" fill=""{color}""/>
                <line x1=""0"" y1=""3"" x2=""8"" y2=""3"" stroke=""{darkerColor}"" stroke-width=""2"" opacity=""0.35""/>
            </pattern>
            <pattern id=""pattern-VerticalLines-{colorId}"" patternUnits=""userSpaceOnUse"" width=""6"" height=""8"">
                <rect width=""6"" height=""8"" fill=""{color}""/>
                <line x1=""3"" y1=""0"" x2=""3"" y2=""8"" stroke=""{darkerColor}"" stroke-width=""2"" opacity=""0.35""/>
            </pattern>
            <pattern id=""pattern-Checkerboard-{colorId}"" patternUnits=""userSpaceOnUse"" width=""10"" height=""10"">
                <rect width=""10"" height=""10"" fill=""{color}""/>
                <rect x=""0"" y=""0"" width=""5"" height=""5"" fill=""{darkerColor}"" opacity=""0.25""/>
                <rect x=""5"" y=""5"" width=""5"" height=""5"" fill=""{darkerColor}"" opacity=""0.25""/>
            </pattern>
            <pattern id=""pattern-Zigzag-{colorId}"" patternUnits=""userSpaceOnUse"" width=""12"" height=""8"">
                <rect width=""12"" height=""8"" fill=""{color}""/>
                <path d=""M0,4 l3,-3 l3,3 l3,-3 l3,3"" stroke=""{darkerColor}"" stroke-width=""2"" fill=""none"" opacity=""0.35""/>
            </pattern>";
    }

    /// <summary>
    /// Gets the full color palette.
    /// </summary>
    public static IReadOnlyList<(string Fill, string Stroke)> GetPalette()
    {
        return ColorPalette;
    }

    /// <summary>
    /// Gets the extended color palette (including base colors).
    /// </summary>
    public static IReadOnlyList<(string Fill, string Stroke)> GetExtendedPalette()
    {
        return ColorPalette.Concat(ExtendedPalette).ToList();
    }

    /// <summary>
    /// Darkens a hex color for stroke (20% darker).
    /// </summary>
    public static string DarkenColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            return "#000000";

        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length < 6) return "#000000";

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
    /// Lightens a hex color (20% lighter).
    /// </summary>
    public static string LightenColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            return "#ffffff";

        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length < 6) return "#ffffff";

            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);

            // Lighten by 20%
            r = Math.Min(255, (int)(r * 1.2));
            g = Math.Min(255, (int)(g * 1.2));
            b = Math.Min(255, (int)(b * 1.2));

            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return "#ffffff";
        }
    }

    /// <summary>
    /// Gets a contrasting text color (black or white) for a background color.
    /// </summary>
    public static string GetContrastingTextColor(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            return "#ffffff";

        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length < 6) return "#ffffff";

            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);

            // Calculate relative luminance
            var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

            return luminance > 0.5 ? "#000000" : "#ffffff";
        }
        catch
        {
            return "#ffffff";
        }
    }

    /// <summary>
    /// Creates a semi-transparent version of a color.
    /// </summary>
    public static string GetTransparentColor(string hexColor, double opacity)
    {
        if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            return $"rgba(0,0,0,{opacity})";

        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length < 6) return $"rgba(0,0,0,{opacity})";

            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);

            return $"rgba({r},{g},{b},{opacity})";
        }
        catch
        {
            return $"rgba(0,0,0,{opacity})";
        }
    }
}
