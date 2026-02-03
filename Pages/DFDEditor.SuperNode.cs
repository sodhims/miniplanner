using dfd2wasm.Models;

namespace dfd2wasm.Pages;

/// <summary>
/// Partial class for SuperNode operations - grouping/collapsing multiple nodes
/// </summary>
public partial class DFDEditor
{
    /// <summary>
    /// Creates a SuperNode from the currently selected nodes
    /// </summary>
    private void CreateSuperNodeFromSelection()
    {
        if (selectedNodes.Count < 2) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        var containedIds = selectedNodes.ToList();

        // Block nested SuperNodes for now
        if (containedIds.Any(id => nodes.FirstOrDefault(n => n.Id == id)?.IsSuperNode == true))
        {
            Console.WriteLine("Cannot create SuperNode containing other SuperNodes");
            return;
        }

        // Calculate center of the bounding box for positioning
        var (bboxX, bboxY, bboxWidth, bboxHeight) = CalculateSuperNodeBounds(containedIds);

        // Use standard node size (same as regular nodes)
        const double standardWidth = 120;
        const double standardHeight = 60;

        // Position at center of bounding box
        var centerX = bboxX + bboxWidth / 2 - standardWidth / 2;
        var centerY = bboxY + bboxHeight / 2 - standardHeight / 2;

        // Create the SuperNode with standard size
        var superNode = new Node
        {
            Id = nextId++,
            X = centerX,
            Y = centerY,
            Width = standardWidth,
            Height = standardHeight,
            Text = $"Group ({containedIds.Count})",
            IsSuperNode = true,
            IsCollapsed = true,
            ContainedNodeIds = containedIds,
            Shape = NodeShape.Rectangle,
            StrokeColor = "#dc2626",  // Red border for SuperNodes
            StrokeWidth = 3,
            FillColor = "#fef3c7"  // Yellow background for visibility
        };

        nodes.Add(superNode);

        // Mark contained nodes with parent reference
        foreach (var nodeId in containedIds)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                node.ParentSuperNodeId = superNode.Id;
            }
        }

        // Remap boundary edges
        RemapEdgesToSuperNode(superNode, containedIds);

        // Select the new SuperNode
        selectedNodes.Clear();
        selectedNodes.Add(superNode.Id);
        selectedEdges.Clear();

        // Compact the remaining graph around the SuperNode
        var visibleNodes = nodes.Where(n => ShouldRenderNode(n)).ToList();
        var visibleEdges = edges.Where(e => ShouldRenderEdge(e)).ToList();
        LayoutOptimization.CompactArea(visibleNodes, visibleEdges, superNode, reductionFactor: 0.3);

        RecalculateEdgePaths();
        StateHasChanged();

        Console.WriteLine($"Created SuperNode {superNode.Id} containing {containedIds.Count} nodes");
    }

    /// <summary>
    /// Expands a collapsed SuperNode to show its contained nodes
    /// </summary>
    private void ExpandSuperNode(int superNodeId)
    {
        var superNode = nodes.FirstOrDefault(n => n.Id == superNodeId && n.IsSuperNode);
        if (superNode == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        superNode.IsCollapsed = false;

        // Restore remapped edges to their original endpoints
        RestoreEdgesFromSuperNode(superNode);

        // Unhide internal edges
        foreach (var edge in edges.Where(e => e.IsHiddenInternal))
        {
            var fromInside = superNode.ContainedNodeIds.Contains(edge.From);
            var toInside = superNode.ContainedNodeIds.Contains(edge.To);
            if (fromInside && toInside)
            {
                edge.IsHiddenInternal = false;
            }
        }

        // Select the contained nodes
        selectedNodes.Clear();
        selectedNodes.AddRange(superNode.ContainedNodeIds);
        selectedEdges.Clear();

        RecalculateEdgePaths();
        StateHasChanged();

        Console.WriteLine($"Expanded SuperNode {superNodeId}");
    }

    /// <summary>
    /// Collapses an expanded SuperNode to hide its contained nodes
    /// </summary>
    private void CollapseSuperNode(int superNodeId)
    {
        var superNode = nodes.FirstOrDefault(n => n.Id == superNodeId && n.IsSuperNode);
        if (superNode == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        superNode.IsCollapsed = true;

        // Remap edges again
        RemapEdgesToSuperNode(superNode, superNode.ContainedNodeIds);

        // Select the SuperNode
        selectedNodes.Clear();
        selectedNodes.Add(superNode.Id);
        selectedEdges.Clear();

        RecalculateEdgePaths();
        StateHasChanged();

        Console.WriteLine($"Collapsed SuperNode {superNodeId}");
    }

    /// <summary>
    /// Deletes a SuperNode and ungroups its contained nodes
    /// </summary>
    private void DeleteSuperNode(int superNodeId)
    {
        var superNode = nodes.FirstOrDefault(n => n.Id == superNodeId && n.IsSuperNode);
        if (superNode == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        // First expand if collapsed
        if (superNode.IsCollapsed)
        {
            superNode.IsCollapsed = false;
            RestoreEdgesFromSuperNode(superNode);
        }

        // Clear parent references from contained nodes
        foreach (var nodeId in superNode.ContainedNodeIds)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                node.ParentSuperNodeId = null;
            }
        }

        // Unhide any internal edges
        foreach (var edge in edges.Where(e => e.IsHiddenInternal))
        {
            var fromInside = superNode.ContainedNodeIds.Contains(edge.From);
            var toInside = superNode.ContainedNodeIds.Contains(edge.To);
            if (fromInside && toInside)
            {
                edge.IsHiddenInternal = false;
            }
        }

        // Remove the SuperNode
        nodes.Remove(superNode);

        // Select the formerly contained nodes
        selectedNodes.Clear();
        selectedNodes.AddRange(superNode.ContainedNodeIds.Where(id => nodes.Any(n => n.Id == id)));
        selectedEdges.Clear();

        RecalculateEdgePaths();
        StateHasChanged();

        Console.WriteLine($"Deleted SuperNode {superNodeId}, ungrouped {superNode.ContainedNodeIds.Count} nodes");
    }

    /// <summary>
    /// Calculates the bounding box for a set of nodes with padding
    /// </summary>
    private (double x, double y, double width, double height) CalculateSuperNodeBounds(List<int> nodeIds)
    {
        const double padding = 20;

        var containedNodes = nodes.Where(n => nodeIds.Contains(n.Id)).ToList();
        if (containedNodes.Count == 0)
            return (0, 0, 120, 60);

        var minX = containedNodes.Min(n => n.X) - padding;
        var minY = containedNodes.Min(n => n.Y) - padding;
        var maxX = containedNodes.Max(n => n.X + n.Width) + padding;
        var maxY = containedNodes.Max(n => n.Y + n.Height) + padding;

        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Remaps boundary edges to connect to the SuperNode instead of contained nodes
    /// </summary>
    private void RemapEdgesToSuperNode(Node superNode, List<int> containedIds)
    {
        var containedSet = containedIds.ToHashSet();

        foreach (var edge in edges)
        {
            var fromInside = containedSet.Contains(edge.From);
            var toInside = containedSet.Contains(edge.To);

            if (fromInside && toInside)
            {
                // Internal edge - hide it
                edge.IsHiddenInternal = true;
            }
            else if (fromInside && !toInside)
            {
                // Edge goes FROM inside TO outside - remap From to SuperNode
                if (edge.OriginalFrom == null)
                    edge.OriginalFrom = edge.From;
                edge.From = superNode.Id;

                // Update connection point
                var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
                if (toNode != null)
                {
                    var (fromConn, toConn) = GetSmartConnectionPoints(superNode, toNode);
                    edge.FromConnection = fromConn;
                    edge.ToConnection = toConn;
                }
            }
            else if (!fromInside && toInside)
            {
                // Edge goes FROM outside TO inside - remap To to SuperNode
                if (edge.OriginalTo == null)
                    edge.OriginalTo = edge.To;
                edge.To = superNode.Id;

                // Update connection point
                var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
                if (fromNode != null)
                {
                    var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, superNode);
                    edge.FromConnection = fromConn;
                    edge.ToConnection = toConn;
                }
            }
        }
    }

    /// <summary>
    /// Restores edges to their original endpoints when SuperNode is expanded.
    /// Also handles edges that were created while the SuperNode was collapsed
    /// by remapping them to the nearest contained node.
    /// </summary>
    private void RestoreEdgesFromSuperNode(Node superNode)
    {
        Console.WriteLine($"RestoreEdgesFromSuperNode: SuperNode {superNode.Id}, ContainedIds: [{string.Join(", ", superNode.ContainedNodeIds)}]");

        foreach (var edge in edges)
        {
            Console.WriteLine($"  Edge {edge.Id}: From={edge.From}, To={edge.To}, OriginalFrom={edge.OriginalFrom}, OriginalTo={edge.OriginalTo}");

            // Restore From endpoint if it was remapped (edge existed before collapse)
            if (edge.OriginalFrom.HasValue && edge.From == superNode.Id)
            {
                Console.WriteLine($"    -> Restoring From to OriginalFrom={edge.OriginalFrom.Value}");
                edge.From = edge.OriginalFrom.Value;
                edge.OriginalFrom = null;

                // Recalculate connection point
                var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
                var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
                if (fromNode != null && toNode != null)
                {
                    var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, toNode);
                    edge.FromConnection = fromConn;
                    edge.ToConnection = toConn;
                }
            }
            // Handle edges created while collapsed (no OriginalFrom, but From points to SuperNode)
            else if (!edge.OriginalFrom.HasValue && edge.From == superNode.Id)
            {
                // Find the closest contained node to the target
                var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
                var nearestContained = FindNearestContainedNode(superNode, toNode);
                Console.WriteLine($"    -> Edge created while collapsed (From=SuperNode). Remapping to nearest contained: {nearestContained?.Id}");
                if (nearestContained != null)
                {
                    edge.From = nearestContained.Id;

                    // Recalculate connection point
                    if (toNode != null)
                    {
                        var (fromConn, toConn) = GetSmartConnectionPoints(nearestContained, toNode);
                        edge.FromConnection = fromConn;
                        edge.ToConnection = toConn;
                    }
                }
            }

            // Restore To endpoint if it was remapped (edge existed before collapse)
            if (edge.OriginalTo.HasValue && edge.To == superNode.Id)
            {
                edge.To = edge.OriginalTo.Value;
                edge.OriginalTo = null;

                // Recalculate connection point
                var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
                var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
                if (fromNode != null && toNode != null)
                {
                    var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, toNode);
                    edge.FromConnection = fromConn;
                    edge.ToConnection = toConn;
                }
            }
            // Handle edges created while collapsed (no OriginalTo, but To points to SuperNode)
            else if (!edge.OriginalTo.HasValue && edge.To == superNode.Id)
            {
                // Find the closest contained node to the source
                var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
                var nearestContained = FindNearestContainedNode(superNode, fromNode);
                Console.WriteLine($"    -> Edge created while collapsed (To=SuperNode). Remapping to nearest contained: {nearestContained?.Id}");
                if (nearestContained != null)
                {
                    edge.To = nearestContained.Id;

                    // Recalculate connection point
                    if (fromNode != null)
                    {
                        var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, nearestContained);
                        edge.FromConnection = fromConn;
                        edge.ToConnection = toConn;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Finds the contained node nearest to a reference node (for edge remapping on expand)
    /// </summary>
    private Node? FindNearestContainedNode(Node superNode, Node? referenceNode)
    {
        if (superNode.ContainedNodeIds.Count == 0) return null;

        var containedNodes = nodes.Where(n => superNode.ContainedNodeIds.Contains(n.Id)).ToList();
        if (containedNodes.Count == 0) return null;

        // If no reference, return the first contained node
        if (referenceNode == null) return containedNodes.First();

        // Find the nearest contained node to the reference
        double refCenterX = referenceNode.X + referenceNode.Width / 2;
        double refCenterY = referenceNode.Y + referenceNode.Height / 2;

        return containedNodes
            .OrderBy(n => {
                double centerX = n.X + n.Width / 2;
                double centerY = n.Y + n.Height / 2;
                return Math.Sqrt(Math.Pow(centerX - refCenterX, 2) + Math.Pow(centerY - refCenterY, 2));
            })
            .First();
    }

    /// <summary>
    /// Checks if a node should be rendered (not hidden inside a collapsed SuperNode)
    /// </summary>
    private bool ShouldRenderNode(Node node)
    {
        // In node view mode (!isProjectMode), apply FILO behavior for Project groups
        if (!isProjectMode)
        {
            // Project SuperNodes always render in node view (the group container)
            if (node.IsSuperNode && node.TemplateId == "project")
                return true;

            // Project tasks that belong to a group: FILO behavior
            // - Collapsed group: children are hidden (inside the container)
            // - Expanded group: children are visible (came out of container)
            if (node.ParentSuperNodeId.HasValue && node.TemplateId == "project")
            {
                var parent = nodes.FirstOrDefault(n => n.Id == node.ParentSuperNodeId.Value);
                if (parent != null && parent.IsSuperNode)
                {
                    // FILO: only show children when group is expanded
                    return !parent.IsCollapsed;
                }
            }
        }

        // For non-Project SuperNodes: expanded SuperNodes should not be rendered as nodes themselves
        // (their contained nodes are visible instead)
        // But Project SuperNodes in node view should always render (handled above)
        if (node.IsSuperNode && !node.IsCollapsed && node.TemplateId != "project")
            return false;

        // Nodes inside a collapsed SuperNode should not be rendered
        if (node.ParentSuperNodeId.HasValue)
        {
            var parent = nodes.FirstOrDefault(n => n.Id == node.ParentSuperNodeId.Value);
            if (parent != null && parent.IsSuperNode && parent.IsCollapsed)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if an edge should be rendered
    /// </summary>
    private bool ShouldRenderEdge(Edge edge)
    {
        // Hidden internal edges should not be rendered
        if (edge.IsHiddenInternal)
            return false;

        // Check if From node is visible
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        if (fromNode != null && !ShouldRenderNode(fromNode))
            return false;

        // Check if To node is visible
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
        if (toNode != null && !ShouldRenderNode(toNode))
            return false;

        return true;
    }

    /// <summary>
    /// Handles double-click on a node - expands/collapses SuperNodes (FILO), opens properties dialog for regular nodes
    /// </summary>
    private void HandleNodeDoubleClick(int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        if (node.IsSuperNode)
        {
            // FILO behavior: double-click toggles expand/collapse
            if (node.IsCollapsed)
            {
                ExpandSuperNode(nodeId);
            }
            else
            {
                CollapseSuperNode(nodeId);
            }
        }
        else
        {
            // Regular node - show properties dialog for logical properties
            ShowNodePropertiesDialog(nodeId);
        }
    }

    /// <summary>
    /// Gets the parent SuperNode ID for a node if it belongs to an expanded SuperNode
    /// </summary>
    private int? GetExpandedParentSuperNodeId(int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node?.ParentSuperNodeId == null) return null;

        var parent = nodes.FirstOrDefault(n => n.Id == node.ParentSuperNodeId.Value);
        if (parent != null && parent.IsSuperNode && !parent.IsCollapsed)
        {
            return parent.Id;
        }
        return null;
    }

    /// <summary>
    /// Checks if any selected node belongs to an expanded SuperNode
    /// </summary>
    private int? GetSelectedNodesExpandedParent()
    {
        foreach (var nodeId in selectedNodes)
        {
            var parentId = GetExpandedParentSuperNodeId(nodeId);
            if (parentId.HasValue) return parentId;
        }
        return null;
    }
}
