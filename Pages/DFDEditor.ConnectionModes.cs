using dfd2wasm.Models;
using Microsoft.AspNetCore.Components.Web;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // 1:N Click mode - click source, then click each target
    private bool HandleOneToNNodeClick(int nodeId)
    {
        if (!oneToNSourceNode.HasValue)
        {
            // First click sets source
            oneToNSourceNode = nodeId;
            return true;
        }
        
        // Subsequent clicks create edges
        if (nodeId != oneToNSourceNode.Value)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            var fromNode = nodes.FirstOrDefault(n => n.Id == oneToNSourceNode.Value);
            var toNode = nodes.FirstOrDefault(n => n.Id == nodeId);

            if (fromNode != null && toNode != null)
            {
                var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, toNode);

                var newEdge = CreateEdgeWithDefaults(oneToNSourceNode.Value, nodeId, fromConn, toConn);
                newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
                edges.Add(newEdge);
                StateHasChanged();
            }
        }
        return true;
    }
    
    // 1:N Area mode - click source, then drag rectangle to select targets
    private bool HandleOneToNAreaNodeClick(int nodeId)
    {
        if (!oneToNSourceNode.HasValue)
        {
            oneToNSourceNode = nodeId;
            return true;
        }
        return false;
    }
    
    // Rearrange mode - recalculate edges for selected nodes
    private void RearrangeSelectedNodeEdges()
    {
        foreach (var nodeId in selectedNodes)
        {
            RecalculateEdgePaths(nodeId);
        }
        StateHasChanged();
    }
}