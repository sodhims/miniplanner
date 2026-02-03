using dfd2wasm.Models;
namespace dfd2wasm.Services;

/// <summary>
/// Interface for managing selection state in the DFD Editor
/// </summary>
public interface ISelectionService
{
    // Properties
    IReadOnlyList<int> SelectedNodes { get; }
    IReadOnlyList<int> SelectedEdges { get; }
    IReadOnlyList<int> SelectedLabels { get; }
    
    bool HasSelection { get; }
    int TotalSelectedCount { get; }

    // Events
    event Action? OnSelectionChanged;

    // Node Selection Methods
    void SelectNode(int nodeId, bool addToSelection = false);
    void SelectNodes(IEnumerable<int> nodeIds, bool addToSelection = false);
    void DeselectNode(int nodeId);
    bool IsNodeSelected(int nodeId);

    // Edge Selection Methods
    void SelectEdge(int edgeId, bool addToSelection = false);
    void SelectEdges(IEnumerable<int> edgeIds, bool addToSelection = false);
    void DeselectEdge(int edgeId);
    bool IsEdgeSelected(int edgeId);

    // Label Selection Methods
    void SelectLabel(int labelId, bool addToSelection = false);
    void SelectLabels(IEnumerable<int> labelIds, bool addToSelection = false);
    void DeselectLabel(int labelId);
    bool IsLabelSelected(int labelId);

    // General Selection Methods
    void ClearSelection();
    void ClearNodeSelection();
    void ClearEdgeSelection();
    void ClearLabelSelection();

    // Multi-Select Methods
    void ToggleNodeSelection(int nodeId);
    void ToggleEdgeSelection(int edgeId);
    void ToggleLabelSelection(int labelId);

    // Bulk Operations
    void SelectAll(IEnumerable<int> nodeIds, IEnumerable<int> edgeIds, IEnumerable<int> labelIds);
    void SelectNodesInArea(IEnumerable<int> nodeIds);
}
