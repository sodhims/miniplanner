using Microsoft.JSInterop;
using dfd2wasm.Models;

namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // JSON serialization options - case insensitive to handle both PascalCase and camelCase
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    #region Save/Export

    private void SaveDiagram()
    {
        try
        {
            var diagramData = new
            {
                nodes = nodes,
                edges = edges,
                edgeLabels = edgeLabels,
                strokes = freehandStrokes.Select(s => new
                {
                    id = s.Id,
                    points = s.Points.Select(p => new { x = p.X, y = p.Y }).ToList(),
                    strokeColor = s.StrokeColor,
                    strokeWidth = s.StrokeWidth,
                    strokeDashArray = s.StrokeDashArray
                }).ToList(),
                nextId = nextId,
                nextEdgeId = nextEdgeId,
                nextLabelId = nextLabelId,
                nextStrokeId = nextStrokeId
            };

            var json = System.Text.Json.JsonSerializer.Serialize(diagramData, JsonOptions);

            exportedContent = json;
            exportDialogTitle = "Save Diagram (JSON)";
            exportDialogDescription = "Copy this JSON to save your diagram. You can load it later using the Import button.";
            showExportDialog = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving diagram: {ex.Message}");
        }
    }

    private void ExportDiagram()
    {
        try
        {
            var mermaid = new System.Text.StringBuilder();
            mermaid.AppendLine("flowchart TD");

            foreach (var node in nodes)
            {
                var nodeId = $"N{node.Id}";
                var label = string.IsNullOrEmpty(node.Text) ? nodeId : node.Text;
                
                var formattedNode = node.Shape switch
                {
                    NodeShape.Ellipse => $"{nodeId}(({label}))",
                    NodeShape.Diamond => $"{nodeId}{{{{{label}}}}}",
                    NodeShape.Parallelogram => $"{nodeId}[/{label}/]",
                    NodeShape.Cylinder => $"{nodeId}[({label})]",
                    _ => $"{nodeId}[{label}]"
                };
                
                mermaid.AppendLine($"    {formattedNode}");
            }

            foreach (var edge in edges)
            {
                var fromId = $"N{edge.From}";
                var toId = $"N{edge.To}";
                var label = edgeLabels.FirstOrDefault(l => l.EdgeId == edge.Id)?.Text;
                
                if (!string.IsNullOrEmpty(label))
                {
                    mermaid.AppendLine($"    {fromId} -->|{label}| {toId}");
                }
                else
                {
                    mermaid.AppendLine($"    {fromId} --> {toId}");
                }
            }

            exportedContent = mermaid.ToString();
            exportDialogTitle = "Export as Mermaid";
            exportDialogDescription = "Copy this Mermaid code to use in documentation, GitHub, or other Mermaid-compatible tools.";
            showExportDialog = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting diagram: {ex.Message}");
        }
    }

    private async Task CopyToClipboard()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", exportedContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not copy: {ex.Message}");
        }
    }

    /// <summary>
    /// Export QMaker queueing network configuration as clean JSON
    /// </summary>
    private void ExportQMakerConfig()
    {
        try
        {
            var qmakerNodes = nodes.Where(n => n.TemplateId == "qmaker").ToList();
            var qmakerEdges = edges.Where(e =>
                qmakerNodes.Any(n => n.Id == e.From) &&
                qmakerNodes.Any(n => n.Id == e.To)).ToList();

            // Build network configuration
            var network = new
            {
                name = "Queueing Network",
                description = $"Generated from DFD Editor on {DateTime.Now:yyyy-MM-dd HH:mm}",
                nodes = qmakerNodes.Select(n => BuildQMakerNodeConfig(n)).ToList(),
                connections = qmakerEdges.Select(e => new
                {
                    from = e.From,
                    to = e.To,
                    fromTerminal = GetTerminalIndex(e, true),
                    toTerminal = GetTerminalIndex(e, false)
                }).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(network, JsonOptions);

            exportedContent = json;
            exportDialogTitle = "QMaker Network Configuration";
            exportDialogDescription = "Export queueing network configuration for simulation or analysis.";
            showExportDialog = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting QMaker config: {ex.Message}");
        }
    }

    private object BuildQMakerNodeConfig(Node node)
    {
        var baseConfig = new Dictionary<string, object?>
        {
            ["id"] = node.Id,
            ["name"] = node.Text,
            ["type"] = node.TemplateShapeId ?? "queue"
        };

        // Add type-specific properties
        switch (node.TemplateShapeId)
        {
            case "source":
                baseConfig["arrivalRate"] = node.QueueArrivalRate ?? 1.0;
                baseConfig["arrivalDistribution"] = GetDistributionConfig(node.QueueArrivalDistribution, node.ArrivalDistribution);
                break;

            case "sink":
                // Sink has no special properties
                break;

            case "router":
                baseConfig["routingProbability"] = node.QueueRoutingProbability ?? 0.5;
                baseConfig["outputCount"] = node.OutputTerminalCount;
                if (node.RoutingProbabilities != null && node.RoutingProbabilities.Count > 0)
                    baseConfig["routingProbabilities"] = node.RoutingProbabilities;
                break;

            case "splitter":
                baseConfig["outputCount"] = node.OutputTerminalCount;
                baseConfig["routingMode"] = node.QueueDiscipline == QueueDiscipline.Random ? "probabilistic" : "equal";
                if (node.RoutingProbabilities != null && node.RoutingProbabilities.Count > 0)
                    baseConfig["routingProbabilities"] = node.RoutingProbabilities;
                break;

            case "collector":
                baseConfig["inputCount"] = node.InputTerminalCount;
                break;

            case "fork":
            case "join":
                // Legacy fork/join - map to splitter/collector
                baseConfig["type"] = node.TemplateShapeId == "fork" ? "splitter" : "collector";
                break;

            default:
                // Queue-based nodes (queue-server, mm1, mmc, buffer, server, delay)
                baseConfig["serviceRate"] = node.QueueServiceRate ?? 1.0;
                baseConfig["serviceDistribution"] = GetDistributionConfig(node.QueueServiceDistribution, node.ServiceDistribution);
                baseConfig["discipline"] = node.QueueDiscipline.ToString();

                if (node.QueueCapacity.HasValue && node.QueueCapacity > 0)
                    baseConfig["capacity"] = node.QueueCapacity.Value;
                else
                    baseConfig["capacity"] = "infinite";

                if (node.TemplateShapeId == "mmc" || node.TemplateShapeId == "delay")
                    baseConfig["servers"] = node.QueueServerCount;

                // Generate Kendall notation
                baseConfig["kendall"] = GetKendallNotation(node);
                break;
        }

        return baseConfig;
    }

    private object GetDistributionConfig(QueueDistribution? simpleDistribution, DistributionConfig? detailedConfig)
    {
        if (detailedConfig != null)
        {
            return new
            {
                type = detailedConfig.Type.ToString(),
                code = detailedConfig.GetKendallCode(),
                description = detailedConfig.GetDescription(),
                param1 = detailedConfig.Param1,
                param2 = detailedConfig.Param2,
                param3 = detailedConfig.Param3,
                mean = detailedConfig.GetMean()
            };
        }

        // Fall back to simple distribution
        var dist = simpleDistribution ?? QueueDistribution.Exponential;
        return new
        {
            type = dist.ToString(),
            code = dist switch
            {
                QueueDistribution.Exponential => "M",
                QueueDistribution.Deterministic => "D",
                QueueDistribution.General => "G",
                QueueDistribution.Erlang => "Ek",
                _ => "G"
            }
        };
    }

    private int GetTerminalIndex(Edge edge, bool isFrom)
    {
        // Parse terminal index from terminal string (e.g., "output:1" -> 1)
        var terminal = isFrom ? edge.FromTerminal : edge.ToTerminal;
        if (string.IsNullOrEmpty(terminal)) return 0;

        var parts = terminal.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[1], out var index))
            return index;

        return 0;
    }

    #endregion

    #region SVG Export

    private async Task ExportToSVG()
    {
        try
        {
            var svgContent = ExportService.ExportToSVG(nodes, edges, edgeLabels,
                canvasBackground, swimlaneCount, swimlaneLabels,
                columnCount, columnLabels);

            await JSRuntime.InvokeVoidAsync("downloadFile", "diagram.svg", svgContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting to SVG: {ex.Message}");
        }
    }

    #endregion

    #region Print/PDF

    private void TogglePrintAreaSelection()
    {
        if (isPrintAreaSelection)
        {
            isPrintAreaSelection = false;
            isSelecting = false;
            selectionStart = null;
        }
        else
        {
            isPrintAreaSelection = true;
            printArea = null;
        }
    }

    private async Task PrintAll()
    {
        try
        {
            var svgContent = ExportService.ExportToSVG(nodes, edges, edgeLabels, 
                canvasBackground, swimlaneCount, swimlaneLabels, 
                columnCount, columnLabels);
            
            await JSRuntime.InvokeVoidAsync("printAllHighResolution", svgContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error printing all: {ex.Message}");
        }
    }

    private async Task ExportToPDF()
    {
        try
        {
            var svgContent = ExportService.ExportToSVG(nodes, edges, edgeLabels, 
                canvasBackground, swimlaneCount, swimlaneLabels, 
                columnCount, columnLabels);
            
            if (printArea.HasValue)
            {
                await JSRuntime.InvokeVoidAsync("downloadPDFViaPrint", svgContent, 
                    printArea.Value.X, printArea.Value.Y, printArea.Value.Width, printArea.Value.Height);
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("downloadPDFViaPrint", svgContent, null, null, null, null);
            }
            
            if (printArea.HasValue)
            {
                printArea = null;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting to PDF: {ex.Message}");
        }
    }

    #endregion

    #region Load/Import

    private void LoadDiagram()
    {
        try
        {
            loadErrorMessage = "";

            if (string.IsNullOrWhiteSpace(loadDiagramJson))
            {
                loadErrorMessage = "Please paste diagram content";
                return;
            }

            var content = loadDiagramJson.Trim();
            var detectedFormat = importFormat;

            if (detectedFormat == "auto")
            {
                if (content.StartsWith("{") || content.StartsWith("["))
                    detectedFormat = "json";
                else if (content.Contains("flowchart") || content.Contains("graph TD") || content.Contains("graph LR"))
                    detectedFormat = "mermaid";
                else if (content.StartsWith("digraph") || content.StartsWith("graph"))
                    detectedFormat = "dot";
                else
                    detectedFormat = "json";
            }

            UndoService.SaveState(nodes, edges, edgeLabels, freehandStrokes);

            switch (detectedFormat)
            {
                case "json":
                    LoadFromJson(content);
                    break;
                case "mermaid":
                    LoadFromMermaid(content);
                    break;
                case "dot":
                    LoadFromDot(content);
                    break;
                default:
                    throw new Exception($"Unsupported format: {detectedFormat}");
            }

            showLoadDialog = false;
            loadDiagramJson = "";
            selectedNodes.Clear();
            selectedEdges.Clear();
            selectedLabels.Clear();
            selectedStrokes.Clear();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            loadErrorMessage = $"Error loading diagram: {ex.Message}";
        }
    }

    private void LoadFromJson(string json)
    {
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        freehandStrokes.Clear();

        if (root.TryGetProperty("nodes", out var nodesJson))
        {
            nodes = System.Text.Json.JsonSerializer.Deserialize<List<Node>>(nodesJson.GetRawText(), JsonOptions) ?? new();
        }

        if (root.TryGetProperty("edges", out var edgesJson))
        {
            edges = System.Text.Json.JsonSerializer.Deserialize<List<Edge>>(edgesJson.GetRawText(), JsonOptions) ?? new();
        }

        if (root.TryGetProperty("edgeLabels", out var labelsJson))
        {
            edgeLabels = System.Text.Json.JsonSerializer.Deserialize<List<EdgeLabel>>(labelsJson.GetRawText(), JsonOptions) ?? new();
        }

        // Load freehand strokes if present
        if (root.TryGetProperty("strokes", out var strokesJson))
        {
            var strokeDataList = System.Text.Json.JsonSerializer.Deserialize<List<StrokeJsonData>>(strokesJson.GetRawText());
            if (strokeDataList != null)
            {
                foreach (var s in strokeDataList)
                {
                    var stroke = new FreehandStroke
                    {
                        Id = s.id,
                        StrokeColor = s.strokeColor ?? "#374151",
                        StrokeWidth = s.strokeWidth ?? 2,
                        StrokeDashArray = s.strokeDashArray ?? "",
                        IsComplete = true
                    };

                    if (s.points != null)
                    {
                        stroke.Points = s.points.Select(p => new StrokePoint(p.x, p.y)).ToList();
                    }

                    freehandStrokes.Add(stroke);
                }
            }
        }

        if (root.TryGetProperty("nextId", out var nextIdJson))
            nextId = nextIdJson.GetInt32();
        if (root.TryGetProperty("nextEdgeId", out var nextEdgeIdJson))
            nextEdgeId = nextEdgeIdJson.GetInt32();
        if (root.TryGetProperty("nextLabelId", out var nextLabelIdJson))
            nextLabelId = nextLabelIdJson.GetInt32();
        if (root.TryGetProperty("nextStrokeId", out var nextStrokeIdJson))
            nextStrokeId = nextStrokeIdJson.GetInt32();
        else if (freehandStrokes.Any())
            nextStrokeId = freehandStrokes.Max(s => s.Id) + 1;
    }

    // DTO for JSON stroke deserialization
    private record StrokeJsonData(int id, List<StrokePointJsonData>? points, string? strokeColor, int? strokeWidth, string? strokeDashArray);
    private record StrokePointJsonData(double x, double y);

    private void LoadFromMermaid(string mermaid)
    {
        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();

        var lines = mermaid.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var nodeMap = new Dictionary<string, int>();
        double currentY = 50;
        double currentX = 50;
        int nodesPerRow = 4;
        int nodeCount = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (trimmedLine.StartsWith("%%") || trimmedLine.StartsWith("flowchart") || 
                trimmedLine.StartsWith("graph") || trimmedLine.StartsWith("classDef") || 
                trimmedLine.StartsWith("class ") || trimmedLine.StartsWith("style "))
                continue;

            // Parse standalone node definition: N1[Customer]
            if (!trimmedLine.Contains("-->") && !trimmedLine.Contains("---") && 
                (trimmedLine.Contains("[") || trimmedLine.Contains("(") || trimmedLine.Contains("{")))
            {
                EnsureNodeExists(trimmedLine, nodeMap, ref nodeCount, ref currentX, ref currentY, nodesPerRow);
                continue;
            }

            // Parse edge: A --> B or A -->|label| B
            if (trimmedLine.Contains("-->") || trimmedLine.Contains("---"))
            {
                var arrowType = trimmedLine.Contains("-->") ? "-->" : "---";
                var parts = trimmedLine.Split(new[] { arrowType }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 2)
                {
                    var fromPart = parts[0].Trim();
                    var toPart = parts[1].Trim();
                    string? edgeLabel = null;

                    // Check for label: |label|
                    if (toPart.Contains("|"))
                    {
                        var labelMatch = System.Text.RegularExpressions.Regex.Match(toPart, @"\|([^|]+)\|");
                        if (labelMatch.Success)
                        {
                            edgeLabel = labelMatch.Groups[1].Value;
                            toPart = System.Text.RegularExpressions.Regex.Replace(toPart, @"\|[^|]+\|", "").Trim();
                        }
                    }

                    var fromId = EnsureNodeExists(fromPart, nodeMap, ref nodeCount, ref currentX, ref currentY, nodesPerRow);
                    var toId = EnsureNodeExists(toPart, nodeMap, ref nodeCount, ref currentX, ref currentY, nodesPerRow);

                    var fromNode = nodes.First(n => n.Id == fromId);
                    var toNode = nodes.First(n => n.Id == toId);
                    var (fromConn, toConn) = GeometryService.GetOptimalConnectionPoints(fromNode, toNode);

                    var edge = new Edge
                    {
                        Id = nextEdgeId++,
                        From = fromId,
                        To = toId,
                        FromConnection = fromConn,
                        ToConnection = toConn
                    };
                    edge.PathData = PathService.GetEdgePath(edge, nodes);
                    edges.Add(edge);

                    if (!string.IsNullOrEmpty(edgeLabel))
                    {
                        var midpoint = GetEdgeMidpoint(edge);
                        edgeLabels.Add(new EdgeLabel
                        {
                            Id = nextLabelId++,
                            EdgeId = edge.Id,
                            Text = edgeLabel,
                            X = midpoint.X,
                            Y = midpoint.Y
                        });
                    }
                }
            }
        }

        ApplyHierarchicalLayout();
        
        // Bundle edges for cleaner appearance
        GeometryService.BundleAllEdges(nodes, edges);
        
        // Recalculate edge paths after bundling
        foreach (var edge in edges)
        {
            edge.PathData = PathService.GetEdgePath(edge, nodes);
        }
    }

    private int EnsureNodeExists(string nodeDef, Dictionary<string, int> nodeMap, ref int nodeCount, ref double currentX, ref double currentY, int nodesPerRow)
    {
        var (nodeId, nodeLabel, nodeShape) = ParseMermaidNode(nodeDef);

        if (!nodeMap.ContainsKey(nodeId))
        {
            var node = new Node
            {
                Id = nextId++,
                Text = nodeLabel,
                X = currentX,
                Y = currentY,
                Width = 120,
                Height = 60,
                Shape = nodeShape
            };
            nodes.Add(node);
            nodeMap[nodeId] = node.Id;

            nodeCount++;
            if (nodeCount % nodesPerRow == 0)
            {
                currentX = 50;
                currentY += 100;
            }
            else
            {
                currentX += 180;
            }
        }

        return nodeMap[nodeId];
    }

    private (string nodeId, string label, NodeShape shape) ParseMermaidNode(string nodeDef)
    {
        var shape = NodeShape.Rectangle;
        string nodeId;
        string label;

        // Handle different node shapes
        if (nodeDef.Contains("((") && nodeDef.Contains("))"))
        {
            shape = NodeShape.Ellipse;
            var match = System.Text.RegularExpressions.Regex.Match(nodeDef, @"(\w+)\(\((.+?)\)\)");
            nodeId = match.Success ? match.Groups[1].Value : nodeDef;
            label = match.Success ? match.Groups[2].Value : nodeDef;
        }
        else if (nodeDef.Contains("{{") && nodeDef.Contains("}}"))
        {
            shape = NodeShape.Diamond;
            var match = System.Text.RegularExpressions.Regex.Match(nodeDef, @"(\w+)\{\{(.+?)\}\}");
            nodeId = match.Success ? match.Groups[1].Value : nodeDef;
            label = match.Success ? match.Groups[2].Value : nodeDef;
        }
        else if (nodeDef.Contains("[/") && nodeDef.Contains("/]"))
        {
            shape = NodeShape.Parallelogram;
            var match = System.Text.RegularExpressions.Regex.Match(nodeDef, @"(\w+)\[/(.+?)/\]");
            nodeId = match.Success ? match.Groups[1].Value : nodeDef;
            label = match.Success ? match.Groups[2].Value : nodeDef;
        }
        else if (nodeDef.Contains("[(") && nodeDef.Contains(")]"))
        {
            shape = NodeShape.Cylinder;
            var match = System.Text.RegularExpressions.Regex.Match(nodeDef, @"(\w+)\[\((.+?)\)\]");
            nodeId = match.Success ? match.Groups[1].Value : nodeDef;
            label = match.Success ? match.Groups[2].Value : nodeDef;
        }
        else if (nodeDef.Contains("[") && nodeDef.Contains("]"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(nodeDef, @"(\w+)\[(.+?)\]");
            nodeId = match.Success ? match.Groups[1].Value : nodeDef;
            label = match.Success ? match.Groups[2].Value : nodeDef;
        }
        else
        {
            nodeId = nodeDef.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nodeDef;
            label = nodeId;
        }

        return (nodeId, label, shape);
    }

    private void LoadFromDot(string dot)
    {
        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();

        var lines = dot.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var nodeMap = new Dictionary<string, int>();
        double currentY = 50;
        double currentX = 50;
        int nodesPerRow = 4;
        int nodeCount = 0;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim().TrimEnd(';');
            
            if (trimmedLine.StartsWith("digraph") || trimmedLine.StartsWith("graph") || 
                trimmedLine == "{" || trimmedLine == "}" ||
                trimmedLine.StartsWith("//") || trimmedLine.StartsWith("rankdir") ||
                trimmedLine.StartsWith("node") || trimmedLine.StartsWith("edge"))
                continue;

            // Parse edge: A -> B or A -- B
            if (trimmedLine.Contains("->") || trimmedLine.Contains("--"))
            {
                var arrowType = trimmedLine.Contains("->") ? "->" : "--";
                var parts = trimmedLine.Split(new[] { arrowType }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 2)
                {
                    var fromPart = parts[0].Trim().Trim('"');
                    var toPart = parts[1].Trim().Trim('"');

                    // Remove any attributes
                    if (toPart.Contains("["))
                        toPart = toPart.Substring(0, toPart.IndexOf("[")).Trim();

                    var fromId = EnsureNodeExistsDot(fromPart, nodeMap, ref nodeCount, ref currentX, ref currentY, nodesPerRow);
                    var toId = EnsureNodeExistsDot(toPart, nodeMap, ref nodeCount, ref currentX, ref currentY, nodesPerRow);

                    var fromNode = nodes.First(n => n.Id == fromId);
                    var toNode = nodes.First(n => n.Id == toId);
                    var (fromConn, toConn) = GeometryService.GetOptimalConnectionPoints(fromNode, toNode);

                    var edge = new Edge
                    {
                        Id = nextEdgeId++,
                        From = fromId,
                        To = toId,
                        FromConnection = fromConn,
                        ToConnection = toConn
                    };
                    edge.PathData = PathService.GetEdgePath(edge, nodes);
                    edges.Add(edge);
                }
            }
        }

        ApplyHierarchicalLayout();
        
        // Bundle edges for cleaner appearance
        GeometryService.BundleAllEdges(nodes, edges);
        
        // Recalculate edge paths after bundling
        foreach (var edge in edges)
        {
            edge.PathData = PathService.GetEdgePath(edge, nodes);
        }
    }

    private int EnsureNodeExistsDot(string nodeDef, Dictionary<string, int> nodeMap, ref int nodeCount, ref double currentX, ref double currentY, int nodesPerRow)
    {
        var nodeId = nodeDef.Trim().Trim('"');
        var label = nodeId;

        // Extract label if present
        if (nodeId.Contains("["))
        {
            var attrMatch = System.Text.RegularExpressions.Regex.Match(nodeId, @"label\s*=\s*""([^""]+)""");
            if (attrMatch.Success)
                label = attrMatch.Groups[1].Value;
            nodeId = nodeId.Substring(0, nodeId.IndexOf("[")).Trim();
        }

        if (!nodeMap.ContainsKey(nodeId))
        {
            var node = new Node
            {
                Id = nextId++,
                Text = label,
                X = currentX,
                Y = currentY,
                Width = 120,
                Height = 60,
                Shape = NodeShape.Rectangle
            };
            nodes.Add(node);
            nodeMap[nodeId] = node.Id;

            nodeCount++;
            if (nodeCount % nodesPerRow == 0)
            {
                currentX = 50;
                currentY += 100;
            }
            else
            {
                currentX += 180;
            }
        }

        return nodeMap[nodeId];
    }

    private void ApplyHierarchicalLayout()
    {
        if (nodes.Count == 0) return;

        // Find root nodes (nodes with no incoming edges)
        var nodesWithIncoming = edges.Select(e => e.To).Distinct().ToHashSet();
        var rootNodes = nodes.Where(n => !nodesWithIncoming.Contains(n.Id)).ToList();

        if (rootNodes.Count == 0)
            rootNodes = new List<Node> { nodes.First() };

        var visited = new HashSet<int>();
        var levels = new Dictionary<int, int>();
        
        // BFS to assign levels
        var queue = new Queue<(int nodeId, int level)>();
        foreach (var root in rootNodes)
        {
            queue.Enqueue((root.Id, 0));
            levels[root.Id] = 0;
        }

        while (queue.Count > 0)
        {
            var (nodeId, level) = queue.Dequeue();
            if (visited.Contains(nodeId)) continue;
            visited.Add(nodeId);

            var outgoingEdges = edges.Where(e => e.From == nodeId);
            foreach (var edge in outgoingEdges)
            {
                if (!levels.ContainsKey(edge.To) || levels[edge.To] < level + 1)
                {
                    levels[edge.To] = level + 1;
                    queue.Enqueue((edge.To, level + 1));
                }
            }
        }

        // Position nodes by level
        var nodesByLevel = nodes.GroupBy(n => levels.GetValueOrDefault(n.Id, 0)).OrderBy(g => g.Key);
        double yOffset = 50;
        double levelSpacing = 120;
        double nodeSpacing = 180;

        foreach (var levelGroup in nodesByLevel)
        {
            var levelNodes = levelGroup.ToList();
            double xOffset = 50;
            double totalWidth = (levelNodes.Count - 1) * nodeSpacing;
            xOffset = Math.Max(50, (1000 - totalWidth) / 2);

            foreach (var node in levelNodes)
            {
                node.X = xOffset;
                node.Y = yOffset;
                xOffset += nodeSpacing;
            }

            yOffset += levelSpacing;
        }

        // Recalculate all edge paths
        foreach (var edge in edges)
        {
            edge.PathData = PathService.GetEdgePath(edge, nodes);
        }
    }

    #endregion

    #region QMaker Examples

    /// <summary>
    /// Load a simple M/M/1 queueing example
    /// </summary>
    private void LoadQMakerMM1Example()
    {
        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        selectedNodes.Clear();
        selectedEdges.Clear();

        // Create source -> queue-server -> sink
        var source = new Node
        {
            Id = nextId++,
            X = 100,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Source",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "source",
            ShowTerminals = true,
            QueueArrivalRate = 3.0,
            QueueArrivalDistribution = QueueDistribution.Exponential
        };

        var queue = new Node
        {
            Id = nextId++,
            X = 280,
            Y = 150,
            Width = 120,
            Height = 60,
            Text = "M/M/1 Queue",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "queue-server",
            ShowTerminals = true,
            QueueServiceRate = 5.0,
            QueueServerCount = 1,
            QueueServiceDistribution = QueueDistribution.Exponential,
            QueueDiscipline = QueueDiscipline.FIFO
        };

        var sink = new Node
        {
            Id = nextId++,
            X = 500,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Sink",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "sink",
            ShowTerminals = true
        };

        nodes.Add(source);
        nodes.Add(queue);
        nodes.Add(sink);

        // Create edges
        CreateQMakerEdge(source, queue);
        CreateQMakerEdge(queue, sink);

        StateHasChanged();
    }

    /// <summary>
    /// Load a multi-server M/M/c queueing example
    /// </summary>
    private void LoadQMakerMMCExample()
    {
        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        selectedNodes.Clear();
        selectedEdges.Clear();

        var source = new Node
        {
            Id = nextId++,
            X = 100,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Arrivals",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "source",
            ShowTerminals = true,
            QueueArrivalRate = 10.0,
            QueueArrivalDistribution = QueueDistribution.Exponential
        };

        var mmc = new Node
        {
            Id = nextId++,
            X = 280,
            Y = 150,
            Width = 140,
            Height = 60,
            Text = "M/M/3 Queue",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "mmc",
            ShowTerminals = true,
            QueueServiceRate = 4.0,
            QueueServerCount = 3,
            QueueServiceDistribution = QueueDistribution.Exponential,
            QueueDiscipline = QueueDiscipline.FIFO
        };

        var sink = new Node
        {
            Id = nextId++,
            X = 520,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Departures",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "sink",
            ShowTerminals = true
        };

        nodes.Add(source);
        nodes.Add(mmc);
        nodes.Add(sink);

        CreateQMakerEdge(source, mmc);
        CreateQMakerEdge(mmc, sink);

        StateHasChanged();
    }

    /// <summary>
    /// Load a queueing network example with multiple nodes
    /// </summary>
    private void LoadQMakerNetworkExample()
    {
        nodes.Clear();
        edges.Clear();
        edgeLabels.Clear();
        selectedNodes.Clear();
        selectedEdges.Clear();

        // Source -> Splitter -> Queue1/Queue2 -> Collector -> Sink
        var source = new Node
        {
            Id = nextId++,
            X = 50,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Source",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "source",
            ShowTerminals = true,
            QueueArrivalRate = 6.0
        };

        var splitter = new Node
        {
            Id = nextId++,
            X = 180,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Split",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "splitter",
            ShowTerminals = true,
            InputTerminalCount = 1,
            OutputTerminalCount = 2
        };

        var queue1 = new Node
        {
            Id = nextId++,
            X = 320,
            Y = 80,
            Width = 100,
            Height = 60,
            Text = "Fast Queue",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "queue-server",
            ShowTerminals = true,
            QueueServiceRate = 8.0
        };

        var queue2 = new Node
        {
            Id = nextId++,
            X = 320,
            Y = 220,
            Width = 100,
            Height = 60,
            Text = "Slow Queue",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "queue-server",
            ShowTerminals = true,
            QueueServiceRate = 4.0
        };

        var collector = new Node
        {
            Id = nextId++,
            X = 480,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Collect",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "collector",
            ShowTerminals = true,
            InputTerminalCount = 2,
            OutputTerminalCount = 1
        };

        var sink = new Node
        {
            Id = nextId++,
            X = 620,
            Y = 150,
            Width = 80,
            Height = 60,
            Text = "Sink",
            Shape = NodeShape.Rectangle,
            TemplateId = "qmaker",
            TemplateShapeId = "sink",
            ShowTerminals = true
        };

        nodes.Add(source);
        nodes.Add(splitter);
        nodes.Add(queue1);
        nodes.Add(queue2);
        nodes.Add(collector);
        nodes.Add(sink);

        CreateQMakerEdge(source, splitter);
        CreateQMakerEdge(splitter, queue1);
        CreateQMakerEdge(splitter, queue2);
        CreateQMakerEdge(queue1, collector);
        CreateQMakerEdge(queue2, collector);
        CreateQMakerEdge(collector, sink);

        StateHasChanged();
    }

    private void CreateQMakerEdge(Node from, Node to)
    {
        var (fromConn, toConn) = GeometryService.GetOptimalConnectionPoints(from, to);
        var edge = new Edge
        {
            Id = nextEdgeId++,
            From = from.Id,
            To = to.Id,
            FromConnection = fromConn,
            ToConnection = toConn
        };
        edge.PathData = PathService.GetEdgePath(edge, nodes);
        edges.Add(edge);
    }

    #endregion
}
