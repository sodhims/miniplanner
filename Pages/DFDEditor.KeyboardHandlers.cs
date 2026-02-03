using Microsoft.AspNetCore.Components.Web;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    #region Shape Keyboard Shortcuts

    /// <summary>
    /// Shape shortcuts: Alt+Shift+Key selects a specific shape from the current template.
    /// Key mappings are defined per template.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> ShapeShortcuts = new()
    {
        // Circuit Template shortcuts
        ["circuit"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["R"] = "resistor",
            ["C"] = "capacitor",
            ["L"] = "inductor",       // L is standard symbol for inductance
            ["D"] = "diode",
            ["T"] = "transistor-npn",
            ["G"] = "ground",
            ["V"] = "vcc",
            ["A"] = "and-gate",
            ["O"] = "or-gate",
            ["N"] = "not-gate",
            ["I"] = "ic-chip",
            ["P"] = "op-amp",
            ["B"] = "battery",
            ["W"] = "ac-source",      // W for Wave (sine wave)
            ["E"] = "dc-source"       // E for Electric/DC
        },

        // Flowchart Template shortcuts
        ["flowchart"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["P"] = "process",
            ["D"] = "decision",
            ["T"] = "terminator",
            ["I"] = "data",           // I/O
            ["B"] = "database",       // DataBase
            ["O"] = "document",       // dOcument
            ["R"] = "predefined"      // pRedefined
        },

        // ICD Template shortcuts
        ["icd"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["S"] = "system",
            ["U"] = "subsystem",      // sUbsystem
            ["X"] = "external-system", // eXternal
            ["H"] = "hardware",
            ["W"] = "software",       // softWare
            ["D"] = "data-interface",
            ["C"] = "control-interface",
            ["P"] = "power-interface",
            ["R"] = "connector-serial",   // seRial
            ["E"] = "connector-ethernet",
            ["B"] = "connector-usb",      // usB
            ["I"] = "connector-wireless", // wIreless
            ["K"] = "interface-block"     // blocK
        },

        // Network Template shortcuts
        ["network"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["R"] = "router",
            ["W"] = "switch",         // sWitch
            ["S"] = "server",
            ["F"] = "firewall",
            ["C"] = "cloud",
            ["D"] = "database-server",
            ["K"] = "workstation",    // worKstation
            ["L"] = "laptop",
            ["M"] = "mobile",
            ["P"] = "printer",
            ["I"] = "internet"
        },

        // BPMN Template shortcuts
        ["bpmn"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["T"] = "task",
            ["S"] = "start-event",
            ["E"] = "end-event",
            ["M"] = "intermediate-event",  // interMediate
            ["X"] = "exclusive-gateway",   // eXclusive
            ["A"] = "parallel-gateway",    // pArallel/AND
            ["O"] = "inclusive-gateway",   // Inclusive/OR
            ["B"] = "subprocess",          // suBprocess
            ["P"] = "pool",
            ["L"] = "lane",
            ["D"] = "data-object",
            ["R"] = "data-store",          // stoRe
            ["N"] = "annotation"           // aNnotation
        },

        // STS Template shortcuts
        ["sts"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["M"] = "machine",
            ["B"] = "buffer",
            ["S"] = "source",
            ["K"] = "sink",           // sinK
            ["J"] = "junction",
            ["T"] = "transformer",
            ["P"] = "splitter",       // sPlitter
            ["G"] = "merger",         // merGer
            ["F"] = "filter",
            ["A"] = "amplifier",
            ["D"] = "delay",
            ["W"] = "switch"          // sWitch
        }
    };

    /// <summary>
    /// Handle Alt+Shift+Key shortcuts to quickly select shapes
    /// </summary>
    /// <returns>True if a shortcut was handled, false otherwise</returns>
    private bool HandleShapeShortcut(KeyboardEventArgs e)
    {
        // Must have Alt+Shift pressed (without Ctrl)
        if (!e.AltKey || !e.ShiftKey || e.CtrlKey)
            return false;

        // Get the key (single character)
        var key = e.Key;
        if (string.IsNullOrEmpty(key) || key.Length != 1)
            return false;

        // Must have a template selected
        if (string.IsNullOrEmpty(selectedTemplateId))
            return false;

        // Check if there's a shortcut for this template and key
        if (!ShapeShortcuts.TryGetValue(selectedTemplateId, out var templateShortcuts))
            return false;

        if (!templateShortcuts.TryGetValue(key.ToUpperInvariant(), out var shapeId))
            return false;

        // Verify the shape exists in the current template
        var shapes = GetShapesForTemplate(selectedTemplateId);
        if (!shapes.Any(s => s.Id == shapeId))
            return false;

        // Select the shape and enter Add mode
        selectedTemplateShapeId = shapeId;
        mode = EditorMode.AddNode;
        chainMode = false;
        ClearConnectMode();
        StateHasChanged();

        Console.WriteLine($"Shape shortcut: Alt+Shift+{key.ToUpperInvariant()} -> {selectedTemplateId}/{shapeId}");
        return true;
    }

    #endregion

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        // In Project mode, delegate to Project-specific handler first
        if (isProjectMode)
        {
            HandleProjectKeyDown(e);
            // Let Project handler process Delete/Backspace/Enter/Escape
            if (e.Key == "Delete" || e.Key == "Backspace" || e.Key == "Enter" || e.Key == "Escape")
                return;
        }

        // In Gantt mode, handle Delete/Backspace for selected Gantt task or edges
        if (isGanttMode && (e.Key == "Delete" || e.Key == "Backspace"))
        {
            if (selectedGanttTaskId.HasValue)
            {
                DeleteSelectedGanttTask();
                return;
            }
            // Also handle edge deletion in Gantt mode
            if (selectedEdges.Any())
            {
                DeleteSelectedEdges();
                return;
            }
        }

        // Alt+Shift+Key - Shape shortcuts (check first to avoid conflicts)
        if (e.AltKey && e.ShiftKey && !e.CtrlKey)
        {
            if (HandleShapeShortcut(e))
                return;
        }

        // Escape - cancel current operation (smart handling for 1:N mode)
        if (e.Key == "Escape")
        {
            CancelCurrentOperation();
            return;
        }

        // Delete - delete selected items
        if (e.Key == "Delete" || e.Key == "Backspace")
        {
            if (selectedNodes.Any() || selectedEdges.Any() || selectedLabels.Any())
            {
                DeleteSelected();
            }
            return;
        }

        // Ctrl+Z - Undo
        if (e.CtrlKey && e.Key == "z")
        {
            HandleUndo();
            return;
        }

        // Ctrl+A - Select all
        if (e.CtrlKey && e.Key == "a")
        {
            SelectAll();
            return;
        }

        // Ctrl+C - Copy
        if (e.CtrlKey && e.Key == "c")
        {
            await CopySelected();
            return;
        }

        // Ctrl+V - Paste
        if (e.CtrlKey && e.Key == "v")
        {
            await PasteNodes();
            return;
        }

        // Ctrl+R - Rotate terminals clockwise
        // Ctrl+Shift+R - Rotate terminals counter-clockwise
        if (e.CtrlKey && (e.Key == "r" || e.Key == "R"))
        {
            RotateSelectedTerminals(!e.ShiftKey);
            return;
        }

        // S - Select mode (single select)
        if (e.Key == "s" || e.Key == "S")
        {
            if (!e.CtrlKey && !e.AltKey)
            {
                mode = EditorMode.Select;
                selectMode = SelectModeType.Single;
                ClearConnectMode();
                StateHasChanged();
                return;
            }
        }

        // C - Connect mode (Two-Click as default)
        if (e.Key == "c" || e.Key == "C")
        {
            if (!e.CtrlKey && !e.AltKey)
            {
                mode = EditorMode.Select;
                ActivateTwoClickMode();
                StateHasChanged();
                return;
            }
        }

        // A - Add shape mode
        if (e.Key == "a" || e.Key == "A")
        {
            if (!e.CtrlKey && !e.AltKey)
            {
                mode = EditorMode.AddNode;
                chainMode = false;
                ClearConnectMode();
                StateHasChanged();
                return;
            }
        }

        // T - Toggle terminal mode
        if (e.Key == "t" || e.Key == "T")
        {
            if (!e.CtrlKey && !e.AltKey)
            {
                terminalModeEnabled = !terminalModeEnabled;
                StateHasChanged();
                return;
            }
        }

        // +/= - Zoom in
        if (e.Key == "+" || e.Key == "=")
        {
            ZoomIn();
            return;
        }

        // - - Zoom out
        if (e.Key == "-")
        {
            ZoomOut();
            return;
        }

        // 0 - Reset zoom
        if (e.Key == "0" && e.CtrlKey)
        {
            ResetZoom();
            return;
        }

        // Arrow keys - nudge selected nodes
        if (selectedNodes.Any())
        {
            double dx = 0, dy = 0;
            double step = e.ShiftKey ? 10 : (snapToGrid ? GridSize : 1);

            switch (e.Key)
            {
                case "ArrowUp": dy = -step; break;
                case "ArrowDown": dy = step; break;
                case "ArrowLeft": dx = -step; break;
                case "ArrowRight": dx = step; break;
            }

            if (dx != 0 || dy != 0)
            {
                NudgeSelectedNodes(dx, dy);
            }
        }
    }

    private void CancelCurrentOperation()
    {
        // Special handling for TwoClick mode
        if (connectionMode == ConnectionModeType.TwoClick)
        {
            if (twoClickSourceNode.HasValue)
            {
                // If we have a source selected, just clear it (stay in mode)
                twoClickSourceNode = null;
                selectedNodes.Clear();
                StateHasChanged();
                return;
            }
            // If no source, exit the mode entirely
            connectionMode = ConnectionModeType.Normal;
            StateHasChanged();
            return;
        }

        // Special handling for Chain mode
        if (connectionMode == ConnectionModeType.Chain)
        {
            if (lastChainedNodeId.HasValue)
            {
                // If we have a chain started, just clear it (stay in mode)
                lastChainedNodeId = null;
                selectedNodes.Clear();
                StateHasChanged();
                return;
            }
            // If no chain started, exit the mode entirely
            connectionMode = ConnectionModeType.Normal;
            chainMode = false;
            StateHasChanged();
            return;
        }

        // Special handling for 1:N modes - just reset source, stay in mode
        if (connectionMode == ConnectionModeType.OneToN || connectionMode == ConnectionModeType.OneToNArea)
        {
            // If we have a source selected, just clear it (stay in mode)
            if (oneToNSourceNode.HasValue)
            {
                oneToNSourceNode = null;
                oneToNSourcePoint = null;
                oneToNSourceCoords = null;
                isOneToNAreaSelecting = false;
                oneToNAreaStart = null;
                StateHasChanged();
                return;
            }
            // If no source, exit the mode entirely
            connectionMode = ConnectionModeType.Normal;
            StateHasChanged();
            return;
        }

        // Cancel any pending connections
        pendingConnectionNodeId = null;
        pendingConnection = null;
        pendingConnectionPoint = null;

        // Cancel pending terminal connection
        pendingTerminalNodeId = null;
        pendingTerminalType = null;

        // Cancel chain mode (legacy)
        chainMode = false;
        lastChainedNodeId = null;

        // Cancel two-click mode
        twoClickSourceNode = null;
        
        // Cancel selection
        isSelecting = false;
        selectionStart = null;
        
        // Cancel edge reconnection
        pendingEdgeReconnectId = null;
        pendingEdgeReconnectEnd = null;
        
        // Clear text editing
        showTextEditDialog = false;
        editingTextNodeId = null;
        editingTextLabelId = null;
        
        StateHasChanged();
    }

    private void SelectAll()
    {
        selectedNodes.Clear();
        selectedEdges.Clear();
        selectedLabels.Clear();

        foreach (var node in nodes)
        {
            selectedNodes.Add(node.Id);
        }

        StateHasChanged();
    }

    private void NudgeSelectedNodes(double dx, double dy)
    {
        UndoService.SaveState(nodes, edges, edgeLabels);

        foreach (var nodeId in selectedNodes)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                node.X += dx;
                node.Y += dy;
            }
        }

        RecalculateEdgePaths();
        StateHasChanged();
    }

    /// <summary>
    /// Rotate terminal layout for selected nodes
    /// </summary>
    /// <param name="clockwise">True for clockwise, false for counter-clockwise</param>
    private void RotateSelectedTerminals(bool clockwise)
    {
        if (!selectedNodes.Any()) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        foreach (var nodeId in selectedNodes)
        {
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null && node.ShowTerminals)
            {
                if (clockwise)
                {
                    node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
                }
                else
                {
                    // Counter-clockwise: rotate 3 times clockwise = 1 time counter-clockwise
                    node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
                    node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
                    node.TerminalLayout = TerminalLayouts.GetNextLayout(node.TerminalLayout);
                }
            }
        }

        // Recalculate edge paths since terminal positions changed
        RecalculateEdgePaths();
        StateHasChanged();
    }

    private const int GridSize = 20;
}
