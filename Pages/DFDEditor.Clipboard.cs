using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // Clipboard state
    private List<Node> copiedNodes = new();
    private List<Edge> copiedEdges = new();
    private List<EdgeLabel> copiedLabels = new();

    private async Task CopySelected()
    {
        copiedNodes.Clear();
        copiedEdges.Clear();
        copiedLabels.Clear();

        // Copy selected nodes
        foreach (var nodeId in selectedNodes)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                copiedNodes.Add(new Node
                {
                    Id = node.Id,
                    X = node.X,
                    Y = node.Y,
                    Width = node.Width,
                    Height = node.Height,
                    Text = node.Text,
                    Shape = node.Shape,
                    FillColor = node.FillColor,
                    StrokeColor = node.StrokeColor
                });
            }
        }

        // Copy edges where BOTH endpoints are in the selection
        var selectedNodeSet = new HashSet<int>(selectedNodes);
        foreach (var edge in edges)
        {
            if (selectedNodeSet.Contains(edge.From) && selectedNodeSet.Contains(edge.To))
            {
                copiedEdges.Add(new Edge
                {
                    Id = edge.Id,
                    From = edge.From,
                    To = edge.To,
                    FromConnection = edge.FromConnection,
                    ToConnection = edge.ToConnection,
                    Style = edge.Style,
                    IsOrthogonal = edge.IsOrthogonal,
                    StrokeWidth = edge.StrokeWidth,
                    StrokeColor = edge.StrokeColor,
                    StrokeDashArray = edge.StrokeDashArray,
                    IsDoubleLine = edge.IsDoubleLine,
                    Waypoints = edge.Waypoints.Select(w => new Waypoint { X = w.X, Y = w.Y }).ToList(),
                    PathData = edge.PathData
                });
            }
        }

        // Copy labels for copied edges
        var copiedEdgeIds = new HashSet<int>(copiedEdges.Select(e => e.Id));
        foreach (var label in edgeLabels)
        {
            if (copiedEdgeIds.Contains(label.EdgeId))
            {
                copiedLabels.Add(new EdgeLabel
                {
                    Id = label.Id,
                    EdgeId = label.EdgeId,
                    Text = label.Text
                });
            }
        }

        Console.WriteLine($"Copied {copiedNodes.Count} nodes, {copiedEdges.Count} edges, {copiedLabels.Count} labels");
        await Task.CompletedTask;
    }

    private async Task PasteNodes()
    {
        if (copiedNodes.Count == 0) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        const double offsetX = 50;
        const double offsetY = 50;

        // Map old IDs to new IDs
        var nodeIdMap = new Dictionary<int, int>();
        var edgeIdMap = new Dictionary<int, int>();

        // Paste nodes
        foreach (var copiedNode in copiedNodes)
        {
            var newNode = new Node
            {
                Id = nextId++,
                X = copiedNode.X + offsetX,
                Y = copiedNode.Y + offsetY,
                Width = copiedNode.Width,
                Height = copiedNode.Height,
                Text = copiedNode.Text,
                Shape = copiedNode.Shape,
                FillColor = copiedNode.FillColor,
                StrokeColor = copiedNode.StrokeColor
            };

            nodeIdMap[copiedNode.Id] = newNode.Id;
            nodes.Add(newNode);
        }

        // Paste edges with remapped node IDs
        foreach (var copiedEdge in copiedEdges)
        {
            if (nodeIdMap.ContainsKey(copiedEdge.From) && nodeIdMap.ContainsKey(copiedEdge.To))
            {
                var newEdge = new Edge
                {
                    Id = nextEdgeId++,
                    From = nodeIdMap[copiedEdge.From],
                    To = nodeIdMap[copiedEdge.To],
                    FromConnection = copiedEdge.FromConnection,
                    ToConnection = copiedEdge.ToConnection,
                    Style = copiedEdge.Style,
                    IsOrthogonal = copiedEdge.IsOrthogonal,
                    StrokeWidth = copiedEdge.StrokeWidth,
                    StrokeColor = copiedEdge.StrokeColor,
                    StrokeDashArray = copiedEdge.StrokeDashArray,
                    IsDoubleLine = copiedEdge.IsDoubleLine,
                    Waypoints = copiedEdge.Waypoints.Select(w => new Waypoint
                    {
                        X = w.X + offsetX,
                        Y = w.Y + offsetY
                    }).ToList()
                };

                newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
                edgeIdMap[copiedEdge.Id] = newEdge.Id;
                edges.Add(newEdge);
            }
        }

        // Paste labels with remapped edge IDs
        foreach (var copiedLabel in copiedLabels)
        {
            if (edgeIdMap.ContainsKey(copiedLabel.EdgeId))
            {
                var newLabel = new EdgeLabel
                {
                    Id = nextLabelId++,
                    EdgeId = edgeIdMap[copiedLabel.EdgeId],
                    Text = copiedLabel.Text
                };
                edgeLabels.Add(newLabel);
            }
        }

        // Select pasted nodes
        selectedNodes.Clear();
        selectedEdges.Clear();
        foreach (var newNodeId in nodeIdMap.Values)
        {
            selectedNodes.Add(newNodeId);
        }

        Console.WriteLine($"Pasted {nodeIdMap.Count} nodes, {edgeIdMap.Count} edges");
        StateHasChanged();
        await Task.CompletedTask;
    }
}