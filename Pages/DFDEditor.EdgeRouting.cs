// Add these methods to DFDEditor.LayoutOptimization.cs or create DFDEditor.EdgeRouting.cs

using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    /// <summary>
    /// Rearrange edge connection points to minimize visual crossings at each node
    /// </summary>
    private void RearrangeEdgeConnections()
    {
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        // Process each node
        foreach (var node in nodes)
        {
            RearrangeConnectionsForNode(node);
        }
        
        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Rearrange connections for a single node to minimize crossings
    /// </summary>
    private void RearrangeConnectionsForNode(Node node)
    {
        // Get all edges connected to this node
        var incomingEdges = edges.Where(e => e.To == node.Id).ToList();
        var outgoingEdges = edges.Where(e => e.From == node.Id).ToList();
        
        // For incoming edges: assign connection points based on source direction
        if (incomingEdges.Count > 0)
        {
            AssignOptimalIncomingConnections(node, incomingEdges);
        }
        
        // For outgoing edges: assign connection points based on target direction
        if (outgoingEdges.Count > 0)
        {
            AssignOptimalOutgoingConnections(node, outgoingEdges);
        }
    }

    /// <summary>
    /// Assign optimal connection points for incoming edges
    /// </summary>
    private void AssignOptimalIncomingConnections(Node targetNode, List<Edge> incomingEdges)
    {
        // Calculate angle from each source to this node
        var edgesWithAngles = incomingEdges.Select(edge =>
        {
            var sourceNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            if (sourceNode == null) return (edge, angle: 0.0, side: "left");
            
            var dx = (sourceNode.X + sourceNode.Width / 2) - (targetNode.X + targetNode.Width / 2);
            var dy = (sourceNode.Y + sourceNode.Height / 2) - (targetNode.Y + targetNode.Height / 2);
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI; // -180 to 180
            
            // Determine which side the edge should connect to
            var side = GetSideFromAngle(angle);
            
            return (edge, angle, side);
        }).ToList();

        // Group by side
        var bySide = edgesWithAngles.GroupBy(e => e.side);
        
        foreach (var group in bySide)
        {
            var side = group.Key;
            var edgesOnSide = group.OrderBy(e => e.angle).ToList();
            
            // Distribute positions along the side
            var count = edgesOnSide.Count;
            for (int i = 0; i < count; i++)
            {
                // Position from -(count-1)/2 to (count-1)/2
                var position = count == 1 ? 0 : i - (count - 1) / 2.0;
                edgesOnSide[i].edge.ToConnection = new ConnectionPoint 
                { 
                    Side = side, 
                    Position = (int)Math.Round(position) 
                };
            }
        }
    }

    /// <summary>
    /// Assign optimal connection points for outgoing edges
    /// </summary>
    private void AssignOptimalOutgoingConnections(Node sourceNode, List<Edge> outgoingEdges)
    {
        // Calculate angle to each target
        var edgesWithAngles = outgoingEdges.Select(edge =>
        {
            var targetNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (targetNode == null) return (edge, angle: 0.0, side: "right");
            
            var dx = (targetNode.X + targetNode.Width / 2) - (sourceNode.X + sourceNode.Width / 2);
            var dy = (targetNode.Y + targetNode.Height / 2) - (sourceNode.Y + sourceNode.Height / 2);
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            
            var side = GetSideFromAngle(angle + 180); // Flip for outgoing
            // Actually for outgoing we want the side facing the target
            side = GetSideFromAngle(angle);
            
            return (edge, angle, side);
        }).ToList();

        // Group by side
        var bySide = edgesWithAngles.GroupBy(e => e.side);
        
        foreach (var group in bySide)
        {
            var side = group.Key;
            var edgesOnSide = group.OrderBy(e => e.angle).ToList();
            
            var count = edgesOnSide.Count;
            for (int i = 0; i < count; i++)
            {
                var position = count == 1 ? 0 : i - (count - 1) / 2.0;
                edgesOnSide[i].edge.FromConnection = new ConnectionPoint 
                { 
                    Side = side, 
                    Position = (int)Math.Round(position) 
                };
            }
        }
    }

    /// <summary>
    /// Get the appropriate side based on angle
    /// </summary>
    private string GetSideFromAngle(double angle)
    {
        // Normalize to -180 to 180
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        
        // Divide into quadrants with 45-degree boundaries
        if (angle >= -45 && angle < 45)
            return "right";
        else if (angle >= 45 && angle < 135)
            return "bottom";
        else if (angle >= -135 && angle < -45)
            return "top";
        else
            return "left";
    }

    /// <summary>
    /// Recalculate all edge connection points using optimal algorithm
    /// </summary>
    private void RecalculateAllConnections()
    {
        UndoService.SaveState(nodes, edges, edgeLabels);
        
        foreach (var edge in edges)
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            
            if (fromNode != null && toNode != null)
            {
                var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, toNode);
                edge.FromConnection = fromConn;
                edge.ToConnection = toConn;
            }
        }
        
        // Then bundle
        GeometryService.BundleAllEdges(nodes, edges);
        
        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Bundle all edges (wrapper for toolbar button)
    /// </summary>
    private void BundleAllEdges()
    {
        UndoService.SaveState(nodes, edges, edgeLabels);
        GeometryService.BundleAllEdges(nodes, edges);
        RecalculateEdgePaths();
        StateHasChanged();
    }
}
