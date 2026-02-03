using dfd2wasm.Models;
using Microsoft.AspNetCore.Components.Web;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    private void ToggleOrthoMode()
    {
        useOrthoPlacement = !useOrthoPlacement;
        StateHasChanged();
    }

    private void HandleUndo()
    {
        var state = UndoService.Undo();
        if (state != null)
        {
            nodes.Clear();
            edges.Clear();
            edgeLabels.Clear();
            freehandStrokes.Clear();
            drawingShapes.Clear();

            nodes.AddRange(state.Nodes);
            edges.AddRange(state.Edges);
            edgeLabels.AddRange(state.EdgeLabels);
            freehandStrokes.AddRange(state.FreehandStrokes);
            drawingShapes.AddRange(state.DrawingShapes);

            // Update ID counters
            if (nodes.Any())
                nextId = nodes.Max(n => n.Id) + 1;
            if (edges.Any())
                nextEdgeId = edges.Max(e => e.Id) + 1;
            if (edgeLabels.Any())
                nextLabelId = edgeLabels.Max(l => l.Id) + 1;
            if (freehandStrokes.Any())
                nextStrokeId = freehandStrokes.Max(s => s.Id) + 1;
            if (drawingShapes.Any())
                nextShapeId = drawingShapes.Max(s => s.Id) + 1;

            selectedNodes.Clear();
            selectedEdges.Clear();
            selectedLabels.Clear();
            selectedStrokes.Clear();
            selectedDrawingShapes.Clear();

            RecalculateEdgePaths();
            StateHasChanged();
        }
    }

    private void ConfirmClear()
    {
        UndoService.SaveState(nodes, edges, edgeLabels, freehandStrokes, drawingShapes);

        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        freehandStrokes.Clear();
        drawingShapes.Clear();
        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();
        selectedStrokes.Clear();
        selectedDrawingShapes.Clear();
        nextId = 1;
        nextEdgeId = 1;
        nextLabelId = 1;
        nextStrokeId = 1;
        nextShapeId = 1;

        showClearConfirm = false;
        StateHasChanged();
    }

    #region Text Editing

    private void StartTextEdit(int? nodeId, int? labelId, string currentText)
    {
        editingTextNodeId = nodeId;
        editingTextLabelId = labelId;
        editingText = currentText;
        showTextEditDialog = true;
        StateHasChanged();
    }

    private void SaveTextEdit()
    {
        if (editingTextNodeId.HasValue)
        {
            var node = nodes.FirstOrDefault(n => n.Id == editingTextNodeId.Value);
            if (node != null)
            {
                UndoService.SaveState(nodes, edges, edgeLabels);
                node.Text = editingText;
            }
        }
        else if (editingTextLabelId.HasValue)
        {
            var label = edgeLabels.FirstOrDefault(l => l.Id == editingTextLabelId.Value);
            if (label != null)
            {
                UndoService.SaveState(nodes, edges, edgeLabels);
                label.Text = editingText;
            }
        }

        CancelTextEdit();
    }

    private void CancelTextEdit()
    {
        showTextEditDialog = false;
        editingTextNodeId = null;
        editingTextLabelId = null;
        editingText = "";
        StateHasChanged();
    }

    private void HandleTextEditKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            CancelTextEdit();
        }
        else if (e.Key == "Enter" && e.CtrlKey)
        {
            SaveTextEdit();
        }
    }

    #endregion

    #region Swimlane Labels

    private void UpdateSwimlaneLabel(int index, string newLabel)
    {
        // Swimlanes would be stored in a list - implement based on your data structure
        // For now, this is a placeholder
        StateHasChanged();
    }

    private void UpdateColumnLabel(int index, string newLabel)
    {
        // Columns would be stored in a list - implement based on your data structure
        // For now, this is a placeholder
        StateHasChanged();
    }

    #endregion

}
