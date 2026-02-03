using System.Text.Json;
using dfd2wasm.Models;

namespace dfd2wasm.Services;

/// <summary>
/// Service for collecting training data for GNN layout optimization.
/// Saves user-adjusted layouts to build a training dataset.
/// </summary>
public class LayoutDataCollectionService
{
    private readonly string _dataDirectory;
    private readonly List<LayoutSnapshot> _sessionSnapshots = new();
    
    public LayoutDataCollectionService(string dataDirectory = "training_data")
    {
        _dataDirectory = dataDirectory;
    }
    
    /// <summary>
    /// Capture a layout snapshot after user makes adjustments.
    /// </summary>
    public void CaptureSnapshot(
        List<Node> nodes, 
        List<Edge> edges,
        string? diagramName = null,
        SnapshotReason reason = SnapshotReason.UserAdjustment)
    {
        var snapshot = new LayoutSnapshot
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            DiagramName = diagramName ?? $"diagram_{DateTime.Now:yyyyMMdd_HHmmss}",
            Reason = reason,
            Nodes = nodes.Select(n => new NodeSnapshot
            {
                Id = n.Id,
                X = n.X,
                Y = n.Y,
                Width = n.Width,
                Height = n.Height,
                Text = n.Text,
                Shape = n.Shape.ToString(),
                InDegree = edges.Count(e => e.To == n.Id),
                OutDegree = edges.Count(e => e.From == n.Id)
            }).ToList(),
            Edges = edges.Select(e => new EdgeSnapshot
            {
                Id = e.Id,
                From = e.From,
                To = e.To,
                FromSide = e.FromConnection?.Side ?? "right",
                ToSide = e.ToConnection?.Side ?? "left",
                FromPosition = e.FromConnection?.Position ?? 0,
                ToPosition = e.ToConnection?.Position ?? 0
            }).ToList(),
            Metrics = CalculateMetrics(nodes, edges)
        };
        
        _sessionSnapshots.Add(snapshot);
    }
    
    /// <summary>
    /// Save all session snapshots to disk.
    /// </summary>
    public async Task SaveSessionDataAsync()
    {
        if (_sessionSnapshots.Count == 0) return;
        
        // Create directory if needed
        Directory.CreateDirectory(_dataDirectory);
        
        foreach (var snapshot in _sessionSnapshots)
        {
            var filename = $"{snapshot.DiagramName}_{snapshot.Id[..8]}.json";
            var filepath = Path.Combine(_dataDirectory, filename);
            
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(filepath, json);
        }
        
        Console.WriteLine($"Saved {_sessionSnapshots.Count} layout snapshots to {_dataDirectory}");
        _sessionSnapshots.Clear();
    }
    
    /// <summary>
    /// Export data in format suitable for PyTorch Geometric.
    /// </summary>
    public string ExportForTraining(List<Node> nodes, List<Edge> edges)
    {
        var data = new
        {
            nodes = nodes.Select(n => new
            {
                id = n.Id,
                x = n.X,
                y = n.Y,
                width = n.Width,
                height = n.Height,
                text = n.Text,
                shape = n.Shape.ToString(),
                features = new double[]
                {
                    edges.Count(e => e.To == n.Id),     // in_degree
                    edges.Count(e => e.From == n.Id),  // out_degree
                    n.Text?.Length ?? 0,               // label_length
                    (int)n.Shape,                      // node_type
                    n.Width,                           // width
                    n.Height                           // height
                }
            }),
            edges = edges.Select(e => new
            {
                from = e.From,
                to = e.To,
                from_side = e.FromConnection?.Side ?? "right",
                to_side = e.ToConnection?.Side ?? "left"
            }),
            positions = nodes.ToDictionary(
                n => n.Id.ToString(),
                n => new { x = n.X, y = n.Y }
            )
        };
        
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
    
    /// <summary>
    /// Calculate layout quality metrics.
    /// </summary>
    private LayoutMetrics CalculateMetrics(List<Node> nodes, List<Edge> edges)
    {
        var metrics = new LayoutMetrics();
        
        // Count overlaps
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (NodesOverlap(nodes[i], nodes[j]))
                    metrics.OverlapCount++;
            }
        }
        
        // Count edge crossings
        var edgeList = edges.ToList();
        for (int i = 0; i < edgeList.Count; i++)
        {
            for (int j = i + 1; j < edgeList.Count; j++)
            {
                if (EdgesIntersect(nodes, edgeList[i], edgeList[j]))
                    metrics.CrossingCount++;
            }
        }
        
        // Calculate average edge length
        double totalLength = 0;
        foreach (var edge in edges)
        {
            var from = nodes.FirstOrDefault(n => n.Id == edge.From);
            var to = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (from != null && to != null)
            {
                var dx = (to.X + to.Width / 2) - (from.X + from.Width / 2);
                var dy = (to.Y + to.Height / 2) - (from.Y + from.Height / 2);
                totalLength += Math.Sqrt(dx * dx + dy * dy);
            }
        }
        metrics.AverageEdgeLength = edges.Count > 0 ? totalLength / edges.Count : 0;
        
        // Count upward edges (against top-down flow)
        foreach (var edge in edges)
        {
            var from = nodes.FirstOrDefault(n => n.Id == edge.From);
            var to = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (from != null && to != null && to.Y < from.Y)
                metrics.UpwardEdgeCount++;
        }
        
        // Count alignments
        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (Math.Abs(nodes[i].X - nodes[j].X) < 5)
                    metrics.VerticalAlignments++;
                if (Math.Abs(nodes[i].Y - nodes[j].Y) < 5)
                    metrics.HorizontalAlignments++;
            }
        }
        
        return metrics;
    }
    
    private bool NodesOverlap(Node a, Node b)
    {
        return a.X < b.X + b.Width &&
               a.X + a.Width > b.X &&
               a.Y < b.Y + b.Height &&
               a.Y + a.Height > b.Y;
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
}

public enum SnapshotReason
{
    UserAdjustment,
    AfterImport,
    AfterOptimization,
    ManualSave
}

public class LayoutSnapshot
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string DiagramName { get; set; } = "";
    public SnapshotReason Reason { get; set; }
    public List<NodeSnapshot> Nodes { get; set; } = new();
    public List<EdgeSnapshot> Edges { get; set; } = new();
    public LayoutMetrics Metrics { get; set; } = new();
}

public class NodeSnapshot
{
    public int Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = "";
    public string Shape { get; set; } = "Rectangle";
    public int InDegree { get; set; }
    public int OutDegree { get; set; }
}

public class EdgeSnapshot
{
    public int Id { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public string FromSide { get; set; } = "right";
    public string ToSide { get; set; } = "left";
    public int FromPosition { get; set; }
    public int ToPosition { get; set; }
}

public class LayoutMetrics
{
    public int OverlapCount { get; set; }
    public int CrossingCount { get; set; }
    public double AverageEdgeLength { get; set; }
    public int UpwardEdgeCount { get; set; }
    public int VerticalAlignments { get; set; }
    public int HorizontalAlignments { get; set; }
}
