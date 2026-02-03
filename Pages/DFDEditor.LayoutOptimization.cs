using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using dfd2wasm.Models;
using dfd2wasm.Services;

namespace dfd2wasm.Pages;

/// <summary>
/// Partial class for layout optimization features
/// </summary>
public partial class DFDEditor
{
    [Inject] private LayoutOptimizationService LayoutOptimization { get; set; } = default!;

    // Optimization state
    private bool isOptimizing = false;
    private int optimizationProgress = 0;
    private double currentFitness = 0;

    /// <summary>
    /// Run simulated annealing to optimize layout
    /// </summary>
    private async Task OptimizeLayoutSimulatedAnnealing()
    {
        if (nodes.Count < 2)
        {
            await JSRuntime.InvokeVoidAsync("alert", "Need at least 2 nodes to optimize.");
            return;
        }

        isOptimizing = true;
        optimizationProgress = 0;
        StateHasChanged();

        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            var options = new LayoutOptimizationService.AnnealingOptions
            {
                InitialTemperature = 1000,
                CoolingRate = 0.995,
                MinTemperature = 0.1,
                MaxIterations = 5000,
                MaxNoImprovement = 500,
                GridSize = 20
            };

            var (optimizedNodes, improvement) = await LayoutOptimization.OptimizeWithSimulatedAnnealing(
                nodes, edges, options,
                progressCallback: (iteration, fitness) =>
                {
                    optimizationProgress = (int)((iteration / (double)options.MaxIterations) * 100);
                    currentFitness = fitness;
                    InvokeAsync(StateHasChanged);
                });

            // Apply optimized positions
            foreach (var optimized in optimizedNodes)
            {
                var node = nodes.FirstOrDefault(n => n.Id == optimized.Id);
                if (node != null)
                {
                    node.X = optimized.X;
                    node.Y = optimized.Y;
                }
            }

            // Bundle edges for clean appearance
            GeometryService.BundleAllEdges(nodes, edges);

            RecalculateEdgePaths();
            StateHasChanged();

            await JSRuntime.InvokeVoidAsync("alert", $"Layout optimized! Improvement: {improvement:F1}%");
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("alert", $"Optimization failed: {ex.Message}");
        }
        finally
        {
            isOptimizing = false;
            optimizationProgress = 0;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Quick removal of node overlaps
    /// </summary>
    private async Task QuickRemoveOverlaps()
    {
        if (nodes.Count < 2) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        LayoutOptimization.RemoveOverlaps(nodes);
        
        GeometryService.BundleAllEdges(nodes, edges);
        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Compact layout toward center
    /// </summary>
    private async Task CompactLayoutAction()
    {
        if (nodes.Count < 2) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        LayoutOptimization.CompactLayout(nodes, 0.8);
        LayoutOptimization.RemoveOverlaps(nodes); // Fix any overlaps created
        
        GeometryService.BundleAllEdges(nodes, edges);
        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Snap all nodes to grid
    /// </summary>
    private async Task SnapAllToGrid()
    {
        if (nodes.Count == 0) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        LayoutOptimization.SnapToGrid(nodes, 20);
        
        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Show current fitness score
    /// </summary>
    private async Task ShowFitnessScore()
    {
        if (nodes.Count == 0)
        {
            await JSRuntime.InvokeVoidAsync("alert", "No nodes in diagram.");
            return;
        }

        var result = LayoutOptimization.EvaluateFitness(nodes, edges);
        await JSRuntime.InvokeVoidAsync("alert", result.ToString());
    }

    /// <summary>
    /// Bundle edges for cleaner appearance (manual trigger)
    /// </summary>
    private void BundleEdges()
    {
        if (edges.Count == 0) return;

        UndoService.SaveState(nodes, edges, edgeLabels);
        
        GeometryService.BundleAllEdges(nodes, edges);
        RecalculateEdgePaths();
        StateHasChanged();
    }
}
