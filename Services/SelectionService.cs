using dfd2wasm.Models;
namespace dfd2wasm.Services;

/// <summary>
/// Service for managing selection state in the DFD Editor.
/// Handles selection of nodes, edges, and labels with support for multi-selection.
/// </summary>
public class SelectionService : ISelectionService
{
    private readonly List<int> _selectedNodes = new();
    private readonly List<int> _selectedEdges = new();
    private readonly List<int> _selectedLabels = new();

    // Public read-only access to selections
    public IReadOnlyList<int> SelectedNodes => _selectedNodes.AsReadOnly();
    public IReadOnlyList<int> SelectedEdges => _selectedEdges.AsReadOnly();
    public IReadOnlyList<int> SelectedLabels => _selectedLabels.AsReadOnly();

    // Computed properties
    public bool HasSelection => _selectedNodes.Count > 0 || _selectedEdges.Count > 0 || _selectedLabels.Count > 0;
    public int TotalSelectedCount => _selectedNodes.Count + _selectedEdges.Count + _selectedLabels.Count;

    // Event raised when selection changes
    public event Action? OnSelectionChanged;

    #region Node Selection

    /// <summary>
    /// Selects a node. If addToSelection is false, clears previous selection first.
    /// </summary>
    public void SelectNode(int nodeId, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelection();
        }

        if (!_selectedNodes.Contains(nodeId))
        {
            _selectedNodes.Add(nodeId);
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Selects multiple nodes at once
    /// </summary>
    public void SelectNodes(IEnumerable<int> nodeIds, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelection();
        }

        var changed = false;
        foreach (var nodeId in nodeIds)
        {
            if (!_selectedNodes.Contains(nodeId))
            {
                _selectedNodes.Add(nodeId);
                changed = true;
            }
        }

        if (changed)
        {
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Removes a node from selection
    /// </summary>
    public void DeselectNode(int nodeId)
    {
        if (_selectedNodes.Remove(nodeId))
        {
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Checks if a node is currently selected
    /// </summary>
    public bool IsNodeSelected(int nodeId)
    {
        return _selectedNodes.Contains(nodeId);
    }

    /// <summary>
    /// Toggles node selection (select if not selected, deselect if selected)
    /// </summary>
    public void ToggleNodeSelection(int nodeId)
    {
        if (_selectedNodes.Contains(nodeId))
        {
            _selectedNodes.Remove(nodeId);
        }
        else
        {
            _selectedNodes.Add(nodeId);
        }
        NotifySelectionChanged();
    }

    #endregion

    #region Edge Selection

    /// <summary>
    /// Selects an edge. If addToSelection is false, clears previous selection first.
    /// </summary>
    public void SelectEdge(int edgeId, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelection();
        }

        if (!_selectedEdges.Contains(edgeId))
        {
            _selectedEdges.Add(edgeId);
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Selects multiple edges at once
    /// </summary>
    public void SelectEdges(IEnumerable<int> edgeIds, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelection();
        }

        var changed = false;
        foreach (var edgeId in edgeIds)
        {
            if (!_selectedEdges.Contains(edgeId))
            {
                _selectedEdges.Add(edgeId);
                changed = true;
            }
        }

        if (changed)
        {
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Removes an edge from selection
    /// </summary>
    public void DeselectEdge(int edgeId)
    {
        if (_selectedEdges.Remove(edgeId))
        {
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Checks if an edge is currently selected
    /// </summary>
    public bool IsEdgeSelected(int edgeId)
    {
        return _selectedEdges.Contains(edgeId);
    }

    /// <summary>
    /// Toggles edge selection (select if not selected, deselect if selected)
    /// </summary>
    public void ToggleEdgeSelection(int edgeId)
    {
        if (_selectedEdges.Contains(edgeId))
        {
            _selectedEdges.Remove(edgeId);
        }
        else
        {
            _selectedEdges.Add(edgeId);
        }
        NotifySelectionChanged();
    }

    #endregion

    #region Label Selection

    /// <summary>
    /// Selects a label. If addToSelection is false, clears previous selection first.
    /// </summary>
    public void SelectLabel(int labelId, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelection();
        }

        if (!_selectedLabels.Contains(labelId))
        {
            _selectedLabels.Add(labelId);
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Selects multiple labels at once
    /// </summary>
    public void SelectLabels(IEnumerable<int> labelIds, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            ClearSelection();
        }

        var changed = false;
        foreach (var labelId in labelIds)
        {
            if (!_selectedLabels.Contains(labelId))
            {
                _selectedLabels.Add(labelId);
                changed = true;
            }
        }

        if (changed)
        {
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Removes a label from selection
    /// </summary>
    public void DeselectLabel(int labelId)
    {
        if (_selectedLabels.Remove(labelId))
        {
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Checks if a label is currently selected
    /// </summary>
    public bool IsLabelSelected(int labelId)
    {
        return _selectedLabels.Contains(labelId);
    }

    /// <summary>
    /// Toggles label selection (select if not selected, deselect if selected)
    /// </summary>
    public void ToggleLabelSelection(int labelId)
    {
        if (_selectedLabels.Contains(labelId))
        {
            _selectedLabels.Remove(labelId);
        }
        else
        {
            _selectedLabels.Add(labelId);
        }
        NotifySelectionChanged();
    }

    #endregion

    #region Clear Selection Methods

    /// <summary>
    /// Clears all selections (nodes, edges, and labels)
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedNodes.Count == 0 && _selectedEdges.Count == 0 && _selectedLabels.Count == 0)
        {
            return; // Nothing to clear
        }

        _selectedNodes.Clear();
        _selectedEdges.Clear();
        _selectedLabels.Clear();
        NotifySelectionChanged();
    }

    /// <summary>
    /// Clears only node selection
    /// </summary>
    public void ClearNodeSelection()
    {
        if (_selectedNodes.Count > 0)
        {
            _selectedNodes.Clear();
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Clears only edge selection
    /// </summary>
    public void ClearEdgeSelection()
    {
        if (_selectedEdges.Count > 0)
        {
            _selectedEdges.Clear();
            NotifySelectionChanged();
        }
    }

    /// <summary>
    /// Clears only label selection
    /// </summary>
    public void ClearLabelSelection()
    {
        if (_selectedLabels.Count > 0)
        {
            _selectedLabels.Clear();
            NotifySelectionChanged();
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Selects multiple items at once
    /// </summary>
    public void SelectAll(IEnumerable<int> nodeIds, IEnumerable<int> edgeIds, IEnumerable<int> labelIds)
    {
        ClearSelection();

        _selectedNodes.AddRange(nodeIds);
        _selectedEdges.AddRange(edgeIds);
        _selectedLabels.AddRange(labelIds);

        NotifySelectionChanged();
    }

    /// <summary>
    /// Selects nodes within a specific area (e.g., selection box)
    /// </summary>
    public void SelectNodesInArea(IEnumerable<int> nodeIds)
    {
        SelectNodes(nodeIds, addToSelection: false);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Notifies subscribers that the selection has changed
    /// </summary>
    private void NotifySelectionChanged()
    {
        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Gets a summary of current selection for debugging
    /// </summary>
    public string GetSelectionSummary()
    {
        return $"Nodes: {_selectedNodes.Count}, Edges: {_selectedEdges.Count}, Labels: {_selectedLabels.Count}";
    }

    #endregion
}
