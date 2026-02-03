using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Modular layout optimization with separate routines for different aspects
/// </summary>
public class LayoutOptimizerService
{
    private readonly Random _random = new();
    
    #region Configuration
    
    /// <summary>
    /// Simulated annealing parameters - can be set interactively
    /// </summary>
    public class AnnealingConfig
    {
        public double InitialTemperature { get; set; } = 1000;
        public double CoolingRate { get; set; } = 0.995;
        public double MinTemperature { get; set; } = 0.1;
        public int MaxIterations { get; set; } = 5000;
        public int MaxNoImprovement { get; set; } = 500;
        public int GridSize { get; set; } = 20;
    }
    
    /// <summary>
    /// Weights for fitness function components
    /// </summary>
    public class FitnessWeights
    {
        // Node placement weights
        public double OverlapPenalty { get; set; } = 1000;
        public double EdgeLengthWeight { get; set; } = 1;
        public double EdgeCrossingPenalty { get; set; } = 100;
        public double AlignmentBonus { get; set; } = 10;
        public double DistributionWeight { get; set; } = 5;
        
        // Edge routing weights
        public double ShallowAnglePenalty { get; set; } = 50;
        public double ConnectionSpreadPenalty { get; set; } = 20;
        
        // Flow direction weights
        public double UpwardEdgePenalty { get; set; } = 30;
        public double BackwardEdgePenalty { get; set; } = 20;
        public double FlowConsistencyBonus { get; set; } = 15;
    }
    
    /// <summary>
    /// Preferred flow direction for the diagram
    /// </summary>
    public enum FlowDirection
    {
        TopDown,      // Prefer arrows pointing down
        LeftRight,    // Prefer arrows pointing right
        BottomUp,     // Prefer arrows pointing up
        RightLeft     // Prefer arrows pointing left
    }
    
    #endregion
    
    #region Node Placement Optimizer
    
    /// <summary>
    /// Optimize node positions using simulated annealing
    /// </summary>
    public async Task<(List<Node> nodes, double improvement)> OptimizeNodePlacement(
        List<Node> nodes,
        List<Edge> edges,
        AnnealingConfig config,
        FitnessWeights weights,
        Action<int, double, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var workingNodes = nodes.Select(n => n.Clone()).ToList();
        
        double currentFitness = EvaluateNodePlacement(workingNodes, edges, weights);
        double bestFitness = currentFitness;
        var bestNodes = workingNodes.Select(n => n.Clone()).ToList();
        double initialFitness = currentFitness;
        
        double temperature = config.InitialTemperature;
        int noImprovementCount = 0;
        
        for (int i = 0; i < config.MaxIterations && !cancellationToken.IsCancellationRequested; i++)
        {
            // Random perturbation
            var nodeIndex = _random.Next(workingNodes.Count);
            var node = workingNodes[nodeIndex];
            var oldX = node.X;
            var oldY = node.Y;
            
            // Move by grid-aligned amount, scaled by temperature
            var moveScale = Math.Max(1, temperature / 100);
            node.X += (_random.NextDouble() - 0.5) * config.GridSize * 4 * moveScale;
            node.Y += (_random.NextDouble() - 0.5) * config.GridSize * 4 * moveScale;
            
            // Snap to grid
            node.X = Math.Round(node.X / config.GridSize) * config.GridSize;
            node.Y = Math.Round(node.Y / config.GridSize) * config.GridSize;
            
            // Keep in bounds
            node.X = Math.Max(50, node.X);
            node.Y = Math.Max(50, node.Y);
            
            double newFitness = EvaluateNodePlacement(workingNodes, edges, weights);
            double delta = newFitness - currentFitness;
            
            // Accept if better, or probabilistically if worse
            if (delta < 0 || _random.NextDouble() < Math.Exp(-delta / temperature))
            {
                currentFitness = newFitness;
                
                if (currentFitness < bestFitness)
                {
                    bestFitness = currentFitness;
                    bestNodes = workingNodes.Select(n => n.Clone()).ToList();
                    noImprovementCount = 0;
                }
            }
            else
            {
                // Revert
                node.X = oldX;
                node.Y = oldY;
                noImprovementCount++;
            }
            
            temperature *= config.CoolingRate;
            
            if (temperature < config.MinTemperature || noImprovementCount > config.MaxNoImprovement)
                break;
            
            // Progress callback every 100 iterations
            if (i % 100 == 0)
            {
                progressCallback?.Invoke(i, bestFitness, "Node Placement");
                await Task.Yield(); // Allow UI updates
            }
        }
        
        double improvement = initialFitness > 0 ? ((initialFitness - bestFitness) / initialFitness) * 100 : 0;
        return (bestNodes, improvement);
    }
    
    private double EvaluateNodePlacement(List<Node> nodes, List<Edge> edges, FitnessWeights weights)
    {
        double fitness = 0;
        
        // Overlap penalty
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var overlap = CalculateOverlap(nodes[i], nodes[j]);
                fitness += overlap * weights.OverlapPenalty;
            }
        }
        
        // Edge length (prefer shorter edges)
        foreach (var edge in edges)
        {
            var from = nodes.FirstOrDefault(n => n.Id == edge.From);
            var to = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (from != null && to != null)
            {
                var dist = Distance(from, to);
                fitness += dist * weights.EdgeLengthWeight;
            }
        }
        
        // Edge crossing penalty
        fitness += CountEdgeCrossings(nodes, edges) * weights.EdgeCrossingPenalty;
        
        // Alignment bonus (reward nodes on same row/column)
        fitness -= CountAlignments(nodes) * weights.AlignmentBonus;
        
        return fitness;
    }
    
    #endregion
    
    #region Edge Connection Optimizer
    
    /// <summary>
    /// Optimize which connection points edges use on each node
    /// </summary>
    public (List<Edge> edges, double improvement) OptimizeEdgeConnections(
        List<Node> nodes,
        List<Edge> edges,
        FitnessWeights weights)
    {
        var workingEdges = edges.Select(e => e.Clone()).ToList();
        double initialFitness = EvaluateEdgeConnections(nodes, workingEdges, weights);
        
        // Try different connection point combinations
        foreach (var edge in workingEdges)
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (fromNode == null || toNode == null) continue;
            
            var bestFromSide = edge.FromConnection?.Side ?? "right";
            var bestToSide = edge.ToConnection?.Side ?? "left";
            double bestFitness = double.MaxValue;
            
            var sides = new[] { "top", "right", "bottom", "left" };
            
            foreach (var fromSide in sides)
            {
                foreach (var toSide in sides)
                {
                    edge.FromConnection = new ConnectionPoint { Side = fromSide, Position = 0 };
                    edge.ToConnection = new ConnectionPoint { Side = toSide, Position = 0 };
                    
                    var fitness = EvaluateSingleEdge(fromNode, toNode, edge, weights);
                    if (fitness < bestFitness)
                    {
                        bestFitness = fitness;
                        bestFromSide = fromSide;
                        bestToSide = toSide;
                    }
                }
            }
            
            edge.FromConnection = new ConnectionPoint { Side = bestFromSide, Position = 0 };
            edge.ToConnection = new ConnectionPoint { Side = bestToSide, Position = 0 };
        }
        
        double finalFitness = EvaluateEdgeConnections(nodes, workingEdges, weights);
        double improvement = initialFitness > 0 ? ((initialFitness - finalFitness) / initialFitness) * 100 : 0;
        
        return (workingEdges, improvement);
    }
    
    private double EvaluateEdgeConnections(List<Node> nodes, List<Edge> edges, FitnessWeights weights)
    {
        double fitness = 0;
        
        foreach (var edge in edges)
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (fromNode == null || toNode == null) continue;
            
            fitness += EvaluateSingleEdge(fromNode, toNode, edge, weights);
        }
        
        return fitness;
    }
    
    private double EvaluateSingleEdge(Node from, Node to, Edge edge, FitnessWeights weights)
    {
        double fitness = 0;
        
        // Get connection point coordinates
        var (fromX, fromY) = GetConnectionCoords(from, edge.FromConnection);
        var (toX, toY) = GetConnectionCoords(to, edge.ToConnection);
        
        // Penalize shallow angles (arrows that graze the node edge)
        var dx = toX - fromX;
        var dy = toY - fromY;
        var angle = Math.Abs(Math.Atan2(dy, dx) * 180 / Math.PI);
        
        // Shallow if close to 0, 90, 180, 270
        var shallowness = Math.Min(
            Math.Min(angle % 90, 90 - (angle % 90)),
            Math.Min(Math.Abs(angle), Math.Abs(180 - angle))
        );
        if (shallowness < 15) // Less than 15 degrees from axis
        {
            fitness += (15 - shallowness) * weights.ShallowAnglePenalty;
        }
        
        return fitness;
    }
    
    private (double x, double y) GetConnectionCoords(Node node, ConnectionPoint? conn)
    {
        var side = conn?.Side ?? "right";
        var pos = conn?.Position ?? 0;
        
        double x = node.X + node.Width / 2;
        double y = node.Y + node.Height / 2;
        
        switch (side)
        {
            case "top":
                y = node.Y;
                x = node.X + node.Width / 2 + pos * 15;
                break;
            case "bottom":
                y = node.Y + node.Height;
                x = node.X + node.Width / 2 + pos * 15;
                break;
            case "left":
                x = node.X;
                y = node.Y + node.Height / 2 + pos * 15;
                break;
            case "right":
                x = node.X + node.Width;
                y = node.Y + node.Height / 2 + pos * 15;
                break;
        }
        
        return (x, y);
    }
    
    #endregion
    
    #region Arrow Direction Optimizer
    
    /// <summary>
    /// Optimize node positions to minimize arrows going against preferred flow
    /// </summary>
    public (List<Node> nodes, double improvement) OptimizeFlowDirection(
        List<Node> nodes,
        List<Edge> edges,
        FlowDirection preferredFlow,
        FitnessWeights weights)
    {
        var workingNodes = nodes.Select(n => n.Clone()).ToList();
        double initialScore = EvaluateFlowDirection(workingNodes, edges, preferredFlow, weights);
        
        // Topological sort to determine levels
        var levels = ComputeLevels(workingNodes, edges);
        
        // Rearrange nodes based on levels and flow direction
        double currentY = 50;
        double currentX = 50;
        int nodesPerRow = 4;
        int count = 0;
        
        var sortedNodes = workingNodes.OrderBy(n => levels.GetValueOrDefault(n.Id, 0)).ToList();
        
        foreach (var node in sortedNodes)
        {
            switch (preferredFlow)
            {
                case FlowDirection.TopDown:
                    node.Y = 50 + levels.GetValueOrDefault(node.Id, 0) * 120;
                    break;
                case FlowDirection.BottomUp:
                    var maxLevel = levels.Values.DefaultIfEmpty(0).Max();
                    node.Y = 50 + (maxLevel - levels.GetValueOrDefault(node.Id, 0)) * 120;
                    break;
                case FlowDirection.LeftRight:
                    node.X = 50 + levels.GetValueOrDefault(node.Id, 0) * 180;
                    break;
                case FlowDirection.RightLeft:
                    var maxLevelLR = levels.Values.DefaultIfEmpty(0).Max();
                    node.X = 50 + (maxLevelLR - levels.GetValueOrDefault(node.Id, 0)) * 180;
                    break;
            }
        }
        
        double finalScore = EvaluateFlowDirection(workingNodes, edges, preferredFlow, weights);
        double improvement = initialScore > 0 ? ((initialScore - finalScore) / initialScore) * 100 : 0;
        
        return (workingNodes, improvement);
    }
    
    private double EvaluateFlowDirection(List<Node> nodes, List<Edge> edges, FlowDirection flow, FitnessWeights weights)
    {
        double fitness = 0;
        
        foreach (var edge in edges)
        {
            var from = nodes.FirstOrDefault(n => n.Id == edge.From);
            var to = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (from == null || to == null) continue;
            
            var fromCenter = (X: from.X + from.Width / 2, Y: from.Y + from.Height / 2);
            var toCenter = (X: to.X + to.Width / 2, Y: to.Y + to.Height / 2);
            
            bool goesAgainstFlow = flow switch
            {
                FlowDirection.TopDown => toCenter.Y < fromCenter.Y,    // Upward edge
                FlowDirection.BottomUp => toCenter.Y > fromCenter.Y,   // Downward edge
                FlowDirection.LeftRight => toCenter.X < fromCenter.X,  // Leftward edge
                FlowDirection.RightLeft => toCenter.X > fromCenter.X,  // Rightward edge
                _ => false
            };
            
            if (goesAgainstFlow)
            {
                fitness += weights.UpwardEdgePenalty;
            }
        }
        
        return fitness;
    }
    
    private Dictionary<int, int> ComputeLevels(List<Node> nodes, List<Edge> edges)
    {
        var levels = new Dictionary<int, int>();
        var inDegree = nodes.ToDictionary(n => n.Id, n => 0);
        
        foreach (var edge in edges)
        {
            if (inDegree.ContainsKey(edge.To))
                inDegree[edge.To]++;
        }
        
        // Start with nodes that have no incoming edges
        var queue = new Queue<int>(nodes.Where(n => inDegree[n.Id] == 0).Select(n => n.Id));
        foreach (var id in queue)
            levels[id] = 0;
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentLevel = levels[current];
            
            foreach (var edge in edges.Where(e => e.From == current))
            {
                if (!levels.ContainsKey(edge.To))
                {
                    levels[edge.To] = currentLevel + 1;
                    queue.Enqueue(edge.To);
                }
                else
                {
                    levels[edge.To] = Math.Max(levels[edge.To], currentLevel + 1);
                }
            }
        }
        
        // Handle cycles - assign remaining nodes
        foreach (var node in nodes.Where(n => !levels.ContainsKey(n.Id)))
        {
            levels[node.Id] = 0;
        }
        
        return levels;
    }
    
    #endregion
    
    #region Combined Optimizer
    
    /// <summary>
    /// Run all optimizers in sequence
    /// </summary>
    public async Task<OptimizationResult> OptimizeAll(
        List<Node> nodes,
        List<Edge> edges,
        AnnealingConfig config,
        FitnessWeights weights,
        FlowDirection preferredFlow,
        Action<int, double, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var result = new OptimizationResult();
        
        // Step 1: Optimize flow direction first (structural)
        progressCallback?.Invoke(0, 0, "Optimizing flow direction...");
        var (flowNodes, flowImprovement) = OptimizeFlowDirection(nodes, edges, preferredFlow, weights);
        result.FlowImprovement = flowImprovement;
        
        // Step 2: Optimize node placement (simulated annealing)
        progressCallback?.Invoke(0, 0, "Optimizing node placement...");
        var (placedNodes, placementImprovement) = await OptimizeNodePlacement(
            flowNodes, edges, config, weights, progressCallback, cancellationToken);
        result.PlacementImprovement = placementImprovement;
        
        // Step 3: Optimize edge connections
        progressCallback?.Invoke(0, 0, "Optimizing edge connections...");
        var (optimizedEdges, connectionImprovement) = OptimizeEdgeConnections(placedNodes, edges, weights);
        result.ConnectionImprovement = connectionImprovement;
        
        result.OptimizedNodes = placedNodes;
        result.OptimizedEdges = optimizedEdges;
        
        return result;
    }
    
    public class OptimizationResult
    {
        public List<Node> OptimizedNodes { get; set; } = new();
        public List<Edge> OptimizedEdges { get; set; } = new();
        public double FlowImprovement { get; set; }
        public double PlacementImprovement { get; set; }
        public double ConnectionImprovement { get; set; }
        public double TotalImprovement => (FlowImprovement + PlacementImprovement + ConnectionImprovement) / 3;
    }
    
    #endregion
    
    #region Helper Methods
    
    private double CalculateOverlap(Node a, Node b)
    {
        var overlapX = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X));
        var overlapY = Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
        return overlapX * overlapY;
    }
    
    private double Distance(Node a, Node b)
    {
        var dx = (a.X + a.Width / 2) - (b.X + b.Width / 2);
        var dy = (a.Y + a.Height / 2) - (b.Y + b.Height / 2);
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    private int CountEdgeCrossings(List<Node> nodes, List<Edge> edges)
    {
        int crossings = 0;
        
        for (int i = 0; i < edges.Count; i++)
        {
            for (int j = i + 1; j < edges.Count; j++)
            {
                if (EdgesIntersect(nodes, edges[i], edges[j]))
                    crossings++;
            }
        }
        
        return crossings;
    }
    
    private bool EdgesIntersect(List<Node> nodes, Edge e1, Edge e2)
    {
        // Skip if edges share a node
        if (e1.From == e2.From || e1.From == e2.To || e1.To == e2.From || e1.To == e2.To)
            return false;
        
        var n1 = nodes.FirstOrDefault(n => n.Id == e1.From);
        var n2 = nodes.FirstOrDefault(n => n.Id == e1.To);
        var n3 = nodes.FirstOrDefault(n => n.Id == e2.From);
        var n4 = nodes.FirstOrDefault(n => n.Id == e2.To);
        
        if (n1 == null || n2 == null || n3 == null || n4 == null) return false;
        
        return LinesIntersect(
            n1.X + n1.Width / 2, n1.Y + n1.Height / 2,
            n2.X + n2.Width / 2, n2.Y + n2.Height / 2,
            n3.X + n3.Width / 2, n3.Y + n3.Height / 2,
            n4.X + n4.Width / 2, n4.Y + n4.Height / 2
        );
    }
    
    private bool LinesIntersect(double x1, double y1, double x2, double y2,
                                 double x3, double y3, double x4, double y4)
    {
        double d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(d) < 0.0001) return false;
        
        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / d;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / d;
        
        return t > 0 && t < 1 && u > 0 && u < 1;
    }
    
    private int CountAlignments(List<Node> nodes, double tolerance = 5)
    {
        int alignments = 0;
        
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                // Horizontal alignment (same Y)
                if (Math.Abs(nodes[i].Y - nodes[j].Y) < tolerance)
                    alignments++;
                
                // Vertical alignment (same X)
                if (Math.Abs(nodes[i].X - nodes[j].X) < tolerance)
                    alignments++;
            }
        }
        
        return alignments;
    }
    
    #endregion
}

/// <summary>
/// Extension method for cloning nodes
/// </summary>
public static class NodeExtensions
{
    public static Node Clone(this Node node)
    {
        return new Node
        {
            Id = node.Id,
            X = node.X,
            Y = node.Y,
            Width = node.Width,
            Height = node.Height,
            Text = node.Text,
            Shape = node.Shape,
        };
    }
    
    public static Edge Clone(this Edge edge)
    {
        return new Edge
        {
            Id = edge.Id,
            From = edge.From,
            To = edge.To,
            Label = edge.Label,
            FromConnection = edge.FromConnection != null 
                ? new ConnectionPoint { Side = edge.FromConnection.Side, Position = edge.FromConnection.Position }
                : null,
            ToConnection = edge.ToConnection != null
                ? new ConnectionPoint { Side = edge.ToConnection.Side, Position = edge.ToConnection.Position }
                : null,
            PathData = edge.PathData,
            StrokeWidth = edge.StrokeWidth,
            StrokeDashArray = edge.StrokeDashArray
        };
    }
}
