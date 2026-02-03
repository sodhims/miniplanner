using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Graph layout algorithms for automatic node positioning
/// </summary>
public class GraphLayoutService
{
    private readonly Random _random = new();

    #region Force-Directed Layout (Fruchterman-Reingold)

    /// <summary>
    /// Force-directed layout using Fruchterman-Reingold algorithm.
    /// Good for general graphs, produces organic-looking layouts.
    /// </summary>
    public void ApplyForceDirectedLayout(List<Node> nodes, List<Edge> edges, 
        double width = 2000, double height = 1500, int iterations = 100)
    {
        if (nodes.Count == 0) return;

        var area = width * height;
        var k = Math.Sqrt(area / nodes.Count); // Optimal distance between vertices

        // Initialize random positions if nodes are stacked
        InitializePositions(nodes, width, height);

        // Create node position dictionary for faster lookup
        var positions = nodes.ToDictionary(n => n.Id, n => (X: n.X + n.Width / 2, Y: n.Y + n.Height / 2));
        var displacements = nodes.ToDictionary(n => n.Id, n => (X: 0.0, Y: 0.0));

        var temperature = width / 10; // Initial temperature
        var cooling = temperature / (iterations + 1);

        for (int i = 0; i < iterations; i++)
        {
            // Reset displacements
            foreach (var node in nodes)
                displacements[node.Id] = (0.0, 0.0);

            // Repulsive forces between all pairs
            for (int j = 0; j < nodes.Count; j++)
            {
                for (int l = j + 1; l < nodes.Count; l++)
                {
                    var v = nodes[j];
                    var u = nodes[l];

                    var dx = positions[v.Id].X - positions[u.Id].X;
                    var dy = positions[v.Id].Y - positions[u.Id].Y;
                    var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);

                    var repulsion = (k * k) / dist;
                    var fx = (dx / dist) * repulsion;
                    var fy = (dy / dist) * repulsion;

                    displacements[v.Id] = (displacements[v.Id].X + fx, displacements[v.Id].Y + fy);
                    displacements[u.Id] = (displacements[u.Id].X - fx, displacements[u.Id].Y - fy);
                }
            }

            // Attractive forces along edges
            foreach (var edge in edges)
            {
                if (!positions.ContainsKey(edge.From) || !positions.ContainsKey(edge.To))
                    continue;

                var dx = positions[edge.From].X - positions[edge.To].X;
                var dy = positions[edge.From].Y - positions[edge.To].Y;
                var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.01);

                var attraction = (dist * dist) / k;
                var fx = (dx / dist) * attraction;
                var fy = (dy / dist) * attraction;

                displacements[edge.From] = (displacements[edge.From].X - fx, displacements[edge.From].Y - fy);
                displacements[edge.To] = (displacements[edge.To].X + fx, displacements[edge.To].Y + fy);
            }

            // Apply displacements with temperature limiting
            foreach (var node in nodes)
            {
                var disp = displacements[node.Id];
                var dist = Math.Max(Math.Sqrt(disp.X * disp.X + disp.Y * disp.Y), 0.01);

                var limitedX = (disp.X / dist) * Math.Min(dist, temperature);
                var limitedY = (disp.Y / dist) * Math.Min(dist, temperature);

                var newX = positions[node.Id].X + limitedX;
                var newY = positions[node.Id].Y + limitedY;

                // Keep within bounds
                newX = Math.Clamp(newX, node.Width / 2, width - node.Width / 2);
                newY = Math.Clamp(newY, node.Height / 2, height - node.Height / 2);

                positions[node.Id] = (newX, newY);
            }

            temperature -= cooling;
        }

        // Apply final positions
        foreach (var node in nodes)
        {
            node.X = positions[node.Id].X - node.Width / 2;
            node.Y = positions[node.Id].Y - node.Height / 2;
        }
    }

    #endregion

    #region Sugiyama (Hierarchical) Layout - IMPROVED

    /// <summary>
    /// Sugiyama hierarchical layout - best for DAGs, flowcharts, dependency graphs.
    /// Minimizes edge crossings in layered graphs.
    /// </summary>
    public void ApplySugiyamaLayout(List<Node> nodes, List<Edge> edges,
        double layerSpacing = 150, double nodeSpacing = 80, bool topToBottom = true)
    {
        if (nodes.Count == 0) return;
        if (nodes.Count == 1)
        {
            nodes[0].X = 100;
            nodes[0].Y = 100;
            return;
        }

        // Create working copies
        var workingEdges = edges.Select(e => (From: e.From, To: e.To, IsReversed: false)).ToList();
        var nodeDict = nodes.ToDictionary(n => n.Id);

        // Step 1: Remove cycles by reversing back-edges
        var reversedEdges = RemoveCycles(nodes, workingEdges);

        // Step 2: Assign layers using longest path
        var nodeLayer = AssignLayersLongestPath(nodes, workingEdges);

        // Step 3: Group nodes by layer
        var layers = GroupByLayers(nodes, nodeLayer);

        // Step 4: Add dummy nodes for edges spanning multiple layers
        var dummyNodes = new List<DummyNode>();
        var augmentedLayers = InsertDummyNodes(layers, workingEdges, nodeDict, dummyNodes);

        // Step 5: Order nodes within layers to minimize crossings (multiple passes)
        MinimizeCrossings(augmentedLayers, workingEdges, dummyNodes, 24);

        // Step 6: Assign X coordinates using priority method
        AssignXCoordinates(augmentedLayers, nodeSpacing);

        // Step 7: Assign final positions to real nodes
        ApplyFinalPositions(augmentedLayers, nodeDict, layerSpacing, nodeSpacing, topToBottom);
    }

    /// <summary>
    /// Removes cycles by reversing back-edges using DFS
    /// </summary>
    private HashSet<(int, int)> RemoveCycles(List<Node> nodes, List<(int From, int To, bool IsReversed)> edges)
    {
        var reversed = new HashSet<(int, int)>();
        var white = new HashSet<int>(nodes.Select(n => n.Id)); // unvisited
        var gray = new HashSet<int>(); // in current path
        var black = new HashSet<int>(); // finished

        var adjacency = new Dictionary<int, List<int>>();
        foreach (var edge in edges)
        {
            if (!adjacency.ContainsKey(edge.From))
                adjacency[edge.From] = new List<int>();
            adjacency[edge.From].Add(edge.To);
        }

        void DFS(int node)
        {
            white.Remove(node);
            gray.Add(node);

            if (adjacency.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors.ToList())
                {
                    if (gray.Contains(neighbor))
                    {
                        // Back-edge found - mark for reversal
                        reversed.Add((node, neighbor));
                    }
                    else if (white.Contains(neighbor))
                    {
                        DFS(neighbor);
                    }
                }
            }

            gray.Remove(node);
            black.Add(node);
        }

        // Run DFS from all unvisited nodes
        while (white.Count > 0)
        {
            DFS(white.First());
        }

        // Reverse the back-edges in the working list
        for (int i = 0; i < edges.Count; i++)
        {
            if (reversed.Contains((edges[i].From, edges[i].To)))
            {
                edges[i] = (edges[i].To, edges[i].From, true);
            }
        }

        return reversed;
    }

    /// <summary>
    /// Assigns layers using longest path from sources (proper hierarchical assignment)
    /// </summary>
    private Dictionary<int, int> AssignLayersLongestPath(List<Node> nodes, List<(int From, int To, bool IsReversed)> edges)
    {
        var nodeLayer = new Dictionary<int, int>();
        var inDegree = nodes.ToDictionary(n => n.Id, n => 0);
        var adjacency = new Dictionary<int, List<int>>();

        foreach (var edge in edges)
        {
            if (!adjacency.ContainsKey(edge.From))
                adjacency[edge.From] = new List<int>();
            adjacency[edge.From].Add(edge.To);
            
            if (inDegree.ContainsKey(edge.To))
                inDegree[edge.To]++;
        }

        // Initialize all nodes to layer 0
        foreach (var node in nodes)
            nodeLayer[node.Id] = 0;

        // Topological sort with longest path
        var queue = new Queue<int>();
        var processed = new HashSet<int>();

        // Start from sources (in-degree 0)
        foreach (var node in nodes.Where(n => inDegree[n.Id] == 0))
            queue.Enqueue(node.Id);

        // If no sources (all cycles), start from minimum in-degree
        if (queue.Count == 0)
        {
            var minInDegree = nodes.OrderBy(n => inDegree[n.Id]).First();
            queue.Enqueue(minInDegree.Id);
            nodeLayer[minInDegree.Id] = 0;
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (processed.Contains(current)) continue;
            processed.Add(current);

            if (adjacency.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    // Key: use MAX to get longest path
                    var newLayer = nodeLayer[current] + 1;
                    if (newLayer > nodeLayer[neighbor])
                    {
                        nodeLayer[neighbor] = newLayer;
                    }

                    inDegree[neighbor]--;
                    if (inDegree[neighbor] <= 0 && !processed.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // Handle any unprocessed nodes (disconnected components)
        foreach (var node in nodes)
        {
            if (!processed.Contains(node.Id))
            {
                nodeLayer[node.Id] = 0;
            }
        }

        return nodeLayer;
    }

    /// <summary>
    /// Groups nodes into layers
    /// </summary>
    private Dictionary<int, List<object>> GroupByLayers(List<Node> nodes, Dictionary<int, int> nodeLayer)
    {
        var layers = new Dictionary<int, List<object>>();
        
        foreach (var node in nodes)
        {
            var layer = nodeLayer[node.Id];
            if (!layers.ContainsKey(layer))
                layers[layer] = new List<object>();
            layers[layer].Add(node);
        }

        return layers;
    }

    /// <summary>
    /// Dummy node for edges spanning multiple layers
    /// </summary>
    private class DummyNode
    {
        public int Id { get; set; }
        public int Layer { get; set; }
        public double X { get; set; }
        public double Width { get; set; } = 0; // Dummy nodes have no width
        public int OriginalFrom { get; set; }
        public int OriginalTo { get; set; }
    }

    private int _dummyIdCounter = -1;

    /// <summary>
    /// Inserts dummy nodes for edges that span multiple layers
    /// </summary>
    private Dictionary<int, List<object>> InsertDummyNodes(
        Dictionary<int, List<object>> layers,
        List<(int From, int To, bool IsReversed)> edges,
        Dictionary<int, Node> nodeDict,
        List<DummyNode> dummyNodes)
    {
        _dummyIdCounter = -1;
        
        // Build node-to-layer mapping including real nodes
        var nodeToLayer = new Dictionary<int, int>();
        foreach (var kvp in layers)
        {
            foreach (var item in kvp.Value)
            {
                if (item is Node n)
                    nodeToLayer[n.Id] = kvp.Key;
            }
        }

        // Process edges that span multiple layers
        var edgesToProcess = edges.ToList();
        foreach (var edge in edgesToProcess)
        {
            if (!nodeToLayer.TryGetValue(edge.From, out var fromLayer)) continue;
            if (!nodeToLayer.TryGetValue(edge.To, out var toLayer)) continue;

            var span = toLayer - fromLayer;
            if (span > 1)
            {
                // Create dummy nodes for intermediate layers
                for (int layer = fromLayer + 1; layer < toLayer; layer++)
                {
                    var dummy = new DummyNode
                    {
                        Id = _dummyIdCounter--,
                        Layer = layer,
                        OriginalFrom = edge.From,
                        OriginalTo = edge.To
                    };
                    dummyNodes.Add(dummy);
                    
                    if (!layers.ContainsKey(layer))
                        layers[layer] = new List<object>();
                    layers[layer].Add(dummy);
                }
            }
        }

        return layers;
    }

    /// <summary>
    /// Minimizes edge crossings using barycenter method with multiple sweeps
    /// </summary>
    private void MinimizeCrossings(
        Dictionary<int, List<object>> layers,
        List<(int From, int To, bool IsReversed)> edges,
        List<DummyNode> dummyNodes,
        int iterations)
    {
        var maxLayer = layers.Keys.DefaultIfEmpty(0).Max();
        
        // Build adjacency including dummy nodes
        var adjacency = new Dictionary<int, List<int>>();
        var reverseAdjacency = new Dictionary<int, List<int>>();

        foreach (var edge in edges)
        {
            if (!adjacency.ContainsKey(edge.From))
                adjacency[edge.From] = new List<int>();
            adjacency[edge.From].Add(edge.To);

            if (!reverseAdjacency.ContainsKey(edge.To))
                reverseAdjacency[edge.To] = new List<int>();
            reverseAdjacency[edge.To].Add(edge.From);
        }

        // Add dummy node connections
        foreach (var dummy in dummyNodes)
        {
            // Find what this dummy connects
            // Dummies create a chain: realFrom -> dummy1 -> dummy2 -> ... -> realTo
        }

        // Create a mapping from any node ID to its layer position
        Func<int, int, int> GetPositionInLayer = (nodeId, layerIndex) =>
        {
            if (!layers.ContainsKey(layerIndex)) return 0;
            var layer = layers[layerIndex];
            for (int i = 0; i < layer.Count; i++)
            {
                if (layer[i] is Node n && n.Id == nodeId) return i;
                if (layer[i] is DummyNode d && d.Id == nodeId) return i;
            }
            return 0;
        };

        Func<object, int> GetId = (obj) => obj is Node n ? n.Id : ((DummyNode)obj).Id;

        for (int iter = 0; iter < iterations; iter++)
        {
            // Forward sweep (top to bottom)
            for (int layerIdx = 1; layerIdx <= maxLayer; layerIdx++)
            {
                if (!layers.ContainsKey(layerIdx) || !layers.ContainsKey(layerIdx - 1)) continue;

                var currentLayer = layers[layerIdx];
                var prevLayer = layers[layerIdx - 1];
                var prevPositions = prevLayer.Select((obj, idx) => (GetId(obj), idx))
                    .ToDictionary(x => x.Item1, x => x.idx);

                var barycenters = new Dictionary<int, double>();
                foreach (var obj in currentLayer)
                {
                    var id = GetId(obj);
                    var parents = new List<int>();

                    // Get parents from reverse adjacency
                    if (reverseAdjacency.TryGetValue(id, out var p))
                        parents.AddRange(p.Where(pid => prevPositions.ContainsKey(pid)));

                    // For dummy nodes, also check if there's a previous dummy in the chain
                    if (obj is DummyNode dummy)
                    {
                        var prevDummies = dummyNodes
                            .Where(d => d.Layer == layerIdx - 1 && 
                                       d.OriginalFrom == dummy.OriginalFrom && 
                                       d.OriginalTo == dummy.OriginalTo)
                            .Select(d => d.Id);
                        parents.AddRange(prevDummies.Where(pid => prevPositions.ContainsKey(pid)));

                        // If this is the first dummy, connect to original source
                        if (!parents.Any() && prevPositions.ContainsKey(dummy.OriginalFrom))
                            parents.Add(dummy.OriginalFrom);
                    }

                    if (parents.Count > 0)
                    {
                        barycenters[id] = parents
                            .Where(pid => prevPositions.ContainsKey(pid))
                            .Select(pid => (double)prevPositions[pid])
                            .DefaultIfEmpty(currentLayer.IndexOf(obj))
                            .Average();
                    }
                    else
                    {
                        barycenters[id] = currentLayer.IndexOf(obj);
                    }
                }

                layers[layerIdx] = currentLayer.OrderBy(obj => barycenters[GetId(obj)]).ToList();
            }

            // Backward sweep (bottom to top)
            for (int layerIdx = maxLayer - 1; layerIdx >= 0; layerIdx--)
            {
                if (!layers.ContainsKey(layerIdx) || !layers.ContainsKey(layerIdx + 1)) continue;

                var currentLayer = layers[layerIdx];
                var nextLayer = layers[layerIdx + 1];
                var nextPositions = nextLayer.Select((obj, idx) => (GetId(obj), idx))
                    .ToDictionary(x => x.Item1, x => x.idx);

                var barycenters = new Dictionary<int, double>();
                foreach (var obj in currentLayer)
                {
                    var id = GetId(obj);
                    var children = new List<int>();

                    // Get children from adjacency
                    if (adjacency.TryGetValue(id, out var c))
                        children.AddRange(c.Where(cid => nextPositions.ContainsKey(cid)));

                    // For real nodes, check if there's a dummy starting from this node
                    if (obj is Node)
                    {
                        var nextDummies = dummyNodes
                            .Where(d => d.Layer == layerIdx + 1 && d.OriginalFrom == id)
                            .Select(d => d.Id);
                        children.AddRange(nextDummies.Where(cid => nextPositions.ContainsKey(cid)));
                    }

                    // For dummy nodes, find the next dummy or target
                    if (obj is DummyNode dummy)
                    {
                        var nextDummies = dummyNodes
                            .Where(d => d.Layer == layerIdx + 1 && 
                                       d.OriginalFrom == dummy.OriginalFrom && 
                                       d.OriginalTo == dummy.OriginalTo)
                            .Select(d => d.Id);
                        children.AddRange(nextDummies.Where(cid => nextPositions.ContainsKey(cid)));

                        // If this is the last dummy, connect to target
                        if (!children.Any() && nextPositions.ContainsKey(dummy.OriginalTo))
                            children.Add(dummy.OriginalTo);
                    }

                    if (children.Count > 0)
                    {
                        barycenters[id] = children
                            .Where(cid => nextPositions.ContainsKey(cid))
                            .Select(cid => (double)nextPositions[cid])
                            .DefaultIfEmpty(currentLayer.IndexOf(obj))
                            .Average();
                    }
                    else
                    {
                        barycenters[id] = currentLayer.IndexOf(obj);
                    }
                }

                layers[layerIdx] = currentLayer.OrderBy(obj => barycenters[GetId(obj)]).ToList();
            }
        }
    }

    /// <summary>
    /// Assigns X coordinates to minimize edge length and straighten edges
    /// </summary>
    private void AssignXCoordinates(Dictionary<int, List<object>> layers, double nodeSpacing)
    {
        if (layers.Count == 0) return;

        // First pass: assign initial X based on position in layer
        foreach (var kvp in layers)
        {
            double currentX = 0;
            foreach (var obj in kvp.Value)
            {
                if (obj is Node n)
                {
                    n.X = currentX;
                    currentX += n.Width + nodeSpacing;
                }
                else if (obj is DummyNode d)
                {
                    d.X = currentX;
                    currentX += nodeSpacing; // Dummies take minimal space
                }
            }
        }

        // Second pass: center each layer
        double maxWidth = layers.Values
            .Select(layer => layer.Sum(obj => 
                (obj is Node n ? n.Width : 0) + nodeSpacing))
            .DefaultIfEmpty(0)
            .Max();

        foreach (var kvp in layers)
        {
            var layerWidth = kvp.Value.Sum(obj => 
                (obj is Node n ? n.Width : 0) + nodeSpacing);
            var offset = (maxWidth - layerWidth) / 2;

            foreach (var obj in kvp.Value)
            {
                if (obj is Node n)
                    n.X += offset;
                else if (obj is DummyNode d)
                    d.X += offset;
            }
        }
    }

    /// <summary>
    /// Applies final positions to real nodes
    /// </summary>
    private void ApplyFinalPositions(
        Dictionary<int, List<object>> layers,
        Dictionary<int, Node> nodeDict,
        double layerSpacing,
        double nodeSpacing,
        bool topToBottom)
    {
        var startX = 100.0;
        var startY = 100.0;

        foreach (var kvp in layers.OrderBy(k => k.Key))
        {
            var layerIndex = kvp.Key;
            var layer = kvp.Value;

            // Calculate total width of this layer
            double totalWidth = 0;
            foreach (var obj in layer)
            {
                if (obj is Node n)
                    totalWidth += n.Width + nodeSpacing;
                else
                    totalWidth += nodeSpacing;
            }
            totalWidth -= nodeSpacing; // Remove last spacing

            // Center the layer
            var currentX = startX;

            foreach (var obj in layer)
            {
                if (obj is Node node)
                {
                    if (topToBottom)
                    {
                        node.X = currentX;
                        node.Y = startY + layerIndex * layerSpacing;
                    }
                    else
                    {
                        node.Y = currentX;
                        node.X = startY + layerIndex * layerSpacing;
                    }
                    currentX += node.Width + nodeSpacing;
                }
                else if (obj is DummyNode dummy)
                {
                    dummy.X = currentX;
                    currentX += nodeSpacing;
                }
            }
        }
    }

    #endregion

    #region Tree Layout

    /// <summary>
    /// Tree layout for hierarchical tree structures.
    /// </summary>
    public void ApplyTreeLayout(List<Node> nodes, List<Edge> edges,
        double levelSpacing = 120, double siblingSpacing = 40, int? rootId = null)
    {
        if (nodes.Count == 0) return;

        var inDegree = CalculateInDegree(nodes, edges);
        var adjacency = BuildAdjacencyList(edges);

        // Find root
        Node? root;
        if (rootId.HasValue)
            root = nodes.FirstOrDefault(n => n.Id == rootId.Value);
        else
            root = nodes.FirstOrDefault(n => inDegree[n.Id] == 0) ?? nodes.First();

        if (root == null) return;

        var nodePositions = new Dictionary<int, (double X, int Level)>();
        var subtreeWidths = new Dictionary<int, double>();

        // Calculate subtree widths
        CalculateSubtreeWidths(root.Id, adjacency, nodes.ToDictionary(n => n.Id), 
            subtreeWidths, siblingSpacing);

        // Assign positions
        AssignTreePositions(root.Id, 0, 0, adjacency, nodes.ToDictionary(n => n.Id),
            subtreeWidths, nodePositions, siblingSpacing);

        // Apply positions
        foreach (var node in nodes)
        {
            if (nodePositions.TryGetValue(node.Id, out var pos))
            {
                node.X = pos.X + 100;
                node.Y = pos.Level * levelSpacing + 100;
            }
        }
    }

    private double CalculateSubtreeWidths(int nodeId, Dictionary<int, List<int>> adjacency,
        Dictionary<int, Node> nodeDict, Dictionary<int, double> subtreeWidths, double spacing)
    {
        if (!nodeDict.TryGetValue(nodeId, out var node))
            return 0;

        var children = adjacency.TryGetValue(nodeId, out var c) ? c : new List<int>();
        
        if (children.Count == 0)
        {
            subtreeWidths[nodeId] = node.Width;
            return node.Width;
        }

        var totalChildWidth = children.Sum(childId => 
            CalculateSubtreeWidths(childId, adjacency, nodeDict, subtreeWidths, spacing));
        totalChildWidth += (children.Count - 1) * spacing;

        subtreeWidths[nodeId] = Math.Max(node.Width, totalChildWidth);
        return subtreeWidths[nodeId];
    }

    private void AssignTreePositions(int nodeId, int level, double leftBound,
        Dictionary<int, List<int>> adjacency, Dictionary<int, Node> nodeDict,
        Dictionary<int, double> subtreeWidths, Dictionary<int, (double X, int Level)> positions,
        double spacing)
    {
        if (!nodeDict.TryGetValue(nodeId, out var node))
            return;

        var subtreeWidth = subtreeWidths[nodeId];
        var nodeX = leftBound + (subtreeWidth - node.Width) / 2;
        positions[nodeId] = (nodeX, level);

        var children = adjacency.TryGetValue(nodeId, out var c) ? c : new List<int>();
        var childLeft = leftBound;

        foreach (var childId in children)
        {
            AssignTreePositions(childId, level + 1, childLeft, adjacency, nodeDict,
                subtreeWidths, positions, spacing);
            childLeft += subtreeWidths[childId] + spacing;
        }
    }

    #endregion

    #region Circle Layout

    /// <summary>
    /// Circular layout - arranges nodes in a circle.
    /// </summary>
    public void ApplyCircleLayout(List<Node> nodes, double centerX = 1000, double centerY = 750, double radius = 400)
    {
        if (nodes.Count == 0) return;

        var angleStep = 2 * Math.PI / nodes.Count;

        for (int i = 0; i < nodes.Count; i++)
        {
            var angle = i * angleStep - Math.PI / 2; // Start from top
            nodes[i].X = centerX + radius * Math.Cos(angle) - nodes[i].Width / 2;
            nodes[i].Y = centerY + radius * Math.Sin(angle) - nodes[i].Height / 2;
        }
    }

    #endregion

    #region Grid Layout

    /// <summary>
    /// Grid layout - arranges nodes in a regular grid.
    /// </summary>
    public void ApplyGridLayout(List<Node> nodes, int columns = 0, 
        double spacingX = 50, double spacingY = 50, double startX = 100, double startY = 100)
    {
        if (nodes.Count == 0) return;

        if (columns <= 0)
            columns = (int)Math.Ceiling(Math.Sqrt(nodes.Count));

        var maxWidth = nodes.Max(n => n.Width);
        var maxHeight = nodes.Max(n => n.Height);

        for (int i = 0; i < nodes.Count; i++)
        {
            var row = i / columns;
            var col = i % columns;

            nodes[i].X = startX + col * (maxWidth + spacingX);
            nodes[i].Y = startY + row * (maxHeight + spacingY);
        }
    }

    #endregion

    #region Radial Layout

    /// <summary>
    /// Radial layout - hierarchical layout in concentric circles.
    /// Distributes edges symmetrically from center node.
    /// </summary>
    public void ApplyRadialLayout(List<Node> nodes, List<Edge> edges,
        double centerX = 1000, double centerY = 750, double ringSpacing = 150, int? rootId = null)
    {
        if (nodes.Count == 0) return;

        var inDegree = CalculateInDegree(nodes, edges);
        var adjacency = BuildAdjacencyList(edges);

        // Find root
        Node? root;
        if (rootId.HasValue)
            root = nodes.FirstOrDefault(n => n.Id == rootId.Value);
        else
            root = nodes.FirstOrDefault(n => inDegree[n.Id] == 0) ?? nodes.First();

        if (root == null) return;

        // BFS to get levels
        var levels = new Dictionary<int, int>();
        var queue = new Queue<int>();
        queue.Enqueue(root.Id);
        levels[root.Id] = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = adjacency.TryGetValue(current, out var c) ? c : new List<int>();
            foreach (var child in children)
            {
                if (!levels.ContainsKey(child))
                {
                    levels[child] = levels[current] + 1;
                    queue.Enqueue(child);
                }
            }
        }

        // Assign level 1 to any unvisited nodes
        foreach (var node in nodes)
        {
            if (!levels.ContainsKey(node.Id))
                levels[node.Id] = 1;
        }

        // Group by level
        var levelGroups = nodes.GroupBy(n => levels.TryGetValue(n.Id, out var l) ? l : 0)
            .OrderBy(g => g.Key)
            .ToList();

        // Position root at center
        root.X = centerX - root.Width / 2;
        root.Y = centerY - root.Height / 2;

        // Position each ring - distribute nodes evenly
        foreach (var group in levelGroups.Skip(1))
        {
            var level = group.Key;
            var nodesInLevel = group.ToList();
            var radius = level * ringSpacing;
            var angleStep = 2 * Math.PI / nodesInLevel.Count;

            // Sort nodes by their parent's angle for better edge routing
            nodesInLevel = SortNodesByParentAngle(nodesInLevel, nodes, edges, centerX, centerY);

            for (int i = 0; i < nodesInLevel.Count; i++)
            {
                var angle = i * angleStep - Math.PI / 2; // Start from top
                nodesInLevel[i].X = centerX + radius * Math.Cos(angle) - nodesInLevel[i].Width / 2;
                nodesInLevel[i].Y = centerY + radius * Math.Sin(angle) - nodesInLevel[i].Height / 2;
            }
        }

        // Distribute edges from center node symmetrically
        DistributeEdgesFromCenter(root, nodes, edges, centerX, centerY);
    }

    /// <summary>
    /// Sort nodes by the angle of their parent node for smoother edge routing
    /// </summary>
    private List<Node> SortNodesByParentAngle(List<Node> nodesInLevel, List<Node> allNodes, 
        List<Edge> edges, double centerX, double centerY)
    {
        var nodeAngles = new Dictionary<int, double>();
        
        foreach (var node in nodesInLevel)
        {
            // Find parent edge
            var parentEdge = edges.FirstOrDefault(e => e.To == node.Id);
            if (parentEdge != null)
            {
                var parent = allNodes.FirstOrDefault(n => n.Id == parentEdge.From);
                if (parent != null)
                {
                    var dx = (parent.X + parent.Width / 2) - centerX;
                    var dy = (parent.Y + parent.Height / 2) - centerY;
                    nodeAngles[node.Id] = Math.Atan2(dy, dx);
                }
                else
                {
                    nodeAngles[node.Id] = 0;
                }
            }
            else
            {
                nodeAngles[node.Id] = 0;
            }
        }

        return nodesInLevel.OrderBy(n => nodeAngles.TryGetValue(n.Id, out var a) ? a : 0).ToList();
    }

    /// <summary>
    /// Distribute edges from center node to use different connection points
    /// </summary>
    private void DistributeEdgesFromCenter(Node centerNode, List<Node> nodes, List<Edge> edges, 
        double centerX, double centerY)
    {
        // Get all edges from the center node
        var outgoingEdges = edges.Where(e => e.From == centerNode.Id).ToList();
        if (outgoingEdges.Count == 0) return;

        // Calculate angle to each target and assign appropriate connection side/position
        var edgeAngles = new List<(Edge edge, double angle, Node target)>();
        
        foreach (var edge in outgoingEdges)
        {
            var target = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (target == null) continue;

            var dx = (target.X + target.Width / 2) - centerX;
            var dy = (target.Y + target.Height / 2) - centerY;
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI; // -180 to 180
            
            edgeAngles.Add((edge, angle, target));
        }

        // Sort by angle
        edgeAngles = edgeAngles.OrderBy(e => e.angle).ToList();

        // Group into sides based on angle
        var topEdges = edgeAngles.Where(e => e.angle >= -135 && e.angle < -45).ToList();
        var rightEdges = edgeAngles.Where(e => e.angle >= -45 && e.angle < 45).ToList();
        var bottomEdges = edgeAngles.Where(e => e.angle >= 45 && e.angle < 135).ToList();
        var leftEdges = edgeAngles.Where(e => e.angle >= 135 || e.angle < -135).ToList();

        // Assign connection points for each side
        AssignConnectionPointsToSide(topEdges, "top");
        AssignConnectionPointsToSide(rightEdges, "right");
        AssignConnectionPointsToSide(bottomEdges, "bottom");
        AssignConnectionPointsToSide(leftEdges, "left");

        // Also set the ToConnection on target nodes to face the center
        foreach (var (edge, angle, target) in edgeAngles)
        {
            // Target should connect on the side facing the center
            var reverseAngle = angle + 180;
            if (reverseAngle > 180) reverseAngle -= 360;
            
            string toSide;
            if (reverseAngle >= -45 && reverseAngle < 45) toSide = "right";
            else if (reverseAngle >= 45 && reverseAngle < 135) toSide = "bottom";
            else if (reverseAngle >= -135 && reverseAngle < -45) toSide = "top";
            else toSide = "left";

            edge.ToConnection = new ConnectionPoint { Side = toSide, Position = 0 };
        }
    }

    private void AssignConnectionPointsToSide(List<(Edge edge, double angle, Node target)> edgesOnSide, string side)
    {
        if (edgesOnSide.Count == 0) return;

        // Sort by angle for consistent ordering
        var sorted = edgesOnSide.OrderBy(e => e.angle).ToList();
        
        for (int i = 0; i < sorted.Count; i++)
        {
            // Distribute positions: -1, 0, 1 for 3 edges; -1, 0 for 2; 0 for 1
            int position = sorted.Count == 1 ? 0 : i - (sorted.Count - 1) / 2;
            sorted[i].edge.FromConnection = new ConnectionPoint { Side = side, Position = position };
        }
    }

    #endregion

    #region Compact Layout

    /// <summary>
    /// Compact layout - moves nodes closer together while maintaining relative positions
    /// and avoiding overlaps.
    /// </summary>
    public void ApplyCompactLayout(List<Node> nodes, double targetSpacing = 30)
    {
        if (nodes.Count < 2) return;

        // Find the center of mass
        var centerX = nodes.Average(n => n.X + n.Width / 2);
        var centerY = nodes.Average(n => n.Y + n.Height / 2);

        // Sort nodes by distance from center (process outer nodes first)
        var nodesByDistance = nodes
            .OrderByDescending(n => {
                var dx = (n.X + n.Width / 2) - centerX;
                var dy = (n.Y + n.Height / 2) - centerY;
                return Math.Sqrt(dx * dx + dy * dy);
            })
            .ToList();

        // Move each node toward center until it would overlap
        foreach (var node in nodesByDistance)
        {
            var nodeCenterX = node.X + node.Width / 2;
            var nodeCenterY = node.Y + node.Height / 2;

            var dx = centerX - nodeCenterX;
            var dy = centerY - nodeCenterY;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 1) continue; // Already at center

            // Normalize direction
            dx /= dist;
            dy /= dist;

            // Try moving toward center in steps
            var stepSize = 10.0;
            var maxSteps = (int)(dist / stepSize);

            for (int step = 0; step < maxSteps; step++)
            {
                var newX = node.X + dx * stepSize;
                var newY = node.Y + dy * stepSize;

                // Check for overlaps with other nodes
                bool wouldOverlap = false;
                foreach (var other in nodes)
                {
                    if (other.Id == node.Id) continue;

                    if (CheckOverlap(newX, newY, node.Width, node.Height,
                        other.X, other.Y, other.Width, other.Height, targetSpacing))
                    {
                        wouldOverlap = true;
                        break;
                    }
                }

                if (wouldOverlap)
                    break;

                node.X = newX;
                node.Y = newY;
            }
        }
    }

    private bool CheckOverlap(double x1, double y1, double w1, double h1,
        double x2, double y2, double w2, double h2, double margin)
    {
        return !(x1 + w1 + margin < x2 ||
                 x2 + w2 + margin < x1 ||
                 y1 + h1 + margin < y2 ||
                 y2 + h2 + margin < y1);
    }

    #endregion

    #region Flip Layout

    /// <summary>
    /// Flip layout horizontally (mirror around vertical axis)
    /// </summary>
    public void FlipHorizontal(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        // Find bounding box
        double minX = nodes.Min(n => n.X);
        double maxX = nodes.Max(n => n.X + n.Width);
        double centerX = (minX + maxX) / 2;

        // Mirror each node around center
        foreach (var node in nodes)
        {
            double nodeCenterX = node.X + node.Width / 2;
            double distFromCenter = nodeCenterX - centerX;
            node.X = centerX - distFromCenter - node.Width / 2;
        }
    }

    /// <summary>
    /// Flip layout vertically (mirror around horizontal axis)
    /// </summary>
    public void FlipVertical(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        // Find bounding box
        double minY = nodes.Min(n => n.Y);
        double maxY = nodes.Max(n => n.Y + n.Height);
        double centerY = (minY + maxY) / 2;

        // Mirror each node around center
        foreach (var node in nodes)
        {
            double nodeCenterY = node.Y + node.Height / 2;
            double distFromCenter = nodeCenterY - centerY;
            node.Y = centerY - distFromCenter - node.Height / 2;
        }
    }

    #endregion

    #region Symmetric Layout

    /// <summary>
    /// Arrange nodes symmetrically around a horizontal axis.
    /// Algorithm: Sort nodes by connectivity, place highest-connected at center,
    /// then pair remaining nodes on opposite sides.
    /// </summary>
    public void ApplySymmetricHorizontal(List<Node> nodes, List<Edge> edges, double spacing = 150)
    {
        if (nodes.Count == 0) return;

        // Calculate connectivity for each node
        var connectivity = CalculateConnectivity(nodes, edges);

        // Sort by connectivity (highest first)
        var sortedNodes = nodes.OrderByDescending(n => connectivity[n.Id]).ToList();

        // Find center of current layout
        double centerX = nodes.Average(n => n.X + n.Width / 2);
        double centerY = nodes.Average(n => n.Y + n.Height / 2);

        // Place nodes symmetrically around horizontal axis (left-right symmetry)
        int placed = 0;
        double currentY = centerY;
        
        foreach (var node in sortedNodes)
        {
            if (placed == 0)
            {
                // First (most connected) node at center
                node.X = centerX - node.Width / 2;
                node.Y = currentY - node.Height / 2;
            }
            else
            {
                // Alternate left and right, moving outward
                int level = (placed + 1) / 2;
                bool isLeft = placed % 2 == 1;
                
                double offsetX = level * spacing;
                node.X = isLeft ? centerX - offsetX - node.Width / 2 : centerX + offsetX - node.Width / 2;
                node.Y = currentY - node.Height / 2;
            }
            placed++;
        }
    }

    /// <summary>
    /// Arrange nodes symmetrically around a vertical axis.
    /// Algorithm: Sort nodes by connectivity, place highest-connected at center,
    /// then pair remaining nodes on opposite sides (top-bottom).
    /// </summary>
    public void ApplySymmetricVertical(List<Node> nodes, List<Edge> edges, double spacing = 120)
    {
        if (nodes.Count == 0) return;

        // Calculate connectivity for each node
        var connectivity = CalculateConnectivity(nodes, edges);

        // Sort by connectivity (highest first)
        var sortedNodes = nodes.OrderByDescending(n => connectivity[n.Id]).ToList();

        // Find center of current layout
        double centerX = nodes.Average(n => n.X + n.Width / 2);
        double centerY = nodes.Average(n => n.Y + n.Height / 2);

        // Place nodes symmetrically around vertical axis (top-bottom symmetry)
        int placed = 0;
        
        foreach (var node in sortedNodes)
        {
            if (placed == 0)
            {
                // First (most connected) node at center
                node.X = centerX - node.Width / 2;
                node.Y = centerY - node.Height / 2;
            }
            else
            {
                // Alternate top and bottom, moving outward
                int level = (placed + 1) / 2;
                bool isTop = placed % 2 == 1;
                
                double offsetY = level * spacing;
                node.X = centerX - node.Width / 2;
                node.Y = isTop ? centerY - offsetY - node.Height / 2 : centerY + offsetY - node.Height / 2;
            }
            placed++;
        }
    }

    /// <summary>
    /// Calculate connectivity score for each node (number of edges connected)
    /// </summary>
    private Dictionary<int, int> CalculateConnectivity(List<Node> nodes, List<Edge> edges)
    {
        var connectivity = nodes.ToDictionary(n => n.Id, n => 0);
        
        foreach (var edge in edges)
        {
            if (connectivity.ContainsKey(edge.From))
                connectivity[edge.From]++;
            if (connectivity.ContainsKey(edge.To))
                connectivity[edge.To]++;
        }
        
        return connectivity;
    }

    #endregion

    #region Helper Methods

    private void InitializePositions(List<Node> nodes, double width, double height)
    {
        // Check if all nodes are at same position
        var allSamePosition = nodes.All(n => 
            Math.Abs(n.X - nodes[0].X) < 1 && Math.Abs(n.Y - nodes[0].Y) < 1);

        if (allSamePosition)
        {
            foreach (var node in nodes)
            {
                node.X = _random.NextDouble() * (width - node.Width);
                node.Y = _random.NextDouble() * (height - node.Height);
            }
        }
    }

    private Dictionary<int, List<int>> BuildAdjacencyList(List<Edge> edges)
    {
        var adj = new Dictionary<int, List<int>>();
        foreach (var edge in edges)
        {
            if (!adj.ContainsKey(edge.From))
                adj[edge.From] = new List<int>();
            adj[edge.From].Add(edge.To);
        }
        return adj;
    }

    private Dictionary<int, List<int>> BuildReverseAdjacencyList(List<Edge> edges)
    {
        var adj = new Dictionary<int, List<int>>();
        foreach (var edge in edges)
        {
            if (!adj.ContainsKey(edge.To))
                adj[edge.To] = new List<int>();
            adj[edge.To].Add(edge.From);
        }
        return adj;
    }

    private Dictionary<int, int> CalculateInDegree(List<Node> nodes, List<Edge> edges)
    {
        var inDegree = nodes.ToDictionary(n => n.Id, n => 0);
        foreach (var edge in edges)
        {
            if (inDegree.ContainsKey(edge.To))
                inDegree[edge.To]++;
        }
        return inDegree;
    }

    #endregion
}
