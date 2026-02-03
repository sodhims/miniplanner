using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    private async Task HandleCanvasMouseDown(MouseEventArgs e)
    {
        try
        {
            Console.WriteLine($"HandleCanvasMouseDown - Mode: {mode}, PrintAreaSelection: {isPrintAreaSelection}");

            var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
            double scrollOffsetX = scrollInfo?[0] ?? 0;
            double scrollOffsetY = scrollInfo?[1] ?? 0;

            // For canvas mouse down (selection start), use OffsetX/Y since target is canvas/SVG
            double diagX = (e.OffsetX + scrollOffsetX) / zoomLevel;
            double diagY = (e.OffsetY + scrollOffsetY) / zoomLevel;

            if (isPrintAreaSelection)
            {
                isSelecting = true;
                selectionStart = (diagX, diagY);
                Console.WriteLine($"Print area selection started at ({diagX}, {diagY})");
            }
            else if ((mode == EditorMode.Select || selectToolActive) && !e.ShiftKey && !e.CtrlKey)
            {
                isSelecting = true;
                selectionStart = (diagX, diagY);
                Console.WriteLine($"Selection started at ({diagX}, {diagY})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION in HandleCanvasMouseDown: {ex.Message}");
        }
    }

    private async Task HandleCanvasMouseMove(MouseEventArgs e)
    {
        try
        {
            var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
            double scrollOffsetX = scrollInfo?[0] ?? 0;
            double scrollOffsetY = scrollInfo?[1] ?? 0;

            // Use ClientX/Y with canvas bounds for consistent coordinates
            // e.OffsetX/Y is unreliable because it's relative to whichever element
            // received the event (could be canvas div, SVG, node group, etc.)
            var canvasBounds = await JS.InvokeAsync<double[]>("getCanvasBounds", canvasRef);
            double canvasLeft = canvasBounds?[0] ?? 0;
            double canvasTop = canvasBounds?[1] ?? 0;

            double mouseInCanvasX = e.ClientX - canvasLeft;
            double mouseInCanvasY = e.ClientY - canvasTop;
            double diagX = (mouseInCanvasX + scrollOffsetX) / zoomLevel;
            double diagY = (mouseInCanvasY + scrollOffsetY) / zoomLevel;

            currentMousePosition = (diagX, diagY);
            lastMouseX = diagX;
            lastMouseY = diagY;

            svgMouseX = diagX;
            svgMouseY = diagY;
        }
        catch
        {
            // If JS call fails, fall back to raw offsets
            currentMousePosition = (e.OffsetX, e.OffsetY);
            lastMouseX = e.OffsetX;
            lastMouseY = e.OffsetY;
            svgMouseX = e.OffsetX;
            svgMouseY = e.OffsetY;
        }

        // Update rubberbanding position if active
        if (isRubberbanding)
        {
            UpdateRubberbandPosition(currentMousePosition.X, currentMousePosition.Y);
        }

        // IMPORTANT: Check draggingNodeId FIRST - it takes priority over area selection
        // This fixes the bug where clicking on a node started both selection AND dragging
        if (draggingNodeId != null)
        {
            var node = nodes.FirstOrDefault(n => n.Id == draggingNodeId);
            if (node != null)
            {
                // Use diagram coordinates (already scroll/zoom adjusted)
                double newX = currentMousePosition.X - dragOffsetX;
                double newY = currentMousePosition.Y - dragOffsetY;

                double deltaX = newX - node.X;
                double deltaY = newY - node.Y;

                if (e.ShiftKey)
                {
                    var dx = Math.Abs(newX - dragStartX);
                    var dy = Math.Abs(newY - dragStartY);
                    if (dx > dy)
                    {
                        deltaY = 0;
                        newY = node.Y;
                    }
                    else
                    {
                        deltaX = 0;
                        newX = node.X;
                    }
                }

                node.X = newX;
                node.Y = newY;
                
                if (selectedNodes.Count > 1)
                {
                    foreach (var selectedNodeId in selectedNodes)
                    {
                        if (selectedNodeId != draggingNodeId.Value)
                        {
                            var selectedNode = nodes.FirstOrDefault(n => n.Id == selectedNodeId);
                            if (selectedNode != null)
                            {
                                selectedNode.X += deltaX;
                                selectedNode.Y += deltaY;
                                RecalculateEdgePaths(selectedNodeId);
                            }
                        }
                    }
                }
                
                RecalculateEdgePaths(draggingNodeId.Value);
                StateHasChanged();
            }
        }
        else if (resizingNodeId != null)
        {
            var node = nodes.FirstOrDefault(n => n.Id == resizingNodeId);
            if (node != null)
            {
                double newWidth = Math.Max(40, currentMousePosition.X - node.X);
                double newHeight = Math.Max(30, currentMousePosition.Y - node.Y);

                node.Width = newWidth;
                node.Height = newHeight;

                RecalculateEdgePaths(resizingNodeId.Value);
                StateHasChanged();
            }
        }
        else if (draggingEdgeId != null && draggingWaypointIndex >= 0)
        {
            var edge = edges.FirstOrDefault(e => e.Id == draggingEdgeId);
            if (edge != null && draggingWaypointIndex < edge.Waypoints.Count)
            {
                edge.Waypoints[draggingWaypointIndex] = new Waypoint
                {
                    X = currentMousePosition.X,
                    Y = currentMousePosition.Y
                };
                UpdateEdgePath(edge);
                StateHasChanged();
            }
        }
        else if (isSelecting && selectionStart.HasValue)
        {
            // Area selection - render the selection rectangle
            StateHasChanged();
        }
    }

    private async Task HandleCanvasMouseUp(MouseEventArgs e)
    {
        if (isSelecting)
        {
            if (isPrintAreaSelection)
            {
                var rect = GetSelectionRectangle();
                printArea = (rect.X, rect.Y, rect.Width, rect.Height);
                
                isSelecting = false;
                selectionStart = null;
                isPrintAreaSelection = false;
                
                _ = ExportToPDF();
            }
            else
            {
                var rect = GetSelectionRectangle();
                selectedNodes.Clear();
                selectedEdges.Clear();

                // Select nodes within rectangle
                foreach (var node in nodes)
                {
                    if (node.X >= rect.X && node.X <= rect.X + rect.Width &&
                        node.Y >= rect.Y && node.Y <= rect.Y + rect.Height)
                    {
                        selectedNodes.Add(node.Id);
                    }
                }

                // Select edges where BOTH endpoints are within the selection
                foreach (var edge in edges)
                {
                    if (selectedNodes.Contains(edge.From) && selectedNodes.Contains(edge.To))
                    {
                        selectedEdges.Add(edge.Id);
                    }
                }

                isSelecting = false;
                selectionStart = null;
            }
        }

        // Auto-connect terminals if we were dragging nodes
        if (draggingNodeId != null)
        {
            var draggedNode = nodes.FirstOrDefault(n => n.Id == draggingNodeId);

            // Check for resource-to-task assignment in Project mode
            if (isProjectMode && draggedNode != null && draggedNode.IsProjectResource)
            {
                // Find if resource was dropped on a task
                var targetTask = nodes.FirstOrDefault(n =>
                    n.TemplateId == "project" &&
                    !n.IsProjectResource &&
                    !n.IsSuperNode &&
                    IsPointInNode(e.OffsetX, e.OffsetY, n));

                if (targetTask != null)
                {
                    Console.WriteLine($"Resource {draggedNode.Id} dropped on task {targetTask.Id}");
                    AssignResourceToTask(draggedNode.Id, targetTask.Id);

                    // Move resource back to original position (or a resource panel area)
                    // For now, position it near the task
                    draggedNode.X = targetTask.X + targetTask.Width + 10;
                    draggedNode.Y = targetTask.Y;
                }
            }

            // Collect all nodes that were dragged (primary + any selected nodes that moved with it)
            var movedNodeIds = new List<int> { draggingNodeId.Value };
            if (selectedNodes.Count > 1 && selectedNodes.Contains(draggingNodeId.Value))
            {
                movedNodeIds.AddRange(selectedNodes.Where(id => id != draggingNodeId.Value));
            }

            // Try to auto-connect terminals that are now touching
            int edgesCreated = TryAutoConnectTerminals(movedNodeIds);
            if (edgesCreated > 0)
            {
                Console.WriteLine($"Auto-connected {edgesCreated} terminal(s)");
            }
        }

        draggingNodeId = null;
        resizingNodeId = null;
        draggingEdgeId = null;
        draggingWaypointIndex = -1;
    }

    /// <summary>Checks if a point is inside a node's bounding box</summary>
    private bool IsPointInNode(double x, double y, Node node)
    {
        return x >= node.X && x <= node.X + node.Width &&
               y >= node.Y && y <= node.Y + node.Height;
    }

    private async Task HandleCanvasClick(MouseEventArgs e)
    {
        try
        {
            Console.WriteLine("=== HandleCanvasClick START ===");
            Console.WriteLine($"Raw event: ClientX={e.ClientX}, ClientY={e.ClientY}, OffsetX={e.OffsetX}, OffsetY={e.OffsetY}");

            var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
            double scrollOffsetX = scrollInfo?[0] ?? 0;
            double scrollOffsetY = scrollInfo?[1] ?? 0;

            // For canvas click, use OffsetX/Y since the click target is the canvas div or SVG
            // The OffsetX/Y values are relative to the target element, which works correctly
            // for clicks on the canvas background
            double clickX = (e.OffsetX + scrollOffsetX) / zoomLevel;
            double clickY = (e.OffsetY + scrollOffsetY) / zoomLevel;

            Console.WriteLine($"Mode: {mode}, selectToolActive: {selectToolActive}, isRubberbanding: {isRubberbanding}");
            Console.WriteLine($"Template: {selectedTemplateId}, Shape: {selectedTemplateShapeId}");
            Console.WriteLine($"ClickX: {clickX}, ClickY: {clickY} (scroll: {scrollOffsetX}, {scrollOffsetY}, zoom: {zoomLevel})");

            // Cancel rubberbanding if clicking on empty canvas (not on a terminal)
            if (isRubberbanding)
            {
                // Check if we clicked on a terminal - if so, let HandleTerminalClick handle it
                // The terminal click may not have fired due to RenderFragment event issues
                foreach (var node in nodes)
                {
                    if (!node.ShowTerminals) continue;

                    var clickedTerminal = GetTerminalAtPosition(node, clickX, clickY);
                    if (clickedTerminal != null)
                    {
                        Console.WriteLine($"Canvas click detected terminal hit: node={node.Id}, terminal={clickedTerminal}");
                        // Manually trigger terminal click since the RenderFragment event didn't fire
                        HandleTerminalClick(node.Id, clickedTerminal);
                        return;
                    }
                }

                Console.WriteLine("Canceling rubberbanding - clicked on empty canvas");
                CancelRubberbanding();
                return;
            }

            if (mode == EditorMode.Select || selectToolActive)
            {
                Console.WriteLine($"In Select mode (mode={mode}, selectToolActive={selectToolActive}), clearing edges and returning");
                if (selectedEdges.Count > 0)
                {
                    selectedEdges.Clear();
                    StateHasChanged();
                }
                return;
            }

            if (mode != EditorMode.AddNode)
            {
                Console.WriteLine($"Not in AddNode mode (current mode={mode}), returning");
                return;
            }

            // Special handling for Project shapes - create Project nodes on timeline
            if (selectedTemplateId == "project" && isProjectMode && projectTimeline != null)
            {
                Console.WriteLine($"Creating Project shape: {selectedTemplateShapeId} at ({clickX}, {clickY})");
                CreateProjectNodeAtPosition(clickX, clickY, selectedTemplateShapeId ?? "task");
                StateHasChanged();
                return;
            }

            Console.WriteLine($"Array Mode: {arrayMode}, Count: {arrayCount}, Orientation: {arrayOrientation}");

            UndoService.SaveState(nodes, edges, edgeLabels);

            if (arrayMode)
            {
                for (int i = 0; i < arrayCount; i++)
                {
                    double nodeX, nodeY;

                    if (arrayOrientation == "horizontal")
                    {
                        nodeX = clickX - 60 + (i * arraySpacing);
                        nodeY = clickY - 30;
                    }
                    else
                    {
                        nodeX = clickX - 60;
                        nodeY = clickY - 30 + (i * arraySpacing);
                    }

                    // Generate component label for circuit components
                    string? componentLabel = null;
                    string nodeText = $"Node {nodes.Count + 1}";
                    if (selectedTemplateId == "circuit" && !string.IsNullOrEmpty(selectedTemplateShapeId))
                    {
                        componentLabel = GetNextComponentLabel(selectedTemplateShapeId);
                        nodeText = componentLabel;
                    }

                    var nodeDefaults = GetCurrentNodeDefaults();
                    var terminalConfig = GetEffectiveTerminalConfig(selectedTemplateId, selectedTemplateShapeId);
                    var terminalLayout = GetEffectiveTerminalLayout(selectedTemplateId, selectedTemplateShapeId);
                    var terminalPositions = GetEffectiveTerminalPositions(selectedTemplateId, selectedTemplateShapeId);
                    var newNode = new Node
                    {
                        Id = nextId++,
                        X = nodeX,
                        Y = nodeY,
                        Width = 120,
                        Height = 60,
                        Text = nodeText,
                        Shape = selectedShape,
                        TemplateId = selectedTemplateId,
                        TemplateShapeId = selectedTemplateShapeId,
                        ComponentLabel = componentLabel,
                        ShowTerminals = terminalModeEnabled || selectedTemplateId == "sts" || selectedTemplateId == "circuit" || selectedTemplateId == "qmaker",
                        FillColor = nodeDefaults.FillColor,
                        StrokeColor = nodeDefaults.StrokeColor,
                        StrokeWidth = nodeDefaults.StrokeWidth,
                        InputTerminalColor = nodeDefaults.InputTerminalColor,
                        OutputTerminalColor = nodeDefaults.OutputTerminalColor,
                        InputTerminalType = terminalConfig.inputType,
                        OutputTerminalType = terminalConfig.outputType,
                        HasThirdTerminal = terminalConfig.hasThird,
                        ThirdTerminalType = terminalConfig.thirdType,
                        InputTerminalCount = terminalConfig.inputCount,
                        OutputTerminalCount = terminalConfig.outputCount,
                        TerminalLayout = terminalLayout,
                        // Precise terminal positions (null = use TerminalLayout)
                        T1X = terminalPositions?.T1X,
                        T1Y = terminalPositions?.T1Y,
                        T2X = terminalPositions?.T2X,
                        T2Y = terminalPositions?.T2Y,
                        T3X = terminalPositions?.T3X,
                        T3Y = terminalPositions?.T3Y
                    };

                    nodes.Add(newNode);
                    Console.WriteLine($"Array node {i + 1} created at ({nodeX}, {nodeY})");

                    // Check if the new node was placed on an edge - if so, split the edge
                    var edgeToSplit = FindEdgeAtRectangle(newNode.X, newNode.Y, newNode.Width, newNode.Height);
                    if (edgeToSplit != null)
                    {
                        Console.WriteLine($"Array node placed on edge {edgeToSplit.Id}, splitting...");
                        SplitEdgeWithNode(edgeToSplit, newNode);
                    }
                }
            }
            else
            {
                // Generate component label for circuit components
                string? componentLabel = null;
                string nodeText = $"Node {nodes.Count + 1}";
                if (selectedTemplateId == "circuit" && !string.IsNullOrEmpty(selectedTemplateShapeId))
                {
                    componentLabel = GetNextComponentLabel(selectedTemplateShapeId);
                    nodeText = componentLabel;
                }

                var nodeDefaults = GetCurrentNodeDefaults();
                var terminalConfig = GetEffectiveTerminalConfig(selectedTemplateId, selectedTemplateShapeId);
                var terminalLayout = GetEffectiveTerminalLayout(selectedTemplateId, selectedTemplateShapeId);
                var terminalPositions = GetEffectiveTerminalPositions(selectedTemplateId, selectedTemplateShapeId);
                var newNode = new Node
                {
                    Id = nextId++,
                    X = clickX - 60,
                    Y = clickY - 30,
                    Width = 120,
                    Height = 60,
                    Text = nodeText,
                    Shape = selectedShape,
                    TemplateId = selectedTemplateId,
                    TemplateShapeId = selectedTemplateShapeId,
                    ComponentLabel = componentLabel,
                    ShowTerminals = terminalModeEnabled || selectedTemplateId == "sts" || selectedTemplateId == "circuit" || selectedTemplateId == "qmaker",
                    FillColor = nodeDefaults.FillColor,
                    StrokeColor = nodeDefaults.StrokeColor,
                    StrokeWidth = nodeDefaults.StrokeWidth,
                    InputTerminalColor = nodeDefaults.InputTerminalColor,
                    OutputTerminalColor = nodeDefaults.OutputTerminalColor,
                    InputTerminalType = terminalConfig.inputType,
                    OutputTerminalType = terminalConfig.outputType,
                    HasThirdTerminal = terminalConfig.hasThird,
                    ThirdTerminalType = terminalConfig.thirdType,
                    InputTerminalCount = terminalConfig.inputCount,
                    OutputTerminalCount = terminalConfig.outputCount,
                    TerminalLayout = terminalLayout,
                    // Precise terminal positions (null = use TerminalLayout)
                    T1X = terminalPositions?.T1X,
                    T1Y = terminalPositions?.T1Y,
                    T2X = terminalPositions?.T2X,
                    T2Y = terminalPositions?.T2Y,
                    T3X = terminalPositions?.T3X,
                    T3Y = terminalPositions?.T3Y
                };

                Console.WriteLine($"Node created with ID: {newNode.Id}");
                nodes.Add(newNode);

                // Check if the new node was placed on an edge - if so, split the edge
                var edgeToSplit = FindEdgeAtRectangle(newNode.X, newNode.Y, newNode.Width, newNode.Height);
                if (edgeToSplit != null)
                {
                    Console.WriteLine($"Node placed on edge {edgeToSplit.Id}, splitting...");
                    SplitEdgeWithNode(edgeToSplit, newNode);
                }
            }

            Console.WriteLine("=== HandleCanvasClick END ===");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EXCEPTION in HandleCanvasClick: {ex.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private void HandleNodeClick(int nodeId, MouseEventArgs e)
    {
        Console.WriteLine($"HandleNodeClick - NodeId: {nodeId}, Mode: {mode}, isRubberbanding={isRubberbanding}");
        Console.WriteLine($"  chainMode={chainMode}, connectionMode={connectionMode}, lastChainedNodeId={lastChainedNodeId}");

        // Don't handle node clicks during rubberbanding - let terminal clicks handle it
        if (isRubberbanding)
        {
            Console.WriteLine("  Skipping HandleNodeClick - rubberbanding is active");
            return;
        }

        if (!(mode == EditorMode.Select || selectToolActive)) return;

        var clickedNode = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (clickedNode == null) return;

        // CHAIN MODE - handle early before any terminal logic
        // Chain mode connects nodes directly, ignoring terminal-specific behavior
        if (connectionMode == ConnectionModeType.Chain || chainMode)
        {
            Console.WriteLine($"  CHAIN MODE: lastChainedNodeId={lastChainedNodeId}");

            if (lastChainedNodeId.HasValue && lastChainedNodeId.Value != nodeId)
            {
                Console.WriteLine($"  CREATING EDGE: {lastChainedNodeId.Value} -> {nodeId}");

                UndoService.SaveState(nodes, edges, edgeLabels);

                var fromNode = nodes.FirstOrDefault(n => n.Id == lastChainedNodeId.Value);

                if (fromNode != null)
                {
                    var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, clickedNode);

                    var newEdge = CreateEdgeWithDefaults(lastChainedNodeId.Value, nodeId, fromConn, toConn);
                    newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
                    edges.Add(newEdge);
                    Console.WriteLine($"  Edge created: {lastChainedNodeId.Value} -> {nodeId}");
                }
            }

            lastChainedNodeId = nodeId;
            selectedNodes.Clear();
            selectedNodes.Add(nodeId);
            Console.WriteLine($"  Set lastChainedNodeId to {nodeId}");
            StateHasChanged();
            return;
        }

        // Handle TwoClick mode EARLY - before any terminal-specific logic
        // This ensures TwoClick mode works consistently regardless of node type
        if (connectionMode == ConnectionModeType.TwoClick)
        {
            Console.WriteLine($"  TWO-CLICK MODE: twoClickSourceNode={twoClickSourceNode}");

            if (!twoClickSourceNode.HasValue)
            {
                // First click - set source
                twoClickSourceNode = nodeId;
                selectedNodes.Clear();
                selectedNodes.Add(nodeId);
                Console.WriteLine($"  Set source node: {nodeId}");
            }
            else if (twoClickSourceNode.Value != nodeId)
            {
                // Second click - create edge
                Console.WriteLine($"  CREATING EDGE: {twoClickSourceNode.Value} -> {nodeId}");

                UndoService.SaveState(nodes, edges, edgeLabels);

                var fromNode = nodes.FirstOrDefault(n => n.Id == twoClickSourceNode.Value);
                var toNode = nodes.FirstOrDefault(n => n.Id == nodeId);

                if (fromNode != null && toNode != null)
                {
                    var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, toNode);

                    var newEdge = CreateEdgeWithDefaults(twoClickSourceNode.Value, nodeId, fromConn, toConn);
                    newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
                    edges.Add(newEdge);
                }

                // Clear source and selection - ready for next pair
                twoClickSourceNode = null;
                selectedNodes.Clear();
                Console.WriteLine($"  Edge created, ready for next TwoClick connection");
            }
            else
            {
                // Clicked same node - deselect/cancel
                twoClickSourceNode = null;
                selectedNodes.Clear();
                Console.WriteLine($"  Clicked same node, clearing source");
            }

            StateHasChanged();
            return;
        }

        // Handle 1:N mode EARLY - before any terminal-specific logic
        // This ensures 1:N mode works consistently regardless of node type
        if (connectionMode == ConnectionModeType.OneToN)
        {
            Console.WriteLine($"  ONE-TO-N MODE: oneToNSourceNode={oneToNSourceNode}");
            if (HandleOneToNNodeClick(nodeId))
            {
                selectedNodes.Clear();
                selectedNodes.Add(nodeId);
                StateHasChanged();
                return;
            }
        }

        // Handle 1:N Area mode EARLY - first click sets source
        if (connectionMode == ConnectionModeType.OneToNArea)
        {
            Console.WriteLine($"  ONE-TO-N AREA MODE: oneToNSourceNode={oneToNSourceNode}");
            if (HandleOneToNAreaNodeClick(nodeId))
            {
                selectedNodes.Clear();
                selectedNodes.Add(nodeId);
                StateHasChanged();
                return;
            }
        }

        // For circuit template, detect which terminal was clicked
        // For other templates, use input/output semantics
        bool isCircuitMode = clickedNode.TemplateId == "circuit";

        // Calculate click position in diagram coordinates
        // e.OffsetX/Y is relative to the clicked element (the node group)
        // which has transform="translate(node.X, node.Y)"
        // So absolute position = node position + local offset
        var clickX = clickedNode.X + e.OffsetX;
        var clickY = clickedNode.Y + e.OffsetY;

        Console.WriteLine($"  Click coords: e.Offset=({e.OffsetX:F1},{e.OffsetY:F1}), node=({clickedNode.X},{clickedNode.Y}), absolute=({clickX:F1},{clickY:F1})");
        Console.WriteLine($"  svgMouse=({svgMouseX:F1},{svgMouseY:F1}) for comparison");

        // Detect which terminal was clicked (if any)
        string? clickedTerminal = null;
        if (clickedNode.ShowTerminals)
        {
            clickedTerminal = GetTerminalAtPosition(clickedNode, clickX, clickY);
            Console.WriteLine($"  Clicked terminal: {clickedTerminal ?? "none (node body)"}");
        }

        // Check if we have a pending connection from a previous click
        if (pendingTerminalNodeId.HasValue && pendingTerminalNodeId.Value != nodeId)
        {
            // Complete the connection to this node
            var fromNode = nodes.FirstOrDefault(n => n.Id == pendingTerminalNodeId.Value);
            if (fromNode != null)
            {
                // Check if connection is allowed based on connectivity types
                if (!IsConnectionAllowed(fromNode, clickedNode))
                {
                    var reason = GetConnectionBlockedReason(fromNode, clickedNode);
                    Console.WriteLine($"  Connection blocked: {reason}");
                    // Clear pending state and deselect
                    pendingTerminalNodeId = null;
                    pendingTerminalType = null;
                    selectedNodes.Clear();
                    StateHasChanged();
                    return;
                }

                UndoService.SaveState(nodes, edges, edgeLabels);

                var fromSide = GetTerminalSide(fromNode, pendingTerminalType!);
                var fromConn = new ConnectionPoint { Side = fromSide, Position = 0 };

                ConnectionPoint toConn;
                if (clickedNode.ShowTerminals)
                {
                    // Target has terminals - determine which one to connect to
                    string toTerminalType;
                    if (isCircuitMode && clickedTerminal != null)
                    {
                        // Circuit mode: use the clicked terminal
                        toTerminalType = clickedTerminal;
                    }
                    else
                    {
                        // Non-circuit or no specific terminal clicked: use input terminal
                        toTerminalType = "input";
                    }
                    var toSide = GetTerminalSide(clickedNode, toTerminalType);
                    toConn = new ConnectionPoint { Side = toSide, Position = 0 };
                    Console.WriteLine($"  Created terminal-to-terminal edge: {pendingTerminalNodeId.Value} ({fromSide}) -> {nodeId} ({toSide})");
                }
                else
                {
                    // Target has no terminals - connect to best side
                    (_, toConn) = GeometryService.GetOptimalConnectionPoints(fromNode, clickedNode);
                    Console.WriteLine($"  Created terminal-to-node edge: {pendingTerminalNodeId.Value} ({fromSide}) -> {nodeId} ({toConn.Side})");
                }

                var newEdge = CreateEdgeWithDefaults(pendingTerminalNodeId.Value, nodeId, fromConn, toConn);
                newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
                edges.Add(newEdge);
            }

            // Clear pending state
            pendingTerminalNodeId = null;
            pendingTerminalType = null;
            selectedNodes.Clear();
            StateHasChanged();
            return;
        }

        // Check if we have a non-terminal node selected and clicking on a node with terminals
        if (selectedNodes.Count == 1 && selectedNodes[0] != nodeId)
        {
            var fromNodeId = selectedNodes[0];
            var fromNode = nodes.FirstOrDefault(n => n.Id == fromNodeId);
            if (fromNode != null)
            {
                // Check if connection is allowed based on connectivity types
                if (!IsConnectionAllowed(fromNode, clickedNode))
                {
                    var reason = GetConnectionBlockedReason(fromNode, clickedNode);
                    Console.WriteLine($"  Connection blocked: {reason}");
                    // Clear selection
                    selectedNodes.Clear();
                    StateHasChanged();
                    return;
                }

                UndoService.SaveState(nodes, edges, edgeLabels);

                ConnectionPoint fromConn;
                ConnectionPoint toConn;

                if (fromNode.ShowTerminals)
                {
                    // Source has terminals - use output terminal (with rotation applied)
                    var fromSide = GetTerminalSide(fromNode, "output");
                    fromConn = new ConnectionPoint { Side = fromSide, Position = 0 };
                }
                else
                {
                    // Source has no terminals - use geometry
                    (fromConn, _) = GeometryService.GetOptimalConnectionPoints(fromNode, clickedNode);
                }

                if (clickedNode.ShowTerminals)
                {
                    // Target has terminals - determine which one to connect to
                    string toTerminalType;
                    if (isCircuitMode && clickedTerminal != null)
                    {
                        // Circuit mode: use the clicked terminal
                        toTerminalType = clickedTerminal;
                    }
                    else
                    {
                        // Non-circuit or no specific terminal clicked: use input terminal
                        toTerminalType = "input";
                    }
                    var toSide = GetTerminalSide(clickedNode, toTerminalType);
                    toConn = new ConnectionPoint { Side = toSide, Position = 0 };
                }
                else
                {
                    // Target has no terminals - use geometry
                    (_, toConn) = GeometryService.GetOptimalConnectionPoints(fromNode, clickedNode);
                }

                var newEdge = CreateEdgeWithDefaults(fromNodeId, nodeId, fromConn, toConn);
                newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
                edges.Add(newEdge);

                Console.WriteLine($"  Created edge: {fromNodeId} ({fromConn.Side}) -> {nodeId} ({toConn.Side})");
            }

            selectedNodes.Clear();
            pendingTerminalNodeId = null;
            pendingTerminalType = null;
            StateHasChanged();
            return;
        }

        // First click on a node - only start connection if clicked DIRECTLY on a terminal
        // Clicking on node body should just select the node
        if (clickedNode.ShowTerminals && clickedTerminal != null)
        {
            // User clicked directly on a terminal - start a connection
            pendingTerminalNodeId = nodeId;
            pendingTerminalType = clickedTerminal;
            selectedNodes.Clear();
            selectedNodes.Add(nodeId);
            Console.WriteLine($"  Started connection from node {nodeId}, terminal: {clickedTerminal}");
            StateHasChanged();
            return;
        }

        // Node has terminals but click was on body (not terminal) - fall through to normal selection

        // In Normal or Rearrange mode, just select/deselect nodes - no connection logic
        if (connectionMode == ConnectionModeType.Normal || connectionMode == ConnectionModeType.Rearrange)
        {
            if (e.ShiftKey || e.CtrlKey)
            {
                if (selectedNodes.Contains(nodeId))
                    selectedNodes.Remove(nodeId);
                else
                    selectedNodes.Add(nodeId);
            }
            else
            {
                selectedNodes.Clear();
                selectedNodes.Add(nodeId);
            }
            selectedEdges.Clear();
            StateHasChanged();
            return;
        }

        // NOTE: TwoClick, Chain, and 1:N modes are handled at the top of this function

        // Handle pending connection from connection point
        if (pendingConnectionNodeId.HasValue && pendingConnection != null)
        {
            if (pendingConnectionNodeId.Value == nodeId)
            {
                pendingConnectionNodeId = null;
                pendingConnection = null;
                return;
            }

            UndoService.SaveState(nodes, edges, edgeLabels);

            var toNode = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (toNode != null)
            {
                var fromNode = nodes.FirstOrDefault(n => n.Id == pendingConnectionNodeId.Value);
                var (_, toConn) = GetSmartConnectionPoints(fromNode!, toNode);

                var newEdge = CreateEdgeWithDefaults(pendingConnectionNodeId.Value, nodeId, pendingConnection!, toConn);
                newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);

                edges.Add(newEdge);
                pendingConnectionNodeId = null;
                pendingConnection = null;
                selectedNodes.Clear();
                return;
            }
        }

        selectedEdges.Clear();

        if (e.ShiftKey)
        {
            if (selectedNodes.Contains(nodeId))
            {
                selectedNodes.Remove(nodeId);
            }
            else
            {
                selectedNodes.Add(nodeId);
            }
            return;
        }

        // For connection modes that reach here, create an edge if one node is already selected
        // (Normal and Rearrange modes are handled earlier and return before reaching this point)
        if (selectedNodes.Contains(nodeId))
        {
            selectedNodes.Remove(nodeId);
        }
        else if (selectedNodes.Count == 0)
        {
            selectedNodes.Add(nodeId);
        }
        else if (selectedNodes.Count == 1)
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            var fromNode = nodes.First(n => n.Id == selectedNodes[0]);
            var toNode = nodes.First(n => n.Id == nodeId);

            var (fromConn, toConn) = GetSmartConnectionPoints(fromNode, toNode);

            var newEdge = CreateEdgeWithDefaults(selectedNodes[0], nodeId, fromConn, toConn);
            newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);

            edges.Add(newEdge);
            selectedNodes.Clear();
        }
        else
        {
            selectedNodes.Clear();
            selectedNodes.Add(nodeId);
        }
    }

    private async Task HandleNodeMouseDown(int nodeId, MouseEventArgs e)
    {
        Console.WriteLine($"=== HandleNodeMouseDown START === NodeId={nodeId}, Mode={mode}, selectToolActive={selectToolActive}");

        // Allow dragging in Select mode, or when selectToolActive, or when clicking on existing nodes in AddNode mode
        // This allows users to drag nodes without having to switch back to Select mode
        if (!(mode == EditorMode.Select || selectToolActive || mode == EditorMode.AddNode))
        {
            Console.WriteLine($"HandleNodeMouseDown BLOCKED - mode check failed");
            return;
        }
        if (resizingNodeId != null)
        {
            Console.WriteLine($"HandleNodeMouseDown BLOCKED - resizingNodeId={resizingNodeId}");
            return;
        }

        // Only block dragging in Chain mode when actively chaining
        // For other connection modes, allow dragging - clicks still work via HandleNodeClick
        if (chainMode && lastChainedNodeId.HasValue)
        {
            Console.WriteLine($"HandleNodeMouseDown BLOCKED - chainMode active");
            return;
        }

        // In Normal or Rearrange mode, always allow dragging
        // In TwoClick/1:N modes, allow dragging (clicks are handled separately)

        var node = nodes.FirstOrDefault(n => n.Id == nodeId);

        if (e.Detail == 2)
        {
            // Double-click: SuperNodes toggle expand/collapse, regular nodes edit text
            Console.WriteLine($"HandleNodeMouseDown - double click, node IsSuperNode={node?.IsSuperNode}");
            if (node?.IsSuperNode == true)
            {
                HandleNodeDoubleClick(nodeId);
            }
            else
            {
                EnableTextEdit(nodeId);
            }
            return;
        }
        if (node == null) return;

        // Calculate the click position in diagram space using the same method as HandleCanvasMouseMove
        // This ensures consistency between where we start the drag and where we track the mouse
        var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
        double scrollOffsetX = scrollInfo?[0] ?? 0;
        double scrollOffsetY = scrollInfo?[1] ?? 0;

        // Get canvas bounding rect to convert clientX/Y to canvas-relative coords
        var canvasBounds = await JS.InvokeAsync<double[]>("getCanvasBounds", canvasRef);
        double canvasLeft = canvasBounds?[0] ?? 0;
        double canvasTop = canvasBounds?[1] ?? 0;

        // Convert client coordinates to diagram coordinates
        double mouseInCanvasX = e.ClientX - canvasLeft;
        double mouseInCanvasY = e.ClientY - canvasTop;
        double diagX = (mouseInCanvasX + scrollOffsetX) / zoomLevel;
        double diagY = (mouseInCanvasY + scrollOffsetY) / zoomLevel;

        UndoService.SaveState(nodes, edges, edgeLabels);

        // Cancel any area selection that might have been started by HandleCanvasMouseDown
        isSelecting = false;
        selectionStart = null;

        draggingNodeId = nodeId;
        // Store offset from mouse position to node origin, both in diagram coordinates
        dragOffsetX = diagX - node.X;
        dragOffsetY = diagY - node.Y;
        dragStartX = node.X;
        dragStartY = node.Y;

        Console.WriteLine($"HandleNodeMouseDown SUCCESS - draggingNodeId set to {nodeId}, dragOffset=({dragOffsetX}, {dragOffsetY})");

        // Also add to selection if not already selected
        if (!selectedNodes.Contains(nodeId))
        {
            if (!e.CtrlKey && !e.ShiftKey)
            {
                selectedNodes.Clear();
            }
            selectedNodes.Add(nodeId);
        }
    }

    private void HandleConnectionPointClick(int nodeId, string side, int position, MouseEventArgs e)
    {
        if (!(mode == EditorMode.Select || selectToolActive)) return;

        // Check for multi-connect mode first
        if (HandleMultiConnectClick(nodeId, side, position))
            return;

        if (pendingConnectionNodeId.HasValue && pendingConnection != null)
        {
            if (pendingConnectionNodeId.Value == nodeId)
            {
                pendingConnectionNodeId = null;
                pendingConnection = null;
                return;
            }

            UndoService.SaveState(nodes, edges, edgeLabels);

            var fromConn = new ConnectionPoint
            {
                Side = pendingConnection.Side,
                Position = pendingConnection.Position
            };
            var toConn = new ConnectionPoint
            {
                Side = side,
                Position = position
            };

            var newEdge = CreateEdgeWithDefaults(pendingConnectionNodeId.Value, nodeId, fromConn, toConn);
            newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
            edges.Add(newEdge);

            pendingConnectionNodeId = null;
            pendingConnection = null;
            pendingConnectionPoint = null;
        }
        else
        {
            pendingConnectionNodeId = nodeId;
            pendingConnection = new ConnectionPoint
            {
                Side = side,
                Position = position
            };

            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                pendingConnectionPoint = GeometryService.GetConnectionPointCoordinates(node, side, position);
            }
        }

        StateHasChanged();
    }

    private void HandleEdgeClick(int edgeId, MouseEventArgs e)
    {
        if (e.ShiftKey)
        {
            if (selectedEdges.Contains(edgeId))
            {
                selectedEdges.Remove(edgeId);
            }
            else
            {
                selectedEdges.Add(edgeId);
            }
            
            StateHasChanged();
        }
        else
        {
            selectedEdges.Clear();
            selectedEdges.Add(edgeId);
            selectedNodes.Clear();
            
            var edge = edges.FirstOrDefault(ed => ed.Id == edgeId);
            if (edge != null)
            {
                editStrokeWidth = edge.StrokeWidth ?? 1;
                editStrokeColor = edge.StrokeColor ?? "#374151";
                editStrokeDashArray = edge.StrokeDashArray ?? "";
                editIsDoubleLine = edge.IsDoubleLine;
                editEdgeStyle = edge.Style;
                editArrowDirection = edge.ArrowDirection;

                // Initialize Project dependency properties
                if (edge.IsProjectDependency)
                {
                    editProjectDepType = edge.ProjectDepType;
                    editProjectLagDays = edge.ProjectLagDays;
                }
            }

            showEdgeStylePanel = true;
            StateHasChanged();
        }
    }

    private void StartDraggingWaypoint(int edgeId, Waypoint waypoint, MouseEventArgs e)
    {
        draggingEdgeId = edgeId;
        var edge = edges.FirstOrDefault(ed => ed.Id == edgeId);
        if (edge != null)
        {
            draggingWaypointIndex = edge.Waypoints.IndexOf(waypoint);
        }
    }

    private async Task HandleCanvasScroll()
    {
        var scrollInfo = await JS.InvokeAsync<double[]>("getScrollInfo", canvasRef);
        if (scrollInfo != null && scrollInfo.Length >= 4)
        {
            scrollX = scrollInfo[0];
            scrollY = scrollInfo[1];
            viewportWidth = scrollInfo[2];
            viewportHeight = scrollInfo[3];
            StateHasChanged();
        }
    }

    private async Task HandleMinimapClick(MouseEventArgs e)
    {
        try
        {
            var minimapBounds = await JS.InvokeAsync<double[]>("getMinimapBounds", minimapRef);
            if (minimapBounds == null || minimapBounds.Length < 4) return;

            double minimapWidth = minimapBounds[2];
            double minimapHeight = minimapBounds[3];

            // Use different canvas dimensions for Gantt/Project mode vs regular mode
            double canvasWidth, canvasHeight;
            if (isGanttMode && ganttTimeline != null && ganttTimelineView)
            {
                canvasWidth = ganttTimeline.GetTimelineWidth();
                canvasHeight = ganttTimeline.GetTimelineHeight(GetGanttMachineNodes().Count());
            }
            else if (isProjectMode && projectTimeline != null && projectViewMode == "timeline")
            {
                canvasWidth = GetProjectTotalWidth();
                canvasHeight = GetProjectTotalHeight();
            }
            else
            {
                canvasWidth = 4000.0;
                canvasHeight = 4000.0;
            }

            double scaleX = canvasWidth / minimapWidth;
            double scaleY = canvasHeight / minimapHeight;

            // Click position in canvas coordinates (before zoom)
            double canvasX = e.OffsetX * scaleX;
            double canvasY = e.OffsetY * scaleY;

            // Calculate target scroll position (accounting for zoom - viewport is in screen pixels)
            double targetScrollX = canvasX * zoomLevel - (viewportWidth / 2);
            double targetScrollY = canvasY * zoomLevel - (viewportHeight / 2);

            // Clamp to valid scroll range
            double maxScrollX = canvasWidth * zoomLevel - viewportWidth;
            double maxScrollY = canvasHeight * zoomLevel - viewportHeight;
            targetScrollX = Math.Max(0, Math.Min(targetScrollX, maxScrollX));
            targetScrollY = Math.Max(0, Math.Min(targetScrollY, maxScrollY));

            await JS.InvokeVoidAsync("scrollCanvasTo", canvasRef, targetScrollX, targetScrollY);

            scrollX = targetScrollX;
            scrollY = targetScrollY;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleMinimapClick: {ex.Message}");
        }
    }

    private void EnableTextEdit(int nodeId) { /* Implement text editing */ }
}
