namespace dfd2wasm.Models;

/// <summary>
/// Types of grouping relationships for nodes
/// </summary>
public enum GroupingType
{
    /// <summary>
    /// Containment grouping - nodes are visually grouped as peers inside a container.
    /// No hierarchy implied. Used for flowcharts, circuit diagrams, visual organization.
    /// </summary>
    Containment,

    /// <summary>
    /// Hierarchical grouping - parent-child relationship where the parent summarizes children.
    /// Used for Project charts (Phase->Tasks), org charts, WBS structures.
    /// </summary>
    Hierarchical
}

/// <summary>
/// Types of Project resources with associated icons
/// </summary>
public enum ProjectResourceType
{
    Person,         // Human resource
    Team,           // Team/group of people
    Equipment,      // General equipment
    Vehicle,        // Vehicle/transport
    Machine,        // Heavy machinery
    Tool,           // Hand tool
    Material,       // Materials/supplies
    Room,           // Room/space
    Computer,       // Computer/IT equipment
    Custom          // User-defined
}

public class Node
{
    public int Id { get; init; }
    public string Text { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 120;
    public double Height { get; set; } = 60;
    public NodeShape Shape { get; set; } = NodeShape.Rectangle;
    public string StrokeColor { get; set; } = "#475569";
    public string? Icon { get; set; } = null; // Icon identifier (e.g., "user", "database", "cloud")
    public string? FillColor { get; set; }
    public int? StrokeWidth { get; set; }
    public string? StrokeDashArray { get; set; }
    public int? CornerRadius { get; set; }  // For rectangles: 0=square, 4=small, 8=medium, etc.
    // Optional template id and shape id allow using a shape library (e.g. "flowchart", "circuit").
    // When set, the renderer will prefer the template shape over the `NodeShape` enum.
    public string? TemplateId { get; set; }
    public string? TemplateShapeId { get; set; }
    // Component-specific label (e.g., "R1", "C2", "L1" for circuit components)
    public string? ComponentLabel { get; set; }
    // Component value (e.g., "10kΩ", "100µF", "5V")
    public string? ComponentValue { get; set; }

    // Attachments (SVG/PDF files embedded as data URIs)
    public List<NodeAttachment>? Attachments { get; set; }

    // Generic data dictionary for custom node properties (simulation, etc.)
    public Dictionary<string, string>? Data { get; set; }

    // STS (Source-Terminal-Sink) style properties
    // Terminal layout determines where input/output terminals are positioned (legacy)
    public string TerminalLayout { get; set; } = "left-right";
    // Whether to show terminal circles on the node
    public bool ShowTerminals { get; set; } = false;
    // List of terminal instances on this node (populated from template)
    public List<NodeTerminal>? Terminals { get; set; }
    // Extra terminals added by user (beyond the default input/output pair)
    public List<ExtraTerminal> ExtraTerminals { get; set; } = new();
    // Custom terminal colors (null = use defaults based on terminal type)
    public string? InputTerminalColor { get; set; }
    public string? OutputTerminalColor { get; set; }

    // Terminal connectivity types for the default input/output terminals
    // These determine connection rules: Input can connect TO Output, General can connect to General
    public TerminalType InputTerminalType { get; set; } = TerminalType.Input;
    public TerminalType OutputTerminalType { get; set; } = TerminalType.Output;

    // Dynamic terminal counts - for nodes with configurable N inputs / M outputs
    // (e.g., splitter: 1 in, N out; collector: N in, 1 out; general: N in, M out)
    public int InputTerminalCount { get; set; } = 1;
    public int OutputTerminalCount { get; set; } = 1;

    // Precise terminal positions (normalized 0-1 coordinates)
    // X: 0=left edge, 1=right edge; Y: 0=top edge, 1=bottom edge
    // null values = use TerminalLayout for backward compatibility
    public double? T1X { get; set; }  // Input terminal X
    public double? T1Y { get; set; }  // Input terminal Y
    public double? T2X { get; set; }  // Output terminal X
    public double? T2Y { get; set; }  // Output terminal Y
    public double? T3X { get; set; }  // Third terminal X
    public double? T3Y { get; set; }  // Third terminal Y

    // Third terminal support (for transistors, op-amps, etc.)
    // When true, a third terminal is shown at the bottom (or top based on layout)
    public bool HasThirdTerminal { get; set; } = false;
    public TerminalType ThirdTerminalType { get; set; } = TerminalType.Bidirectional;
    public string? ThirdTerminalColor { get; set; }

    // Rotation angle in degrees (0, 90, 180, 270)
    public int Rotation { get; set; } = 0;

    // SuperNode support - allows grouping multiple nodes into a single collapsed node
    public bool IsSuperNode { get; set; } = false;
    public List<int> ContainedNodeIds { get; set; } = new();
    public bool IsCollapsed { get; set; } = true;  // When true, children are hidden
    public int? ParentSuperNodeId { get; set; }     // For child nodes - references parent SuperNode

    // Grouping type - determines the relationship between parent and children
    // Containment: visual grouping, no hierarchy (flowcharts, circuits)
    // Hierarchical: parent-child relationship (Project, org charts)
    public GroupingType GroupType { get; set; } = GroupingType.Containment;

    // Project chart properties (used when TemplateId == "project")
    public DateTime? ProjectStartDate { get; set; }
    public DateTime? ProjectEndDate { get; set; }
    public int ProjectDurationDays { get; set; } = 0;
    public int ProjectPercentComplete { get; set; } = 0;
    public bool ProjectIsMilestone { get; set; } = false;
    public string? ProjectAssignedTo { get; set; }
    public int ProjectPriority { get; set; } = 0;
    public string? ProjectNotes { get; set; }
    public int ProjectRowIndex { get; set; } = -1;  // Position in timeline (-1 = auto-calculate)
    public decimal ProjectCostPerDay { get; set; } = 100m;  // Cost per day (default $100/day, aggregated from resources)

    // Task compression/crashing properties (for solver optimization)
    public bool ProjectAllowCompression { get; set; } = false;  // Whether this task can be compressed
    public int ProjectMinDuration { get; set; } = 1;            // Minimum duration if compressed (days)
    public int ProjectMaxDuration { get; set; } = 0;            // Maximum duration (0 = use current duration)
    public decimal ProjectCrashCostPerDay { get; set; } = 50m;  // Additional cost per day of compression

    // Project resource properties (used when TemplateShapeId == "resource")
    public bool IsProjectResource { get; set; } = false;
    public ProjectResourceType ProjectResourceType { get; set; } = ProjectResourceType.Person;
    public List<int>? ProjectAssignedTaskIds { get; set; }  // Tasks this resource is assigned to
    public int ProjectResourceCapacity { get; set; } = 100;  // Availability percentage (100 = full time)
    public decimal ProjectResourceRate { get; set; } = 100m; // Hourly/daily rate for costing
    public string? ProjectResourceEmail { get; set; }        // Contact email for person resources
    public decimal ProjectResourceCostPerHour { get; set; } = 0m; // Cost per hour (alias for rate, for import/export)

    // Task property: list of resource IDs assigned to this task
    public List<int>? ProjectAssignedResourceIds { get; set; }  // Resources assigned to this task

    // Custom shape support (user-created shapes that appear in dropdown)
    public bool IsCustomShape { get; set; } = false;
    public string? CustomShapeIcon { get; set; }  // Icon identifier from preset list

    // ============================================
    // Gantt machine scheduling properties
    // (used when TemplateId == "gantt")
    // ============================================

    /// <summary>
    /// Start time from the beginning of the schedule (e.g., 0:00 = start, 2:30 = 2.5 hours in).
    /// </summary>
    public TimeSpan? GanttStartTime { get; set; }

    /// <summary>
    /// Duration of the task (processing time).
    /// </summary>
    public TimeSpan? GanttDuration { get; set; }

    /// <summary>
    /// Calculated end time (GanttStartTime + GanttDuration).
    /// </summary>
    public TimeSpan? GanttEndTime => GanttStartTime.HasValue && GanttDuration.HasValue
        ? GanttStartTime.Value + GanttDuration.Value
        : null;

    /// <summary>
    /// The job this task belongs to (for task nodes). All tasks in a job share the same color.
    /// </summary>
    public int? GanttJobId { get; set; }

    /// <summary>
    /// The machine this task is assigned to (for task nodes).
    /// </summary>
    public int? GanttMachineId { get; set; }

    /// <summary>
    /// Row index in the timeline view (based on machine assignment).
    /// </summary>
    public int GanttRowIndex { get; set; } = -1;

    /// <summary>
    /// True if this task has a precedence violation (starts before predecessor ends).
    /// </summary>
    public bool GanttIsViolation { get; set; }

    /// <summary>
    /// True if this node represents a Job (grouping of tasks with shared color).
    /// </summary>
    public bool IsGanttJob { get; set; }

    /// <summary>
    /// True if this node represents a Machine (row in timeline).
    /// </summary>
    public bool IsGanttMachine { get; set; }

    /// <summary>
    /// True if this node represents a schedulable Task.
    /// </summary>
    public bool IsGanttTask { get; set; }

    /// <summary>
    /// Percentage complete (0-100) for Gantt tasks.
    /// </summary>
    public int GanttPercentComplete { get; set; }

    /// <summary>
    /// Task priority for scheduling (higher = more important).
    /// </summary>
    public int GanttPriority { get; set; }

    /// <summary>
    /// Processing time in minutes (for scheduling algorithms like SPT).
    /// </summary>
    public double GanttProcessingTimeMinutes => GanttDuration?.TotalMinutes ?? 0;

    /// <summary>
    /// Machine type (for machine nodes).
    /// </summary>
    public GanttMachineType GanttMachineType { get; set; } = GanttMachineType.Machine;

    /// <summary>
    /// Job color (for job nodes - tasks inherit this color).
    /// </summary>
    public string? GanttJobColor { get; set; }

    /// <summary>
    /// Due time (deadline) for jobs.
    /// </summary>
    public TimeSpan? GanttDueTime { get; set; }

    /// <summary>
    /// Release time - earliest time processing can begin.
    /// </summary>
    public TimeSpan? GanttReleaseTime { get; set; }

    // ============================================
    // QMaker queueing network properties
    // (used when TemplateId == "qmaker")
    // ============================================

    /// <summary>
    /// Queue capacity (K). null or 0 = infinite capacity (∞).
    /// </summary>
    public int? QueueCapacity { get; set; }

    /// <summary>
    /// Arrival rate λ (lambda) - average arrivals per time unit.
    /// Used for source nodes.
    /// </summary>
    public double? QueueArrivalRate { get; set; }

    /// <summary>
    /// Service rate μ (mu) - average services completed per time unit per server.
    /// </summary>
    public double? QueueServiceRate { get; set; }

    /// <summary>
    /// Number of parallel servers (c). Default = 1 for M/M/1, >1 for M/M/c.
    /// </summary>
    public int QueueServerCount { get; set; } = 1;

    /// <summary>
    /// Queue discipline: FIFO (default), LIFO, Priority, Random.
    /// </summary>
    public QueueDiscipline QueueDiscipline { get; set; } = QueueDiscipline.FIFO;

    /// <summary>
    /// Interarrival time distribution type (legacy - use ArrivalDistribution for full config).
    /// </summary>
    public QueueDistribution QueueArrivalDistribution { get; set; } = QueueDistribution.Exponential;

    /// <summary>
    /// Service time distribution type (legacy - use ServiceDistribution for full config).
    /// </summary>
    public QueueDistribution QueueServiceDistribution { get; set; } = QueueDistribution.Exponential;

    /// <summary>
    /// Full arrival distribution configuration with parameters.
    /// </summary>
    public DistributionConfig? ArrivalDistribution { get; set; }

    /// <summary>
    /// Full service distribution configuration with parameters.
    /// </summary>
    public DistributionConfig? ServiceDistribution { get; set; }

    /// <summary>
    /// Routing probability for router nodes (0.0 to 1.0).
    /// For multi-output routers, use RoutingProbabilities list.
    /// </summary>
    public double? QueueRoutingProbability { get; set; }

    /// <summary>
    /// Routing probabilities for each output terminal (for routers/splitters with N outputs).
    /// Index 0 = first output, Index 1 = second output, etc.
    /// Probabilities should sum to 1.0.
    /// </summary>
    public List<double>? RoutingProbabilities { get; set; }

    /// <summary>
    /// Node type in queueing network context.
    /// </summary>
    public QueueNodeType QueueNodeType { get; set; } = QueueNodeType.QueueServer;
}

/// <summary>
/// Represents an attached file (SVG or PDF) embedded in a node
/// </summary>
public class NodeAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string FileName { get; set; } = string.Empty;
    public AttachmentType FileType { get; set; }
    public string DataUri { get; set; } = string.Empty;  // Base64-encoded data URI
    public double DisplayWidth { get; set; } = 80;
    public double DisplayHeight { get; set; } = 80;
}

public enum AttachmentType
{
    Svg,
    Pdf,
    Image  // GIF, JPEG, PNG, WebP
}

/// <summary>
/// Queue discipline - order in which customers are served
/// </summary>
public enum QueueDiscipline
{
    FIFO,       // First In First Out (default)
    LIFO,       // Last In First Out (stack)
    Priority,   // Priority-based service
    Random      // Random selection
}

/// <summary>
/// Probability distribution type for arrivals/services
/// </summary>
public enum DistributionType
{
    Exponential,   // M - Markovian (memoryless), Param1 = rate λ
    Deterministic, // D - Fixed/constant value, Param1 = value
    Normal,        // Normal/Gaussian, Param1 = mean μ, Param2 = stddev σ
    Uniform,       // Uniform, Param1 = min, Param2 = max
    Erlang,        // Erlang-k, Param1 = shape k, Param2 = rate λ
    Triangular,    // Triangular, Param1 = min, Param2 = mode, Param3 = max
    LogNormal,     // Log-normal, Param1 = μ, Param2 = σ
    Weibull,       // Weibull, Param1 = shape k, Param2 = scale λ
    Gamma,         // Gamma, Param1 = shape α, Param2 = rate β
    // Discrete distributions for simulation
    Constant,      // Alias for Deterministic - fixed constant value
    Poisson,       // Poisson distribution, Param1 = λ (mean)
    Binomial       // Binomial distribution, Param1 = n (trials), Param2 = p (probability)
}

/// <summary>
/// Legacy enum for backward compatibility - maps to DistributionType
/// </summary>
public enum QueueDistribution
{
    Exponential,  // M - Markovian (memoryless)
    Deterministic, // D - Fixed/constant
    General,      // G - General distribution
    Erlang,       // Erlang-k
    Uniform       // Uniform distribution
}

/// <summary>
/// Configuration for a probability distribution with its parameters
/// </summary>
public class DistributionConfig
{
    /// <summary>
    /// Type of distribution
    /// </summary>
    public DistributionType Type { get; set; } = DistributionType.Exponential;

    /// <summary>
    /// Primary parameter (rate for Exponential, mean for Normal, min for Uniform, etc.)
    /// </summary>
    public double Param1 { get; set; } = 1.0;

    /// <summary>
    /// Secondary parameter (stddev for Normal, max for Uniform, rate for Erlang, etc.)
    /// </summary>
    public double? Param2 { get; set; }

    /// <summary>
    /// Tertiary parameter (mode for Triangular)
    /// </summary>
    public double? Param3 { get; set; }

    /// <summary>
    /// Get Kendall notation letter for this distribution
    /// </summary>
    public string GetKendallCode() => Type switch
    {
        DistributionType.Exponential => "M",
        DistributionType.Deterministic => "D",
        DistributionType.Erlang => $"E{(int)Param1}",
        DistributionType.Normal => "G",
        DistributionType.Uniform => "G",
        DistributionType.Triangular => "G",
        DistributionType.LogNormal => "G",
        DistributionType.Weibull => "G",
        DistributionType.Gamma => "G",
        _ => "G"
    };

    /// <summary>
    /// Get human-readable description of this distribution
    /// </summary>
    public string GetDescription() => Type switch
    {
        DistributionType.Exponential => $"Exp(λ={Param1:F2})",
        DistributionType.Deterministic => $"Const({Param1:F2})",
        DistributionType.Normal => $"N(μ={Param1:F2}, σ={Param2:F2})",
        DistributionType.Uniform => $"U({Param1:F2}, {Param2:F2})",
        DistributionType.Erlang => $"Erlang(k={(int)Param1}, λ={Param2:F2})",
        DistributionType.Triangular => $"Tri({Param1:F2}, {Param2:F2}, {Param3:F2})",
        DistributionType.LogNormal => $"LogN(μ={Param1:F2}, σ={Param2:F2})",
        DistributionType.Weibull => $"Weibull(k={Param1:F2}, λ={Param2:F2})",
        DistributionType.Gamma => $"Γ(α={Param1:F2}, β={Param2:F2})",
        _ => "Unknown"
    };

    /// <summary>
    /// Calculate mean of this distribution
    /// </summary>
    public double GetMean() => Type switch
    {
        DistributionType.Exponential => 1.0 / Param1,
        DistributionType.Deterministic => Param1,
        DistributionType.Normal => Param1,
        DistributionType.Uniform => (Param1 + (Param2 ?? Param1)) / 2.0,
        DistributionType.Erlang => Param1 / (Param2 ?? 1.0),
        DistributionType.Triangular => (Param1 + (Param2 ?? Param1) + (Param3 ?? Param1)) / 3.0,
        _ => Param1
    };
}

/// <summary>
/// Node type in queueing network
/// </summary>
public enum QueueNodeType
{
    QueueServer,  // Combined queue + server (most common)
    Queue,        // Buffer/queue only
    Server,       // Server only
    Source,       // Arrival source (λ)
    Sink,         // Departure sink
    Splitter,     // Split flow (1 in, N out) - was Fork
    Collector,    // Merge flows (N in, 1 out) - was Join
    Router,       // Probabilistic routing (1 in, N out with probabilities)
    Delay,        // Infinite server (delay station)
    Fork,         // Legacy alias for Splitter
    Join          // Legacy alias for Collector
}

/// <summary>
/// Represents an extra terminal added by the user to a node
/// </summary>
public class ExtraTerminal
{
    /// <summary>
    /// Unique identifier for this terminal within the node
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Position on the node edge: left, right, top, bottom
    /// </summary>
    public string Side { get; set; } = "left";

    /// <summary>
    /// Position along the side (0 = center, negative = toward start, positive = toward end)
    /// </summary>
    public double Position { get; set; } = 0;

    /// <summary>
    /// Terminal type: Input (green), Output (red), or Bidirectional (blue)
    /// </summary>
    public TerminalType Type { get; set; } = TerminalType.Input;

    /// <summary>
    /// Optional label for the terminal
    /// </summary>
    public string? Label { get; set; }
}

public enum NodeShape
{
    Rectangle,
    Ellipse,
    Diamond,
    Parallelogram,
    Cylinder
}
