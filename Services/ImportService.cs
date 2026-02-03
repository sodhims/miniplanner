using dfd2wasm.Models;
using dfd2wasm.Services;
using System.Text.RegularExpressions;

namespace dfd2wasm.Services
{
    public class ImportService
    {
        private int nodeIdCounter = 1;
        private int edgeIdCounter = 1;
        private Dictionary<string, int> nodeMapping = new Dictionary<string, int>();

        public class ImportResult
        {
            public List<Node> Nodes { get; set; } = new List<Node>();
            public List<Edge> Edges { get; set; } = new List<Edge>();
            public string Format { get; set; } = "";
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = "";
        }

        public ImportResult ImportFromText(string input)
        { Console.WriteLine($"=== ImportFromText START === Input length: {input?.Length}");
            var result = new ImportResult();

            if (string.IsNullOrWhiteSpace(input))
            {
                result.ErrorMessage = "Input is empty";
                return result;
            }

            // Detect format
            if (input.TrimStart().StartsWith("graph") || input.TrimStart().StartsWith("flowchart"))
            {
                result.Format = "Mermaid";
                return ImportMermaid(input);
            }
            else if (input.Contains("digraph") || input.Contains("graph {"))
            {
                result.Format = "Graphviz";
                return ImportGraphviz(input);
            }
            else
            {
                result.ErrorMessage = "Unrecognized format. Expected Mermaid (flowchart/graph) or Graphviz (digraph)";
                return result;
            }
        }

        #region Mermaid Import

        private ImportResult ImportMermaid(string input)
        {
            var result = new ImportResult { Format = "Mermaid" };
            nodeIdCounter = 1;
            edgeIdCounter = 1;
            nodeMapping.Clear();

            try
            {
                var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                // Skip the first line (graph/flowchart declaration)
                lines = lines.Skip(1).ToList();

                // TWO-PASS PARSING:
                // Pass 1: Extract all node definitions first (ensures labels are captured)
                foreach (var line in lines)
                {
                    if (line.StartsWith("%") || line.StartsWith("%%"))
                        continue;
                    
                    ExtractNodeDefinitions(line, result);
                }

                // Pass 2: Process edges
                foreach (var line in lines)
                {
                    if (line.StartsWith("%") || line.StartsWith("%%"))
                        continue;

                    ParseMermaidEdge(line, result);
                }

                // Auto-layout nodes with edge information for hierarchical layout
                AutoLayoutNodes(result.Nodes, result.Edges);

                // Calculate edge paths
                foreach (var edge in result.Edges)
                {
                    edge.PathData = CalculateEdgePath(edge, result.Nodes);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error parsing Mermaid: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Extract node definitions with labels from a line (e.g., N1[Customer])
        /// </summary>
        private void ExtractNodeDefinitions(string line, ImportResult result)
        { Console.WriteLine($"ExtractNodeDefinitions called with: [{line}]");
            // Also handles A[["Text"]] for special shapes
            var nodePatterns = new[]
            {
                @"(\w+)\s*\[\[([^\]]+)\]\]",      // A[[Text]] - subroutine shape
                @"(\w+)\s*\[\(([^\)]+)\)\]",      // A[(Text)] - cylindrical shape  
                @"(\w+)\s*\(\[([^\]]+)\]\)",      // A([Text]) - stadium shape
                @"(\w+)\s*\{\{([^\}]+)\}\}",      // A{{Text}} - hexagon shape
                @"(\w+)\s*\[([^\]]+)\]",          // A[Text] - rectangle
                @"(\w+)\s*\(([^\)]+)\)",          // A(Text) - rounded/stadium
                @"(\w+)\s*\{([^\}]+)\}",          // A{Text} - diamond/rhombus
                @"(\w+)\s*>([^\]]+)\]",           // A>Text] - asymmetric shape
            };

            foreach (var pattern in nodePatterns)
            {
                var matches = Regex.Matches(line, pattern);
                foreach (Match match in matches)
                {
                    var nodeKey = match.Groups[1].Value;
                    var text = match.Groups[2].Value.Trim().Trim('"'); // Remove quotes if present

                    // Determine shape from the pattern/brackets
                    var shape = DetermineShapeFromPattern(pattern, match.Value);

                    Console.WriteLine($"ExtractNodeDefinitions: key={nodeKey}, text={text}"); EnsureNode(nodeKey, result, text, shape);
                }
            }
        }

        private string DetermineShapeFromPattern(string pattern, string matchedText)
        {
            if (pattern.Contains(@"\[\[")) return "rectangle";      // subroutine
            if (pattern.Contains(@"\[\(")) return "cylinder";       // cylindrical/database
            if (pattern.Contains(@"\(\[")) return "rounded";        // stadium
            if (pattern.Contains(@"\{\{")) return "hexagon";        // hexagon
            if (pattern.Contains(@"\[") && !pattern.Contains(@"\[\[")) return "rectangle";
            if (pattern.Contains(@"\(") && !pattern.Contains(@"\(\[")) return "rounded";
            if (pattern.Contains(@"\{") && !pattern.Contains(@"\{\{")) return "diamond";
            if (pattern.Contains(@">")) return "parallelogram";     // asymmetric
            return "rectangle";
        }

        /// <summary>
        /// Parse edge definitions from a line
        /// </summary>
        private void ParseMermaidEdge(string line, ImportResult result)
        {
            // Edge patterns - handle various arrow styles
            // A --> B, A --- B, A ==> B, A -.-> B, A -->|label| B, etc.
            var edgePattern = @"(\w+)(?:\s*[\[\(\{<>\]]?[^\-=\.>\|\s]*[\]\)\}>]?)?\s*(-->|---|==>|\.\.>|-\.-|==|-\.->|-->\||---\||==>\|)\|?([^\|]*)\|?\s*(\w+)";
            
            var edgeMatch = Regex.Match(line, edgePattern);

            if (edgeMatch.Success)
            {
                var fromNodeKey = edgeMatch.Groups[1].Value;
                var arrowType = edgeMatch.Groups[2].Value;
                var label = edgeMatch.Groups[3].Value.Trim();
                var toNodeKey = edgeMatch.Groups[4].Value;

                // Ensure nodes exist (in case they weren't defined with brackets)
                EnsureNode(fromNodeKey, result);
                EnsureNode(toNodeKey, result);

                // Create edge
                var edge = new Edge
                {
                    Id = edgeIdCounter++,
                    From = nodeMapping[fromNodeKey],
                    To = nodeMapping[toNodeKey],
                    Label = string.IsNullOrEmpty(label) ? null : label,
                    FromConnection = new ConnectionPoint { Side = "right", Position = 0 },
                    ToConnection = new ConnectionPoint { Side = "left", Position = 0 },
                    IsOrthogonal = false
                };

                // Set style based on arrow type
                switch (arrowType)
                {
                    case "==>":
                    case "==>|":
                    case "==":
                        edge.StrokeWidth = 4;
                        break;
                    case "-.->":
                    case "-.-":
                    case "..>":
                        edge.StrokeDashArray = "5,5";
                        break;
                    default:
                        edge.StrokeWidth = 2;
                        break;
                }

                result.Edges.Add(edge);
            }
        }

        // Keep old method for backward compatibility but redirect to new implementation
        private void ParseMermaidLine(string line, ImportResult result)
        {
            ExtractNodeDefinitions(line, result);
            ParseMermaidEdge(line, result);
        }

        /// <summary>
        /// First pass: Extract only node definitions from Mermaid line.
        /// This ensures labels are captured before edges create unlabeled nodes.
        /// </summary>
        private void ExtractMermaidNodeDefinitions(string line, ImportResult result)
        {
            // Node definition patterns:
            // N1[Label]      - Rectangle
            // N2(Label)      - Rounded / Ellipse
            // N3{Label}      - Diamond
            // N4[[Label]]    - Subroutine
            // N5[(Label)]    - Cylinder
            // N6([Label])    - Stadium
            // N7{{Label}}    - Hexagon

            // Standard node definitions: key[text], key(text), key{text}
            var nodePattern = @"^\s*(\w+)\s*([\[\(\{])\s*([^\]\)\}]+)\s*([\]\)\}])\s*$";
            var nodeMatch = Regex.Match(line, nodePattern);

            if (nodeMatch.Success)
            {
                var nodeKey = nodeMatch.Groups[1].Value;
                var openBracket = nodeMatch.Groups[2].Value;
                var text = nodeMatch.Groups[3].Value.Trim();
                var closeBracket = nodeMatch.Groups[4].Value;

                var shape = DetermineMermaidShape(openBracket, closeBracket, text);
                
                // Remove shape markers from text if present (e.g., [[text]] -> text)
                text = CleanMermaidNodeText(text);

                EnsureNode(nodeKey, result, text, shape);
                return;
            }

            // Also extract inline definitions from edge lines
            var inlineNodePattern = @"(\w+)\s*([\[\(\{])\s*([^\]\)\}]+)\s*([\]\)\}])";
            var inlineMatches = Regex.Matches(line, inlineNodePattern);

            foreach (Match match in inlineMatches)
            {
                var nodeKey = match.Groups[1].Value;
                var openBracket = match.Groups[2].Value;
                var text = match.Groups[3].Value.Trim();
                var closeBracket = match.Groups[4].Value;

                var shape = DetermineMermaidShape(openBracket, closeBracket, text);
                text = CleanMermaidNodeText(text);

                EnsureNode(nodeKey, result, text, shape);
            }
        }

        private string DetermineMermaidShape(string openBracket, string closeBracket, string text)
        {
            // Special shapes with nested brackets
            if (text.StartsWith("[") && text.EndsWith("]"))
                return "rectangle"; // Subroutine [[text]]
            if (text.StartsWith("(") && text.EndsWith(")"))
                return "cylinder";  // Database [(text)]
            if (text.StartsWith("{") && text.EndsWith("}"))
                return "hexagon";   // Hexagon {{text}}

            // Standard shapes
            if (openBracket == "(" && closeBracket == ")")
                return "ellipse";
            if (openBracket == "{" && closeBracket == "}")
                return "diamond";
            
            return "rectangle";
        }

        private string CleanMermaidNodeText(string text)
        {
            // Remove nested brackets from special shapes
            if (text.StartsWith("[") && text.EndsWith("]"))
                return text.Substring(1, text.Length - 2).Trim();
            if (text.StartsWith("(") && text.EndsWith(")"))
                return text.Substring(1, text.Length - 2).Trim();
            if (text.StartsWith("{") && text.EndsWith("}"))
                return text.Substring(1, text.Length - 2).Trim();
            
            return text;
        }

        private void EnsureNode(string key, ImportResult result, string? text = null, string? shape = null)
        {
            if (!nodeMapping.ContainsKey(key))
            {
                // Create new node
                var nodeId = nodeIdCounter++;
                nodeMapping[key] = nodeId;

                result.Nodes.Add(new Node
                {
                    Id = nodeId,
                    X = 0, // Will be set by auto-layout
                    Y = 0,
                    Width = 120,
                    Height = 60,
                    Text = text ?? key,
                    Shape = ParseNodeShape(shape)
                });
            }
            else if (text != null)
            {
                // Node exists - update text if we have a real label
                // (not just the auto-generated key)
                var node = result.Nodes.FirstOrDefault(n => n.Id == nodeMapping[key]);
                if (node != null)
                {
                    // Always update text if provided and different from current
                    // This handles cases where node was first referenced in an edge
                    // before its definition with label was encountered
                    if (node.Text == key || string.IsNullOrEmpty(node.Text))
                    {
                        node.Text = text;
                    }
                    
                    // Update shape if provided and node still has default
                    if (!string.IsNullOrEmpty(shape) && node.Shape == NodeShape.Rectangle)
                    {
                        node.Shape = ParseNodeShape(shape);
                    }
                }
            }
        }

        #endregion

        #region Graphviz Import

        private ImportResult ImportGraphviz(string input)
        {
            var result = new ImportResult { Format = "Graphviz" };
            nodeIdCounter = 1;
            edgeIdCounter = 1;
            nodeMapping.Clear();

            try
            {
                // Remove comments
                input = Regex.Replace(input, @"//.*$", "", RegexOptions.Multiline);
                input = Regex.Replace(input, @"/\*.*?\*/", "", RegexOptions.Singleline);

                // Extract graph content between braces
                var graphMatch = Regex.Match(input, @"(?:digraph|graph)\s+\w*\s*\{(.*)\}", RegexOptions.Singleline);
                if (!graphMatch.Success)
                {
                    result.ErrorMessage = "Could not parse graph structure";
                    return result;
                }

                var content = graphMatch.Groups[1].Value;

                // Split into statements
                var statements = content.Split(';')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                foreach (var statement in statements)
                {
                    ParseGraphvizStatement(statement, result, input.Contains("digraph"));
                }

                // Auto-layout
                AutoLayoutNodes(result.Nodes, result.Edges);

                // Calculate edge paths
                foreach (var edge in result.Edges)
                {
                    edge.PathData = CalculateEdgePath(edge, result.Nodes);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error parsing Graphviz: {ex.Message}";
                result.Success = false;
            }

            return result;
        }

        private void ParseGraphvizStatement(string statement, ImportResult result, bool isDigraph)
        {
            // Skip graph-level attributes
            if (statement.StartsWith("graph ") || statement.StartsWith("node ") || statement.StartsWith("edge "))
                return;

            // Check for edge: A -> B or A -- B
            var edgePattern = isDigraph ? @"(\w+)\s*->\s*(\w+)" : @"(\w+)\s*--\s*(\w+)";
            var edgeMatch = Regex.Match(statement, edgePattern);

            if (edgeMatch.Success)
            {
                var fromKey = edgeMatch.Groups[1].Value;
                var toKey = edgeMatch.Groups[2].Value;

                // Extract label from attributes if present
                string? label = null;
                var labelMatch = Regex.Match(statement, @"label\s*=\s*""([^""]+)""");
                if (labelMatch.Success)
                {
                    label = labelMatch.Groups[1].Value;
                }

                EnsureNode(fromKey, result);
                EnsureNode(toKey, result);

                result.Edges.Add(new Edge
                {
                    Id = edgeIdCounter++,
                    From = nodeMapping[fromKey],
                    To = nodeMapping[toKey],
                    Label = label,
                    FromConnection = new ConnectionPoint { Side = "right", Position = 0 },
                    ToConnection = new ConnectionPoint { Side = "left", Position = 0 }
                });
            }
            else
            {
                // Node definition: A [label="Text" shape=box]
                var nodeMatch = Regex.Match(statement, @"^(\w+)\s*(\[.*\])?$");
                if (nodeMatch.Success)
                {
                    var nodeKey = nodeMatch.Groups[1].Value;
                    var attributes = nodeMatch.Groups[2].Value;

                    string? label = null;
                    string? shape = null;

                    if (!string.IsNullOrEmpty(attributes))
                    {
                        var labelMatch = Regex.Match(attributes, @"label\s*=\s*""([^""]+)""");
                        if (labelMatch.Success)
                            label = labelMatch.Groups[1].Value;

                        var shapeMatch = Regex.Match(attributes, @"shape\s*=\s*(\w+)");
                        if (shapeMatch.Success)
                            shape = shapeMatch.Groups[1].Value;
                    }

                    EnsureNode(nodeKey, result, label, shape);
                }
            }
        }

        #endregion

        #region Auto Layout

        private void AutoLayoutNodes(List<Node> nodes, List<Edge> edges)
        {
            if (nodes.Count == 0) return;

            // Use Sugiyama-style hierarchical layout
            var layers = AssignLayers(nodes, edges);
            var adjacency = BuildAdjacencyLists(nodes, edges);

            // Minimize crossings
            MinimizeCrossings(layers, adjacency);

            // Position nodes
            int horizontalSpacing = 180;
            int verticalSpacing = 120;
            int startX = 50;
            int startY = 50;

            PositionNodes(layers, horizontalSpacing, verticalSpacing, startX, startY, nodes);
        }

        private (Dictionary<int, List<int>> outgoing, Dictionary<int, List<int>> incoming) BuildAdjacencyLists(List<Node> nodes, List<Edge> edges)
        {
            var outgoing = new Dictionary<int, List<int>>();
            var incoming = new Dictionary<int, List<int>>();

            foreach (var node in nodes)
            {
                outgoing[node.Id] = new List<int>();
                incoming[node.Id] = new List<int>();
            }

            foreach (var edge in edges)
            {
                if (outgoing.ContainsKey(edge.From) && incoming.ContainsKey(edge.To))
                {
                    outgoing[edge.From].Add(edge.To);
                    incoming[edge.To].Add(edge.From);
                }
            }

            return (outgoing, incoming);
        }

        private List<List<int>> AssignLayers(List<Node> nodes, List<Edge> edges)
        {
            var layers = new List<List<int>>();
            var nodeToLayer = new Dictionary<int, int>();
            var adjacency = BuildAdjacencyLists(nodes, edges);
            var (outgoing, incoming) = adjacency;

            // Initialize all nodes to layer 0
            foreach (var node in nodes)
            {
                nodeToLayer[node.Id] = 0;
            }

            // Find root nodes (no incoming edges)
            var rootNodes = nodes.Where(n => incoming[n.Id].Count == 0).ToList();

            // If no root nodes found, use all nodes as potential roots
            if (rootNodes.Count == 0)
            {
                rootNodes = nodes.ToList();
            }

            // Detect back-edges to handle cycles
            var visited = new HashSet<int>();
            var recursionStack = new HashSet<int>();
            var backEdges = new HashSet<(int from, int to)>();

            void DetectBackEdges(int nodeId)
            {
                visited.Add(nodeId);
                recursionStack.Add(nodeId);

                foreach (var childId in outgoing[nodeId])
                {
                    if (!visited.Contains(childId))
                    {
                        DetectBackEdges(childId);
                    }
                    else if (recursionStack.Contains(childId))
                    {
                        // This is a back-edge (creates a cycle)
                        backEdges.Add((nodeId, childId));
                    }
                }

                recursionStack.Remove(nodeId);
            }

            // Run DFS from all roots to detect cycles
            foreach (var root in rootNodes)
            {
                if (!visited.Contains(root.Id))
                {
                    DetectBackEdges(root.Id);
                }
            }

            // Calculate longest path to each node, ignoring back-edges
            bool changed = true;
            int maxIterations = nodes.Count * 2; // Limit iterations
            int iteration = 0;

            while (changed && iteration < maxIterations)
            {
                changed = false;
                iteration++;

                foreach (var node in nodes)
                {
                    foreach (var childId in outgoing[node.Id])
                    {
                        // Skip back-edges when calculating layers
                        if (backEdges.Contains((node.Id, childId)))
                            continue;

                        int newLayer = nodeToLayer[node.Id] + 1;
                        if (newLayer > nodeToLayer[childId])
                        {
                            nodeToLayer[childId] = newLayer;
                            changed = true;
                        }
                    }
                }
            }

            // Build layers from nodeToLayer mapping
            int maxLayer = nodeToLayer.Values.Count > 0 ? nodeToLayer.Values.Max() : 0;

            // Cap maximum layer to prevent nodes going too far down
            maxLayer = Math.Min(maxLayer, nodes.Count - 1);

            for (int i = 0; i <= maxLayer; i++)
            {
                layers.Add(new List<int>());
            }

            foreach (var kvp in nodeToLayer)
            {
                int layer = Math.Min(kvp.Value, maxLayer); // Cap layer
                layers[layer].Add(kvp.Key);
            }

            // Ensure we have at least one layer
            if (layers.Count == 0)
            {
                layers.Add(new List<int>());
                foreach (var node in nodes)
                {
                    layers[0].Add(node.Id);
                }
            }

            return layers;
        }

        private void MinimizeCrossings(List<List<int>> layers, (Dictionary<int, List<int>> outgoing, Dictionary<int, List<int>> incoming) adjacency)
        {
            // Use barycenter heuristic to minimize crossings
            // Iterate multiple times for better results
            const int iterations = 4;

            for (int iter = 0; iter < iterations; iter++)
            {
                // Forward pass: order based on parents
                for (int i = 1; i < layers.Count; i++)
                {
                    OrderLayerByBarycenter(layers[i], layers[i - 1], adjacency.incoming, isForward: true);
                }

                // Backward pass: order based on children
                for (int i = layers.Count - 2; i >= 0; i--)
                {
                    OrderLayerByBarycenter(layers[i], layers[i + 1], adjacency.outgoing, isForward: false);
                }
            }
        }

        private void OrderLayerByBarycenter(List<int> currentLayer, List<int> referenceLayer, Dictionary<int, List<int>> connections, bool isForward)
        {
            if (currentLayer.Count <= 1) return;

            // Calculate barycenter (average position) for each node
            var barycenters = new Dictionary<int, double>();

            foreach (var nodeId in currentLayer)
            {
                var connectedNodes = connections[nodeId];
                if (connectedNodes.Count == 0)
                {
                    // No connections, keep current relative position
                    barycenters[nodeId] = currentLayer.IndexOf(nodeId);
                    continue;
                }

                double sum = 0;
                int count = 0;
                foreach (var connectedId in connectedNodes)
                {
                    int pos = referenceLayer.IndexOf(connectedId);
                    if (pos >= 0)
                    {
                        sum += pos;
                        count++;
                    }
                }

                barycenters[nodeId] = count > 0 ? sum / count : currentLayer.IndexOf(nodeId);
            }

            // Sort layer by barycenter values (stable sort to maintain order for ties)
            currentLayer.Sort((a, b) =>
            {
                int cmp = barycenters[a].CompareTo(barycenters[b]);
                return cmp != 0 ? cmp : a.CompareTo(b); // Use ID as tiebreaker for stability
            });
        }

        private void PositionNodes(List<List<int>> layers, int horizontalSpacing, int verticalSpacing, int startX, int startY, List<Node> nodes)
        {
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                var layerY = startY + (layerIndex * verticalSpacing);

                // Center the layer horizontally
                var layerWidth = (layer.Count - 1) * horizontalSpacing;
                var layerStartX = startX + 400 - (layerWidth / 2); // Center with offset to keep visible

                for (int posIndex = 0; posIndex < layer.Count; posIndex++)
                {
                    var nodeId = layer[posIndex];
                    var node = nodes.FirstOrDefault(n => n.Id == nodeId);

                    if (node != null)
                    {
                        node.X = layerStartX + (posIndex * horizontalSpacing);
                        node.Y = layerY;
                    }
                }
            }
        }

        #endregion

        #region Edge Path Calculation

        private string CalculateEdgePath(Edge edge, List<Node> nodes)
        {
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);

            if (fromNode == null || toNode == null)
                return "";

            // Simple straight line path
            var fromX = fromNode.X + fromNode.Width;
            var fromY = fromNode.Y + fromNode.Height / 2;
            var toX = toNode.X;
            var toY = toNode.Y + toNode.Height / 2;

            return $"M {fromX} {fromY} L {toX} {toY}";
        }

        #endregion

        #region Helper Methods

        private NodeShape ParseNodeShape(string? shape)
        {
            if (string.IsNullOrEmpty(shape))
                return NodeShape.Rectangle;

            return shape.ToLower() switch
            {
                "rectangle" => NodeShape.Rectangle,
                "rounded" => NodeShape.Rectangle,  // Use Rectangle if your enum doesn't have Rounded
                "ellipse" => NodeShape.Ellipse,
                "circle" => NodeShape.Ellipse,     // Use Ellipse if your enum doesn't have Circle
                "diamond" => NodeShape.Diamond,
                "parallelogram" => NodeShape.Parallelogram,
                "trapezoid" => NodeShape.Rectangle,
                "cylinder" => NodeShape.Cylinder,
                "hexagon" => NodeShape.Rectangle,  // Map to Rectangle or add Hexagon to enum
                "document" => NodeShape.Rectangle,
                "box" => NodeShape.Rectangle,
                "oval" => NodeShape.Ellipse,
                "rhombus" => NodeShape.Diamond,
                _ => NodeShape.Rectangle
            };
        }

        #endregion
    }
}



