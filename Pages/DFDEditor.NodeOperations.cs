namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    #region Node Alignment

    private void AlignLeft()
    {
        if (selectedNodes.Count < 2) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToAlign = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        var minX = nodesToAlign.Min(n => n.X);
        foreach (var node in nodesToAlign)
        {
            node.X = minX;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void AlignCenterH()
    {
        if (selectedNodes.Count < 2) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToAlign = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        var centerX = nodesToAlign.Average(n => n.X + n.Width / 2);
        foreach (var node in nodesToAlign)
        {
            node.X = centerX - node.Width / 2;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void AlignRight()
    {
        if (selectedNodes.Count < 2) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToAlign = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        var maxRight = nodesToAlign.Max(n => n.X + n.Width);
        foreach (var node in nodesToAlign)
        {
            node.X = maxRight - node.Width;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void AlignTop()
    {
        if (selectedNodes.Count < 2) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToAlign = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        var minY = nodesToAlign.Min(n => n.Y);
        foreach (var node in nodesToAlign)
        {
            node.Y = minY;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void AlignCenterV()
    {
        if (selectedNodes.Count < 2) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToAlign = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        var centerY = nodesToAlign.Average(n => n.Y + n.Height / 2);
        foreach (var node in nodesToAlign)
        {
            node.Y = centerY - node.Height / 2;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void AlignBottom()
    {
        if (selectedNodes.Count < 2) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToAlign = nodes.Where(n => selectedNodes.Contains(n.Id)).ToList();
        var maxBottom = nodesToAlign.Max(n => n.Y + n.Height);
        foreach (var node in nodesToAlign)
        {
            node.Y = maxBottom - node.Height;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void DistributeH()
    {
        if (selectedNodes.Count < 3) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToDistribute = nodes.Where(n => selectedNodes.Contains(n.Id))
            .OrderBy(n => n.X).ToList();
        var leftmost = nodesToDistribute.First();
        var rightmost = nodesToDistribute.Last();
        var totalWidth = (rightmost.X + rightmost.Width) - leftmost.X;
        var nodesWidth = nodesToDistribute.Sum(n => n.Width);
        var spacing = (totalWidth - nodesWidth) / (nodesToDistribute.Count - 1);
        var currentX = leftmost.X;
        foreach (var node in nodesToDistribute)
        {
            node.X = currentX;
            currentX += node.Width + spacing;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    private void DistributeV()
    {
        if (selectedNodes.Count < 3) return;
        UndoService.SaveState(nodes, edges, edgeLabels);
        var nodesToDistribute = nodes.Where(n => selectedNodes.Contains(n.Id))
            .OrderBy(n => n.Y).ToList();
        var topmost = nodesToDistribute.First();
        var bottommost = nodesToDistribute.Last();
        var totalHeight = (bottommost.Y + bottommost.Height) - topmost.Y;
        var nodesHeight = nodesToDistribute.Sum(n => n.Height);
        var spacing = (totalHeight - nodesHeight) / (nodesToDistribute.Count - 1);
        var currentY = topmost.Y;
        foreach (var node in nodesToDistribute)
        {
            node.Y = currentY;
            currentY += node.Height + spacing;
            RecalculateEdgePaths(node.Id);
        }
        StateHasChanged();
    }

    #endregion

    #region Node Text Editing

    private void UpdateNodeText(int nodeId, string newText)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null)
        {
            node.Text = newText;
            StateHasChanged();
        }
    }

    private void StartEditingSelectedNode()
    {
        if (selectedNodes.Count != 1) return;
        var nodeId = selectedNodes.First();
        StartEditingNode(nodeId);
    }

    private void StartEditingNode(int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        editingTextNodeId = nodeId;
        editingTextLabelId = null;
        editingText = node.Text;
        showTextEditDialog = true;
    }

    #endregion

    #region Node Properties Dialog

    /// <summary>Show node properties dialog for logical properties</summary>
    private void ShowNodePropertiesDialog(int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        propertiesDialogNode = node;
        showNodePropertiesDialog = true;
        StateHasChanged();
    }

    /// <summary>Close node properties dialog</summary>
    private void CloseNodePropertiesDialog()
    {
        showNodePropertiesDialog = false;
        propertiesDialogNode = null;
        StateHasChanged();
    }

    #endregion
}
