using Microsoft.AspNetCore.Components;
using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    /// <summary>
    /// Check if the diagram has any simulation nodes
    /// </summary>
    private bool HasSimulationNodes()
    {
        return nodes.Any(n => SimulationConfigHelper.IsSimulationNode(n));
    }

    /// <summary>
    /// Start the simulation
    /// </summary>
    private async Task StartSimulation()
    {
        if (simulationEngine.IsRunning) return;

        // Initialize the simulation with current nodes and edges
        simulationEngine.Initialize(nodes.ToList(), edges.ToList());
        simulationEngine.SimulationSpeed = simulationSpeed;
        simulationTime = 0;
        simulationLog.Clear();

        AddSimulationLogEntry("Simulation started");

        try
        {
            await simulationEngine.StartAsync();
        }
        catch (Exception ex)
        {
            AddSimulationLogEntry($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Pause the simulation
    /// </summary>
    private void PauseSimulation()
    {
        simulationEngine.Pause();
        AddSimulationLogEntry("Simulation paused");
    }

    /// <summary>
    /// Resume the simulation
    /// </summary>
    private async Task ResumeSimulation()
    {
        simulationEngine.SimulationSpeed = simulationSpeed;
        await simulationEngine.StartAsync();
        AddSimulationLogEntry("Simulation resumed");
    }

    /// <summary>
    /// Stop the simulation
    /// </summary>
    private void StopSimulation()
    {
        simulationEngine.Stop();
        AddSimulationLogEntry("Simulation stopped");
    }

    /// <summary>
    /// Reset the simulation
    /// </summary>
    private void ResetSimulation()
    {
        simulationEngine.Reset();
        simulationTime = 0;
        simulationLog.Clear();
        AddSimulationLogEntry("Simulation reset");
        StateHasChanged();
    }

    /// <summary>
    /// Handle simulation event
    /// </summary>
    private void OnSimulationEvent(object? sender, SimulationEventArgs e)
    {
        InvokeAsync(() =>
        {
            if (!string.IsNullOrEmpty(e.Message))
            {
                AddSimulationLogEntry($"[{e.SimulationTime:F2}] {e.Message}");
            }

            // Update counter displays if this was a counter update
            if (e.EventType == "CounterUpdated" && e.NodeId.HasValue)
            {
                // Force re-render to show updated counter value
                StateHasChanged();
            }
        });
    }

    /// <summary>
    /// Handle simulation time update
    /// </summary>
    private void OnSimulationTimeUpdated(object? sender, double time)
    {
        InvokeAsync(() =>
        {
            simulationTime = time;
            StateHasChanged();
        });
    }

    /// <summary>
    /// Handle simulation started
    /// </summary>
    private void OnSimulationStarted(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Handle simulation stopped
    /// </summary>
    private void OnSimulationStopped(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Handle simulation paused
    /// </summary>
    private void OnSimulationPaused(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Handle simulation resumed
    /// </summary>
    private void OnSimulationResumed(object? sender, EventArgs e)
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Add an entry to the simulation log
    /// </summary>
    private void AddSimulationLogEntry(string message)
    {
        simulationLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

        // Keep log limited to last 100 entries
        if (simulationLog.Count > 100)
        {
            simulationLog.RemoveAt(0);
        }
    }

    /// <summary>
    /// Get formatted counter display value for a counter node
    /// </summary>
    private string GetCounterDisplayValue(int nodeId)
    {
        var stats = simulationEngine.GetCounterStats(nodeId);
        if (stats == null) return "0";

        var config = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (config == null) return stats.TotalCount.ToString();

        var counterConfig = SimulationConfigHelper.GetCounterConfig(config);
        var format = counterConfig?.DisplayFormat ?? "N0";

        try
        {
            return stats.TotalCount.ToString(format);
        }
        catch
        {
            return stats.TotalCount.ToString();
        }
    }

    /// <summary>
    /// Get throughput value for a counter node
    /// </summary>
    private double GetCounterThroughput(int nodeId)
    {
        var stats = simulationEngine.GetCounterStats(nodeId);
        return stats?.Throughput ?? 0;
    }

    /// <summary>
    /// Get dashboard stat value
    /// </summary>
    private string GetDashboardStatValue(DashboardStat stat)
    {
        if (!stat.SourceCounterId.HasValue) return "N/A";

        var stats = simulationEngine.GetCounterStats(stat.SourceCounterId.Value);
        if (stats == null) return "N/A";

        double value = stat.StatType switch
        {
            DashboardStatType.Count => stats.TotalCount,
            DashboardStatType.Rate => stats.Throughput,
            DashboardStatType.Average => stats.AverageInterArrival,
            DashboardStatType.Min => stats.MinInterArrival,
            DashboardStatType.Max => stats.MaxInterArrival,
            DashboardStatType.StdDev => stats.StdDevInterArrival,
            _ => 0
        };

        try
        {
            return value.ToString(stat.Format);
        }
        catch
        {
            return value.ToString("F2");
        }
    }

    /// <summary>
    /// Handle simulation speed change
    /// </summary>
    private void OnSimulationSpeedChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var speed))
        {
            simulationSpeed = speed;
            simulationEngine.SimulationSpeed = speed;
        }
    }

    /// <summary>
    /// Toggle simulation log visibility
    /// </summary>
    private void ToggleSimulationLog()
    {
        showSimulationLog = !showSimulationLog;
    }
}
