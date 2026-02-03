using dfd2wasm.Models;
using Microsoft.AspNetCore.Components;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // Simulation panel collapse state
    private bool collapseSimulationProperties = false;

    /// <summary>
    /// Check if the currently selected node is a simulation node
    /// </summary>
    private bool IsSimulationNodeSelected()
    {
        if (selectedNodes.Count != 1) return false;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return false;
        return SimulationConfigHelper.IsSimulationNode(node);
    }

    /// <summary>
    /// Get the simulation type of the selected node
    /// </summary>
    private SimulationNodeType? GetSelectedSimulationType()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetSimulationType(node);
    }

    /// <summary>
    /// Get generator config for selected node
    /// </summary>
    private GeneratorConfig? GetSelectedGeneratorConfig()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetGeneratorConfig(node) ?? new GeneratorConfig();
    }

    /// <summary>
    /// Update generator config for selected node
    /// </summary>
    private void UpdateGeneratorConfig(Action<GeneratorConfig> updateAction)
    {
        if (selectedNodes.Count != 1) return;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return;

        var config = SimulationConfigHelper.GetGeneratorConfig(node) ?? new GeneratorConfig();
        updateAction(config);
        SimulationConfigHelper.SetGeneratorConfig(node, config);
        StateHasChanged();
    }

    /// <summary>
    /// Get sink config for selected node
    /// </summary>
    private SinkConfig? GetSelectedSinkConfig()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetSinkConfig(node) ?? new SinkConfig();
    }

    /// <summary>
    /// Update sink config for selected node
    /// </summary>
    private void UpdateSinkConfig(Action<SinkConfig> updateAction)
    {
        if (selectedNodes.Count != 1) return;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return;

        var config = SimulationConfigHelper.GetSinkConfig(node) ?? new SinkConfig();
        updateAction(config);
        SimulationConfigHelper.SetSinkConfig(node, config);
        StateHasChanged();
    }

    /// <summary>
    /// Get clock config for selected node
    /// </summary>
    private ClockConfig? GetSelectedClockConfig()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetClockConfig(node) ?? new ClockConfig();
    }

    /// <summary>
    /// Update clock config for selected node
    /// </summary>
    private void UpdateClockConfig(Action<ClockConfig> updateAction)
    {
        if (selectedNodes.Count != 1) return;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return;

        var config = SimulationConfigHelper.GetClockConfig(node) ?? new ClockConfig();
        updateAction(config);
        SimulationConfigHelper.SetClockConfig(node, config);
        StateHasChanged();
    }

    /// <summary>
    /// Get chance config for selected node
    /// </summary>
    private ChanceConfig? GetSelectedChanceConfig()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetChanceConfig(node) ?? new ChanceConfig();
    }

    /// <summary>
    /// Update chance config for selected node
    /// </summary>
    private void UpdateChanceConfig(Action<ChanceConfig> updateAction)
    {
        if (selectedNodes.Count != 1) return;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return;

        var config = SimulationConfigHelper.GetChanceConfig(node) ?? new ChanceConfig();
        updateAction(config);
        SimulationConfigHelper.SetChanceConfig(node, config);
        StateHasChanged();
    }

    /// <summary>
    /// Get counter config for selected node
    /// </summary>
    private CounterConfig? GetSelectedCounterConfig()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetCounterConfig(node) ?? new CounterConfig();
    }

    /// <summary>
    /// Update counter config for selected node
    /// </summary>
    private void UpdateCounterConfig(Action<CounterConfig> updateAction)
    {
        if (selectedNodes.Count != 1) return;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return;

        var config = SimulationConfigHelper.GetCounterConfig(node) ?? new CounterConfig();
        updateAction(config);
        SimulationConfigHelper.SetCounterConfig(node, config);
        StateHasChanged();
    }

    /// <summary>
    /// Get dashboard config for selected node
    /// </summary>
    private DashboardConfig? GetSelectedDashboardConfig()
    {
        if (selectedNodes.Count != 1) return null;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return null;
        return SimulationConfigHelper.GetDashboardConfig(node) ?? new DashboardConfig();
    }

    /// <summary>
    /// Update dashboard config for selected node
    /// </summary>
    private void UpdateDashboardConfig(Action<DashboardConfig> updateAction)
    {
        if (selectedNodes.Count != 1) return;
        var node = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
        if (node == null) return;

        var config = SimulationConfigHelper.GetDashboardConfig(node) ?? new DashboardConfig();
        updateAction(config);
        SimulationConfigHelper.SetDashboardConfig(node, config);
        StateHasChanged();
    }

    /// <summary>
    /// Get all counter nodes that can be linked to a dashboard
    /// </summary>
    private IEnumerable<Node> GetCounterNodes()
    {
        return nodes.Where(n => SimulationConfigHelper.GetSimulationType(n) == SimulationNodeType.Counter);
    }

    /// <summary>
    /// Add a stat to the dashboard
    /// </summary>
    private void AddDashboardStat()
    {
        UpdateDashboardConfig(config =>
        {
            config.Stats.Add(new DashboardStat
            {
                Label = $"Stat {config.Stats.Count + 1}",
                StatType = DashboardStatType.Count
            });
        });
    }

    /// <summary>
    /// Remove a stat from the dashboard
    /// </summary>
    private void RemoveDashboardStat(int index)
    {
        UpdateDashboardConfig(config =>
        {
            if (config.Stats.Count > 1 && index >= 0 && index < config.Stats.Count)
            {
                config.Stats.RemoveAt(index);
            }
        });
    }

    /// <summary>
    /// Get display name for dashboard stat type
    /// </summary>
    private static string GetStatTypeDisplayName(DashboardStatType statType)
    {
        return statType switch
        {
            DashboardStatType.Count => "Count",
            DashboardStatType.Rate => "Rate",
            DashboardStatType.Average => "Average",
            DashboardStatType.Min => "Minimum",
            DashboardStatType.Max => "Maximum",
            DashboardStatType.StdDev => "Std Deviation",
            DashboardStatType.Histogram => "Histogram",
            _ => statType.ToString()
        };
    }

    /// <summary>
    /// Initialize a node as a simulation node when placed from the simulation template
    /// </summary>
    private void InitializeSimulationNode(Node node, string shapeId)
    {
        switch (shapeId)
        {
            case "generator":
                SimulationConfigHelper.InitializeAsGenerator(node);
                break;
            case "sink":
                SimulationConfigHelper.InitializeAsSink(node);
                break;
            case "clock":
                SimulationConfigHelper.InitializeAsClock(node);
                break;
            case "chance":
                SimulationConfigHelper.InitializeAsChance(node);
                break;
            case "counter":
                SimulationConfigHelper.InitializeAsCounter(node);
                break;
            case "dashboard":
                SimulationConfigHelper.InitializeAsDashboard(node);
                break;
        }
    }

    /// <summary>
    /// Get display name for distribution type
    /// </summary>
    private static string GetDistributionDisplayName(DistributionType dist)
    {
        return dist switch
        {
            DistributionType.Constant => "Constant",
            DistributionType.Exponential => "Exponential",
            DistributionType.Uniform => "Uniform",
            DistributionType.Normal => "Normal (Gaussian)",
            DistributionType.Triangular => "Triangular",
            DistributionType.Erlang => "Erlang",
            DistributionType.Poisson => "Poisson",
            DistributionType.Binomial => "Binomial",
            _ => dist.ToString()
        };
    }

    /// <summary>
    /// Get display name for termination condition
    /// </summary>
    private static string GetTerminationDisplayName(TerminationCondition term)
    {
        return term switch
        {
            TerminationCondition.Infinite => "Run Forever",
            TerminationCondition.Count => "Stop After Count",
            TerminationCondition.Time => "Stop At Time",
            TerminationCondition.CountOrTime => "Count or Time",
            _ => term.ToString()
        };
    }

    /// <summary>
    /// Add a new branch to the chance config
    /// </summary>
    private void AddChanceBranch()
    {
        UpdateChanceConfig(config =>
        {
            config.Branches.Add(new ChanceBranch
            {
                Label = $"Branch {config.Branches.Count + 1}",
                Probability = 0.0
            });
            NormalizeChanceProbabilities(config);
        });
    }

    /// <summary>
    /// Remove a branch from the chance config
    /// </summary>
    private void RemoveChanceBranch(int index)
    {
        UpdateChanceConfig(config =>
        {
            if (config.Branches.Count > 2 && index >= 0 && index < config.Branches.Count)
            {
                config.Branches.RemoveAt(index);
                NormalizeChanceProbabilities(config);
            }
        });
    }

    /// <summary>
    /// Normalize chance probabilities to sum to 1.0
    /// </summary>
    private static void NormalizeChanceProbabilities(ChanceConfig config)
    {
        if (config.Branches.Count == 0) return;

        var sum = config.Branches.Sum(b => b.Probability);
        if (sum > 0 && Math.Abs(sum - 1.0) > 0.001)
        {
            foreach (var branch in config.Branches)
            {
                branch.Probability /= sum;
            }
        }
        else if (sum == 0)
        {
            // Distribute evenly
            var evenProb = 1.0 / config.Branches.Count;
            foreach (var branch in config.Branches)
            {
                branch.Probability = evenProb;
            }
        }
    }
}
