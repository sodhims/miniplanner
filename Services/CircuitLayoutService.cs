using System;
using System.Collections.Generic;
using System.Linq;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

public class CircuitLayoutService
{
    /// <summary>
    /// Arrange nodes in a grid using BFS ordering to cluster connected nodes, then route edges using a grid-based maze router.
    /// </summary>
    public void ApplyCircuitLayout(List<Node> nodes, List<Edge> edges, GridMazeRouter.RouteOptions? routeOptions = null, int rowSpacing = 140, int colSpacing = 220, int startX = 100, int startY = 100)
    {
        if (nodes == null || nodes.Count == 0) return;

        int n = nodes.Count;
        int cols = (int)Math.Ceiling(Math.Sqrt(n));
        if (cols <= 0) cols = 1;

        // Build adjacency for provided edges (only between these nodes)
        var adj = nodes.ToDictionary(nu => nu.Id, nu => new List<int>());
        foreach (var e in edges)
        {
            if (adj.ContainsKey(e.From)) adj[e.From].Add(e.To);
            if (adj.ContainsKey(e.To)) adj[e.To].Add(e.From);
        }

        // BFS order starting from highest-degree nodes to cluster connected nodes
        var visited = new HashSet<int>();
        var order = new List<int>();
        var startNodes = adj.Keys.OrderByDescending(id => adj[id].Count).ToList();
        foreach (var s in startNodes)
        {
            if (visited.Contains(s)) continue;
            var q = new Queue<int>();
            q.Enqueue(s);
            visited.Add(s);
            while (q.Count > 0)
            {
                var v = q.Dequeue();
                order.Add(v);
                foreach (var nb in adj[v].OrderByDescending(x => adj[x].Count))
                {
                    if (!visited.Contains(nb))
                    {
                        visited.Add(nb);
                        q.Enqueue(nb);
                    }
                }
            }
        }

        // Ensure order covers all nodes
        foreach (var node in nodes)
        {
            if (!order.Contains(node.Id)) order.Add(node.Id);
        }

        // Place nodes in grid according to BFS order
        for (int i = 0; i < n; i++)
        {
            var id = order[i];
            var node = nodes.First(nd => nd.Id == id);
            int row = i / cols;
            int col = i % cols;

            double centerX = startX + col * colSpacing;
            double centerY = startY + row * rowSpacing;

            node.X = centerX - node.Width / 2.0;
            node.Y = centerY - node.Height / 2.0;
        }

        // Map node id -> grid coords
        var gridPos = new Dictionary<int, (int row, int col)>();
        for (int i = 0; i < n; i++)
        {
            var id = order[i];
            gridPos[id] = (i / cols, i % cols);
        }

        // Use GridMazeRouter to route edges along a grid with bend/via/proximity penalties
        var maze = new GridMazeRouter();
        var geometry = new GeometryService();
        var pathService = new PathService(geometry);

        // Use nodes (the passed-in list) as obstacles for routing
        var allNodes = nodes;

        foreach (var edge in edges)
        {
            edge.Style = EdgeStyle.Circuit;
            edge.Waypoints.Clear();

            var fromNode = nodes.FirstOrDefault(nu => nu.Id == edge.From);
            var toNode = nodes.FirstOrDefault(nu => nu.Id == edge.To);
            if (fromNode == null || toNode == null)
            {
                edge.PathData = pathService.GetEdgePath(edge, nodes);
                continue;
            }

            double ax = fromNode.X + fromNode.Width / 2.0;
            double ay = fromNode.Y + fromNode.Height / 2.0;
            double bx = toNode.X + toNode.Width / 2.0;
            double by = toNode.Y + toNode.Height / 2.0;

            var opts = routeOptions ?? new GridMazeRouter.RouteOptions
            {
                GridSpacing = Math.Min(colSpacing, rowSpacing) / 2.0,
                ObstacleMargin = 12,
                BendPenalty = 6,
                ViaPenalty = 30,
                ProximityPenalty = 10,
                MaxGridSize = 300
            };

            var routed = maze.Route(ax, ay, bx, by, allNodes, opts);
            if (routed == null || routed.Count == 0)
            {
                // Fallback to PathService if routing failed
                edge.PathData = pathService.GetEdgePath(edge, nodes);
            }
            else
            {
                edge.Waypoints = routed;
                edge.PathData = pathService.GetEdgePath(edge, nodes);
            }
        }

        // Post-process intersections between edges: where two edge polylines cross,
        // insert a via waypoint on one of the edges so the renderer can draw a via/jump.
        // Policy: insert via on the edge with the larger Id to keep behavior deterministic.
        double tol = 0.5;
        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                var e1 = edges[i];
                var e2 = edges[j];

                // Only consider routed circuit edges
                if (e1.Style != EdgeStyle.Circuit || e2.Style != EdgeStyle.Circuit) continue;

                // Build polylines (start center -> waypoints -> end center)
                var p1 = BuildPolylineForEdge(e1, nodes);
                var p2 = BuildPolylineForEdge(e2, nodes);

                for (int a = 0; a < p1.Count - 1; a++)
                {
                    var a1 = p1[a];
                    var a2 = p1[a + 1];
                    for (int b = 0; b < p2.Count - 1; b++)
                    {
                        var b1 = p2[b];
                        var b2 = p2[b + 1];

                        if (TrySegmentIntersection(a1.x, a1.y, a2.x, a2.y, b1.x, b1.y, b2.x, b2.y, out var ix, out var iy))
                        {
                            // Choose target edge deterministically (larger Id)
                            var target = e1.Id >= e2.Id ? e1 : e2;
                            var targetPoly = target == e1 ? p1 : p2;

                            // Insert via waypoint into the target edge at the intersection point
                            InsertViaWaypoint(target, targetPoly, ix, iy, tol);
                        }
                    }
                }
            }
        }

        // Regenerate path data after insertion
        foreach (var edge in edges)
        {
            edge.PathData = pathService.GetEdgePath(edge, nodes);
        }
    }

    private List<(double x, double y)> BuildPolylineForEdge(Edge edge, List<Node> nodes)
    {
        var pts = new List<(double x, double y)>();
        var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
        var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
        if (fromNode == null || toNode == null) return pts;

        double sx = fromNode.X + fromNode.Width / 2.0;
        double sy = fromNode.Y + fromNode.Height / 2.0;
        double tx = toNode.X + toNode.Width / 2.0;
        double ty = toNode.Y + toNode.Height / 2.0;

        pts.Add((sx, sy));
        foreach (var wp in edge.Waypoints)
        {
            pts.Add((wp.X, wp.Y));
        }
        pts.Add((tx, ty));
        return pts;
    }

    private void InsertViaWaypoint(Edge edge, List<(double x, double y)> poly, double ix, double iy, double tol)
    {
        // find segment index in poly where (ix,iy) lies
        for (int k = 0; k < poly.Count - 1; k++)
        {
            var p1 = poly[k];
            var p2 = poly[k + 1];
            if (PointOnSegment(ix, iy, p1.x, p1.y, p2.x, p2.y, tol))
            {
                // Find insertion index in edge.Waypoints: waypoint list corresponds to segments between
                // start(center) and first waypoint, between waypoints, and last waypoint->end(center).
                // So segment k corresponds to insertion index = k (if k==0 => before waypoint[0]).
                int insertIndex = Math.Max(0, k);

                // Avoid inserting duplicate via near existing waypoint
                bool nearExisting = edge.Waypoints.Any(w => Math.Abs(w.X - ix) < 1.0 && Math.Abs(w.Y - iy) < 1.0 && w.Layer > 0);
                if (nearExisting) return;

                var via = new Waypoint { X = ix, Y = iy, Layer = 1 };

                if (insertIndex >= edge.Waypoints.Count)
                {
                    edge.Waypoints.Add(via);
                }
                else
                {
                    edge.Waypoints.Insert(insertIndex, via);
                }

                return;
            }
        }
    }

    private bool PointOnSegment(double px, double py, double x1, double y1, double x2, double y2, double tol)
    {
        // Check bounding box
        if (px < Math.Min(x1, x2) - tol || px > Math.Max(x1, x2) + tol) return false;
        if (py < Math.Min(y1, y2) - tol || py > Math.Max(y1, y2) + tol) return false;

        // Check collinearity via cross product
        double dx = x2 - x1;
        double dy = y2 - y1;
        double dxp = px - x1;
        double dyp = py - y1;
        double cross = Math.Abs(dxp * dy - dyp * dx);
        return cross <= tol * Math.Sqrt(dx * dx + dy * dy);
    }

    private bool TrySegmentIntersection(double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4, out double ix, out double iy)
    {
        ix = iy = 0;
        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-6) return false;

        double det1 = x1 * y2 - y1 * x2;
        double det2 = x3 * y4 - y3 * x4;

        ix = (det1 * (x3 - x4) - (x1 - x2) * det2) / denom;
        iy = (det1 * (y3 - y4) - (y1 - y2) * det2) / denom;

        // Check if intersection lies on both segments
        if (ix < Math.Min(x1, x2) - 0.5 || ix > Math.Max(x1, x2) + 0.5) return false;
        if (iy < Math.Min(y1, y2) - 0.5 || iy > Math.Max(y1, y2) + 0.5) return false;
        if (ix < Math.Min(x3, x4) - 0.5 || ix > Math.Max(x3, x4) + 0.5) return false;
        if (iy < Math.Min(y3, y4) - 0.5 || iy > Math.Max(y3, y4) + 0.5) return false;

        return true;
    }
}
