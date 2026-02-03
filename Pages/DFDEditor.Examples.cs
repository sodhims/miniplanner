using dfd2wasm.Models;
using System.Net.Http.Json;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    private bool showExamplesMenu = false;

    // Built-in examples
#if GHPAGES_LIMITED
    // Limited set for GitHub Pages publish (flowchart + decision-tree only)
    private static readonly Dictionary<string, (string Name, string Description, Action<DFDEditor> Load)> Examples = new()
    {
        ["login"] = ("Flowchart", "User authentication flowchart", LoadLoginFlow),
        ["decision-tree"] = ("Decision Tree", "Colaco market decision analysis", LoadDecisionTree),
        ["help"] = ("📖 Help Guide", "How to use this editor", OpenHelpFromExamples),
    };
#else
    // Full set of examples
    private static readonly Dictionary<string, (string Name, string Description, Action<DFDEditor> Load)> Examples = new()
    {
        ["context"] = ("Context Diagram", "Simple system with external entities", LoadContextDiagram),
        ["level0"] = ("Level 0 DFD", "Expanded system with data stores", LoadLevel0DFD),
        ["login"] = ("Login Flow", "User authentication process", LoadLoginFlow),
        ["ecommerce"] = ("E-Commerce Flow", "Online shopping process", LoadEcommerceFlow),
        ["software"] = ("Software Architecture", "Typical web app layers", LoadSoftwareArchitecture),
        ["etl"] = ("ETL Pipeline", "Data processing workflow", LoadETLPipeline),
        ["decision-tree"] = ("Decision Tree", "Colaco market decision analysis", LoadDecisionTree),
        ["sketch"] = ("✏️ Sketch Demo", "Freehand drawing example", LoadSketchExample),
        ["sketchpad"] = ("🎨 Sketchpad", "Blank canvas with brush presets", LoadSketchpadExample),
        ["help"] = ("📖 Help Guide", "How to use this editor", OpenHelpFromExamples),
        ["generator"] = ("🔧 Create Example...", "Generate code from current diagram", OpenExampleGenerator),
    };
#endif

    // Custom examples loaded from .examples folder
    private List<CustomExampleInfo> customExamples = new();

    private record CustomExampleInfo(string Key, string Name, string Description);

    // Helper method to create ConnectionPoint easily
    private static ConnectionPoint CP(string side, int position = 0) => new() { Side = side, Position = position };

    private async Task LoadCustomExamples()
    {
        try
        {
            var examples = await HttpClient.GetFromJsonAsync<List<CustomExampleInfo>>("api/examples");
            if (examples != null)
            {
                customExamples = examples;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load custom examples: {ex.Message}");
        }
    }

    private async Task LoadCustomExample(string key)
    {
        try
        {
            var response = await HttpClient.GetFromJsonAsync<CustomExampleData>($"api/examples/{key}");
            if (response != null)
            {
                // Clear existing
                nodes.Clear();
                edges.Clear();
                edgeLabels.Clear();
                freehandStrokes.Clear();
                selectedNodes.Clear();
                selectedEdges.Clear();
                selectedStrokes.Clear();
                nextId = 1;
                nextEdgeId = 1;
                nextStrokeId = 1;

                // Load nodes
                if (response.Nodes != null)
                {
                    foreach (var n in response.Nodes)
                    {
                        var node = new Node
                        {
                            Id = n.Id,
                            Text = n.Text ?? "",
                            X = n.X,
                            Y = n.Y,
                            Width = n.Width,
                            Height = n.Height,
                            StrokeColor = n.StrokeColor ?? "#374151",
                            FillColor = n.FillColor,
                            StrokeWidth = n.StrokeWidth,
                            StrokeDashArray = n.StrokeDashArray,
                            TemplateId = n.TemplateId,
                            TemplateShapeId = n.TemplateShapeId,
                            Icon = n.Icon,
                            CornerRadius = n.CornerRadius.HasValue ? (int)n.CornerRadius.Value : null
                        };

                        // Parse shape
                        if (!string.IsNullOrEmpty(n.Shape) && Enum.TryParse<NodeShape>(n.Shape, out var shape))
                        {
                            node.Shape = shape;
                        }

                        nodes.Add(node);
                        if (n.Id >= nextId) nextId = n.Id + 1;
                    }
                }

                // Load edges
                if (response.Edges != null)
                {
                    foreach (var e in response.Edges)
                    {
                        var edge = new Edge
                        {
                            Id = e.Id,
                            From = e.From,
                            To = e.To,
                            Label = e.Label,
                            StrokeColor = e.StrokeColor,
                            StrokeWidth = e.StrokeWidth,
                            StrokeDashArray = e.StrokeDashArray,
                            IsDoubleLine = e.IsDoubleLine,
                            DoubleLineSpacing = e.DoubleLineSpacing
                        };

                        // Parse arrow direction
                        if (!string.IsNullOrEmpty(e.ArrowDirection) && Enum.TryParse<ArrowDirection>(e.ArrowDirection, out var dir))
                        {
                            edge.ArrowDirection = dir;
                        }

                        // Parse connections
                        if (e.FromConnection != null)
                        {
                            edge.FromConnection = new ConnectionPoint { Side = e.FromConnection.Side ?? "right", Position = e.FromConnection.Position };
                        }
                        if (e.ToConnection != null)
                        {
                            edge.ToConnection = new ConnectionPoint { Side = e.ToConnection.Side ?? "left", Position = e.ToConnection.Position };
                        }

                        edges.Add(edge);
                        if (e.Id >= nextEdgeId) nextEdgeId = e.Id + 1;
                    }
                }

                // Load edge labels
                if (response.EdgeLabels != null)
                {
                    foreach (var l in response.EdgeLabels)
                    {
                        edgeLabels.Add(new EdgeLabel { Id = l.Id, EdgeId = l.EdgeId, Text = l.Text ?? "" });
                    }
                }

                // Load freehand strokes
                if (response.Strokes != null)
                {
                    foreach (var s in response.Strokes)
                    {
                        var stroke = new FreehandStroke
                        {
                            Id = s.Id,
                            StrokeColor = s.StrokeColor ?? "#374151",
                            StrokeWidth = s.StrokeWidth ?? 2,
                            StrokeDashArray = s.StrokeDashArray ?? "",
                            IsComplete = true
                        };

                        if (s.Points != null)
                        {
                            stroke.Points = s.Points.Select(p => new StrokePoint(p.X, p.Y)).ToList();
                        }

                        freehandStrokes.Add(stroke);
                        if (s.Id >= nextStrokeId) nextStrokeId = s.Id + 1;
                    }
                }

                // Recalculate paths
                foreach (var edge in edges)
                {
                    edge.PathData = PathService.GetEdgePath(edge, nodes);
                }

                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load custom example: {ex.Message}");
        }
    }

    // DTO classes for JSON deserialization
    private record CustomExampleData(
        string? Key, string? Name, string? Description,
        List<CustomNodeData>? Nodes, List<CustomEdgeData>? Edges, List<CustomEdgeLabelData>? EdgeLabels,
        List<CustomStrokeData>? Strokes);
    private record CustomNodeData(
        int Id, string? Text, double X, double Y, double Width, double Height,
        string? Shape, string? StrokeColor, string? FillColor, int? StrokeWidth, string? StrokeDashArray,
        string? TemplateId, string? TemplateShapeId, string? Icon, double? CornerRadius);
    private record CustomEdgeData(
        int Id, int From, int To, string? Label, string? StrokeColor, int? StrokeWidth, string? StrokeDashArray,
        bool IsDoubleLine, double DoubleLineSpacing, string? ArrowDirection,
        CustomConnectionData? FromConnection, CustomConnectionData? ToConnection);
    private record CustomConnectionData(string? Side, int Position);
    private record CustomEdgeLabelData(int Id, int EdgeId, string? Text);
    private record CustomStrokeData(int Id, List<CustomStrokePointData>? Points, string? StrokeColor, int? StrokeWidth, string? StrokeDashArray);
    private record CustomStrokePointData(double X, double Y);

    private async void LoadExample(string key)
    {
        // Handle Help separately
        if (key == "help")
        {
            showHelpModal = true;
            showExamplesMenu = false;
            StateHasChanged();
            return;
        }

        // Handle Example Generator separately
        if (key == "generator")
        {
            showExampleGenerator = true;
            showExamplesMenu = false;
            StateHasChanged();
            return;
        }

        // Check built-in examples first
        if (Examples.TryGetValue(key, out var example))
        {
            // Clear existing
            nodes.Clear();
            edges.Clear();
            edgeLabels.Clear();
            selectedNodes.Clear();
            selectedEdges.Clear();
            nextId = 1;
            nextEdgeId = 1;

            // Load the example
            example.Load(this);

            // Recalculate paths
            foreach (var edge in edges)
            {
                edge.PathData = PathService.GetEdgePath(edge, nodes);
            }

            StateHasChanged();
        }
        // Check custom examples
        else if (customExamples.Any(e => e.Key == key))
        {
            await LoadCustomExample(key);
        }

        showExamplesMenu = false;
    }

    private static void OpenHelpFromExamples(DFDEditor editor)
    {
        editor.showHelpModal = true;
        editor.showExamplesMenu = false;
    }

    private static void OpenExampleGenerator(DFDEditor editor)
    {
        editor.showExampleGenerator = true;
        editor.showExamplesMenu = false;
    }

    private static void LoadContextDiagram(DFDEditor editor)
    {
        // External entities
        editor.nodes.Add(new Node { Id = 1, Text = "Customer", X = 100, Y = 200, Width = 100, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#059669", Icon = "user" });
        editor.nodes.Add(new Node { Id = 2, Text = "Admin", X = 100, Y = 350, Width = 100, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#059669", Icon = "users" });
        
        // Central process
        editor.nodes.Add(new Node { Id = 3, Text = "Order\nManagement\nSystem", X = 350, Y = 250, Width = 140, Height = 100, Shape = NodeShape.Ellipse, StrokeColor = "#3b82f6", Icon = "gear" });
        
        // External systems
        editor.nodes.Add(new Node { Id = 4, Text = "Payment\nGateway", X = 600, Y = 150, Width = 100, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "credit-card" });
        editor.nodes.Add(new Node { Id = 5, Text = "Shipping\nProvider", X = 600, Y = 300, Width = 100, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "cart" });
        editor.nodes.Add(new Node { Id = 6, Text = "Email\nService", X = 600, Y = 450, Width = 100, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "email" });
        
        editor.nextId = 7;
        
        // Edges
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 3, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 3, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 3, From = 3, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 4, From = 3, To = 5, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 5, From = 3, To = 6, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.nextEdgeId = 6;
        
        // Labels
        editor.edgeLabels.Add(new EdgeLabel { Id = 1, EdgeId = 1, Text = "Orders" });
        editor.edgeLabels.Add(new EdgeLabel { Id = 2, EdgeId = 2, Text = "Reports" });
        editor.edgeLabels.Add(new EdgeLabel { Id = 3, EdgeId = 3, Text = "Payment" });
        editor.edgeLabels.Add(new EdgeLabel { Id = 4, EdgeId = 4, Text = "Shipment" });
        editor.edgeLabels.Add(new EdgeLabel { Id = 5, EdgeId = 5, Text = "Notifications" });
    }

    private static void LoadLevel0DFD(DFDEditor editor)
    {
        // External entity
        editor.nodes.Add(new Node { Id = 1, Text = "User", X = 100, Y = 250, Width = 100, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#059669", Icon = "user" });
        
        // Processes
        editor.nodes.Add(new Node { Id = 2, Text = "1.0\nProcess\nRequest", X = 280, Y = 150, Width = 120, Height = 80, Shape = NodeShape.Ellipse, StrokeColor = "#3b82f6", Icon = "play" });
        editor.nodes.Add(new Node { Id = 3, Text = "2.0\nValidate\nData", X = 280, Y = 320, Width = 120, Height = 80, Shape = NodeShape.Ellipse, StrokeColor = "#3b82f6", Icon = "check" });
        editor.nodes.Add(new Node { Id = 4, Text = "3.0\nStore\nResult", X = 500, Y = 250, Width = 120, Height = 80, Shape = NodeShape.Ellipse, StrokeColor = "#3b82f6", Icon = "database" });
        
        // Data stores
        editor.nodes.Add(new Node { Id = 5, Text = "D1: User Data", X = 480, Y = 80, Width = 140, Height = 50, Shape = NodeShape.Parallelogram, StrokeColor = "#f59e0b", Icon = "storage" });
        editor.nodes.Add(new Node { Id = 6, Text = "D2: Logs", X = 480, Y = 400, Width = 140, Height = 50, Shape = NodeShape.Parallelogram, StrokeColor = "#f59e0b", Icon = "file" });
        
        editor.nextId = 7;
        
        // Edges
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 2, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 2, From = 1, To = 3, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 3, From = 2, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 4, From = 3, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 5, From = 2, To = 5, FromConnection = CP("top"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 6, From = 4, To = 6, FromConnection = CP("bottom"), ToConnection = CP("left") });
        editor.nextEdgeId = 7;
    }

    private static void LoadLoginFlow(DFDEditor editor)
    {
        // Start/End
        editor.nodes.Add(new Node { Id = 1, Text = "Start", X = 300, Y = 50, Width = 80, Height = 40, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "play", TemplateId = "flowchart", TemplateShapeId = "terminator" });
        editor.nodes.Add(new Node { Id = 8, Text = "End", X = 300, Y = 650, Width = 80, Height = 40, Shape = NodeShape.Ellipse, StrokeColor = "#dc2626", Icon = "stop", TemplateId = "flowchart", TemplateShapeId = "terminator" });
        
        // Process steps
        editor.nodes.Add(new Node { Id = 2, Text = "Display\nLogin Form", X = 270, Y = 120, Width = 140, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "computer", TemplateId = "flowchart", TemplateShapeId = "process" });
        editor.nodes.Add(new Node { Id = 3, Text = "Enter\nCredentials", X = 270, Y = 210, Width = 140, Height = 60, Shape = NodeShape.Parallelogram, StrokeColor = "#8b5cf6", Icon = "user", TemplateId = "flowchart", TemplateShapeId = "data" });
        editor.nodes.Add(new Node { Id = 4, Text = "Valid?", X = 290, Y = 310, Width = 100, Height = 60, Shape = NodeShape.Diamond, StrokeColor = "#f59e0b", Icon = "key", TemplateId = "flowchart", TemplateShapeId = "decision" });
        
        // Branches
        editor.nodes.Add(new Node { Id = 5, Text = "Show\nError", X = 480, Y = 310, Width = 120, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#dc2626", Icon = "error" });
        editor.nodes.Add(new Node { Id = 6, Text = "Create\nSession", X = 270, Y = 420, Width = 140, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "shield" });
        editor.nodes.Add(new Node { Id = 7, Text = "Redirect to\nDashboard", X = 270, Y = 530, Width = 140, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "home" });
        
        editor.nextId = 9;
        
        // Edges
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 2, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 3, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 3, From = 3, To = 4, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 4, From = 4, To = 5, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 5, From = 4, To = 6, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 6, From = 5, To = 2, FromConnection = CP("top"), ToConnection = CP("right") });
        editor.edges.Add(new Edge { Id = 7, From = 6, To = 7, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 8, From = 7, To = 8, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.nextEdgeId = 9;
        
        // Labels
        editor.edgeLabels.Add(new EdgeLabel { Id = 1, EdgeId = 4, Text = "No" });
        editor.edgeLabels.Add(new EdgeLabel { Id = 2, EdgeId = 5, Text = "Yes" });
    }

    private static void LoadEcommerceFlow(DFDEditor editor)
    {
        // Customer journey
        editor.nodes.Add(new Node { Id = 1, Text = "Browse\nProducts", X = 100, Y = 150, Width = 120, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "search" });
        editor.nodes.Add(new Node { Id = 2, Text = "Add to\nCart", X = 280, Y = 150, Width = 120, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "cart" });
        editor.nodes.Add(new Node { Id = 3, Text = "Checkout", X = 460, Y = 150, Width = 120, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "credit-card" });
        editor.nodes.Add(new Node { Id = 4, Text = "Payment", X = 640, Y = 150, Width = 120, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "lock" });
        
        // Backend
        editor.nodes.Add(new Node { Id = 5, Text = "Product\nCatalog", X = 100, Y = 300, Width = 120, Height = 50, Shape = NodeShape.Cylinder, StrokeColor = "#f59e0b", Icon = "database" });
        editor.nodes.Add(new Node { Id = 6, Text = "Cart\nService", X = 280, Y = 300, Width = 120, Height = 50, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "gear" });
        editor.nodes.Add(new Node { Id = 7, Text = "Order\nService", X = 460, Y = 300, Width = 120, Height = 50, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "gear" });
        editor.nodes.Add(new Node { Id = 8, Text = "Payment\nGateway", X = 640, Y = 300, Width = 120, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#dc2626", Icon = "shield" });
        
        // Data stores
        editor.nodes.Add(new Node { Id = 9, Text = "Orders DB", X = 460, Y = 420, Width = 120, Height = 50, Shape = NodeShape.Cylinder, StrokeColor = "#f59e0b", Icon = "database" });
        editor.nodes.Add(new Node { Id = 10, Text = "Notify\nCustomer", X = 640, Y = 420, Width = 120, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "email" });
        
        editor.nextId = 11;
        
        // Edges  
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 2, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 3, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 3, From = 3, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 4, From = 1, To = 5, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 5, From = 2, To = 6, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 6, From = 3, To = 7, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 7, From = 4, To = 8, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 8, From = 7, To = 9, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 9, From = 8, To = 10, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.nextEdgeId = 10;
    }

    private static void LoadSoftwareArchitecture(DFDEditor editor)
    {
        // Presentation layer
        editor.nodes.Add(new Node { Id = 1, Text = "Web Browser", X = 100, Y = 80, Width = 120, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "computer" });
        editor.nodes.Add(new Node { Id = 2, Text = "Mobile App", X = 260, Y = 80, Width = 120, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "mobile" });
        editor.nodes.Add(new Node { Id = 3, Text = "API Client", X = 420, Y = 80, Width = 120, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", Icon = "code" });
        
        // API Gateway
        editor.nodes.Add(new Node { Id = 4, Text = "API Gateway\n/ Load Balancer", X = 220, Y = 180, Width = 160, Height = 60, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "cloud" });
        
        // Services
        editor.nodes.Add(new Node { Id = 5, Text = "Auth\nService", X = 80, Y = 300, Width = 100, Height = 60, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "lock" });
        editor.nodes.Add(new Node { Id = 6, Text = "User\nService", X = 220, Y = 300, Width = 100, Height = 60, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "user" });
        editor.nodes.Add(new Node { Id = 7, Text = "Order\nService", X = 360, Y = 300, Width = 100, Height = 60, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "cart" });
        editor.nodes.Add(new Node { Id = 8, Text = "Email\nService", X = 500, Y = 300, Width = 100, Height = 60, Shape = NodeShape.Ellipse, StrokeColor = "#059669", Icon = "email" });
        
        // Databases
        editor.nodes.Add(new Node { Id = 9, Text = "Users DB", X = 150, Y = 430, Width = 100, Height = 50, Shape = NodeShape.Cylinder, StrokeColor = "#f59e0b", Icon = "database" });
        editor.nodes.Add(new Node { Id = 10, Text = "Orders DB", X = 330, Y = 430, Width = 100, Height = 50, Shape = NodeShape.Cylinder, StrokeColor = "#f59e0b", Icon = "database" });
        editor.nodes.Add(new Node { Id = 11, Text = "Cache", X = 500, Y = 430, Width = 100, Height = 50, Shape = NodeShape.Parallelogram, StrokeColor = "#dc2626", Icon = "storage" });
        
        editor.nextId = 12;
        
        // Edges
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 4, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 4, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 3, From = 3, To = 4, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 4, From = 4, To = 5, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 5, From = 4, To = 6, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 6, From = 4, To = 7, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 7, From = 4, To = 8, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 8, From = 5, To = 9, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 9, From = 6, To = 9, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 10, From = 7, To = 10, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 11, From = 7, To = 11, FromConnection = CP("bottom"), ToConnection = CP("left") });
        editor.nextEdgeId = 12;
    }

    private static void LoadETLPipeline(DFDEditor editor)
    {
        // Sources
        editor.nodes.Add(new Node { Id = 1, Text = "CSV Files", X = 80, Y = 100, Width = 100, Height = 50, Shape = NodeShape.Parallelogram, StrokeColor = "#059669", Icon = "file" });
        editor.nodes.Add(new Node { Id = 2, Text = "API Data", X = 80, Y = 180, Width = 100, Height = 50, Shape = NodeShape.Parallelogram, StrokeColor = "#059669", Icon = "api" });
        editor.nodes.Add(new Node { Id = 3, Text = "Database", X = 80, Y = 260, Width = 100, Height = 50, Shape = NodeShape.Cylinder, StrokeColor = "#059669", Icon = "database" });
        
        // Extract
        editor.nodes.Add(new Node { Id = 4, Text = "Extract", X = 250, Y = 170, Width = 100, Height = 60, Shape = NodeShape.Ellipse, StrokeColor = "#3b82f6", Icon = "cloud-download" });
        
        // Transform
        editor.nodes.Add(new Node { Id = 5, Text = "Clean\nData", X = 400, Y = 100, Width = 100, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "refresh" });
        editor.nodes.Add(new Node { Id = 6, Text = "Validate", X = 400, Y = 180, Width = 100, Height = 50, Shape = NodeShape.Diamond, StrokeColor = "#8b5cf6", Icon = "check" });
        editor.nodes.Add(new Node { Id = 7, Text = "Transform", X = 400, Y = 270, Width = 100, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#8b5cf6", Icon = "gear" });
        
        // Load
        editor.nodes.Add(new Node { Id = 8, Text = "Load", X = 560, Y = 170, Width = 100, Height = 60, Shape = NodeShape.Ellipse, StrokeColor = "#f59e0b", Icon = "cloud-upload" });
        
        // Destinations
        editor.nodes.Add(new Node { Id = 9, Text = "Data\nWarehouse", X = 700, Y = 120, Width = 110, Height = 50, Shape = NodeShape.Cylinder, StrokeColor = "#dc2626", Icon = "database" });
        editor.nodes.Add(new Node { Id = 10, Text = "Analytics\nDashboard", X = 700, Y = 220, Width = 110, Height = 50, Shape = NodeShape.Rectangle, StrokeColor = "#dc2626", Icon = "computer" });
        
        editor.nextId = 11;
        
        // Edges
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 3, From = 3, To = 4, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 4, From = 4, To = 5, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 5, From = 5, To = 6, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 6, From = 6, To = 7, FromConnection = CP("bottom"), ToConnection = CP("top") });
        editor.edges.Add(new Edge { Id = 7, From = 7, To = 8, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 8, From = 8, To = 9, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.edges.Add(new Edge { Id = 9, From = 8, To = 10, FromConnection = CP("right"), ToConnection = CP("left") });
        editor.nextEdgeId = 10;
    }

    private static void LoadDecisionTree(DFDEditor editor)
    {
        // Helper function
        ConnectionPoint CP(string side, int pos) => new ConnectionPoint { Side = side, Position = pos };

        // Nodes - Decision (square), Chance (circle), Terminal (payoff rectangle)
        // Root decision node
        editor.nodes.Add(new Node { Id = 1, Text = "$270,000", X = 50, Y = 350, Width = 70, Height = 50,
            TemplateId = "decision-tree", TemplateShapeId = "decision", StrokeColor = "#475569", FillColor = "#bfdbfe" });

        // Upper branch: Test market
        editor.nodes.Add(new Node { Id = 2, Text = "$264,000", X = 200, Y = 200, Width = 60, Height = 60,
            TemplateId = "decision-tree", TemplateShapeId = "chance", StrokeColor = "#475569", FillColor = "#99f6e4" });
        editor.nodes.Add(new Node { Id = 3, Text = "$360,000", X = 350, Y = 100, Width = 70, Height = 50,
            TemplateId = "decision-tree", TemplateShapeId = "decision", StrokeColor = "#475569", FillColor = "#bfdbfe" });
        editor.nodes.Add(new Node { Id = 4, Text = "$150,000 – $30,000 = $120,000", X = 650, Y = 50, Width = 200, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });
        editor.nodes.Add(new Node { Id = 5, Text = "$360,000", X = 500, Y = 130, Width = 60, Height = 60,
            TemplateId = "decision-tree", TemplateShapeId = "chance", StrokeColor = "#475569", FillColor = "#99f6e4" });
        editor.nodes.Add(new Node { Id = 6, Text = "$150,000 – $30,000 + $300,000 = $420,000", X = 700, Y = 100, Width = 250, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });
        editor.nodes.Add(new Node { Id = 7, Text = "$150,000 – $30,000 – $100,000 = $20,000", X = 700, Y = 160, Width = 250, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });

        // Local failure branch
        editor.nodes.Add(new Node { Id = 8, Text = "$120,000", X = 350, Y = 280, Width = 70, Height = 50,
            TemplateId = "decision-tree", TemplateShapeId = "decision", StrokeColor = "#475569", FillColor = "#bfdbfe" });
        editor.nodes.Add(new Node { Id = 9, Text = "$150,000 – $30,000 = $120,000", X = 650, Y = 230, Width = 200, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });
        editor.nodes.Add(new Node { Id = 10, Text = "$60,000", X = 500, Y = 340, Width = 60, Height = 60,
            TemplateId = "decision-tree", TemplateShapeId = "chance", StrokeColor = "#475569", FillColor = "#99f6e4" });
        editor.nodes.Add(new Node { Id = 11, Text = "$150,000 – $30,000 + $300,000 = $420,000", X = 700, Y = 300, Width = 250, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });
        editor.nodes.Add(new Node { Id = 12, Text = "$150,000 – $30,000 – $100,000 = $20,000", X = 700, Y = 380, Width = 250, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });

        // Lower branch: Don't test market
        editor.nodes.Add(new Node { Id = 13, Text = "$270,000", X = 350, Y = 520, Width = 70, Height = 50,
            TemplateId = "decision-tree", TemplateShapeId = "decision", StrokeColor = "#475569", FillColor = "#bfdbfe" });
        editor.nodes.Add(new Node { Id = 14, Text = "$270,000", X = 500, Y = 480, Width = 60, Height = 60,
            TemplateId = "decision-tree", TemplateShapeId = "chance", StrokeColor = "#475569", FillColor = "#99f6e4" });
        editor.nodes.Add(new Node { Id = 15, Text = "$150,000 + $300,000 = $450,000", X = 700, Y = 450, Width = 220, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });
        editor.nodes.Add(new Node { Id = 16, Text = "$150,000 – $100,000 = $50,000", X = 700, Y = 510, Width = 200, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });
        editor.nodes.Add(new Node { Id = 17, Text = "$150,000", X = 650, Y = 580, Width = 100, Height = 30,
            TemplateId = "decision-tree", TemplateShapeId = "terminal", StrokeColor = "#475569", FillColor = "#f0fdf4" });

        editor.nextId = 18;

        // Edges - Decision branches (IsDoubleLine) are red, probability branches are black
        // Upper branch: Test market
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 2, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Test market" });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 3, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".60 Local success" });
        editor.edges.Add(new Edge { Id = 3, From = 2, To = 8, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".40 Local failure" });
        editor.edges.Add(new Edge { Id = 4, From = 3, To = 4, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Don't market nationally" });
        editor.edges.Add(new Edge { Id = 5, From = 3, To = 5, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Market nationally" });
        editor.edges.Add(new Edge { Id = 6, From = 5, To = 6, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".85 National success" });
        editor.edges.Add(new Edge { Id = 7, From = 5, To = 7, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".15 National failure" });
        editor.edges.Add(new Edge { Id = 8, From = 8, To = 9, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Don't market nationally" });
        editor.edges.Add(new Edge { Id = 9, From = 8, To = 10, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Market nationally" });
        editor.edges.Add(new Edge { Id = 10, From = 10, To = 11, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".10 National success" });
        editor.edges.Add(new Edge { Id = 11, From = 10, To = 12, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".90 National failure" });
        // Lower branch: Don't test market
        editor.edges.Add(new Edge { Id = 12, From = 1, To = 13, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Don't test market" });
        editor.edges.Add(new Edge { Id = 13, From = 13, To = 14, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Market nationally" });
        editor.edges.Add(new Edge { Id = 14, From = 14, To = 15, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".55 National success" });
        editor.edges.Add(new Edge { Id = 15, From = 14, To = 16, FromConnection = CP("right", 0), ToConnection = CP("left", 0), Label = ".45 National failure" });
        editor.edges.Add(new Edge { Id = 16, From = 13, To = 17, FromConnection = CP("right", 0), ToConnection = CP("left", 0), IsDoubleLine = true, StrokeColor = "#dc2626", StrokeWidth = 2, Label = "Don't market nationally" });
        editor.nextEdgeId = 17;
    }

    private static void LoadSketchExample(DFDEditor editor)
    {
        // Clear existing content
        editor.nodes.Clear();
        editor.edges.Clear();
        editor.edgeLabels.Clear();
        editor.freehandStrokes.Clear();
        editor.selectedNodes.Clear();
        editor.selectedEdges.Clear();
        editor.selectedStrokes.Clear();

        // Add a few nodes to show sketching alongside structured elements
        editor.nodes.Add(new Node { Id = 1, Text = "Brainstorm\nIdeas", X = 100, Y = 100, Width = 100, Height = 60,
            Shape = NodeShape.Rectangle, StrokeColor = "#3b82f6", FillColor = "#dbeafe", CornerRadius = 8 });
        editor.nodes.Add(new Node { Id = 2, Text = "Sketch\nConcepts", X = 300, Y = 100, Width = 100, Height = 60,
            Shape = NodeShape.Rectangle, StrokeColor = "#10b981", FillColor = "#d1fae5", CornerRadius = 8 });
        editor.nodes.Add(new Node { Id = 3, Text = "Refine\nDesign", X = 500, Y = 100, Width = 100, Height = 60,
            Shape = NodeShape.Rectangle, StrokeColor = "#f59e0b", FillColor = "#fef3c7", CornerRadius = 8 });

        editor.nextId = 4;

        // Connect the nodes
        ConnectionPoint CP(string side, int pos) => new ConnectionPoint { Side = side, Position = pos };
        editor.edges.Add(new Edge { Id = 1, From = 1, To = 2, FromConnection = CP("right", 0), ToConnection = CP("left", 0), ArrowDirection = ArrowDirection.End });
        editor.edges.Add(new Edge { Id = 2, From = 2, To = 3, FromConnection = CP("right", 0), ToConnection = CP("left", 0), ArrowDirection = ArrowDirection.End });

        editor.nextEdgeId = 3;

        // Add some freehand strokes to demonstrate sketching
        // A simple star/asterisk shape
        var stroke1 = new FreehandStroke
        {
            Id = 1,
            StrokeColor = "#ef4444",
            StrokeWidth = 3,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(150, 200), new(155, 220), new(160, 240), new(170, 260), new(180, 280),
                new(175, 260), new(185, 240), new(200, 230), new(220, 225)
            }
        };
        editor.freehandStrokes.Add(stroke1);

        // A wavy underline
        var stroke2 = new FreehandStroke
        {
            Id = 2,
            StrokeColor = "#8b5cf6",
            StrokeWidth = 2,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(100, 180), new(120, 185), new(140, 178), new(160, 185),
                new(180, 178), new(200, 185), new(220, 178), new(240, 185)
            }
        };
        editor.freehandStrokes.Add(stroke2);

        // An arrow pointing down
        var stroke3 = new FreehandStroke
        {
            Id = 3,
            StrokeColor = "#10b981",
            StrokeWidth = 4,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(350, 180), new(350, 200), new(350, 220), new(350, 240),
                new(350, 260), new(340, 250), new(350, 260), new(360, 250)
            }
        };
        editor.freehandStrokes.Add(stroke3);

        // A circle/loop annotation
        var stroke4 = new FreehandStroke
        {
            Id = 4,
            StrokeColor = "#f59e0b",
            StrokeWidth = 2,
            StrokeDashArray = "5,5",
            IsComplete = true,
            Points = GenerateCirclePoints(550, 200, 30, 24)
        };
        editor.freehandStrokes.Add(stroke4);

        // A checkmark
        var stroke5 = new FreehandStroke
        {
            Id = 5,
            StrokeColor = "#22c55e",
            StrokeWidth = 4,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(500, 220), new(520, 250), new(560, 190)
            }
        };
        editor.freehandStrokes.Add(stroke5);

        editor.nextStrokeId = 6;

        // Switch to Draw mode so user can start sketching immediately
        editor.mode = EditorMode.Draw;
    }

    // Helper to generate circle points for sketch demo
    private static List<StrokePoint> GenerateCirclePoints(double cx, double cy, double radius, int segments)
    {
        var points = new List<StrokePoint>();
        for (int i = 0; i <= segments; i++)
        {
            var angle = 2 * Math.PI * i / segments;
            points.Add(new StrokePoint(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
        }
        return points;
    }

    private static void LoadSketchpadExample(DFDEditor editor)
    {
        // Clear everything for a fresh sketchpad
        editor.nodes.Clear();
        editor.edges.Clear();
        editor.edgeLabels.Clear();
        editor.freehandStrokes.Clear();
        editor.drawingShapes.Clear();
        editor.selectedNodes.Clear();
        editor.selectedEdges.Clear();
        editor.selectedStrokes.Clear();
        editor.selectedDrawingShapes.Clear();

        // Add demo strokes showing different brush styles
        // 1. Fine pen sample
        var finePenStroke = new FreehandStroke
        {
            Id = 1,
            StrokeColor = "#374151",
            StrokeWidth = 1,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(50, 50), new(55, 52), new(60, 48), new(65, 50),
                new(70, 45), new(80, 50), new(90, 48), new(100, 52), new(110, 50)
            }
        };
        editor.freehandStrokes.Add(finePenStroke);

        // 2. Marker stroke (thick blue)
        var markerStroke = new FreehandStroke
        {
            Id = 2,
            StrokeColor = "#3b82f6",
            StrokeWidth = 6,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(50, 100), new(70, 95), new(90, 105), new(110, 100),
                new(130, 95), new(150, 100)
            }
        };
        editor.freehandStrokes.Add(markerStroke);

        // 3. Highlighter stroke (wide yellow, semi-transparent effect)
        var highlightStroke = new FreehandStroke
        {
            Id = 3,
            StrokeColor = "#fbbf24",
            StrokeWidth = 16,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(50, 150), new(80, 150), new(110, 150), new(140, 150), new(170, 150)
            }
        };
        editor.freehandStrokes.Add(highlightStroke);

        // 4. Dashed line stroke
        var dashedStroke = new FreehandStroke
        {
            Id = 4,
            StrokeColor = "#374151",
            StrokeWidth = 2,
            StrokeDashArray = "8,4",
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(50, 200), new(80, 200), new(110, 200), new(140, 200), new(170, 200)
            }
        };
        editor.freehandStrokes.Add(dashedStroke);

        // 5. Chain stroke demo - connected line segments
        var chainStroke1 = new FreehandStroke
        {
            Id = 5,
            StrokeColor = "#22c55e",
            StrokeWidth = 3,
            IsComplete = true,
            Points = new List<StrokePoint>
            {
                new(250, 50), new(280, 80), new(310, 50), new(340, 80), new(370, 50)
            }
        };
        editor.freehandStrokes.Add(chainStroke1);

        // Add some demo shapes
        // Rectangle
        editor.drawingShapes.Add(new RectShape
        {
            Id = 1,
            X = 250, Y = 120, Width = 100, Height = 60,
            StrokeColor = "#ef4444", StrokeWidth = 2, FillColor = "#fee2e2",
            IsComplete = true
        });

        // Ellipse
        editor.drawingShapes.Add(new EllipseShape
        {
            Id = 2,
            Cx = 450, Cy = 80, Rx = 50, Ry = 30,
            StrokeColor = "#8b5cf6", StrokeWidth = 2, FillColor = "#ede9fe",
            IsComplete = true
        });

        // Line
        editor.drawingShapes.Add(new LineShape
        {
            Id = 3,
            X1 = 400, Y1 = 150, X2 = 500, Y2 = 180,
            StrokeColor = "#0891b2", StrokeWidth = 3,
            IsComplete = true
        });

        // Arrow
        editor.drawingShapes.Add(new ArrowShape
        {
            Id = 4,
            X1 = 250, Y1 = 220, X2 = 350, Y2 = 220,
            StrokeColor = "#f97316", StrokeWidth = 2, HeadSize = 12,
            IsComplete = true
        });

        // Triangle
        editor.drawingShapes.Add(new TriangleShape
        {
            Id = 5,
            X1 = 450, Y1 = 200, X2 = 400, Y2 = 260, X3 = 500, Y3 = 260,
            StrokeColor = "#14b8a6", StrokeWidth = 2, FillColor = "#ccfbf1",
            IsComplete = true
        });

        editor.nextStrokeId = 6;
        editor.nextShapeId = 6;

        // Set up for immediate sketching
        editor.mode = EditorMode.Draw;
        editor.currentDrawTool = DrawTool.Pencil;
        editor.chainStrokeMode = true;  // Enable chain mode by default for sketchpad
        editor.ApplyPreset(StrokePresets.MediumPen);  // Start with medium pen
    }
}
