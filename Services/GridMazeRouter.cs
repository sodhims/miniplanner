using System;
using System.Collections.Generic;
using System.Linq;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

public class GridMazeRouter
{
    public class RouteOptions
    {
        public double GridSpacing { get; set; } = 40;
        public double ObstacleMargin { get; set; } = 12;
        public double BendPenalty { get; set; } = 5.0;
        public double ViaPenalty { get; set; } = 20.0; // cost to switch layer
        public double ProximityPenalty { get; set; } = 8.0; // added cost near obstacles
        public int MaxGridSize { get; set; } = 200;
    }

    // Route from start to end returning waypoints (cell centers). Allows two-layer routing (layer 0 avoids obstacles, layer1 can go over obstacles at via cost)
    public List<Waypoint> Route(double startX, double startY, double endX, double endY, List<Node> obstaclesNodes, RouteOptions opts = null)
    {
        opts ??= new RouteOptions();

        // Determine grid bounds covering start, end and obstacles
        double minX = Math.Min(startX, endX);
        double minY = Math.Min(startY, endY);
        double maxX = Math.Max(startX, endX);
        double maxY = Math.Max(startY, endY);

        foreach (var n in obstaclesNodes)
        {
            minX = Math.Min(minX, n.X - opts.ObstacleMargin);
            minY = Math.Min(minY, n.Y - opts.ObstacleMargin);
            maxX = Math.Max(maxX, n.X + n.Width + opts.ObstacleMargin);
            maxY = Math.Max(maxY, n.Y + n.Height + opts.ObstacleMargin);
        }

        // Add padding
        double pad = opts.GridSpacing * 2;
        minX -= pad; minY -= pad; maxX += pad; maxY += pad;

        int cols = Math.Min(opts.MaxGridSize, Math.Max(3, (int)Math.Ceiling((maxX - minX) / opts.GridSpacing)));
        int rows = Math.Min(opts.MaxGridSize, Math.Max(3, (int)Math.Ceiling((maxY - minY) / opts.GridSpacing)));

        // Map coordinates to grid indices
        int sx = (int)Math.Round((startX - minX) / opts.GridSpacing);
        int sy = (int)Math.Round((startY - minY) / opts.GridSpacing);
        int tx = (int)Math.Round((endX - minX) / opts.GridSpacing);
        int ty = (int)Math.Round((endY - minY) / opts.GridSpacing);

        sx = Clamp(sx, 0, cols - 1); sy = Clamp(sy, 0, rows - 1);
        tx = Clamp(tx, 0, cols - 1); ty = Clamp(ty, 0, rows - 1);

        // Build occupancy grid for layer 0 (blocked) and layer1 (free)
        bool[,,] blocked = new bool[cols, rows, 1]; // we use blocked for layer0
        var obstacleRects = obstaclesNodes.Select(n => (left: n.X - opts.ObstacleMargin, top: n.Y - opts.ObstacleMargin, right: n.X + n.Width + opts.ObstacleMargin, bottom: n.Y + n.Height + opts.ObstacleMargin)).ToList();

        for (int gx = 0; gx < cols; gx++)
        {
            for (int gy = 0; gy < rows; gy++)
            {
                double cx = minX + gx * opts.GridSpacing;
                double cy = minY + gy * opts.GridSpacing;
                foreach (var r in obstacleRects)
                {
                    if (cx >= r.left && cx <= r.right && cy >= r.top && cy <= r.bottom)
                    {
                        blocked[gx, gy, 0] = true;
                        break;
                    }
                }
            }
        }

        // A* priority queue
        var pq = new System.Collections.Generic.PriorityQueue<(int x, int y, int layer), double>();
        var dist = new Dictionary<(int x, int y, int layer), double>();
        var prev = new Dictionary<(int x, int y, int layer), (int x, int y, int layer)>();
        var dirPrev = new Dictionary<(int x, int y, int layer), (int dx, int dy)>();

        (int x, int y, int layer) startState = (sx, sy, 0);
        (int x, int y, int layer) targetState = (tx, ty, 0);

        double Heuristic(int x, int y) => Math.Abs(x - tx) + Math.Abs(y - ty);

        dist[startState] = 0;
        pq.Enqueue(startState, Heuristic(sx, sy));

        var directions = new (int dx, int dy)[] { (1,0), (-1,0), (0,1), (0,-1) };

        (int x, int y, int layer)? found = null;

        while (pq.Count > 0)
        {
            var cur = pq.Dequeue();
            if (dist.TryGetValue(cur, out var curCost) == false) continue;
            if (cur.x == tx && cur.y == ty && cur.layer == 0)
            {
                found = cur; break;
            }

            // Move in four directions on same layer
            foreach (var d in directions)
            {
                int nx = cur.x + d.dx;
                int ny = cur.y + d.dy;
                int nl = cur.layer;
                if (nx < 0 || nx >= cols || ny < 0 || ny >= rows) continue;

                // If layer 0 and blocked, skip
                if (nl == 0 && blocked[nx, ny, 0]) continue;

                double moveCost = 1.0;
                // bend penalty
                if (dirPrev.TryGetValue(cur, out var pd))
                {
                    if (pd.dx != d.dx || pd.dy != d.dy) moveCost += opts.BendPenalty;
                }

                // proximity penalty: distance to nearest obstacle rect center
                double cx = minX + nx * opts.GridSpacing;
                double cy = minY + ny * opts.GridSpacing;
                double minDist = double.MaxValue;
                foreach (var r in obstacleRects)
                {
                    double dx = 0;
                    if (cx < r.left) dx = r.left - cx;
                    else if (cx > r.right) dx = cx - r.right;
                    double dy = 0;
                    if (cy < r.top) dy = r.top - cy;
                    else if (cy > r.bottom) dy = cy - r.bottom;
                    double d2 = Math.Sqrt(dx*dx + dy*dy);
                    minDist = Math.Min(minDist, d2);
                }
                double proxCost = 0;
                if (minDist < opts.GridSpacing * 1.5) proxCost = opts.ProximityPenalty * (1.0 / (minDist + 1.0));

                double ndist = curCost + moveCost + proxCost;

                var nstate = (nx, ny, nl);
                if (!dist.ContainsKey(nstate) || ndist < dist[nstate])
                {
                    dist[nstate] = ndist;
                    prev[nstate] = cur;
                    dirPrev[nstate] = (d.dx, d.dy);
                    double priority = ndist + Heuristic(nx, ny);
                    pq.Enqueue(nstate, priority);
                }
            }

            // Layer switch (via) at same cell: can always switch to layer1 (which ignores obstacles)
            if (cur.layer == 0)
            {
                var nstate = (cur.x, cur.y, 1);
                double ndist = curCost + opts.ViaPenalty;
                if (!dist.ContainsKey(nstate) || ndist < dist[nstate])
                {
                    dist[nstate] = ndist;
                    prev[nstate] = cur;
                    dirPrev[nstate] = (0,0);
                    pq.Enqueue(nstate, ndist + Heuristic(cur.x, cur.y));
                }
            }
            else
            {
                // can switch back to layer0 if not blocked
                var nstate = (cur.x, cur.y, 0);
                if (!blocked[cur.x, cur.y, 0])
                {
                    double ndist = curCost + opts.ViaPenalty;
                    if (!dist.ContainsKey(nstate) || ndist < dist[nstate])
                    {
                        dist[nstate] = ndist;
                        prev[nstate] = cur;
                        dirPrev[nstate] = (0,0);
                        pq.Enqueue(nstate, ndist + Heuristic(cur.x, cur.y));
                    }
                }
            }
        }

        if (found == null)
        {
            return new List<Waypoint>();
        }

        // Reconstruct path
        var pathStates = new List<(int x, int y, int layer)>();
        var curS = found.Value;
        while (true)
        {
            pathStates.Add(curS);
            if (prev.ContainsKey(curS)) curS = prev[curS];
            else break;
        }
        pathStates.Reverse();

        // Convert states to waypoints (only include position changes)
        var waypoints = new List<Waypoint>();
        (int x, int y, int layer)? lastPos = null;
        foreach (var s in pathStates)
        {
            if (lastPos == null || s.x != lastPos.Value.x || s.y != lastPos.Value.y || s.layer != lastPos.Value.layer)
            {
                double wx = minX + s.x * opts.GridSpacing;
                double wy = minY + s.y * opts.GridSpacing;
                waypoints.Add(new Waypoint { X = wx, Y = wy, Layer = s.layer });
            }
            lastPos = s;
        }

        // Simplify waypoints by removing consecutive collinear points
        var simplified = new List<Waypoint>();
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (i == 0 || i == waypoints.Count - 1)
            {
                simplified.Add(waypoints[i]);
                continue;
            }
            var a = waypoints[i - 1];
            var b = waypoints[i];
            var c = waypoints[i + 1];
            // Check collinearity (axis-aligned)
            if ((Math.Abs(a.X - b.X) < 0.001 && Math.Abs(b.X - c.X) < 0.001 && a.Layer == b.Layer && b.Layer == c.Layer) ||
                (Math.Abs(a.Y - b.Y) < 0.001 && Math.Abs(b.Y - c.Y) < 0.001 && a.Layer == b.Layer && b.Layer == c.Layer))
            {
                // skip b
                continue;
            }
            simplified.Add(b);
        }

        // Ensure start and end are included
        if (simplified.Count == 0)
        {
            simplified.Add(new Waypoint { X = startX, Y = startY });
            simplified.Add(new Waypoint { X = endX, Y = endY });
        }

        // Prepend start and append end as exact connection points
        var final = new List<Waypoint>();
        final.Add(new Waypoint { X = startX, Y = startY, Layer = 0 });
        foreach (var w in simplified) final.Add(w);
        final.Add(new Waypoint { X = endX, Y = endY, Layer = 0 });

        return final;
    }

    private static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
}
