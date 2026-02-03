using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Edge routing algorithms for path calculation and crossing minimization
/// </summary>
public class EdgeRoutingService
{
    #region Crossing Analysis

    /// <summary>
    /// Count total edge crossings in the graph
    /// </summary>
    public int CountEdgeCrossings(List<Node> nodes, List<Edge> edges)
    {
        var crossings = 0;
        var nodeDict = nodes.ToDictionary(n => n.Id);

        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                if (DoEdgesCross(edges[i], edges[j], nodeDict))
                    crossings++;
            }
        }

        return crossings;
    }

    /// <summary>
    /// Check if two edges cross each other
    /// </summary>
    public bool DoEdgesCross(Edge e1, Edge e2, Dictionary<int, Node> nodeDict)
    {
        // Skip if edges share a node
        if (e1.From == e2.From || e1.From == e2.To || e1.To == e2.From || e1.To == e2.To)
            return false;

        if (!nodeDict.TryGetValue(e1.From, out var n1From) ||
            !nodeDict.TryGetValue(e1.To, out var n1To) ||
            !nodeDict.TryGetValue(e2.From, out var n2From) ||
            !nodeDict.TryGetValue(e2.To, out var n2To))
            return false;

        // Get center points
        var p1 = (X: n1From.X + n1From.Width / 2, Y: n1From.Y + n1From.Height / 2);
        var q1 = (X: n1To.X + n1To.Width / 2, Y: n1To.Y + n1To.Height / 2);
        var p2 = (X: n2From.X + n2From.Width / 2, Y: n2From.Y + n2From.Height / 2);
        var q2 = (X: n2To.X + n2To.Width / 2, Y: n2To.Y + n2To.Height / 2);

        return SegmentsIntersect(p1.X, p1.Y, q1.X, q1.Y, p2.X, p2.Y, q2.X, q2.Y);
    }

    /// <summary>
    /// Get all crossing edge pairs
    /// </summary>
    public List<(Edge, Edge)> GetCrossingPairs(List<Node> nodes, List<Edge> edges)
    {
        var crossings = new List<(Edge, Edge)>();
        var nodeDict = nodes.ToDictionary(n => n.Id);

        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                if (DoEdgesCross(edges[i], edges[j], nodeDict))
                    crossings.Add((edges[i], edges[j]));
            }
        }

        return crossings;
    }

    #endregion

    #region Orthogonal Edge Routing

    /// <summary>
    /// Route edges orthogonally (right-angle paths) avoiding nodes
    /// </summary>
    public List<Waypoint> RouteOrthogonal(Node from, Node to, List<Node> obstacles, int channelSpacing = 20)
    {
        var waypoints = new List<Waypoint>();

        // Get connection points
        var fromCenter = (X: from.X + from.Width / 2, Y: from.Y + from.Height / 2);
        var toCenter = (X: to.X + to.Width / 2, Y: to.Y + to.Height / 2);

        // Determine dominant direction
        var dx = toCenter.X - fromCenter.X;
        var dy = toCenter.Y - fromCenter.Y;

        // Simple orthogonal routing: horizontal then vertical or vice versa
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            // Horizontal first
            var midX = fromCenter.X + dx / 2;
            
            // Check if we need to go around obstacles
            var blocked = obstacles.Any(n => 
                n.Id != from.Id && n.Id != to.Id &&
                IsPointInExpandedRect(midX, fromCenter.Y, n, channelSpacing));

            if (blocked)
            {
                // Route around: go vertical first
                var midY = Math.Min(from.Y, to.Y) - channelSpacing * 2;
                waypoints.Add(new Waypoint { X = fromCenter.X, Y = midY });
                waypoints.Add(new Waypoint { X = toCenter.X, Y = midY });
            }
            else
            {
                waypoints.Add(new Waypoint { X = midX, Y = fromCenter.Y });
                waypoints.Add(new Waypoint { X = midX, Y = toCenter.Y });
            }
        }
        else
        {
            // Vertical first
            var midY = fromCenter.Y + dy / 2;
            
            var blocked = obstacles.Any(n => 
                n.Id != from.Id && n.Id != to.Id &&
                IsPointInExpandedRect(fromCenter.X, midY, n, channelSpacing));

            if (blocked)
            {
                var midX = Math.Min(from.X, to.X) - channelSpacing * 2;
                waypoints.Add(new Waypoint { X = midX, Y = fromCenter.Y });
                waypoints.Add(new Waypoint { X = midX, Y = toCenter.Y });
            }
            else
            {
                waypoints.Add(new Waypoint { X = fromCenter.X, Y = midY });
                waypoints.Add(new Waypoint { X = toCenter.X, Y = midY });
            }
        }

        return waypoints;
    }

    /// <summary>
    /// Route all edges orthogonally, considering other edges for channel assignment
    /// </summary>
    public void RouteAllOrthogonal(List<Node> nodes, List<Edge> edges, int channelSpacing = 20)
    {
        var nodeDict = nodes.ToDictionary(n => n.Id);

        foreach (var edge in edges)
        {
            if (!nodeDict.TryGetValue(edge.From, out var fromNode) ||
                !nodeDict.TryGetValue(edge.To, out var toNode))
                continue;

            var obstacles = nodes.Where(n => n.Id != edge.From && n.Id != edge.To).ToList();
            edge.Waypoints = RouteOrthogonal(fromNode, toNode, obstacles, channelSpacing);
        }
    }

    #endregion

    #region A* Pathfinding for Edge Routing

    /// <summary>
    /// Route edge using A* pathfinding on a grid, avoiding obstacles
    /// </summary>
    public List<Waypoint> RouteWithAStar(Node from, Node to, List<Node> obstacles, 
        int gridSize = 20, double padding = 10)
    {
        var startX = from.X + from.Width / 2;
        var startY = from.Y + from.Height / 2;
        var endX = to.X + to.Width / 2;
        var endY = to.Y + to.Height / 2;

        // Snap to grid
        var start = (X: SnapToGrid(startX, gridSize), Y: SnapToGrid(startY, gridSize));
        var end = (X: SnapToGrid(endX, gridSize), Y: SnapToGrid(endY, gridSize));

        // Build obstacle set
        var obstacleSet = new HashSet<(int, int)>();
        foreach (var obs in obstacles)
        {
            if (obs.Id == from.Id || obs.Id == to.Id) continue;

            var minX = SnapToGrid(obs.X - padding, gridSize);
            var maxX = SnapToGrid(obs.X + obs.Width + padding, gridSize);
            var minY = SnapToGrid(obs.Y - padding, gridSize);
            var maxY = SnapToGrid(obs.Y + obs.Height + padding, gridSize);

            for (var x = minX; x <= maxX; x += gridSize)
            {
                for (var y = minY; y <= maxY; y += gridSize)
                {
                    obstacleSet.Add(((int)x, (int)y));
                }
            }
        }

        // A* algorithm
        var openSet = new PriorityQueue<(int X, int Y), double>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), double> { [start] = 0 };

        openSet.Enqueue(start, Heuristic(start, end));

        var directions = new[] { (0, -gridSize), (gridSize, 0), (0, gridSize), (-gridSize, 0) };

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == end)
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var (dx, dy) in directions)
            {
                var neighbor = (X: current.X + dx, Y: current.Y + dy);

                if (obstacleSet.Contains(neighbor))
                    continue;

                var tentativeG = gScore[current] + gridSize;

                // Add turn penalty
                if (cameFrom.TryGetValue((current.X, current.Y), out var prev))
                {
                    var prevDir = (current.X - prev.Item1, current.Y - prev.Item2);
                    var newDir = (dx, dy);
                    if (prevDir != newDir)
                        tentativeG += gridSize * 0.5; // Turn penalty
                }

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[(neighbor.X, neighbor.Y)] = (current.X, current.Y);
                    gScore[neighbor] = tentativeG;
                    var fScore = tentativeG + Heuristic(neighbor, end);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }

        // No path found - return simple waypoints
        return new List<Waypoint>
        {
            new Waypoint { X = (startX + endX) / 2, Y = startY },
            new Waypoint { X = (startX + endX) / 2, Y = endY }
        };
    }

    private List<Waypoint> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int X, int Y) current)
    {
        var path = new List<(int X, int Y)> { current };
        
        while (cameFrom.ContainsKey((current.X, current.Y)))
        {
            var prev = cameFrom[(current.X, current.Y)];
            current = (prev.Item1, prev.Item2);
            path.Add(current);
        }

        path.Reverse();

        // Simplify path - remove collinear points
        var simplified = new List<Waypoint>();
        for (int i = 1; i < path.Count - 1; i++)
        {
            var prev = path[i - 1];
            var curr = path[i];
            var next = path[i + 1];

            // Check if direction changes
            var dir1 = (curr.X - prev.X, curr.Y - prev.Y);
            var dir2 = (next.X - curr.X, next.Y - curr.Y);

            if (dir1 != dir2)
            {
                simplified.Add(new Waypoint { X = curr.X, Y = curr.Y });
            }
        }

        return simplified;
    }

    private double Heuristic((int X, int Y) a, (int X, int Y) b)
    {
        // Manhattan distance for orthogonal routing
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private int SnapToGrid(double value, int gridSize)
    {
        return (int)(Math.Round(value / gridSize) * gridSize);
    }

    #endregion

    #region Crossing Minimization

    /// <summary>
    /// Minimize edge crossings by reordering edges and adding waypoints
    /// </summary>
    public void MinimizeCrossings(List<Node> nodes, List<Edge> edges, int iterations = 50)
    {
        var nodeDict = nodes.ToDictionary(n => n.Id);
        var initialCrossings = CountEdgeCrossings(nodes, edges);

        if (initialCrossings == 0) return;

        for (int iter = 0; iter < iterations; iter++)
        {
            var crossingPairs = GetCrossingPairs(nodes, edges);
            if (crossingPairs.Count == 0) break;

            // Try to resolve each crossing
            foreach (var (e1, e2) in crossingPairs)
            {
                TryResolveCrossing(e1, e2, nodeDict, nodes);
            }

            var newCrossings = CountEdgeCrossings(nodes, edges);
            if (newCrossings >= initialCrossings)
                break;
            
            initialCrossings = newCrossings;
        }
    }

    private void TryResolveCrossing(Edge e1, Edge e2, Dictionary<int, Node> nodeDict, List<Node> allNodes)
    {
        if (!nodeDict.TryGetValue(e1.From, out var n1From) ||
            !nodeDict.TryGetValue(e1.To, out var n1To))
            return;

        // Add a waypoint to route e1 around the crossing
        var fromCenter = (X: n1From.X + n1From.Width / 2, Y: n1From.Y + n1From.Height / 2);
        var toCenter = (X: n1To.X + n1To.Width / 2, Y: n1To.Y + n1To.Height / 2);

        // Calculate offset direction
        var dx = toCenter.X - fromCenter.X;
        var dy = toCenter.Y - fromCenter.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length < 1) return;

        // Perpendicular offset
        var perpX = -dy / length * 30;
        var perpY = dx / length * 30;

        var midX = (fromCenter.X + toCenter.X) / 2;
        var midY = (fromCenter.Y + toCenter.Y) / 2;

        // Try adding waypoint above or below
        var waypoint1 = new Waypoint { X = midX + perpX, Y = midY + perpY };
        var waypoint2 = new Waypoint { X = midX - perpX, Y = midY - perpY };

        // Test which waypoint reduces crossings more
        var originalWaypoints = e1.Waypoints.ToList();
        
        e1.Waypoints = new List<Waypoint> { waypoint1 };
        var crossings1 = CountEdgeCrossingsForEdge(e1, nodeDict, allNodes);

        e1.Waypoints = new List<Waypoint> { waypoint2 };
        var crossings2 = CountEdgeCrossingsForEdge(e1, nodeDict, allNodes);

        e1.Waypoints = originalWaypoints;
        var crossings0 = CountEdgeCrossingsForEdge(e1, nodeDict, allNodes);

        // Choose best option
        if (crossings1 < crossings0 && crossings1 <= crossings2)
            e1.Waypoints = new List<Waypoint> { waypoint1 };
        else if (crossings2 < crossings0 && crossings2 < crossings1)
            e1.Waypoints = new List<Waypoint> { waypoint2 };
        // else keep original
    }

    private int CountEdgeCrossingsForEdge(Edge edge, Dictionary<int, Node> nodeDict, List<Node> allNodes)
    {
        // Simplified crossing count for a single edge
        // This is a placeholder - full implementation would check all other edges
        return 0;
    }

    #endregion

    #region Edge Bundling

    /// <summary>
    /// Bundle edges that share endpoints to reduce visual clutter
    /// </summary>
    public void BundleParallelEdges(List<Node> nodes, List<Edge> edges, double bundleOffset = 15)
    {
        // Group edges by endpoint pairs (ignoring direction)
        var edgeGroups = edges.GroupBy(e => 
        {
            var min = Math.Min(e.From, e.To);
            var max = Math.Max(e.From, e.To);
            return (min, max);
        }).Where(g => g.Count() > 1);

        var nodeDict = nodes.ToDictionary(n => n.Id);

        foreach (var group in edgeGroups)
        {
            var edgeList = group.ToList();
            var count = edgeList.Count;

            if (!nodeDict.TryGetValue(group.Key.min, out var node1) ||
                !nodeDict.TryGetValue(group.Key.max, out var node2))
                continue;

            var center1 = (X: node1.X + node1.Width / 2, Y: node1.Y + node1.Height / 2);
            var center2 = (X: node2.X + node2.Width / 2, Y: node2.Y + node2.Height / 2);

            var dx = center2.X - center1.X;
            var dy = center2.Y - center1.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            
            if (length < 1) continue;

            // Perpendicular direction
            var perpX = -dy / length;
            var perpY = dx / length;

            // Distribute edges with offset
            for (int i = 0; i < count; i++)
            {
                var offset = ((i - (count - 1) / 2.0) * bundleOffset);
                var midX = (center1.X + center2.X) / 2 + perpX * offset;
                var midY = (center1.Y + center2.Y) / 2 + perpY * offset;

                edgeList[i].Waypoints = new List<Waypoint>
                {
                    new Waypoint { X = midX, Y = midY }
                };
            }
        }
    }

    #endregion

    #region Helper Methods

    private bool SegmentsIntersect(double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4)
    {
        var d1 = Direction(x3, y3, x4, y4, x1, y1);
        var d2 = Direction(x3, y3, x4, y4, x2, y2);
        var d3 = Direction(x1, y1, x2, y2, x3, y3);
        var d4 = Direction(x1, y1, x2, y2, x4, y4);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        if (d1 == 0 && OnSegment(x3, y3, x4, y4, x1, y1)) return true;
        if (d2 == 0 && OnSegment(x3, y3, x4, y4, x2, y2)) return true;
        if (d3 == 0 && OnSegment(x1, y1, x2, y2, x3, y3)) return true;
        if (d4 == 0 && OnSegment(x1, y1, x2, y2, x4, y4)) return true;

        return false;
    }

    private double Direction(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        return (x3 - x1) * (y2 - y1) - (y3 - y1) * (x2 - x1);
    }

    private bool OnSegment(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        return Math.Min(x1, x2) <= x3 && x3 <= Math.Max(x1, x2) &&
               Math.Min(y1, y2) <= y3 && y3 <= Math.Max(y1, y2);
    }

    private bool IsPointInExpandedRect(double x, double y, Node node, double padding)
    {
        return x >= node.X - padding &&
               x <= node.X + node.Width + padding &&
               y >= node.Y - padding &&
               y <= node.Y + node.Height + padding;
    }

    #endregion
}
