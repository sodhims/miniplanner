using Microsoft.AspNetCore.Components.Web;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

/// <summary>
/// Context menu handling for nodes
/// </summary>
public partial class DFDEditor
{
    /// <summary>
    /// Show the node context menu at the mouse position
    /// </summary>
    private void ShowNodeContextMenu(int nodeId, MouseEventArgs e)
    {
        contextMenuNodeId = nodeId;
        contextMenuX = e.ClientX;
        contextMenuY = e.ClientY;
        showNodeContextMenu = true;

        // Also select the node if not already selected
        if (!selectedNodes.Contains(nodeId))
        {
            selectedNodes.Clear();
            selectedNodes.Add(nodeId);
        }

        StateHasChanged();
    }

    /// <summary>
    /// Hide the context menu
    /// </summary>
    private void HideNodeContextMenu()
    {
        showNodeContextMenu = false;
        StateHasChanged();
    }

    /// <summary>
    /// Toggle terminals visibility on a node
    /// </summary>
    private void ToggleNodeTerminals(int nodeId)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            node.ShowTerminals = !node.ShowTerminals;
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Rotate terminals on the context menu node
    /// </summary>
    private void RotateContextNodeTerminals(bool clockwise)
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null && node.ShowTerminals)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            if (clockwise)
            {
                node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
            }
            else
            {
                // Counter-clockwise: rotate 3 times
                node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
                node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
                node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
            }

            RecalculateEdgePaths();
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Rotate the node 90 degrees clockwise
    /// </summary>
    private void RotateNodeClockwise()
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            node.Rotation = (node.Rotation + 90) % 360;
            RecalculateEdgePaths();
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Rotate the node 90 degrees counter-clockwise
    /// </summary>
    private void RotateNodeCounterClockwise()
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            node.Rotation = (node.Rotation + 270) % 360; // +270 is same as -90
            RecalculateEdgePaths();
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Reset node rotation to 0 degrees
    /// </summary>
    private void ResetNodeRotation()
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null && node.Rotation != 0)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            node.Rotation = 0;
            RecalculateEdgePaths();
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Delete the node that was right-clicked
    /// </summary>
    private void DeleteContextNode()
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            // Remove edges connected to this node
            edges.RemoveAll(e => e.From == contextMenuNodeId || e.To == contextMenuNodeId);

            // Remove the node
            nodes.Remove(node);

            // Remove from selection
            selectedNodes.Remove(contextMenuNodeId);

            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Add an extra input terminal to the left of the node
    /// </summary>
    private void AddExtraInputTerminalLeft()
    {
        AddExtraTerminal("left", TerminalType.Input);
    }

    /// <summary>
    /// Add an extra input terminal to the right of the node
    /// </summary>
    private void AddExtraInputTerminalRight()
    {
        AddExtraTerminal("right", TerminalType.Input);
    }

    /// <summary>
    /// Add an extra input terminal to the top of the node
    /// </summary>
    private void AddExtraInputTerminalTop()
    {
        AddExtraTerminal("top", TerminalType.Input);
    }

    /// <summary>
    /// Add an extra input terminal to the bottom of the node
    /// </summary>
    private void AddExtraInputTerminalBottom()
    {
        AddExtraTerminal("bottom", TerminalType.Input);
    }

    /// <summary>
    /// Add an extra output terminal to the left of the node
    /// </summary>
    private void AddExtraOutputTerminalLeft()
    {
        AddExtraTerminal("left", TerminalType.Output);
    }

    /// <summary>
    /// Add an extra output terminal to the right of the node
    /// </summary>
    private void AddExtraOutputTerminalRight()
    {
        AddExtraTerminal("right", TerminalType.Output);
    }

    /// <summary>
    /// Add an extra output terminal to the top of the node
    /// </summary>
    private void AddExtraOutputTerminalTop()
    {
        AddExtraTerminal("top", TerminalType.Output);
    }

    /// <summary>
    /// Add an extra output terminal to the bottom of the node
    /// </summary>
    private void AddExtraOutputTerminalBottom()
    {
        AddExtraTerminal("bottom", TerminalType.Output);
    }

    /// <summary>
    /// Add a bidirectional terminal to the left of the node
    /// </summary>
    private void AddBidirectionalTerminalLeft()
    {
        AddExtraTerminal("left", TerminalType.Bidirectional);
    }

    /// <summary>
    /// Add a bidirectional terminal to the right of the node
    /// </summary>
    private void AddBidirectionalTerminalRight()
    {
        AddExtraTerminal("right", TerminalType.Bidirectional);
    }

    /// <summary>
    /// Add a bidirectional terminal to the top of the node
    /// </summary>
    private void AddBidirectionalTerminalTop()
    {
        AddExtraTerminal("top", TerminalType.Bidirectional);
    }

    /// <summary>
    /// Add a bidirectional terminal to the bottom of the node
    /// </summary>
    private void AddBidirectionalTerminalBottom()
    {
        AddExtraTerminal("bottom", TerminalType.Bidirectional);
    }

    /// <summary>
    /// Add an extra terminal to the context menu node
    /// </summary>
    private void AddExtraTerminal(string side, TerminalType type)
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            // Enable terminals if not already
            if (!node.ShowTerminals)
            {
                node.ShowTerminals = true;
            }

            // Calculate position - stack terminals along the side
            var existingOnSide = node.ExtraTerminals.Count(t => t.Side == side);
            var position = existingOnSide + 1; // Start at 1 to leave room for default terminal

            // Alternate above/below center
            if (existingOnSide % 2 == 0)
                position = (existingOnSide / 2) + 1;
            else
                position = -((existingOnSide / 2) + 1);

            var newTerminal = new ExtraTerminal
            {
                Id = node.ExtraTerminals.Count > 0 ? node.ExtraTerminals.Max(t => t.Id) + 1 : 1,
                Side = side,
                Position = position,
                Type = type
            };

            node.ExtraTerminals.Add(newTerminal);
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    /// <summary>
    /// Remove all extra terminals from a node
    /// </summary>
    private void ClearExtraTerminals()
    {
        var node = nodes.FirstOrDefault(n => n.Id == contextMenuNodeId);
        if (node != null && node.ExtraTerminals.Count > 0)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);
            node.ExtraTerminals.Clear();
            StateHasChanged();
        }
        HideNodeContextMenu();
    }

    #region SuperNode Context Menu Methods

    /// <summary>
    /// Context menu handler to create a SuperNode from selection
    /// </summary>
    private void CreateSuperNodeFromContextMenu()
    {
        CreateSuperNodeFromSelection();
        HideNodeContextMenu();
    }

    /// <summary>
    /// Context menu handler to expand a SuperNode
    /// </summary>
    private void ExpandSuperNodeFromContextMenu()
    {
        ExpandSuperNode(contextMenuNodeId);
        HideNodeContextMenu();
    }

    /// <summary>
    /// Context menu handler to collapse a SuperNode
    /// </summary>
    private void CollapseSuperNodeFromContextMenu()
    {
        CollapseSuperNode(contextMenuNodeId);
        HideNodeContextMenu();
    }

    /// <summary>
    /// Context menu handler to delete/ungroup a SuperNode
    /// </summary>
    private void DeleteSuperNodeFromContextMenu()
    {
        DeleteSuperNode(contextMenuNodeId);
        HideNodeContextMenu();
    }

    #endregion
}
