using System.Text.Json;
using System.Text.Json.Serialization;

namespace dfd2wasm.Models;

/// <summary>
/// Type of simulation node
/// </summary>
public enum SimulationNodeType
{
    Generator,
    Sink,
    Clock,
    Chance,
    Counter,
    Dashboard
}

/// <summary>
/// How timing is specified for entity generation
/// </summary>
public enum TimingMode
{
    /// <summary>Time between successive entity arrivals (continuous)</summary>
    InterArrival,
    /// <summary>Number of entities per time unit (discrete)</summary>
    EntitiesPerUnit
}

// Note: DistributionType enum is defined in Node.cs (shared with queueing network models)

/// <summary>
/// When to stop generating entities
/// </summary>
public enum TerminationCondition
{
    /// <summary>Generate forever</summary>
    Infinite,
    /// <summary>Stop after N entities</summary>
    Count,
    /// <summary>Stop at time T</summary>
    Time,
    /// <summary>Stop at whichever comes first</summary>
    CountOrTime
}

/// <summary>
/// Configuration for a Generator simulation node
/// </summary>
public class GeneratorConfig
{
    /// <summary>Node type identifier</summary>
    public SimulationNodeType NodeType { get; set; } = SimulationNodeType.Generator;

    /// <summary>Display name for the generator</summary>
    public string Name { get; set; } = "Generator";

    /// <summary>How timing is specified</summary>
    public TimingMode TimingMode { get; set; } = TimingMode.InterArrival;

    /// <summary>Statistical distribution type</summary>
    public DistributionType Distribution { get; set; } = DistributionType.Exponential;

    /// <summary>Primary parameter (mean for Exponential/Normal, value for Constant, min for Uniform/Triangular)</summary>
    public double Param1 { get; set; } = 1.0;

    /// <summary>Secondary parameter (stddev for Normal, max for Uniform/Triangular, mode for Triangular)</summary>
    public double Param2 { get; set; } = 0.0;

    /// <summary>Tertiary parameter (max for Triangular, k for Erlang, n for Binomial)</summary>
    public double Param3 { get; set; } = 0.0;

    /// <summary>When to stop generating</summary>
    public TerminationCondition Termination { get; set; } = TerminationCondition.Infinite;

    /// <summary>Maximum entities to generate (for Count/CountOrTime)</summary>
    public int MaxEntities { get; set; } = 100;

    /// <summary>Simulation time to start generating</summary>
    public double StartTime { get; set; } = 0.0;

    /// <summary>Simulation time to stop generating (for Time/CountOrTime)</summary>
    public double StopTime { get; set; } = 1000.0;

    /// <summary>Number of entities per arrival (batch mode)</summary>
    public int BatchSize { get; set; } = 1;

    /// <summary>Type/category label for generated entities</summary>
    public string EntityType { get; set; } = "Entity";

    /// <summary>Color for entity visualization</summary>
    public string EntityColor { get; set; } = "#4CAF50";

    /// <summary>Get parameter labels based on distribution type</summary>
    public static (string Label1, string Label2, string Label3) GetParameterLabels(DistributionType dist)
    {
        return dist switch
        {
            DistributionType.Constant => ("Value", "", ""),
            DistributionType.Exponential => ("Mean", "", ""),
            DistributionType.Uniform => ("Min", "Max", ""),
            DistributionType.Normal => ("Mean", "Std Dev", ""),
            DistributionType.Triangular => ("Min", "Mode", "Max"),
            DistributionType.Erlang => ("Mean", "k (shape)", ""),
            DistributionType.Poisson => ("Lambda (λ)", "", ""),
            DistributionType.Binomial => ("Probability", "n (trials)", ""),
            _ => ("Param 1", "Param 2", "Param 3")
        };
    }

    /// <summary>Get number of parameters needed for distribution</summary>
    public static int GetParameterCount(DistributionType dist)
    {
        return dist switch
        {
            DistributionType.Constant => 1,
            DistributionType.Exponential => 1,
            DistributionType.Uniform => 2,
            DistributionType.Normal => 2,
            DistributionType.Triangular => 3,
            DistributionType.Erlang => 2,
            DistributionType.Poisson => 1,
            DistributionType.Binomial => 2,
            _ => 1
        };
    }
}

/// <summary>
/// Configuration for a Sink simulation node
/// </summary>
public class SinkConfig
{
    public SimulationNodeType NodeType { get; set; } = SimulationNodeType.Sink;
    public string Name { get; set; } = "Sink";
    public bool CollectStatistics { get; set; } = true;
    public bool TrackEntityTypes { get; set; } = true;
}

/// <summary>
/// Configuration for a Clock simulation node
/// </summary>
public class ClockConfig
{
    public SimulationNodeType NodeType { get; set; } = SimulationNodeType.Clock;
    public string Name { get; set; } = "Clock";
    public double Interval { get; set; } = 1.0;
    public double StartTime { get; set; } = 0.0;
    public double StopTime { get; set; } = double.MaxValue;
}

/// <summary>
/// Configuration for a Chance (probabilistic branching) node
/// </summary>
public class ChanceConfig
{
    public SimulationNodeType NodeType { get; set; } = SimulationNodeType.Chance;
    public string Name { get; set; } = "Chance";

    /// <summary>Probabilities for each output branch (should sum to 1.0)</summary>
    public List<ChanceBranch> Branches { get; set; } = new()
    {
        new ChanceBranch { Label = "Yes", Probability = 0.5 },
        new ChanceBranch { Label = "No", Probability = 0.5 }
    };
}

/// <summary>
/// A single branch in a Chance node
/// </summary>
public class ChanceBranch
{
    public string Label { get; set; } = "";
    public double Probability { get; set; } = 0.5;
}

/// <summary>
/// Configuration for a Counter simulation node
/// Counts entities passing through and tracks statistics
/// </summary>
public class CounterConfig
{
    public SimulationNodeType NodeType { get; set; } = SimulationNodeType.Counter;
    public string Name { get; set; } = "Counter";

    /// <summary>Whether to track counts by entity type</summary>
    public bool TrackByEntityType { get; set; } = true;

    /// <summary>Whether to track inter-arrival times</summary>
    public bool TrackInterArrivalTimes { get; set; } = true;

    /// <summary>Whether to track throughput (entities per time unit)</summary>
    public bool TrackThroughput { get; set; } = true;

    /// <summary>Time window for throughput calculation</summary>
    public double ThroughputWindow { get; set; } = 60.0;

    /// <summary>Dashboard node ID this counter reports to (null if standalone)</summary>
    public int? DashboardNodeId { get; set; }

    /// <summary>Display format for the counter value</summary>
    public string DisplayFormat { get; set; } = "N0"; // Numeric with no decimals

    /// <summary>Reset count periodically</summary>
    public bool PeriodicReset { get; set; } = false;

    /// <summary>Reset interval (if PeriodicReset is true)</summary>
    public double ResetInterval { get; set; } = 1000.0;
}

/// <summary>
/// Statistic type for dashboard display
/// </summary>
public enum DashboardStatType
{
    Count,
    Rate,
    Average,
    Min,
    Max,
    StdDev,
    Histogram
}

/// <summary>
/// A single statistic displayed on a dashboard
/// </summary>
public class DashboardStat
{
    public string Label { get; set; } = "";
    public DashboardStatType StatType { get; set; } = DashboardStatType.Count;
    public int? SourceCounterId { get; set; }
    public string Format { get; set; } = "N0";
}

/// <summary>
/// Configuration for a Dashboard simulation node
/// Aggregates and displays statistics from multiple counters
/// </summary>
public class DashboardConfig
{
    public SimulationNodeType NodeType { get; set; } = SimulationNodeType.Dashboard;
    public string Name { get; set; } = "Dashboard";

    /// <summary>Title displayed at top of dashboard</summary>
    public string Title { get; set; } = "Simulation Dashboard";

    /// <summary>Statistics to display</summary>
    public List<DashboardStat> Stats { get; set; } = new()
    {
        new DashboardStat { Label = "Total Count", StatType = DashboardStatType.Count },
        new DashboardStat { Label = "Rate/min", StatType = DashboardStatType.Rate }
    };

    /// <summary>Refresh rate in simulation time units</summary>
    public double RefreshRate { get; set; } = 1.0;

    /// <summary>Show mini charts for each stat</summary>
    public bool ShowCharts { get; set; } = true;

    /// <summary>Number of data points to keep for charts</summary>
    public int ChartDataPoints { get; set; } = 60;

    /// <summary>Background color for the dashboard</summary>
    public string BackgroundColor { get; set; } = "#1e293b";

    /// <summary>Text color for the dashboard</summary>
    public string TextColor { get; set; } = "#f8fafc";

    /// <summary>Accent color for highlights</summary>
    public string AccentColor { get; set; } = "#22c55e";
}

/// <summary>
/// Helper methods for simulation configuration
/// </summary>
public static class SimulationConfigHelper
{
    private const string SimulationTypeKey = "simulationType";
    private const string SimulationConfigKey = "simulationConfig";

    /// <summary>Check if a node is a simulation node</summary>
    public static bool IsSimulationNode(Node node)
    {
        return node.Data.ContainsKey(SimulationTypeKey);
    }

    /// <summary>Get simulation node type from node data</summary>
    public static SimulationNodeType? GetSimulationType(Node node)
    {
        if (node.Data.TryGetValue(SimulationTypeKey, out var typeStr) &&
            Enum.TryParse<SimulationNodeType>(typeStr, out var type))
        {
            return type;
        }
        return null;
    }

    /// <summary>Set simulation type on a node</summary>
    public static void SetSimulationType(Node node, SimulationNodeType type)
    {
        node.Data[SimulationTypeKey] = type.ToString();
    }

    /// <summary>Get generator config from node data</summary>
    public static GeneratorConfig? GetGeneratorConfig(Node node)
    {
        if (node.Data.TryGetValue(SimulationConfigKey, out var configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<GeneratorConfig>(configJson);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Set generator config on a node</summary>
    public static void SetGeneratorConfig(Node node, GeneratorConfig config)
    {
        node.Data[SimulationTypeKey] = SimulationNodeType.Generator.ToString();
        node.Data[SimulationConfigKey] = JsonSerializer.Serialize(config);
    }

    /// <summary>Get sink config from node data</summary>
    public static SinkConfig? GetSinkConfig(Node node)
    {
        if (node.Data.TryGetValue(SimulationConfigKey, out var configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<SinkConfig>(configJson);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Set sink config on a node</summary>
    public static void SetSinkConfig(Node node, SinkConfig config)
    {
        node.Data[SimulationTypeKey] = SimulationNodeType.Sink.ToString();
        node.Data[SimulationConfigKey] = JsonSerializer.Serialize(config);
    }

    /// <summary>Get clock config from node data</summary>
    public static ClockConfig? GetClockConfig(Node node)
    {
        if (node.Data.TryGetValue(SimulationConfigKey, out var configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<ClockConfig>(configJson);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Set clock config on a node</summary>
    public static void SetClockConfig(Node node, ClockConfig config)
    {
        node.Data[SimulationTypeKey] = SimulationNodeType.Clock.ToString();
        node.Data[SimulationConfigKey] = JsonSerializer.Serialize(config);
    }

    /// <summary>Get chance config from node data</summary>
    public static ChanceConfig? GetChanceConfig(Node node)
    {
        if (node.Data.TryGetValue(SimulationConfigKey, out var configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<ChanceConfig>(configJson);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Set chance config on a node</summary>
    public static void SetChanceConfig(Node node, ChanceConfig config)
    {
        node.Data[SimulationTypeKey] = SimulationNodeType.Chance.ToString();
        node.Data[SimulationConfigKey] = JsonSerializer.Serialize(config);
    }

    /// <summary>Create a new node with generator config</summary>
    public static void InitializeAsGenerator(Node node, GeneratorConfig? config = null)
    {
        config ??= new GeneratorConfig();
        SetGeneratorConfig(node, config);
    }

    /// <summary>Create a new node with sink config</summary>
    public static void InitializeAsSink(Node node, SinkConfig? config = null)
    {
        config ??= new SinkConfig();
        SetSinkConfig(node, config);
    }

    /// <summary>Create a new node with clock config</summary>
    public static void InitializeAsClock(Node node, ClockConfig? config = null)
    {
        config ??= new ClockConfig();
        SetClockConfig(node, config);
    }

    /// <summary>Create a new node with chance config</summary>
    public static void InitializeAsChance(Node node, ChanceConfig? config = null)
    {
        config ??= new ChanceConfig();
        SetChanceConfig(node, config);
    }

    /// <summary>Get counter config from node data</summary>
    public static CounterConfig? GetCounterConfig(Node node)
    {
        if (node.Data.TryGetValue(SimulationConfigKey, out var configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<CounterConfig>(configJson);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Set counter config on a node</summary>
    public static void SetCounterConfig(Node node, CounterConfig config)
    {
        node.Data[SimulationTypeKey] = SimulationNodeType.Counter.ToString();
        node.Data[SimulationConfigKey] = JsonSerializer.Serialize(config);
    }

    /// <summary>Create a new node with counter config</summary>
    public static void InitializeAsCounter(Node node, CounterConfig? config = null)
    {
        config ??= new CounterConfig();
        SetCounterConfig(node, config);
    }

    /// <summary>Get dashboard config from node data</summary>
    public static DashboardConfig? GetDashboardConfig(Node node)
    {
        if (node.Data.TryGetValue(SimulationConfigKey, out var configJson))
        {
            try
            {
                return JsonSerializer.Deserialize<DashboardConfig>(configJson);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Set dashboard config on a node</summary>
    public static void SetDashboardConfig(Node node, DashboardConfig config)
    {
        node.Data[SimulationTypeKey] = SimulationNodeType.Dashboard.ToString();
        node.Data[SimulationConfigKey] = JsonSerializer.Serialize(config);
    }

    /// <summary>Create a new node with dashboard config</summary>
    public static void InitializeAsDashboard(Node node, DashboardConfig? config = null)
    {
        config ??= new DashboardConfig();
        SetDashboardConfig(node, config);
    }
}
