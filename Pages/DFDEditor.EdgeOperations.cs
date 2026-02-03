using dfd2wasm.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    #region Multi-Connect Mode

    // Handle connection point click in multi-connect mode
    // Returns true if handled, false if should use normal behavior
    private bool HandleMultiConnectClick(int nodeId, string side, int position)
    {
        if (!multiConnectMode) return false;

        var connection = new ConnectionPoint { Side = side, Position = position };

        if (multiConnectSourceNode == null)
        {
            // First click - set source
            oneToNSourceNode = nodeId;
            oneToNSourcePoint = connection;
            
            // Get coordinates for visual feedback
            var node = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                oneToNSourceCoords = GeometryService.GetConnectionPointCoordinates(node, side, position);
            }
            
            StateHasChanged();
            return true;
        }
        else
        {
            // Subsequent clicks - create edge to this destination
            if (nodeId != oneToNSourceNode) // Don't connect to self
            {
                CreateEdgeFromMultiConnect(nodeId, connection);
            }
            return true;
        }
    }

    private void CreateEdgeFromMultiConnect(int destNodeId, ConnectionPoint destConnection)
    {
        if (oneToNSourceNode == null || multiConnectSourcePoint == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        var newEdge = new Edge
        {
            Id = nextEdgeId++,
            From = oneToNSourceNode.Value,
            To = destNodeId,
            FromConnection = oneToNSourcePoint,
            ToConnection = destConnection,
            StrokeWidth = defaultStrokeWidth,
            StrokeColor = defaultStrokeColor,
            StrokeDashArray = defaultStrokeDashArray,
            IsDoubleLine = defaultIsDoubleLine,
            Style = defaultEdgeStyle,
            IsOrthogonal = defaultEdgeStyle == EdgeStyle.Ortho || defaultEdgeStyle == EdgeStyle.OrthoRound,
            ArrowDirection = ArrowDirection.End // Default arrow direction
        };

        newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
        edges.Add(newEdge);
        StateHasChanged();
    }

    private void CancelMultiConnect()
    {
        ClearMultiConnectState();
        multiConnectMode = false;
        StateHasChanged();
    }

    #endregion

    #region Edge Connection Editing

    /// <summary>
    /// Change the source (From) node of an edge
    /// </summary>
    private void ChangeEdgeFrom(int edgeId, int newFromNodeId)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null || edge.From == newFromNodeId) return;

        // Don't allow connecting to itself
        if (newFromNodeId == edge.To)
        {
            Console.WriteLine("[Edge] Cannot connect node to itself");
            return;
        }

        UndoService.SaveState(nodes, edges, edgeLabels);

        edge.From = newFromNodeId;
        // Reset connection point to default
        edge.FromConnection = new ConnectionPoint { Side = "right", Position = 0 };
        edge.CustomFromSide = null;
        edge.PathData = PathService.GetEdgePath(edge, nodes);

        StateHasChanged();
    }

    /// <summary>
    /// Change the destination (To) node of an edge
    /// </summary>
    private void ChangeEdgeTo(int edgeId, int newToNodeId)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null || edge.To == newToNodeId) return;

        // Don't allow connecting to itself
        if (newToNodeId == edge.From)
        {
            Console.WriteLine("[Edge] Cannot connect node to itself");
            return;
        }

        UndoService.SaveState(nodes, edges, edgeLabels);

        edge.To = newToNodeId;
        // Reset connection point to default
        edge.ToConnection = new ConnectionPoint { Side = "left", Position = 0 };
        edge.CustomToSide = null;
        edge.PathData = PathService.GetEdgePath(edge, nodes);

        StateHasChanged();
    }

    /// <summary>
    /// Change which side of the FROM node the edge attaches to
    /// </summary>
    private void ChangeEdgeFromSide(int edgeId, string newSide)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        edge.CustomFromSide = newSide;
        edge.FromConnection = new ConnectionPoint { Side = newSide, Position = 0 };
        edge.PathData = PathService.GetEdgePath(edge, nodes);

        StateHasChanged();
    }

    /// <summary>
    /// Change which side of the TO node the edge attaches to
    /// </summary>
    private void ChangeEdgeToSide(int edgeId, string newSide)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        edge.CustomToSide = newSide;
        edge.ToConnection = new ConnectionPoint { Side = newSide, Position = 0 };
        edge.PathData = PathService.GetEdgePath(edge, nodes);

        StateHasChanged();
    }

    /// <summary>
    /// Flip the direction of a single selected edge (swap From and To)
    /// </summary>
    private void FlipSelectedEdge()
    {
        if (selectedEdges.Count != 1) return;

        var edge = edges.FirstOrDefault(e => e.Id == selectedEdges[0]);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        // Swap From and To
        (edge.From, edge.To) = (edge.To, edge.From);

        // Swap connection points
        (edge.FromConnection, edge.ToConnection) = (edge.ToConnection, edge.FromConnection);
        (edge.CustomFromSide, edge.CustomToSide) = (edge.CustomToSide, edge.CustomFromSide);

        // Recalculate path
        edge.PathData = PathService.GetEdgePath(edge, nodes);

        StateHasChanged();
    }

    /// <summary>
    /// Flip the direction of all selected edges
    /// </summary>
    private void FlipSelectedEdges()
    {
        if (selectedEdges.Count == 0) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        foreach (var edgeId in selectedEdges)
        {
            var edge = edges.FirstOrDefault(e => e.Id == edgeId);
            if (edge == null) continue;

            // Swap From and To
            (edge.From, edge.To) = (edge.To, edge.From);

            // Swap connection points
            (edge.FromConnection, edge.ToConnection) = (edge.ToConnection, edge.FromConnection);
            (edge.CustomFromSide, edge.CustomToSide) = (edge.CustomToSide, edge.CustomFromSide);

            // Recalculate path
            edge.PathData = PathService.GetEdgePath(edge, nodes);
        }

        StateHasChanged();
    }

    #endregion

    #region Edge Styles

    private void ApplyEdgeStylesToSelected()
    {
        if (selectedEdges.Count == 0)
        {
            return;
        }

        UndoService.SaveState(nodes, edges, edgeLabels);

        foreach (var edgeId in selectedEdges)
        {
            var edge = edges.FirstOrDefault(e => e.Id == edgeId);
            if (edge != null)
            {
                edge.StrokeWidth = editStrokeWidth;
                edge.StrokeColor = editStrokeColor;
                edge.StrokeDashArray = editStrokeDashArray;
                edge.IsDoubleLine = editIsDoubleLine;
                edge.DoubleLineSpacing = editDoubleLineSpacing;
                edge.Style = editEdgeStyle;
                edge.IsOrthogonal = editEdgeStyle == EdgeStyle.Ortho || editEdgeStyle == EdgeStyle.OrthoRound;
                edge.ArrowDirection = editArrowDirection;

                edge.PathData = PathService.GetEdgePath(edge, nodes);
            }
        }

        InvokeAsync(StateHasChanged);
    }

    private void ApplyEdgeStylesToAll()
    {
        UndoService.SaveState(nodes, edges, edgeLabels);

        foreach (var edge in edges)
        {
            edge.StrokeWidth = editStrokeWidth;
            edge.StrokeColor = editStrokeColor;
            edge.StrokeDashArray = editStrokeDashArray;
            edge.IsDoubleLine = editIsDoubleLine;
            edge.DoubleLineSpacing = editDoubleLineSpacing;
            edge.Style = editEdgeStyle;
            edge.IsOrthogonal = editEdgeStyle == EdgeStyle.Ortho || editEdgeStyle == EdgeStyle.OrthoRound;
            edge.ArrowDirection = editArrowDirection;

            edge.PathData = PathService.GetEdgePath(edge, nodes);
        }

        showEdgeStylePanel = false;
        StateHasChanged();
    }

    // Called when connector style dropdown changes - updates immediately
    private void OnEdgeStyleChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<EdgeStyle>(e.Value?.ToString(), out var newStyle))
        {
            editEdgeStyle = newStyle;
            ApplyEdgeStylesToSelected();
        }
    }

    // Called when any edge property changes - updates immediately
    private void OnEdgePropertyChanged()
    {
        ApplyEdgeStylesToSelected();
    }

    // Called when Project dependency properties change - updates selected edges
    private void OnProjectDepPropertyChanged()
    {
        try
        {
            UndoService.SaveState(nodes, edges, edgeLabels);

            foreach (var edgeId in selectedEdges)
            {
                var edge = edges.FirstOrDefault(e => e.Id == edgeId);
                if (edge != null && edge.IsProjectDependency)
                {
                    edge.ProjectDepType = editProjectDepType;
                    edge.ProjectLagDays = editProjectLagDays;
                }
            }

            // Recalculate the Project schedule with new dependency settings
            UpdateProjectView();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating Project dependency properties: {ex.Message}");
            // Still update the state even if Project view update fails
            StateHasChanged();
        }
    }

    #endregion

    #region Waypoints

    // Double-click on midpoint handle to add a waypoint there
    private void AddWaypointAtMidpoint(int edgeId)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        var mid = GetEdgeMidpoint(edge);
        edge.Waypoints.Add(new Waypoint { X = mid.X, Y = mid.Y });
        edge.PathData = PathService.GetEdgePath(edge, nodes);
        StateHasChanged();
    }

    // Double-click on edge to add a waypoint
    private void AddWaypointToEdge(int edgeId, MouseEventArgs e)
    {
        var edge = edges.FirstOrDefault(ed => ed.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        // Get click position (adjusted for any transforms)
        double x = e.OffsetX;
        double y = e.OffsetY;

        // Create new waypoint at click position
        var newWaypoint = new Waypoint { X = x, Y = y };

        // Insert waypoint in the right position (find nearest segment)
        if (edge.Waypoints.Count == 0)
        {
            edge.Waypoints.Add(newWaypoint);
        }
        else
        {
            // Find best insertion point based on distance to existing waypoints
            int insertIndex = FindBestWaypointInsertIndex(edge, x, y);
            edge.Waypoints.Insert(insertIndex, newWaypoint);
        }

        // Recalculate path
        edge.PathData = PathService.GetEdgePath(edge, nodes);
        StateHasChanged();
    }

    private int FindBestWaypointInsertIndex(Edge edge, double x, double y)
    {
        // Simple approach: find where this point fits best in the sequence
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
        
        if (fromNode == null || toNode == null)
            return 0;

        double fromX = fromNode.X + fromNode.Width / 2;
        double fromY = fromNode.Y + fromNode.Height / 2;
        double toX = toNode.X + toNode.Width / 2;
        double toY = toNode.Y + toNode.Height / 2;

        // Build list of all points
        var points = new List<(double x, double y)>();
        points.Add((fromX, fromY));
        foreach (var wp in edge.Waypoints)
        {
            points.Add((wp.X, wp.Y));
        }
        points.Add((toX, toY));

        // Find which segment the new point is closest to
        double minDist = double.MaxValue;
        int bestIndex = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            double dist = DistanceToSegment(x, y, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y);
            if (dist < minDist)
            {
                minDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
            return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

        double t = Math.Max(0, Math.Min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSquared));
        double projX = x1 + t * dx;
        double projY = y1 + t * dy;

        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    // Right-click on waypoint to delete it
    private void DeleteWaypoint(int edgeId, Waypoint waypoint)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        edge.Waypoints.Remove(waypoint);
        edge.PathData = PathService.GetEdgePath(edge, nodes);
        StateHasChanged();
    }

    #endregion

    #region Edge Splitting

    /// <summary>
    /// Finds an edge that passes through or near a given rectangle (node bounds).
    /// Checks if any edge segment intersects or passes close to the node rectangle.
    /// </summary>
    private Edge? FindEdgeAtRectangle(double x, double y, double width, double height, double threshold = 10)
    {
        Console.WriteLine($"FindEdgeAtRectangle: rect=({x}, {y}, {width}, {height}), threshold={threshold}");

        Edge? closestEdge = null;
        double closestDistance = double.MaxValue;

        foreach (var edge in edges)
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (fromNode == null || toNode == null) continue;

            // Parse the actual rendered path to get all points
            var pathPoints = ParseSvgPathToPoints(edge.PathData);
            Console.WriteLine($"  Edge {edge.Id} ({edge.From}->{edge.To}): {pathPoints.Count} points from path '{edge.PathData}'");

            if (pathPoints.Count < 2)
            {
                // Fallback to simple start/end points
                pathPoints = GetEdgePathPoints(edge, fromNode, toNode);
                Console.WriteLine($"    Fallback to simple points: {pathPoints.Count} points");
            }

            // Check each segment of the edge path
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                // Check if segment intersects or passes close to the rectangle
                double dist = DistanceFromSegmentToRectangle(
                    pathPoints[i].x, pathPoints[i].y,
                    pathPoints[i + 1].x, pathPoints[i + 1].y,
                    x, y, width, height);

                Console.WriteLine($"    Segment {i}: ({pathPoints[i].x:F1}, {pathPoints[i].y:F1}) -> ({pathPoints[i + 1].x:F1}, {pathPoints[i + 1].y:F1}), dist to rect={dist:F1}");

                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    if (dist <= threshold)
                    {
                        closestEdge = edge;
                        Console.WriteLine($"    *** Edge {edge.Id} intersects/near rectangle, dist={dist:F1}");
                    }
                }
            }
        }

        Console.WriteLine($"  Result: {(closestEdge != null ? $"Edge {closestEdge.Id}" : "null")}, closest dist={closestDistance:F1}");
        return closestEdge;
    }

    /// <summary>
    /// Calculates the minimum distance from a line segment to a rectangle.
    /// Returns 0 if the segment intersects the rectangle.
    /// </summary>
    private double DistanceFromSegmentToRectangle(double x1, double y1, double x2, double y2,
        double rectX, double rectY, double rectW, double rectH)
    {
        // Check if segment intersects rectangle
        if (SegmentIntersectsRectangle(x1, y1, x2, y2, rectX, rectY, rectW, rectH))
            return 0;

        // Otherwise find minimum distance from segment to rectangle edges/corners
        double minDist = double.MaxValue;

        // Check distance from segment to each corner of rectangle
        double[] cornersX = { rectX, rectX + rectW, rectX + rectW, rectX };
        double[] cornersY = { rectY, rectY, rectY + rectH, rectY + rectH };

        for (int i = 0; i < 4; i++)
        {
            double dist = DistanceToSegment(cornersX[i], cornersY[i], x1, y1, x2, y2);
            minDist = Math.Min(minDist, dist);
        }

        // Check distance from rectangle center to segment
        double centerX = rectX + rectW / 2;
        double centerY = rectY + rectH / 2;
        double centerDist = DistanceToSegment(centerX, centerY, x1, y1, x2, y2);
        minDist = Math.Min(minDist, centerDist);

        return minDist;
    }

    /// <summary>
    /// Checks if a line segment intersects a rectangle
    /// </summary>
    private bool SegmentIntersectsRectangle(double x1, double y1, double x2, double y2,
        double rectX, double rectY, double rectW, double rectH)
    {
        // Check if either endpoint is inside rectangle
        if (PointInRectangle(x1, y1, rectX, rectY, rectW, rectH) ||
            PointInRectangle(x2, y2, rectX, rectY, rectW, rectH))
            return true;

        // Check if segment intersects any of the 4 edges of the rectangle
        double left = rectX;
        double right = rectX + rectW;
        double top = rectY;
        double bottom = rectY + rectH;

        if (SegmentsIntersect(x1, y1, x2, y2, left, top, right, top)) return true;       // Top edge
        if (SegmentsIntersect(x1, y1, x2, y2, left, bottom, right, bottom)) return true; // Bottom edge
        if (SegmentsIntersect(x1, y1, x2, y2, left, top, left, bottom)) return true;     // Left edge
        if (SegmentsIntersect(x1, y1, x2, y2, right, top, right, bottom)) return true;   // Right edge

        return false;
    }

    private bool PointInRectangle(double px, double py, double rx, double ry, double rw, double rh)
    {
        return px >= rx && px <= rx + rw && py >= ry && py <= ry + rh;
    }

    /// <summary>
    /// Checks if two line segments intersect
    /// </summary>
    private bool SegmentsIntersect(double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4)
    {
        double d1 = CrossProduct(x3, y3, x4, y4, x1, y1);
        double d2 = CrossProduct(x3, y3, x4, y4, x2, y2);
        double d3 = CrossProduct(x1, y1, x2, y2, x3, y3);
        double d4 = CrossProduct(x1, y1, x2, y2, x4, y4);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        if (d1 == 0 && OnSegment(x3, y3, x4, y4, x1, y1)) return true;
        if (d2 == 0 && OnSegment(x3, y3, x4, y4, x2, y2)) return true;
        if (d3 == 0 && OnSegment(x1, y1, x2, y2, x3, y3)) return true;
        if (d4 == 0 && OnSegment(x1, y1, x2, y2, x4, y4)) return true;

        return false;
    }

    private double CrossProduct(double ax, double ay, double bx, double by, double cx, double cy)
    {
        return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
    }

    private bool OnSegment(double ax, double ay, double bx, double by, double px, double py)
    {
        return Math.Min(ax, bx) <= px && px <= Math.Max(ax, bx) &&
               Math.Min(ay, by) <= py && py <= Math.Max(ay, by);
    }

    /// <summary>
    /// Parses an SVG path string (M, L commands) into a list of points
    /// </summary>
    private List<(double x, double y)> ParseSvgPathToPoints(string? pathData)
    {
        var points = new List<(double x, double y)>();
        if (string.IsNullOrEmpty(pathData)) return points;

        try
        {
            // Split by M and L commands, handling both "M x y" and "M x,y" formats
            var parts = pathData.Split(new[] { 'M', 'L', 'C', 'Q', 'A' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Parse "x y" or "x,y" format
                var coords = trimmed.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                // For simple paths, we expect pairs of coordinates
                for (int i = 0; i < coords.Length - 1; i += 2)
                {
                    if (double.TryParse(coords[i], out double x) &&
                        double.TryParse(coords[i + 1], out double y))
                    {
                        points.Add((x, y));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing SVG path: {ex.Message}");
        }

        return points;
    }

    /// <summary>
    /// Gets the key points of an edge path (start, waypoints, end)
    /// </summary>
    private List<(double x, double y)> GetEdgePathPoints(Edge edge, Node fromNode, Node toNode)
    {
        var points = new List<(double x, double y)>();

        // Get start point (from terminal or connection point)
        double fromX, fromY;
        if (fromNode.ShowTerminals)
        {
            var termPos = GetTerminalPosition(fromNode, "output");
            if (termPos.HasValue)
            {
                fromX = termPos.Value.x;
                fromY = termPos.Value.y;
            }
            else
            {
                fromX = fromNode.X + fromNode.Width / 2;
                fromY = fromNode.Y + fromNode.Height / 2;
            }
        }
        else
        {
            (fromX, fromY) = GeometryService.GetConnectionPointCoordinates(
                fromNode, edge.FromConnection.Side, edge.FromConnection.Position);
        }
        points.Add((fromX, fromY));

        // Add waypoints
        foreach (var wp in edge.Waypoints)
        {
            points.Add((wp.X, wp.Y));
        }

        // Get end point (to terminal or connection point)
        double toX, toY;
        if (toNode.ShowTerminals)
        {
            var termPos = GetTerminalPosition(toNode, "input");
            if (termPos.HasValue)
            {
                toX = termPos.Value.x;
                toY = termPos.Value.y;
            }
            else
            {
                toX = toNode.X + toNode.Width / 2;
                toY = toNode.Y + toNode.Height / 2;
            }
        }
        else
        {
            (toX, toY) = GeometryService.GetConnectionPointCoordinates(
                toNode, edge.ToConnection.Side, edge.ToConnection.Position);
        }
        points.Add((toX, toY));

        return points;
    }

    /// <summary>
    /// Splits an edge by inserting a new node in the middle.
    /// The original edge is removed and two new edges are created:
    /// - One from the original source to the new node's input
    /// - One from the new node's output to the original target
    /// </summary>
    private void SplitEdgeWithNode(Edge edgeToSplit, Node newNode)
    {
        var fromNode = nodes.FirstOrDefault(n => n.Id == edgeToSplit.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edgeToSplit.To);
        if (fromNode == null || toNode == null) return;

        Console.WriteLine($"Splitting edge {edgeToSplit.Id} ({edgeToSplit.From} -> {edgeToSplit.To}) with node {newNode.Id}");

        // Create edge from original source to new node
        var (fromConn1, toConn1) = GetSmartConnectionPoints(fromNode, newNode);
        var edge1 = CreateEdgeWithDefaults(edgeToSplit.From, newNode.Id, fromConn1, toConn1);

        // Copy relevant properties from original edge
        edge1.Style = edgeToSplit.Style;
        edge1.IsOrthogonal = edgeToSplit.IsOrthogonal;
        edge1.StrokeColor = edgeToSplit.StrokeColor;
        edge1.StrokeWidth = edgeToSplit.StrokeWidth;
        edge1.StrokeDashArray = edgeToSplit.StrokeDashArray;

        // Set terminal connections if applicable
        if (fromNode.ShowTerminals)
        {
            edge1.FromTerminal = edgeToSplit.FromTerminal ?? "output";
        }
        if (newNode.ShowTerminals)
        {
            edge1.ToTerminal = "input";
        }

        edge1.PathData = PathService.GetEdgePath(edge1, nodes);

        // Create edge from new node to original target
        var (fromConn2, toConn2) = GetSmartConnectionPoints(newNode, toNode);
        var edge2 = CreateEdgeWithDefaults(newNode.Id, edgeToSplit.To, fromConn2, toConn2);

        // Copy relevant properties from original edge
        edge2.Style = edgeToSplit.Style;
        edge2.IsOrthogonal = edgeToSplit.IsOrthogonal;
        edge2.StrokeColor = edgeToSplit.StrokeColor;
        edge2.StrokeWidth = edgeToSplit.StrokeWidth;
        edge2.StrokeDashArray = edgeToSplit.StrokeDashArray;
        edge2.ArrowDirection = edgeToSplit.ArrowDirection;

        // Set terminal connections if applicable
        if (newNode.ShowTerminals)
        {
            edge2.FromTerminal = "output";
        }
        if (toNode.ShowTerminals)
        {
            edge2.ToTerminal = edgeToSplit.ToTerminal ?? "input";
        }

        edge2.PathData = PathService.GetEdgePath(edge2, nodes);

        // Remove the original edge
        edges.Remove(edgeToSplit);

        // Add the new edges
        edges.Add(edge1);
        edges.Add(edge2);

        Console.WriteLine($"Created edges: {edge1.Id} ({edge1.From} -> {edge1.To}) and {edge2.Id} ({edge2.From} -> {edge2.To})");
    }

    #endregion

    #region Edge Labels

    /// <summary>
    /// Update the label text directly on an edge
    /// </summary>
    private void UpdateEdgeLabel(int edgeId, string newText)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);
        edge.Label = newText;
        StateHasChanged();
    }

    private void UpdateLabelText(int labelId, string newText)
    {
        var label = edgeLabels.FirstOrDefault(l => l.Id == labelId);
        if (label != null)
        {
            label.Text = newText;
            StateHasChanged();
        }
    }

    private void StartEditingSelectedLabel()
    {
        if (selectedLabels.Count != 1) return;
        var labelId = selectedLabels.First();
        var label = edgeLabels.FirstOrDefault(l => l.Id == labelId);
        if (label == null) return;

        editingTextLabelId = labelId;
        editingTextNodeId = null;
        editingText = label.Text;
        showTextEditDialog = true;
    }

    #endregion

    #region Double Line Paths

    /// <summary>
    /// Get a parallel path offset from the original path for double-line edges.
    /// This creates a simple offset by moving the entire path perpendicular to its direction.
    /// </summary>
    private string GetParallelPath(string originalPath, double offset)
    {
        // For a simple implementation, we'll parse the path and offset each point
        // This works for simple L and M commands
        if (string.IsNullOrEmpty(originalPath)) return originalPath;

        try
        {
            var result = new System.Text.StringBuilder();
            var parts = originalPath.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var points = new List<(double x, double y)>();

            // Parse all points from the path
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == "M" || part == "L")
                {
                    if (i + 2 < parts.Length)
                    {
                        if (double.TryParse(parts[i + 1], out var x) && double.TryParse(parts[i + 2], out var y))
                        {
                            points.Add((x, y));
                            i += 2;
                        }
                    }
                }
            }

            if (points.Count < 2) return originalPath;

            // Calculate perpendicular offset for each segment and average for each point
            var offsetPoints = new List<(double x, double y)>();
            for (int i = 0; i < points.Count; i++)
            {
                double nx = 0, ny = 0;
                int count = 0;

                if (i > 0)
                {
                    var dx = points[i].x - points[i - 1].x;
                    var dy = points[i].y - points[i - 1].y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0)
                    {
                        nx += -dy / len;
                        ny += dx / len;
                        count++;
                    }
                }

                if (i < points.Count - 1)
                {
                    var dx = points[i + 1].x - points[i].x;
                    var dy = points[i + 1].y - points[i].y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0)
                    {
                        nx += -dy / len;
                        ny += dx / len;
                        count++;
                    }
                }

                if (count > 0)
                {
                    nx /= count;
                    ny /= count;
                }

                offsetPoints.Add((points[i].x + nx * offset, points[i].y + ny * offset));
            }

            // Build the new path
            for (int i = 0; i < offsetPoints.Count; i++)
            {
                var cmd = i == 0 ? "M" : "L";
                result.Append($"{cmd} {offsetPoints[i].x:F1} {offsetPoints[i].y:F1} ");
            }

            return result.ToString().Trim();
        }
        catch
        {
            return originalPath;
        }
    }

    #endregion
}
