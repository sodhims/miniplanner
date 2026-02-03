using dfd2wasm.Services;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // Layout processing state
    private bool isLayoutProcessing = false;
    private string layoutStatusMessage = "";

    // Helper to get target nodes (selected or all)
    private List<Node> GetTargetNodes()
    {
        return selectedNodes.Count > 0 
            ? nodes.Where(n => selectedNodes.Contains(n.Id)).ToList() 
            : nodes;
    }

    // Helper to get target edges (selected or all)
    private List<Edge> GetTargetEdges()
    {
        return selectedEdges.Count > 0 
            ? edges.Where(e => selectedEdges.Contains(e.Id)).ToList() 
            : edges;
    }

    // Helper to get edges within selected nodes
    private List<Edge> GetEdgesForTargetNodes(List<Node> targetNodes)
    {
        var nodeIds = new HashSet<int>(targetNodes.Select(n => n.Id));
        return edges.Where(e => nodeIds.Contains(e.From) && nodeIds.Contains(e.To)).ToList();
    }

    // Layout algorithms
    private async Task ApplyForceDirectedLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Force-Directed layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            var targetEdges = GetEdgesForTargetNodes(targetNodes);
            _layoutService.ApplyForceDirectedLayout(targetNodes, targetEdges, 2500, 1800, iterations: 30);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task ApplySugiyamaLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Hierarchical layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            var targetEdges = GetEdgesForTargetNodes(targetNodes);
            _layoutService.ApplySugiyamaLayout(targetNodes, targetEdges, layerSpacing: 150, nodeSpacing: 100);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task ApplyTreeLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Tree layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            var targetEdges = GetEdgesForTargetNodes(targetNodes);
            // If exactly one node selected, use it as root; otherwise auto-detect
            int? rootId = selectedNodes.Count == 1 ? selectedNodes.First() : null;
            _layoutService.ApplyTreeLayout(targetNodes, targetEdges, levelSpacing: 120, siblingSpacing: 80, rootId: rootId);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task ApplyCircleLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Circle layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            
            // Calculate center based on current positions of target nodes
            var centerX = targetNodes.Average(n => n.X + n.Width / 2);
            var centerY = targetNodes.Average(n => n.Y + n.Height / 2);
            var radius = Math.Max(200, 50 + targetNodes.Count * 30);
            
            _layoutService.ApplyCircleLayout(targetNodes, centerX, centerY, radius);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task ApplyGridLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Grid layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            
            // Start grid at the top-left of current selection bounds
            var startX = targetNodes.Min(n => n.X);
            var startY = targetNodes.Min(n => n.Y);
            
            _layoutService.ApplyGridLayout(targetNodes, columns: 0, spacingX: 180, spacingY: 120, startX: startX, startY: startY);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task ApplyCircuitLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;

        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Circuit layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);

        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            var targetEdges = GetEdgesForTargetNodes(targetNodes);
            var svc = new CircuitLayoutService();
            var routeOpts = new GridMazeRouter.RouteOptions
            {
                GridSpacing = Math.Max(4.0, circuitGridSpacing),
                ObstacleMargin = Math.Max(0.0, circuitObstacleMargin),
                BendPenalty = Math.Max(0.0, circuitBendPenalty),
                ViaPenalty = Math.Max(0.0, circuitViaPenalty),
                ProximityPenalty = Math.Max(0.0, circuitProximityPenalty),
                MaxGridSize = Math.Max(10, circuitMaxGridSize)
            };

            svc.ApplyCircuitLayout(targetNodes, targetEdges, routeOpts, rowSpacing: circuitRowSpacing, colSpacing: circuitColSpacing, startX: circuitStartX, startY: circuitStartY);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task ApplyRadialLayout()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying Radial layout to {targetNodes.Count} nodes...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            var targetEdges = GetEdgesForTargetNodes(targetNodes);
            
            // Calculate center based on current positions
            var centerX = targetNodes.Average(n => n.X + n.Width / 2);
            var centerY = targetNodes.Average(n => n.Y + n.Height / 2);
            
            int? rootId = selectedNodes.Count == 1 ? selectedNodes.First() : null;
            _layoutService.ApplyRadialLayout(targetNodes, targetEdges, centerX: centerX, centerY: centerY, ringSpacing: 150, rootId: rootId);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    // Edge routing
    private async Task MinimizeEdgeCrossings()
    {
        var targetNodes = GetTargetNodes();
        var targetEdges = selectedNodes.Count > 0 
            ? GetEdgesForTargetNodes(targetNodes) 
            : edges;
        
        if (targetEdges.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Minimizing crossings for {targetEdges.Count} edges...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            _routingService.MinimizeCrossings(targetNodes, targetEdges, iterations: 20);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task RouteEdgesOrthogonal()
    {
        var targetNodes = GetTargetNodes();
        var targetEdges = selectedNodes.Count > 0 
            ? GetEdgesForTargetNodes(targetNodes) 
            : (selectedEdges.Count > 0 ? GetTargetEdges() : edges);
        
        if (targetEdges.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Applying orthogonal routing to {targetEdges.Count} edges...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            _routingService.RouteAllOrthogonal(nodes, targetEdges, channelSpacing: 20);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task BundleParallelEdges()
    {
        var targetNodes = GetTargetNodes();
        var targetEdges = selectedNodes.Count > 0 
            ? GetEdgesForTargetNodes(targetNodes) 
            : (selectedEdges.Count > 0 ? GetTargetEdges() : edges);
        
        if (targetEdges.Count == 0) return;
        
        isLayoutProcessing = true;
        layoutStatusMessage = $"Bundling {targetEdges.Count} edges...";
        StateHasChanged();
        await Task.Delay(50);
        
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            _routingService.BundleParallelEdges(nodes, targetEdges, bundleOffset: 15);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private int GetEdgeCrossingCount()
    {
        return _routingService.CountEdgeCrossings(nodes, edges);
    }

    // Additional layout operations
    private async Task ApplyCompactLayout()
    {
        // Use new area-reducing compact
        // If a single node is selected, compact around that node
        // Otherwise, compact around the center of the graph
        await CompactGraphArea();
    }

    /// <summary>
    /// Compact the graph area by bringing nodes closer together.
    /// Two modes:
    /// - If exactly one node is selected: compact around that node (keeps it fixed)
    /// - Otherwise: compact around the center of the graph
    /// Minimum edge length is maintained based on node size.
    /// </summary>
    private async Task CompactGraphArea(double reductionFactor = 0.3)
    {
        if (nodes.Count < 2) return;

        isLayoutProcessing = true;

        // Determine mode based on selection
        Node? anchorNode = null;
        if (selectedNodes.Count == 1)
        {
            anchorNode = nodes.FirstOrDefault(n => n.Id == selectedNodes.First());
            layoutStatusMessage = anchorNode != null
                ? $"Compacting around '{anchorNode.Text ?? $"Node {anchorNode.Id}"}'..."
                : "Compacting around center...";
        }
        else
        {
            layoutStatusMessage = "Compacting around center...";
        }

        StateHasChanged();
        await Task.Delay(50);

        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            // Use the new area-reducing compact function
            LayoutOptimization.CompactArea(nodes, edges, anchorNode, reductionFactor);

            // Re-route edges for clean appearance
            GeometryService.BundleAllEdges(nodes, edges);
            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    /// <summary>
    /// Legacy compact - just moves graph to origin (100,100)
    /// </summary>
    private async Task MoveGraphToOrigin()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;

        isLayoutProcessing = true;
        layoutStatusMessage = $"Moving {targetNodes.Count} nodes to origin...";
        StateHasChanged();
        await Task.Delay(50);

        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            var minX = targetNodes.Min(n => n.X);
            var minY = targetNodes.Min(n => n.Y);

            foreach (var node in targetNodes)
            {
                node.X = node.X - minX + 100;
                node.Y = node.Y - minY + 100;
            }

            RecalculateEdgePaths();
        }
        finally
        {
            isLayoutProcessing = false;
            layoutStatusMessage = "";
            StateHasChanged();
        }
    }

    private async Task FlipHorizontal()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        var minX = targetNodes.Min(n => n.X);
        var maxX = targetNodes.Max(n => n.X + n.Width);
        var centerX = (minX + maxX) / 2;
        
        foreach (var node in targetNodes)
        {
            node.X = centerX - (node.X + node.Width - centerX);
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task FlipVertical()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        var minY = targetNodes.Min(n => n.Y);
        var maxY = targetNodes.Max(n => n.Y + n.Height);
        var centerY = (minY + maxY) / 2;
        
        foreach (var node in targetNodes)
        {
            node.Y = centerY - (node.Y + node.Height - centerY);
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task ApplySymmetricHorizontal()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        // Center target nodes horizontally around their current center (or canvas center if all nodes)
        var graphCenterX = targetNodes.Average(n => n.X + n.Width / 2);
        var canvasCenterX = selectedNodes.Count > 0 ? graphCenterX : 1000.0;
        
        // For selected nodes, align them symmetrically around their center
        if (selectedNodes.Count > 0)
        {
            // Already centered on themselves, so just recalc
        }
        else
        {
            var offset = canvasCenterX - graphCenterX;
            foreach (var node in targetNodes)
            {
                node.X += offset;
            }
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task ApplySymmetricVertical()
    {
        var targetNodes = GetTargetNodes();
        if (targetNodes.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        var graphCenterY = targetNodes.Average(n => n.Y + n.Height / 2);
        var canvasCenterY = selectedNodes.Count > 0 ? graphCenterY : 800.0;
        
        if (selectedNodes.Count == 0)
        {
            var offset = canvasCenterY - graphCenterY;
            foreach (var node in targetNodes)
            {
                node.Y += offset;
            }
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    // Edge style operations
    private async Task SetAllEdgesDirect()
    {
        var targetEdges = GetTargetEdges();
        if (targetEdges.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        foreach (var edge in targetEdges)
        {
            edge.Style = EdgeStyle.Direct;
            edge.Waypoints.Clear();
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task SetAllEdgesOrtho()
    {
        var targetEdges = GetTargetEdges();
        if (targetEdges.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        foreach (var edge in targetEdges)
        {
            edge.Style = EdgeStyle.Ortho;
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task SetAllEdgesBezier()
    {
        var targetEdges = GetTargetEdges();
        if (targetEdges.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        foreach (var edge in targetEdges)
        {
            edge.Style = EdgeStyle.Bezier;
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task ClearAllWaypoints()
    {
        var targetEdges = GetTargetEdges();
        if (targetEdges.Count == 0) return;
        
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        foreach (var edge in targetEdges)
        {
            edge.Waypoints.Clear();
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
        await Task.CompletedTask;
    }
}
