using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dfd2wasm.Services;

/// <summary>
/// Service for managing DFD/Project color themes/palettes.
/// Loads themes from themes.json config file and supports runtime customization.
/// </summary>
public class DfdThemeService
{
    private readonly HttpClient? _httpClient;
    private bool _initialized = false;

    /// <summary>
    /// All available themes (loaded from config + built-in)
    /// </summary>
    public Dictionary<string, DfdTheme> Themes { get; private set; } = new();

    /// <summary>
    /// Custom themes added at runtime
    /// </summary>
    public Dictionary<string, DfdTheme> CustomThemes { get; private set; } = new();

    /// <summary>
    /// Current active theme
    /// </summary>
    public DfdTheme CurrentTheme { get; private set; } = CreateDefaultTheme();

    /// <summary>
    /// Current theme ID
    /// </summary>
    public string CurrentThemeId { get; private set; } = "default";

    /// <summary>
    /// Event raised when themes are loaded or changed
    /// </summary>
    public event Action? OnThemesChanged;

    public DfdThemeService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient;
        // Initialize with built-in defaults in case config fails to load
        InitializeBuiltInThemes();
    }

    /// <summary>
    /// Initialize themes from config file
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            if (_httpClient != null)
            {
                var config = await _httpClient.GetFromJsonAsync<ThemeConfig>("themes.json");
                if (config?.Themes != null)
                {
                    foreach (var (id, themeData) in config.Themes)
                    {
                        var theme = ThemeDataToTheme(themeData);
                        Themes[id] = theme;
                    }
                    Console.WriteLine($"Loaded {config.Themes.Count} themes from config");

                    // Set default theme from config
                    if (!string.IsNullOrEmpty(config.DefaultTheme) && Themes.ContainsKey(config.DefaultTheme))
                    {
                        SetTheme(config.DefaultTheme);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load themes.json, using built-in defaults: {ex.Message}");
        }

        _initialized = true;
    }

    /// <summary>
    /// Initialize built-in themes as fallback
    /// </summary>
    private void InitializeBuiltInThemes()
    {
        Themes["default"] = CreateDefaultTheme();
        Themes["pastel"] = CreatePastelTheme();
        Themes["bold"] = CreateBoldTheme();
        Themes["forest"] = CreateForestTheme();
        Themes["ocean"] = CreateOceanTheme();
        Themes["sunset"] = CreateSunsetTheme();
        Themes["monochrome"] = CreateMonochromeTheme();
        Themes["highcontrast"] = CreateHighContrastTheme();
        Themes["tartan"] = CreateTartanTheme();
    }

    /// <summary>
    /// Set the active theme by ID
    /// </summary>
    public void SetTheme(string themeId)
    {
        if (Themes.TryGetValue(themeId, out var theme))
        {
            CurrentTheme = theme;
            CurrentThemeId = themeId;
            OnThemesChanged?.Invoke();
        }
        else if (CustomThemes.TryGetValue(themeId, out var customTheme))
        {
            CurrentTheme = customTheme;
            CurrentThemeId = themeId;
            OnThemesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Add a custom theme
    /// </summary>
    public void AddCustomTheme(string id, DfdTheme theme)
    {
        CustomThemes[id] = theme;
        OnThemesChanged?.Invoke();
    }

    /// <summary>
    /// Remove a custom theme
    /// </summary>
    public bool RemoveCustomTheme(string id)
    {
        if (CustomThemes.Remove(id))
        {
            if (CurrentThemeId == id)
            {
                SetTheme("default");
            }
            OnThemesChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clone an existing theme as a new custom theme
    /// </summary>
    public DfdTheme CloneTheme(string sourceThemeId, string newName)
    {
        DfdTheme? source = null;
        if (Themes.TryGetValue(sourceThemeId, out var t))
            source = t;
        else if (CustomThemes.TryGetValue(sourceThemeId, out var ct))
            source = ct;

        source ??= CurrentTheme;

        return new DfdTheme
        {
            Name = newName,
            Description = $"Custom theme based on {source.Name}",
            TaskFill = source.TaskFill,
            TaskStroke = source.TaskStroke,
            TaskText = source.TaskText,
            CriticalFill = source.CriticalFill,
            CriticalStroke = source.CriticalStroke,
            CriticalText = source.CriticalText,
            MilestoneFill = source.MilestoneFill,
            MilestoneStroke = source.MilestoneStroke,
            GroupFill = source.GroupFill,
            GroupStroke = source.GroupStroke,
            GroupText = source.GroupText,
            SelectionStroke = source.SelectionStroke,
            SelectionFill = source.SelectionFill,
            HeaderFill = source.HeaderFill,
            HeaderStroke = source.HeaderStroke,
            HeaderText = source.HeaderText,
            GridLine = source.GridLine,
            WeekendFill = source.WeekendFill,
            SaturdayFill = source.SaturdayFill,
            SundayFill = source.SundayFill,
            HolidayFill = source.HolidayFill,
            ChristmasFill = source.ChristmasFill,
            NewYearFill = source.NewYearFill,
            CompanyEventFill = source.CompanyEventFill,
            VacationFill = source.VacationFill,
            DependencyStroke = source.DependencyStroke,
            DependencyArrow = source.DependencyArrow,
            TodayLine = source.TodayLine,
            TaskNameText = source.TaskNameText,
            DateText = source.DateText,
            WeekendText = source.WeekendText,
            ProcessFill = source.ProcessFill,
            ProcessStroke = source.ProcessStroke,
            ProcessText = source.ProcessText,
            ExternalEntityFill = source.ExternalEntityFill,
            ExternalEntityStroke = source.ExternalEntityStroke,
            ExternalEntityText = source.ExternalEntityText,
            DataStoreFill = source.DataStoreFill,
            DataStoreStroke = source.DataStoreStroke,
            DataStoreText = source.DataStoreText,
            DecisionFill = source.DecisionFill,
            DecisionStroke = source.DecisionStroke,
            DecisionText = source.DecisionText,
            NodeFill = source.NodeFill,
            NodeStroke = source.NodeStroke,
            NodeText = source.NodeText,
            EdgeStroke = source.EdgeStroke,
            EdgeArrow = source.EdgeArrow,
            EdgeText = source.EdgeText,
            CanvasBackground = source.CanvasBackground,
            CanvasGrid = source.CanvasGrid,
            RowColor1 = source.RowColor1,
            RowColor2 = source.RowColor2,
            RowColor3 = source.RowColor3,
            RowColor4 = source.RowColor4,
            RowColor5 = source.RowColor5,
            RowColor6 = source.RowColor6,
            RowColor7 = source.RowColor7,
            RowColor8 = source.RowColor8,
            RowColor9 = source.RowColor9,
            RowColor10 = source.RowColor10,
            GroupRowColor = source.GroupRowColor
        };
    }

    /// <summary>
    /// Get list of available theme IDs and names (built-in + custom)
    /// </summary>
    public IEnumerable<(string Id, string Name, string Description, bool IsCustom)> GetAvailableThemes()
    {
        foreach (var t in Themes)
        {
            yield return (t.Key, t.Value.Name, t.Value.Description, false);
        }
        foreach (var t in CustomThemes)
        {
            yield return (t.Key, t.Value.Name, t.Value.Description, true);
        }
    }

    /// <summary>
    /// Export current theme as JSON string for copying/saving
    /// </summary>
    public string ExportThemeAsJson(string? themeId = null)
    {
        var theme = themeId != null && Themes.TryGetValue(themeId, out var t) ? t : CurrentTheme;
        return JsonSerializer.Serialize(ThemeToThemeData(theme), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Import a theme from JSON string
    /// </summary>
    public DfdTheme? ImportThemeFromJson(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<ThemeData>(json);
            if (data != null)
            {
                return ThemeDataToTheme(data);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to import theme: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Get Gantt timeline theme colors
    /// </summary>
    public GanttThemeColors GetGanttColors()
    {
        var theme = CurrentTheme;
        return new GanttThemeColors
        {
            HeaderFill = theme.HeaderFill,
            HeaderStroke = theme.HeaderStroke,
            HeaderText = theme.HeaderText,
            GridLineMajor = theme.GridLine,
            GridLineMinor = AdjustColorOpacity(theme.GridLine, 0.5),
            RowEvenFill = theme.RowColor1,
            RowOddFill = theme.RowColor2,
            MachineLabelFill = theme.HeaderFill,
            MachineLabelStroke = theme.HeaderStroke,
            MachineLabelText = theme.HeaderText,
            DependencyStroke = theme.DependencyStroke,
            DependencyArrow = theme.DependencyArrow,
            ViolationStroke = theme.CriticalStroke,
            TodayLine = theme.TodayLine,
            SelectionStroke = theme.SelectionStroke,
            SelectionFill = theme.SelectionFill,
            CanvasBackground = theme.CanvasBackground,
            TaskDefaultFill = theme.TaskFill,
            TaskDefaultStroke = theme.TaskStroke,
            TaskDefaultText = theme.TaskText,
            TerminalInputColor = "#22c55e",  // Green for input
            TerminalOutputColor = "#ef4444", // Red for output
            AddButtonFill = "#10b981",       // Emerald green
            AddButtonText = "#ffffff"
        };
    }

    /// <summary>
    /// Adjust a hex color's opacity (returns rgba string)
    /// </summary>
    private static string AdjustColorOpacity(string hexColor, double opacity)
    {
        if (string.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            return hexColor;

        try
        {
            var hex = hexColor.TrimStart('#');
            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return $"rgba({r}, {g}, {b}, {opacity})";
        }
        catch
        {
            return hexColor;
        }
    }

    /// <summary>
    /// Get default fill/stroke colors for a node based on its template and shape
    /// </summary>
    public (string Fill, string Stroke, string Text) GetColorsForShape(string? templateId, string? shapeId)
    {
        var theme = CurrentTheme;

        // Map template/shape combinations to theme colors
        return (templateId?.ToLower(), shapeId?.ToLower()) switch
        {
            // Flowchart shapes
            ("flowchart", "process") or ("flowchart", "rectangle") => (theme.ProcessFill, theme.ProcessStroke, theme.ProcessText),
            ("flowchart", "decision") or ("flowchart", "diamond") => (theme.DecisionFill, theme.DecisionStroke, theme.DecisionText),
            ("flowchart", "data") or ("flowchart", "parallelogram") => (theme.DataStoreFill, theme.DataStoreStroke, theme.DataStoreText),
            ("flowchart", "database") or ("flowchart", "cylinder") => (theme.DataStoreFill, theme.DataStoreStroke, theme.DataStoreText),
            ("flowchart", "terminator") or ("flowchart", "stadium") => (theme.ExternalEntityFill, theme.ExternalEntityStroke, theme.ExternalEntityText),
            ("flowchart", "document") => (theme.NodeFill, theme.NodeStroke, theme.NodeText),

            // STS (DFD) shapes
            ("sts", "process") or ("sts", "ellipse") => (theme.ProcessFill, theme.ProcessStroke, theme.ProcessText),
            ("sts", "external") or ("sts", "rectangle") => (theme.ExternalEntityFill, theme.ExternalEntityStroke, theme.ExternalEntityText),
            ("sts", "datastore") or ("sts", "openrect") => (theme.DataStoreFill, theme.DataStoreStroke, theme.DataStoreText),

            // ICD shapes
            ("icd", "system") => (theme.ProcessFill, theme.ProcessStroke, theme.ProcessText),
            ("icd", "subsystem") => (theme.DataStoreFill, theme.DataStoreStroke, theme.DataStoreText),
            ("icd", "interface") => (theme.ExternalEntityFill, theme.ExternalEntityStroke, theme.ExternalEntityText),
            ("icd", "external") => (theme.DecisionFill, theme.DecisionStroke, theme.DecisionText),

            // Network shapes
            ("network", "server") or ("network", "database") => (theme.DataStoreFill, theme.DataStoreStroke, theme.DataStoreText),
            ("network", "computer") or ("network", "laptop") or ("network", "mobile") => (theme.ProcessFill, theme.ProcessStroke, theme.ProcessText),
            ("network", "router") or ("network", "switch") => (theme.ExternalEntityFill, theme.ExternalEntityStroke, theme.ExternalEntityText),
            ("network", "cloud") or ("network", "internet") => (theme.DecisionFill, theme.DecisionStroke, theme.DecisionText),
            ("network", "firewall") => (theme.CriticalFill, theme.CriticalStroke, theme.CriticalText),

            // BPMN shapes
            ("bpmn", "task") or ("bpmn", "activity") => (theme.ProcessFill, theme.ProcessStroke, theme.ProcessText),
            ("bpmn", "startevent") or ("bpmn", "endevent") => (theme.ExternalEntityFill, theme.ExternalEntityStroke, theme.ExternalEntityText),
            ("bpmn", "gateway") => (theme.DecisionFill, theme.DecisionStroke, theme.DecisionText),
            ("bpmn", "dataobject") => (theme.DataStoreFill, theme.DataStoreStroke, theme.DataStoreText),

            // FMECA shapes
            ("fmeca", "component") => (theme.ProcessFill, theme.ProcessStroke, theme.ProcessText),
            ("fmeca", "failuremode") => (theme.CriticalFill, theme.CriticalStroke, theme.CriticalText),
            ("fmeca", "effect") => (theme.DecisionFill, theme.DecisionStroke, theme.DecisionText),

            // Project
            ("project", _) => (theme.TaskFill, theme.TaskStroke, theme.TaskText),

            // Default fallback
            _ => (theme.NodeFill, theme.NodeStroke, theme.NodeText)
        };
    }

    #region Theme Data Conversion

    private static DfdTheme ThemeDataToTheme(ThemeData data)
    {
        return new DfdTheme
        {
            Name = data.Name ?? "Unnamed Theme",
            Description = data.Description ?? "",
            TaskFill = data.TaskFill ?? "#3b82f6",
            TaskStroke = data.TaskStroke ?? "#1d4ed8",
            TaskText = data.TaskText ?? "#ffffff",
            CriticalFill = data.CriticalFill ?? "#fee2e2",
            CriticalStroke = data.CriticalStroke ?? "#ef4444",
            CriticalText = data.CriticalText ?? "#991b1b",
            MilestoneFill = data.MilestoneFill ?? "#8b5cf6",
            MilestoneStroke = data.MilestoneStroke ?? "#6d28d9",
            GroupFill = data.GroupFill ?? "#1f2937",
            GroupStroke = data.GroupStroke ?? "#f59e0b",
            GroupText = data.GroupText ?? "#ffffff",
            SelectionStroke = data.SelectionStroke ?? "#3b82f6",
            SelectionFill = data.SelectionFill ?? "rgba(59, 130, 246, 0.1)",
            HeaderFill = data.HeaderFill ?? "#e2e8f0",
            HeaderStroke = data.HeaderStroke ?? "#cbd5e1",
            HeaderText = data.HeaderText ?? "#374151",
            GridLine = data.GridLine ?? "#e2e8f0",
            WeekendFill = data.WeekendFill ?? "#f1f5f9",
            SaturdayFill = data.SaturdayFill ?? "#f1f5f9",
            SundayFill = data.SundayFill ?? "#fee2e2",
            HolidayFill = data.HolidayFill ?? "#fef3c7",
            ChristmasFill = data.ChristmasFill ?? "#dcfce7",
            NewYearFill = data.NewYearFill ?? "#fae8ff",
            CompanyEventFill = data.CompanyEventFill ?? "#dbeafe",
            VacationFill = data.VacationFill ?? "#e0f2fe",
            DependencyStroke = data.DependencyStroke ?? "#475569",
            DependencyArrow = data.DependencyArrow ?? "#374151",
            TodayLine = data.TodayLine ?? "#ef4444",
            TaskNameText = data.TaskNameText ?? "#1e40af",
            DateText = data.DateText ?? "#64748b",
            WeekendText = data.WeekendText ?? "#94a3b8",
            ProcessFill = data.ProcessFill ?? "#dbeafe",
            ProcessStroke = data.ProcessStroke ?? "#3b82f6",
            ProcessText = data.ProcessText ?? "#1e40af",
            ExternalEntityFill = data.ExternalEntityFill ?? "#f3e8ff",
            ExternalEntityStroke = data.ExternalEntityStroke ?? "#8b5cf6",
            ExternalEntityText = data.ExternalEntityText ?? "#5b21b6",
            DataStoreFill = data.DataStoreFill ?? "#fef3c7",
            DataStoreStroke = data.DataStoreStroke ?? "#f59e0b",
            DataStoreText = data.DataStoreText ?? "#92400e",
            DecisionFill = data.DecisionFill ?? "#fef3c7",
            DecisionStroke = data.DecisionStroke ?? "#f59e0b",
            DecisionText = data.DecisionText ?? "#92400e",
            NodeFill = data.NodeFill ?? "#ffffff",
            NodeStroke = data.NodeStroke ?? "#374151",
            NodeText = data.NodeText ?? "#1f2937",
            EdgeStroke = data.EdgeStroke ?? "#374151",
            EdgeArrow = data.EdgeArrow ?? "#374151",
            EdgeText = data.EdgeText ?? "#4b5563",
            CanvasBackground = data.CanvasBackground ?? "#ffffff",
            CanvasGrid = data.CanvasGrid ?? "#e5e7eb",
            // Project Row Colors
            RowColor1 = data.RowColor1 ?? "#ffffff",
            RowColor2 = data.RowColor2 ?? "#f8fafc",
            RowColor3 = data.RowColor3 ?? "#f1f5f9",
            RowColor4 = data.RowColor4 ?? "#e2e8f0",
            RowColor5 = data.RowColor5 ?? "#ffffff",
            RowColor6 = data.RowColor6 ?? "#f8fafc",
            RowColor7 = data.RowColor7 ?? "#f1f5f9",
            RowColor8 = data.RowColor8 ?? "#e2e8f0",
            RowColor9 = data.RowColor9 ?? "#ffffff",
            RowColor10 = data.RowColor10 ?? "#f8fafc",
            GroupRowColor = data.GroupRowColor ?? "#e0e7ff"
        };
    }

    private static ThemeData ThemeToThemeData(DfdTheme theme)
    {
        return new ThemeData
        {
            Name = theme.Name,
            Description = theme.Description,
            TaskFill = theme.TaskFill,
            TaskStroke = theme.TaskStroke,
            TaskText = theme.TaskText,
            CriticalFill = theme.CriticalFill,
            CriticalStroke = theme.CriticalStroke,
            CriticalText = theme.CriticalText,
            MilestoneFill = theme.MilestoneFill,
            MilestoneStroke = theme.MilestoneStroke,
            GroupFill = theme.GroupFill,
            GroupStroke = theme.GroupStroke,
            GroupText = theme.GroupText,
            SelectionStroke = theme.SelectionStroke,
            SelectionFill = theme.SelectionFill,
            HeaderFill = theme.HeaderFill,
            HeaderStroke = theme.HeaderStroke,
            HeaderText = theme.HeaderText,
            GridLine = theme.GridLine,
            WeekendFill = theme.WeekendFill,
            SaturdayFill = theme.SaturdayFill,
            SundayFill = theme.SundayFill,
            HolidayFill = theme.HolidayFill,
            ChristmasFill = theme.ChristmasFill,
            NewYearFill = theme.NewYearFill,
            CompanyEventFill = theme.CompanyEventFill,
            VacationFill = theme.VacationFill,
            DependencyStroke = theme.DependencyStroke,
            DependencyArrow = theme.DependencyArrow,
            TodayLine = theme.TodayLine,
            TaskNameText = theme.TaskNameText,
            DateText = theme.DateText,
            WeekendText = theme.WeekendText,
            ProcessFill = theme.ProcessFill,
            ProcessStroke = theme.ProcessStroke,
            ProcessText = theme.ProcessText,
            ExternalEntityFill = theme.ExternalEntityFill,
            ExternalEntityStroke = theme.ExternalEntityStroke,
            ExternalEntityText = theme.ExternalEntityText,
            DataStoreFill = theme.DataStoreFill,
            DataStoreStroke = theme.DataStoreStroke,
            DataStoreText = theme.DataStoreText,
            DecisionFill = theme.DecisionFill,
            DecisionStroke = theme.DecisionStroke,
            DecisionText = theme.DecisionText,
            NodeFill = theme.NodeFill,
            NodeStroke = theme.NodeStroke,
            NodeText = theme.NodeText,
            EdgeStroke = theme.EdgeStroke,
            EdgeArrow = theme.EdgeArrow,
            EdgeText = theme.EdgeText,
            CanvasBackground = theme.CanvasBackground,
            CanvasGrid = theme.CanvasGrid,
            // Project Row Colors
            RowColor1 = theme.RowColor1,
            RowColor2 = theme.RowColor2,
            RowColor3 = theme.RowColor3,
            RowColor4 = theme.RowColor4,
            RowColor5 = theme.RowColor5,
            RowColor6 = theme.RowColor6,
            RowColor7 = theme.RowColor7,
            RowColor8 = theme.RowColor8,
            RowColor9 = theme.RowColor9,
            RowColor10 = theme.RowColor10,
            GroupRowColor = theme.GroupRowColor
        };
    }

    #endregion

    #region Built-in Theme Factories

    private static DfdTheme CreateDefaultTheme() => new()
    {
        Name = "Default (Blue)",
        Description = "Standard blue theme with professional appearance",
        SaturdayFill = "#f1f5f9",
        SundayFill = "#fee2e2",
        HolidayFill = "#fef3c7",
        ChristmasFill = "#dcfce7",
        NewYearFill = "#fae8ff",
        CompanyEventFill = "#dbeafe",
        VacationFill = "#e0f2fe",
        RowColor1 = "#ffffff",
        RowColor2 = "#e0f2fe",
        RowColor3 = "#ffffff",
        RowColor4 = "#dbeafe",
        RowColor5 = "#ffffff",
        RowColor6 = "#e0e7ff",
        RowColor7 = "#ffffff",
        RowColor8 = "#dbeafe",
        RowColor9 = "#ffffff",
        RowColor10 = "#e0f2fe",
        GroupRowColor = "#1e40af"
    };

    private static DfdTheme CreatePastelTheme() => new()
    {
        Name = "Pastel",
        Description = "Soft pastel colors for a gentle appearance",
        TaskFill = "#a5d8ff",
        TaskStroke = "#74c0fc",
        TaskText = "#1864ab",
        CriticalFill = "#ffc9c9",
        CriticalStroke = "#ff8787",
        CriticalText = "#c92a2a",
        MilestoneFill = "#d0bfff",
        MilestoneStroke = "#b197fc",
        GroupFill = "#868e96",
        GroupStroke = "#fab005",
        GroupText = "#ffffff",
        SelectionStroke = "#4dabf7",
        SelectionFill = "rgba(77, 171, 247, 0.15)",
        HeaderFill = "#f1f3f5",
        HeaderStroke = "#dee2e6",
        HeaderText = "#495057",
        GridLine = "#e9ecef",
        WeekendFill = "#f8f9fa",
        SaturdayFill = "#f8f9fa",
        SundayFill = "#ffc9c9",
        HolidayFill = "#fff3bf",
        ChristmasFill = "#d3f9d8",
        NewYearFill = "#e5dbff",
        CompanyEventFill = "#d0ebff",
        VacationFill = "#c5f6fa",
        DependencyStroke = "#868e96",
        DependencyArrow = "#495057",
        TodayLine = "#fa5252",
        TaskNameText = "#1864ab",
        DateText = "#868e96",
        WeekendText = "#adb5bd",
        ProcessFill = "#d0ebff",
        ProcessStroke = "#74c0fc",
        ProcessText = "#1864ab",
        ExternalEntityFill = "#e5dbff",
        ExternalEntityStroke = "#b197fc",
        ExternalEntityText = "#5f3dc4",
        DataStoreFill = "#fff3bf",
        DataStoreStroke = "#fcc419",
        DataStoreText = "#e67700",
        DecisionFill = "#ffe8cc",
        DecisionStroke = "#ff922b",
        DecisionText = "#d9480f",
        NodeFill = "#f8f9fa",
        NodeStroke = "#adb5bd",
        NodeText = "#495057",
        EdgeStroke = "#868e96",
        EdgeArrow = "#495057",
        EdgeText = "#6c757d",
        CanvasBackground = "#ffffff",
        CanvasGrid = "#f1f3f5",
        RowColor1 = "#ffffff",
        RowColor2 = "#f8f9fa",
        RowColor3 = "#f1f3f5",
        RowColor4 = "#e9ecef",
        RowColor5 = "#d0ebff",
        RowColor6 = "#a5d8ff",
        RowColor7 = "#f1f3f5",
        RowColor8 = "#e9ecef",
        RowColor9 = "#d0ebff",
        RowColor10 = "#a5d8ff",
        GroupRowColor = "#1864ab"
    };

    private static DfdTheme CreateBoldTheme() => new()
    {
        Name = "Bold",
        Description = "High impact black, red, and gold colors",
        TaskFill = "#1a1a1a",
        TaskStroke = "#000000",
        TaskText = "#ffd700",
        CriticalFill = "#dd0000",
        CriticalStroke = "#aa0000",
        CriticalText = "#ffffff",
        MilestoneFill = "#ffd700",
        MilestoneStroke = "#daa520",
        GroupFill = "#dd0000",
        GroupStroke = "#ffd700",
        GroupText = "#ffffff",
        SelectionStroke = "#ffd700",
        SelectionFill = "rgba(255, 215, 0, 0.15)",
        HeaderFill = "#2d2d2d",
        HeaderStroke = "#1a1a1a",
        HeaderText = "#ffd700",
        GridLine = "#3d3d3d",
        WeekendFill = "#000000",
        SaturdayFill = "#000000",
        SundayFill = "#000000",
        HolidayFill = "#3d3d1a",
        ChristmasFill = "#1a3d1a",
        NewYearFill = "#3d1a3d",
        CompanyEventFill = "#1a1a3d",
        VacationFill = "#1a3d3d",
        DependencyStroke = "#dd0000",
        DependencyArrow = "#ffd700",
        TodayLine = "#dd0000",
        TaskNameText = "#1a1a1a",
        DateText = "#4d4d4d",
        WeekendText = "#666666",
        ProcessFill = "#1a1a1a",
        ProcessStroke = "#ffd700",
        ProcessText = "#ffd700",
        ExternalEntityFill = "#dd0000",
        ExternalEntityStroke = "#ffd700",
        ExternalEntityText = "#ffffff",
        DataStoreFill = "#ffd700",
        DataStoreStroke = "#1a1a1a",
        DataStoreText = "#1a1a1a",
        DecisionFill = "#dd0000",
        DecisionStroke = "#ffd700",
        DecisionText = "#ffffff",
        NodeFill = "#2d2d2d",
        NodeStroke = "#ffd700",
        NodeText = "#ffd700",
        EdgeStroke = "#dd0000",
        EdgeArrow = "#ffd700",
        EdgeText = "#ffd700",
        CanvasBackground = "#1a1a1a",
        CanvasGrid = "#3d3d3d",
        RowColor1 = "#1a1a1a",
        RowColor2 = "#252525",
        RowColor3 = "#2d2d2d",
        RowColor4 = "#3d3d3d",
        RowColor5 = "#2d1a1a",
        RowColor6 = "#3d2a2a",
        RowColor7 = "#2d2d2d",
        RowColor8 = "#3d3d3d",
        RowColor9 = "#2d2d1a",
        RowColor10 = "#3d3d2a",
        GroupRowColor = "#dd0000"
    };

    private static DfdTheme CreateForestTheme() => new()
    {
        Name = "Forest",
        Description = "Natural greens and earth tones",
        TaskFill = "#2d6a4f",
        TaskStroke = "#1b4332",
        TaskText = "#ffffff",
        CriticalFill = "#ffccd5",
        CriticalStroke = "#ff758f",
        CriticalText = "#a4133c",
        MilestoneFill = "#74c69d",
        MilestoneStroke = "#40916c",
        GroupFill = "#1b4332",
        GroupStroke = "#95d5b2",
        GroupText = "#d8f3dc",
        SelectionStroke = "#52b788",
        SelectionFill = "rgba(82, 183, 136, 0.15)",
        HeaderFill = "#d8f3dc",
        HeaderStroke = "#b7e4c7",
        HeaderText = "#1b4332",
        GridLine = "#b7e4c7",
        WeekendFill = "#e9f5ec",
        SaturdayFill = "#d8f3dc",
        SundayFill = "#ffccd5",
        HolidayFill = "#fef3c7",
        ChristmasFill = "#b7e4c7",
        NewYearFill = "#e5dbff",
        CompanyEventFill = "#95d5b2",
        VacationFill = "#a7f3d0",
        DependencyStroke = "#40916c",
        DependencyArrow = "#2d6a4f",
        TodayLine = "#ff758f",
        TaskNameText = "#1b4332",
        DateText = "#52796f",
        WeekendText = "#74c69d",
        ProcessFill = "#d8f3dc",
        ProcessStroke = "#2d6a4f",
        ProcessText = "#1b4332",
        ExternalEntityFill = "#b7e4c7",
        ExternalEntityStroke = "#40916c",
        ExternalEntityText = "#1b4332",
        DataStoreFill = "#95d5b2",
        DataStoreStroke = "#2d6a4f",
        DataStoreText = "#1b4332",
        DecisionFill = "#74c69d",
        DecisionStroke = "#1b4332",
        DecisionText = "#1b4332",
        NodeFill = "#e9f5ec",
        NodeStroke = "#52b788",
        NodeText = "#1b4332",
        EdgeStroke = "#40916c",
        EdgeArrow = "#2d6a4f",
        EdgeText = "#1b4332",
        CanvasBackground = "#f0fdf4",
        CanvasGrid = "#d8f3dc",
        RowColor1 = "#f0fdf4",
        RowColor2 = "#e9f5ec",
        RowColor3 = "#d8f3dc",
        RowColor4 = "#b7e4c7",
        RowColor5 = "#95d5b2",
        RowColor6 = "#74c69d",
        RowColor7 = "#d8f3dc",
        RowColor8 = "#b7e4c7",
        RowColor9 = "#95d5b2",
        RowColor10 = "#74c69d",
        GroupRowColor = "#1b4332"
    };

    private static DfdTheme CreateOceanTheme() => new()
    {
        Name = "Ocean",
        Description = "Deep blues and teals inspired by the sea",
        TaskFill = "#0077b6",
        TaskStroke = "#023e8a",
        TaskText = "#ffffff",
        CriticalFill = "#ffd6a5",
        CriticalStroke = "#ffb347",
        CriticalText = "#7c2d12",
        MilestoneFill = "#00b4d8",
        MilestoneStroke = "#0096c7",
        GroupFill = "#03045e",
        GroupStroke = "#48cae4",
        GroupText = "#caf0f8",
        SelectionStroke = "#00b4d8",
        SelectionFill = "rgba(0, 180, 216, 0.15)",
        HeaderFill = "#caf0f8",
        HeaderStroke = "#90e0ef",
        HeaderText = "#03045e",
        GridLine = "#ade8f4",
        WeekendFill = "#e0f7fa",
        SaturdayFill = "#caf0f8",
        SundayFill = "#ffd6a5",
        HolidayFill = "#fef3c7",
        ChristmasFill = "#ade8f4",
        NewYearFill = "#e0e7ff",
        CompanyEventFill = "#90e0ef",
        VacationFill = "#a5f3fc",
        DependencyStroke = "#0096c7",
        DependencyArrow = "#0077b6",
        TodayLine = "#ff6b6b",
        TaskNameText = "#023e8a",
        DateText = "#0077b6",
        WeekendText = "#48cae4",
        ProcessFill = "#caf0f8",
        ProcessStroke = "#0077b6",
        ProcessText = "#023e8a",
        ExternalEntityFill = "#ade8f4",
        ExternalEntityStroke = "#0096c7",
        ExternalEntityText = "#03045e",
        DataStoreFill = "#90e0ef",
        DataStoreStroke = "#0077b6",
        DataStoreText = "#023e8a",
        DecisionFill = "#48cae4",
        DecisionStroke = "#03045e",
        DecisionText = "#03045e",
        NodeFill = "#e0f7fa",
        NodeStroke = "#00b4d8",
        NodeText = "#03045e",
        EdgeStroke = "#0096c7",
        EdgeArrow = "#0077b6",
        EdgeText = "#023e8a",
        CanvasBackground = "#f0fdff",
        CanvasGrid = "#caf0f8",
        RowColor1 = "#f0fdff",
        RowColor2 = "#e0f7fa",
        RowColor3 = "#caf0f8",
        RowColor4 = "#ade8f4",
        RowColor5 = "#90e0ef",
        RowColor6 = "#48cae4",
        RowColor7 = "#caf0f8",
        RowColor8 = "#ade8f4",
        RowColor9 = "#90e0ef",
        RowColor10 = "#48cae4",
        GroupRowColor = "#03045e"
    };

    private static DfdTheme CreateSunsetTheme() => new()
    {
        Name = "Sunset",
        Description = "Warm oranges, pinks, and purples",
        TaskFill = "#f72585",
        TaskStroke = "#b5179e",
        TaskText = "#ffffff",
        CriticalFill = "#ffd6a5",
        CriticalStroke = "#ff9500",
        CriticalText = "#7c2d12",
        MilestoneFill = "#7209b7",
        MilestoneStroke = "#560bad",
        GroupFill = "#3a0ca3",
        GroupStroke = "#f72585",
        GroupText = "#ffffff",
        SelectionStroke = "#f72585",
        SelectionFill = "rgba(247, 37, 133, 0.15)",
        HeaderFill = "#fae0e4",
        HeaderStroke = "#f9bec7",
        HeaderText = "#4a0e4e",
        GridLine = "#fcd5ce",
        WeekendFill = "#fff0f3",
        SaturdayFill = "#fae0e4",
        SundayFill = "#ffd6a5",
        HolidayFill = "#fef3c7",
        ChristmasFill = "#e0aaff",
        NewYearFill = "#c77dff",
        CompanyEventFill = "#f9bec7",
        VacationFill = "#fbcfe8",
        DependencyStroke = "#7209b7",
        DependencyArrow = "#b5179e",
        TodayLine = "#ff6d00",
        TaskNameText = "#560bad",
        DateText = "#7209b7",
        WeekendText = "#b5179e",
        ProcessFill = "#fae0e4",
        ProcessStroke = "#f72585",
        ProcessText = "#560bad",
        ExternalEntityFill = "#f9bec7",
        ExternalEntityStroke = "#b5179e",
        ExternalEntityText = "#3a0ca3",
        DataStoreFill = "#e0aaff",
        DataStoreStroke = "#7209b7",
        DataStoreText = "#3a0ca3",
        DecisionFill = "#c77dff",
        DecisionStroke = "#560bad",
        DecisionText = "#ffffff",
        NodeFill = "#fff0f3",
        NodeStroke = "#f72585",
        NodeText = "#4a0e4e",
        EdgeStroke = "#7209b7",
        EdgeArrow = "#b5179e",
        EdgeText = "#560bad",
        CanvasBackground = "#fff5f7",
        CanvasGrid = "#fae0e4",
        RowColor1 = "#fff5f7",
        RowColor2 = "#fff0f3",
        RowColor3 = "#fae0e4",
        RowColor4 = "#f9bec7",
        RowColor5 = "#e0aaff",
        RowColor6 = "#c77dff",
        RowColor7 = "#fae0e4",
        RowColor8 = "#f9bec7",
        RowColor9 = "#e0aaff",
        RowColor10 = "#c77dff",
        GroupRowColor = "#3a0ca3"
    };

    private static DfdTheme CreateMonochromeTheme() => new()
    {
        Name = "Monochrome",
        Description = "Clean black, white, and gray",
        TaskFill = "#212529",
        TaskStroke = "#000000",
        TaskText = "#ffffff",
        CriticalFill = "#e9ecef",
        CriticalStroke = "#6c757d",
        CriticalText = "#212529",
        MilestoneFill = "#495057",
        MilestoneStroke = "#343a40",
        GroupFill = "#000000",
        GroupStroke = "#6c757d",
        GroupText = "#ffffff",
        SelectionStroke = "#495057",
        SelectionFill = "rgba(73, 80, 87, 0.15)",
        HeaderFill = "#f8f9fa",
        HeaderStroke = "#dee2e6",
        HeaderText = "#212529",
        GridLine = "#e9ecef",
        WeekendFill = "#f1f3f5",
        SaturdayFill = "#e9ecef",
        SundayFill = "#dee2e6",
        HolidayFill = "#f8f9fa",
        ChristmasFill = "#e9ecef",
        NewYearFill = "#f8f9fa",
        CompanyEventFill = "#dee2e6",
        VacationFill = "#ced4da",
        DependencyStroke = "#6c757d",
        DependencyArrow = "#495057",
        TodayLine = "#212529",
        TaskNameText = "#212529",
        DateText = "#6c757d",
        WeekendText = "#adb5bd",
        ProcessFill = "#f8f9fa",
        ProcessStroke = "#212529",
        ProcessText = "#212529",
        ExternalEntityFill = "#e9ecef",
        ExternalEntityStroke = "#495057",
        ExternalEntityText = "#212529",
        DataStoreFill = "#dee2e6",
        DataStoreStroke = "#343a40",
        DataStoreText = "#212529",
        DecisionFill = "#ced4da",
        DecisionStroke = "#212529",
        DecisionText = "#212529",
        NodeFill = "#ffffff",
        NodeStroke = "#495057",
        NodeText = "#212529",
        EdgeStroke = "#495057",
        EdgeArrow = "#212529",
        EdgeText = "#495057",
        CanvasBackground = "#ffffff",
        CanvasGrid = "#e9ecef",
        RowColor1 = "#ffffff",
        RowColor2 = "#f8f9fa",
        RowColor3 = "#f1f3f5",
        RowColor4 = "#e9ecef",
        RowColor5 = "#dee2e6",
        RowColor6 = "#ced4da",
        RowColor7 = "#f1f3f5",
        RowColor8 = "#e9ecef",
        RowColor9 = "#dee2e6",
        RowColor10 = "#ced4da",
        GroupRowColor = "#000000"
    };

    private static DfdTheme CreateHighContrastTheme() => new()
    {
        Name = "High Contrast",
        Description = "Accessibility-focused high contrast colors",
        TaskFill = "#0000ff",
        TaskStroke = "#000080",
        TaskText = "#ffffff",
        CriticalFill = "#ffff00",
        CriticalStroke = "#ff0000",
        CriticalText = "#000000",
        MilestoneFill = "#00ff00",
        MilestoneStroke = "#008000",
        GroupFill = "#000000",
        GroupStroke = "#ffff00",
        GroupText = "#ffff00",
        SelectionStroke = "#ff00ff",
        SelectionFill = "rgba(255, 0, 255, 0.2)",
        HeaderFill = "#ffffff",
        HeaderStroke = "#000000",
        HeaderText = "#000000",
        GridLine = "#000000",
        WeekendFill = "#e0e0e0",
        SaturdayFill = "#c0c0c0",
        SundayFill = "#808080",
        HolidayFill = "#00ffff",
        ChristmasFill = "#00ff00",
        NewYearFill = "#ff00ff",
        CompanyEventFill = "#ffff00",
        VacationFill = "#00ffff",
        DependencyStroke = "#000000",
        DependencyArrow = "#000000",
        TodayLine = "#ff0000",
        TaskNameText = "#000080",
        DateText = "#000000",
        WeekendText = "#404040",
        ProcessFill = "#ffffff",
        ProcessStroke = "#0000ff",
        ProcessText = "#000000",
        ExternalEntityFill = "#ffff00",
        ExternalEntityStroke = "#000000",
        ExternalEntityText = "#000000",
        DataStoreFill = "#00ff00",
        DataStoreStroke = "#000000",
        DataStoreText = "#000000",
        DecisionFill = "#ff00ff",
        DecisionStroke = "#000000",
        DecisionText = "#000000",
        NodeFill = "#ffffff",
        NodeStroke = "#000000",
        NodeText = "#000000",
        EdgeStroke = "#000000",
        EdgeArrow = "#000000",
        EdgeText = "#000000",
        CanvasBackground = "#ffffff",
        CanvasGrid = "#c0c0c0",
        RowColor1 = "#ffffff",
        RowColor2 = "#e0e0e0",
        RowColor3 = "#c0c0c0",
        RowColor4 = "#a0a0a0",
        RowColor5 = "#ffff00",
        RowColor6 = "#00ffff",
        RowColor7 = "#c0c0c0",
        RowColor8 = "#a0a0a0",
        RowColor9 = "#ffff00",
        RowColor10 = "#00ffff",
        GroupRowColor = "#000000"
    };

    private static DfdTheme CreateTartanTheme() => new()
    {
        Name = "Tartan Classic",
        Description = "More complex tartan-inspired theme with layered greens, navy, charcoal, and gold accents",
        TaskFill = "#0B5F2A",
        TaskStroke = "#06451F",
        TaskText = "#ffffff",
        CriticalFill = "#FFE4E6",
        CriticalStroke = "#E11D48",
        CriticalText = "#7F1D1D",
        MilestoneFill = "#E0B84A",
        MilestoneStroke = "#B88910",
        GroupFill = "#0A1B12",
        GroupStroke = "#E0B84A",
        GroupText = "#E0B84A",
        SelectionStroke = "#E0B84A",
        SelectionFill = "rgba(224, 184, 74, 0.2)",
        HeaderFill = "#F3F4F6",
        HeaderStroke = "#123A2A",
        HeaderText = "#111827",
        GridLine = "#CFE3D6",
        WeekendFill = "#E6F0E9",
        SaturdayFill = "#CFE3D6",
        SundayFill = "#CFE3D6",
        HolidayFill = "#FEF3C7",
        ChristmasFill = "#DCFCE7",
        NewYearFill = "#DBEAFE",
        CompanyEventFill = "#F3E8FF",
        VacationFill = "#CFFAFE",
        DependencyStroke = "#0B5F2A",
        DependencyArrow = "#0B5F2A",
        TodayLine = "#DC2626",
        TaskNameText = "#0B5F2A",
        DateText = "#111827",
        WeekendText = "#6B7280",
        ProcessFill = "#E6F0E9",
        ProcessStroke = "#0B5F2A",
        ProcessText = "#0A1B12",
        ExternalEntityFill = "#FEF3C7",
        ExternalEntityStroke = "#B88910",
        ExternalEntityText = "#0A1B12",
        DataStoreFill = "#0B1E3B",
        DataStoreStroke = "#E0B84A",
        DataStoreText = "#E0B84A",
        DecisionFill = "#E0B84A",
        DecisionStroke = "#B88910",
        DecisionText = "#0A1B12",
        NodeFill = "#F4FAF6",
        NodeStroke = "#0B5F2A",
        NodeText = "#0A1B12",
        EdgeStroke = "#0B5F2A",
        EdgeArrow = "#0B5F2A",
        EdgeText = "#111827",
        CanvasBackground = "#F4FAF6",
        CanvasGrid = "#CFE3D6",
        RowColor1 = "#F4FAF6",
        RowColor2 = "#E6F0E9",
        RowColor3 = "#CFE3D6",
        RowColor4 = "#E6F0E9",
        RowColor5 = "#F4FAF6",
        RowColor6 = "#E6F0E9",
        RowColor7 = "#CFE3D6",
        RowColor8 = "#E6F0E9",
        RowColor9 = "#F4FAF6",
        RowColor10 = "#E6F0E9",
        GroupRowColor = "#0A1B12"
    };

    #endregion
}

/// <summary>
/// Configuration file structure for themes.json
/// </summary>
public class ThemeConfig
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("defaultTheme")]
    public string? DefaultTheme { get; set; }

    [JsonPropertyName("themes")]
    public Dictionary<string, ThemeData>? Themes { get; set; }
}

/// <summary>
/// JSON-serializable theme data (camelCase property names)
/// </summary>
public class ThemeData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("taskFill")]
    public string? TaskFill { get; set; }
    [JsonPropertyName("taskStroke")]
    public string? TaskStroke { get; set; }
    [JsonPropertyName("taskText")]
    public string? TaskText { get; set; }
    [JsonPropertyName("criticalFill")]
    public string? CriticalFill { get; set; }
    [JsonPropertyName("criticalStroke")]
    public string? CriticalStroke { get; set; }
    [JsonPropertyName("criticalText")]
    public string? CriticalText { get; set; }
    [JsonPropertyName("milestoneFill")]
    public string? MilestoneFill { get; set; }
    [JsonPropertyName("milestoneStroke")]
    public string? MilestoneStroke { get; set; }
    [JsonPropertyName("groupFill")]
    public string? GroupFill { get; set; }
    [JsonPropertyName("groupStroke")]
    public string? GroupStroke { get; set; }
    [JsonPropertyName("groupText")]
    public string? GroupText { get; set; }
    [JsonPropertyName("selectionStroke")]
    public string? SelectionStroke { get; set; }
    [JsonPropertyName("selectionFill")]
    public string? SelectionFill { get; set; }
    [JsonPropertyName("headerFill")]
    public string? HeaderFill { get; set; }
    [JsonPropertyName("headerStroke")]
    public string? HeaderStroke { get; set; }
    [JsonPropertyName("headerText")]
    public string? HeaderText { get; set; }
    [JsonPropertyName("gridLine")]
    public string? GridLine { get; set; }
    [JsonPropertyName("weekendFill")]
    public string? WeekendFill { get; set; }
    [JsonPropertyName("saturdayFill")]
    public string? SaturdayFill { get; set; }
    [JsonPropertyName("sundayFill")]
    public string? SundayFill { get; set; }
    [JsonPropertyName("holidayFill")]
    public string? HolidayFill { get; set; }
    [JsonPropertyName("christmasFill")]
    public string? ChristmasFill { get; set; }
    [JsonPropertyName("newYearFill")]
    public string? NewYearFill { get; set; }
    [JsonPropertyName("companyEventFill")]
    public string? CompanyEventFill { get; set; }
    [JsonPropertyName("vacationFill")]
    public string? VacationFill { get; set; }
    [JsonPropertyName("dependencyStroke")]
    public string? DependencyStroke { get; set; }
    [JsonPropertyName("dependencyArrow")]
    public string? DependencyArrow { get; set; }
    [JsonPropertyName("todayLine")]
    public string? TodayLine { get; set; }
    [JsonPropertyName("taskNameText")]
    public string? TaskNameText { get; set; }
    [JsonPropertyName("dateText")]
    public string? DateText { get; set; }
    [JsonPropertyName("weekendText")]
    public string? WeekendText { get; set; }
    [JsonPropertyName("processFill")]
    public string? ProcessFill { get; set; }
    [JsonPropertyName("processStroke")]
    public string? ProcessStroke { get; set; }
    [JsonPropertyName("processText")]
    public string? ProcessText { get; set; }
    [JsonPropertyName("externalEntityFill")]
    public string? ExternalEntityFill { get; set; }
    [JsonPropertyName("externalEntityStroke")]
    public string? ExternalEntityStroke { get; set; }
    [JsonPropertyName("externalEntityText")]
    public string? ExternalEntityText { get; set; }
    [JsonPropertyName("dataStoreFill")]
    public string? DataStoreFill { get; set; }
    [JsonPropertyName("dataStoreStroke")]
    public string? DataStoreStroke { get; set; }
    [JsonPropertyName("dataStoreText")]
    public string? DataStoreText { get; set; }
    [JsonPropertyName("decisionFill")]
    public string? DecisionFill { get; set; }
    [JsonPropertyName("decisionStroke")]
    public string? DecisionStroke { get; set; }
    [JsonPropertyName("decisionText")]
    public string? DecisionText { get; set; }
    [JsonPropertyName("nodeFill")]
    public string? NodeFill { get; set; }
    [JsonPropertyName("nodeStroke")]
    public string? NodeStroke { get; set; }
    [JsonPropertyName("nodeText")]
    public string? NodeText { get; set; }
    [JsonPropertyName("edgeStroke")]
    public string? EdgeStroke { get; set; }
    [JsonPropertyName("edgeArrow")]
    public string? EdgeArrow { get; set; }
    [JsonPropertyName("edgeText")]
    public string? EdgeText { get; set; }
    [JsonPropertyName("canvasBackground")]
    public string? CanvasBackground { get; set; }
    [JsonPropertyName("canvasGrid")]
    public string? CanvasGrid { get; set; }

    // Project Row Colors
    [JsonPropertyName("rowColor1")]
    public string? RowColor1 { get; set; }
    [JsonPropertyName("rowColor2")]
    public string? RowColor2 { get; set; }
    [JsonPropertyName("rowColor3")]
    public string? RowColor3 { get; set; }
    [JsonPropertyName("rowColor4")]
    public string? RowColor4 { get; set; }
    [JsonPropertyName("rowColor5")]
    public string? RowColor5 { get; set; }
    [JsonPropertyName("rowColor6")]
    public string? RowColor6 { get; set; }
    [JsonPropertyName("rowColor7")]
    public string? RowColor7 { get; set; }
    [JsonPropertyName("rowColor8")]
    public string? RowColor8 { get; set; }
    [JsonPropertyName("rowColor9")]
    public string? RowColor9 { get; set; }
    [JsonPropertyName("rowColor10")]
    public string? RowColor10 { get; set; }
    [JsonPropertyName("groupRowColor")]
    public string? GroupRowColor { get; set; }
}

/// <summary>
/// Represents a complete color theme for DFD diagrams and Project charts
/// </summary>
public class DfdTheme
{
    public string Name { get; set; } = "Default";
    public string Description { get; set; } = "";

    // Task bar colors
    public string TaskFill { get; set; } = "#3b82f6";
    public string TaskStroke { get; set; } = "#1d4ed8";
    public string TaskText { get; set; } = "#ffffff";

    // Critical path colors
    public string CriticalFill { get; set; } = "#fee2e2";
    public string CriticalStroke { get; set; } = "#ef4444";
    public string CriticalText { get; set; } = "#991b1b";

    // Milestone colors
    public string MilestoneFill { get; set; } = "#8b5cf6";
    public string MilestoneStroke { get; set; } = "#6d28d9";

    // Group/Summary colors
    public string GroupFill { get; set; } = "#1f2937";
    public string GroupStroke { get; set; } = "#f59e0b";
    public string GroupText { get; set; } = "#ffffff";

    // Selection colors
    public string SelectionStroke { get; set; } = "#3b82f6";
    public string SelectionFill { get; set; } = "rgba(59, 130, 246, 0.1)";

    // Header and grid colors
    public string HeaderFill { get; set; } = "#e2e8f0";
    public string HeaderStroke { get; set; } = "#cbd5e1";
    public string HeaderText { get; set; } = "#374151";
    public string GridLine { get; set; } = "#e2e8f0";
    public string WeekendFill { get; set; } = "#f1f5f9";

    // Calendar day colors
    public string SaturdayFill { get; set; } = "#f1f5f9";
    public string SundayFill { get; set; } = "#fee2e2";
    public string HolidayFill { get; set; } = "#fef3c7";
    public string ChristmasFill { get; set; } = "#dcfce7";
    public string NewYearFill { get; set; } = "#fae8ff";
    public string CompanyEventFill { get; set; } = "#dbeafe";
    public string VacationFill { get; set; } = "#e0f2fe";

    // Dependency line colors
    public string DependencyStroke { get; set; } = "#475569";
    public string DependencyArrow { get; set; } = "#374151";

    // Today line
    public string TodayLine { get; set; } = "#ef4444";

    // Text colors
    public string TaskNameText { get; set; } = "#1e40af";
    public string DateText { get; set; } = "#64748b";
    public string WeekendText { get; set; } = "#94a3b8";

    // ========== DFD Node Colors ==========
    // Process nodes (ellipse/rounded rect)
    public string ProcessFill { get; set; } = "#dbeafe";
    public string ProcessStroke { get; set; } = "#3b82f6";
    public string ProcessText { get; set; } = "#1e40af";

    // External Entity nodes (rectangle)
    public string ExternalEntityFill { get; set; } = "#f3e8ff";
    public string ExternalEntityStroke { get; set; } = "#8b5cf6";
    public string ExternalEntityText { get; set; } = "#5b21b6";

    // Data Store nodes (cylinder/open rectangle)
    public string DataStoreFill { get; set; } = "#fef3c7";
    public string DataStoreStroke { get; set; } = "#f59e0b";
    public string DataStoreText { get; set; } = "#92400e";

    // Decision nodes (diamond)
    public string DecisionFill { get; set; } = "#fef3c7";
    public string DecisionStroke { get; set; } = "#f59e0b";
    public string DecisionText { get; set; } = "#92400e";

    // Default/Generic nodes
    public string NodeFill { get; set; } = "#ffffff";
    public string NodeStroke { get; set; } = "#374151";
    public string NodeText { get; set; } = "#1f2937";

    // Edge/Arrow colors
    public string EdgeStroke { get; set; } = "#374151";
    public string EdgeArrow { get; set; } = "#374151";
    public string EdgeText { get; set; } = "#4b5563";

    // Canvas background
    public string CanvasBackground { get; set; } = "#ffffff";
    public string CanvasGrid { get; set; } = "#e5e7eb";

    // ========== Project Row Colors ==========
    // Alternating row background colors (10 colors for variety)
    public string RowColor1 { get; set; } = "#ffffff";
    public string RowColor2 { get; set; } = "#f8fafc";
    public string RowColor3 { get; set; } = "#f1f5f9";
    public string RowColor4 { get; set; } = "#e2e8f0";
    public string RowColor5 { get; set; } = "#ffffff";
    public string RowColor6 { get; set; } = "#f8fafc";
    public string RowColor7 { get; set; } = "#f1f5f9";
    public string RowColor8 { get; set; } = "#e2e8f0";
    public string RowColor9 { get; set; } = "#ffffff";
    public string RowColor10 { get; set; } = "#f8fafc";

    // Group row background color (for group/summary tasks)
    public string GroupRowColor { get; set; } = "#e0e7ff";

    /// <summary>
    /// Gets the row color for a given row index (0-based).
    /// Returns GroupRowColor for group tasks, otherwise cycles through RowColor1-10.
    /// </summary>
    /// <param name="rowIndex">The 0-based row index</param>
    /// <param name="isGroup">True if this is a group/summary task</param>
    /// <returns>The color string for the row background</returns>
    public string GetRowColor(int rowIndex, bool isGroup = false)
    {
        if (isGroup)
            return GroupRowColor;

        // Cycle through the 10 row colors based on row index
        return (rowIndex % 10) switch
        {
            0 => RowColor1,
            1 => RowColor2,
            2 => RowColor3,
            3 => RowColor4,
            4 => RowColor5,
            5 => RowColor6,
            6 => RowColor7,
            7 => RowColor8,
            8 => RowColor9,
            _ => RowColor10
        };
    }
}

/// <summary>
/// Gantt timeline theme colors derived from the current DfdTheme
/// </summary>
public class GanttThemeColors
{
    // Header
    public string HeaderFill { get; set; } = "#f1f5f9";
    public string HeaderStroke { get; set; } = "#e2e8f0";
    public string HeaderText { get; set; } = "#374151";

    // Grid lines
    public string GridLineMajor { get; set; } = "#d1d5db";
    public string GridLineMinor { get; set; } = "#e5e7eb";

    // Machine rows
    public string RowEvenFill { get; set; } = "#ffffff";
    public string RowOddFill { get; set; } = "#f8fafc";
    public string MachineLabelFill { get; set; } = "#f1f5f9";
    public string MachineLabelStroke { get; set; } = "#e2e8f0";
    public string MachineLabelText { get; set; } = "#374151";

    // Dependencies
    public string DependencyStroke { get; set; } = "#6b7280";
    public string DependencyArrow { get; set; } = "#6b7280";
    public string ViolationStroke { get; set; } = "#ef4444";

    // Indicators
    public string TodayLine { get; set; } = "#ef4444";
    public string SelectionStroke { get; set; } = "#3b82f6";
    public string SelectionFill { get; set; } = "rgba(59, 130, 246, 0.1)";

    // Canvas
    public string CanvasBackground { get; set; } = "#fafafa";

    // Default task colors (used when task doesn't have explicit colors)
    public string TaskDefaultFill { get; set; } = "#3b82f6";
    public string TaskDefaultStroke { get; set; } = "#1d4ed8";
    public string TaskDefaultText { get; set; } = "#ffffff";

    // Terminals
    public string TerminalInputColor { get; set; } = "#22c55e";
    public string TerminalOutputColor { get; set; } = "#ef4444";

    // Add button
    public string AddButtonFill { get; set; } = "#10b981";
    public string AddButtonText { get; set; } = "#ffffff";
}
