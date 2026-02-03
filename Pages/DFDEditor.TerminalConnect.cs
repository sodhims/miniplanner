using dfd2wasm.Models;

namespace dfd2wasm.Pages;

/// <summary>
/// Terminal-based auto-connection logic for STS and Circuit modes.
/// When a node is dragged and its terminals touch another node's terminals,
/// automatically create an edge between them.
/// </summary>
public partial class DFDEditor
{
    /// <summary>
    /// Distance threshold for terminal "touching" detection (in diagram coordinates)
    /// Increased to match larger terminal size (radius 7 + stickOut 12)
    /// </summary>
    private const double TerminalTouchThreshold = 30.0;

    /// <summary>
    /// After a drag operation ends, check if any terminals are now touching
    /// and automatically create edges between them.
    /// </summary>
    private int TryAutoConnectTerminals(IEnumerable<int> movedNodeIds)
    {
        int edgesCreated = 0;
        var movedSet = movedNodeIds.ToHashSet();
        var movedNodes = nodes.Where(n => movedSet.Contains(n.Id)).ToList();

        foreach (var movedNode in movedNodes)
        {
            // Only auto-connect nodes that have terminals enabled
            if (!movedNode.ShowTerminals) continue;

            foreach (var otherNode in nodes)
            {
                // Skip self
                if (otherNode.Id == movedNode.Id) continue;

                // Skip if both were moved together
                if (movedSet.Contains(otherNode.Id)) continue;

                // Other node must also have terminals
                if (!otherNode.ShowTerminals) continue;

                // Check: movedNode OUTPUT → otherNode INPUT
                if (CanConnectTerminals(movedNode, otherNode) &&
                    AreTerminalsTouching(movedNode, "output", otherNode, "input"))
                {
                    CreateTerminalEdge(movedNode.Id, otherNode.Id);
                    edgesCreated++;
                }

                // Check: otherNode OUTPUT → movedNode INPUT
                if (CanConnectTerminals(otherNode, movedNode) &&
                    AreTerminalsTouching(otherNode, "output", movedNode, "input"))
                {
                    CreateTerminalEdge(otherNode.Id, movedNode.Id);
                    edgesCreated++;
                }
            }
        }

        if (edgesCreated > 0)
        {
            StateHasChanged();
        }

        return edgesCreated;
    }

    /// <summary>
    /// Check if an edge can be created between two nodes (no duplicate edges, connectivity rules)
    /// </summary>
    private bool CanConnectTerminals(Node fromNode, Node toNode)
    {
        // Check if edge already exists
        if (edges.Any(e => e.From == fromNode.Id && e.To == toNode.Id))
            return false;

        // Check connectivity type rules
        return IsConnectionAllowed(fromNode, toNode);
    }

    /// <summary>
    /// Check if two terminals are within touching distance
    /// </summary>
    private bool AreTerminalsTouching(Node node1, string terminal1Type, Node node2, string terminal2Type)
    {
        var pos1 = GetTerminalPosition(node1, terminal1Type);
        var pos2 = GetTerminalPosition(node2, terminal2Type);

        if (!pos1.HasValue || !pos2.HasValue) return false;

        var dx = pos1.Value.x - pos2.Value.x;
        var dy = pos1.Value.y - pos2.Value.y;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        return distance <= TerminalTouchThreshold;
    }

    /// <summary>
    /// Get the absolute position of a terminal on a node, accounting for rotation
    /// Uses precise T1X/T1Y/T2X/T2Y/T3X/T3Y if set, otherwise falls back to TerminalLayout
    /// </summary>
    private (double x, double y)? GetTerminalPosition(Node node, string terminalType)
    {
        double normX, normY;
        TerminalDirection dir;

        if (terminalType == "third")
        {
            if (!node.HasThirdTerminal) return null;
            // Use precise position if set, otherwise default
            normX = node.T3X ?? 0.5;
            normY = node.T3Y ?? 1.0;
            dir = TerminalLayouts.GetDirectionFromNormalizedPosition(normX, normY);
        }
        else if (terminalType == "input")
        {
            // Use precise position if set
            if (node.T1X.HasValue && node.T1Y.HasValue)
            {
                normX = node.T1X.Value;
                normY = node.T1Y.Value;
                dir = TerminalLayouts.GetDirectionFromNormalizedPosition(normX, normY);
            }
            else
            {
                // Fall back to TerminalLayout
                var (inputPos, _) = TerminalLayouts.ParseLayout(node.TerminalLayout);
                (normX, normY, dir) = TerminalLayouts.GetPositionCoords(inputPos);
            }
        }
        else // output
        {
            // Use precise position if set
            if (node.T2X.HasValue && node.T2Y.HasValue)
            {
                normX = node.T2X.Value;
                normY = node.T2Y.Value;
                dir = TerminalLayouts.GetDirectionFromNormalizedPosition(normX, normY);
            }
            else
            {
                // Fall back to TerminalLayout
                var (_, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);
                (normX, normY, dir) = TerminalLayouts.GetPositionCoords(outputPos);
            }
        }

        // Calculate base position relative to node origin
        // stickOut = 12.0 matches the stem line length in ShapeLibraryService.RenderCircuitTerminals
        // This positions the connection at the CENTER of the terminal circle
        double stickOut = 12.0;
        double relX = normX * node.Width;
        double relY = normY * node.Height;

        // Apply stick-out based on direction (before rotation)
        switch (dir)
        {
            case TerminalDirection.Left:
                relX -= stickOut;
                break;
            case TerminalDirection.Right:
                relX += stickOut;
                break;
            case TerminalDirection.Top:
                relY -= stickOut;
                break;
            case TerminalDirection.Bottom:
                relY += stickOut;
                break;
        }

        // Apply rotation if node is rotated
        if (node.Rotation != 0)
        {
            var centerX = node.Width / 2;
            var centerY = node.Height / 2;
            var radians = node.Rotation * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            // Translate to origin, rotate, translate back
            var dx = relX - centerX;
            var dy = relY - centerY;
            relX = dx * cos - dy * sin + centerX;
            relY = dx * sin + dy * cos + centerY;
        }

        // Convert to absolute coordinates
        return (node.X + relX, node.Y + relY);
    }

    /// <summary>
    /// Create an edge between two nodes using terminal connection points
    /// </summary>
    private void CreateTerminalEdge(int fromNodeId, int toNodeId)
    {
        var fromNode = nodes.FirstOrDefault(n => n.Id == fromNodeId);
        var toNode = nodes.FirstOrDefault(n => n.Id == toNodeId);

        if (fromNode == null || toNode == null) return;

        // Get optimal connection points based on terminal positions
        var (fromConn, toConn) = GetTerminalConnectionPoints(fromNode, toNode);

        var newEdge = CreateEdgeWithDefaults(fromNodeId, toNodeId, fromConn, toConn);
        newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
        edges.Add(newEdge);
    }

    /// <summary>
    /// Get connection points based on terminal layout, accounting for node rotation
    /// </summary>
    private (ConnectionPoint from, ConnectionPoint to) GetTerminalConnectionPoints(Node fromNode, Node toNode)
    {
        var (_, fromOutputPos) = TerminalLayouts.ParseLayout(fromNode.TerminalLayout);
        var (toInputPos, _) = TerminalLayouts.ParseLayout(toNode.TerminalLayout);

        // Apply rotation to get the actual side after rotation
        var fromSide = GetRotatedSide(fromOutputPos, fromNode.Rotation);
        var toSide = GetRotatedSide(toInputPos, toNode.Rotation);

        return (
            new ConnectionPoint { Side = fromSide, Position = 0 },
            new ConnectionPoint { Side = toSide, Position = 0 }
        );
    }

    /// <summary>
    /// Get the side name after applying rotation
    /// </summary>
    private static string GetRotatedSide(string side, int rotationDegrees)
    {
        if (rotationDegrees == 0) return side;

        string[] sides = { "right", "bottom", "left", "top" };
        int index = side switch
        {
            "right" => 0,
            "bottom" => 1,
            "left" => 2,
            "top" => 3,
            _ => 0
        };

        // Each 90 degrees shifts by one position
        int shift = (rotationDegrees / 90) % 4;
        int newIndex = (index + shift) % 4;
        return sides[newIndex];
    }

    /// <summary>
    /// Terminal hit detection radius (in diagram coordinates)
    /// </summary>
    private const double TerminalHitRadius = 20.0;

    /// <summary>
    /// Check if a click position hits a terminal on a node.
    /// Returns the terminal type ("input", "output", or extra terminal id) if hit, null otherwise.
    /// Position is in absolute diagram coordinates.
    /// </summary>
    private string? GetTerminalAtPosition(Node node, double clickX, double clickY)
    {
        if (!node.ShowTerminals) return null;

        var stickOut = 12.0;

        Console.WriteLine($"  GetTerminalAtPosition: node={node.Id}, click=({clickX:F1},{clickY:F1})");
        Console.WriteLine($"    Node pos=({node.X},{node.Y}), size=({node.Width}x{node.Height}), rotation={node.Rotation}");
        Console.WriteLine($"    T1X={node.T1X}, T1Y={node.T1Y}, T2X={node.T2X}, T2Y={node.T2Y}");
        Console.WriteLine($"    TerminalLayout='{node.TerminalLayout}'");

        // Calculate the center of the node for rotation
        var centerX = node.X + node.Width / 2;
        var centerY = node.Y + node.Height / 2;

        // Use precise T1X/T1Y coordinates if set, otherwise fall back to TerminalLayout
        double inX, inY;
        TerminalDirection inDir;
        if (node.T1X.HasValue && node.T1Y.HasValue)
        {
            inX = node.T1X.Value;
            inY = node.T1Y.Value;
            inDir = TerminalLayouts.GetDirectionFromNormalizedPosition(inX, inY);
            Console.WriteLine($"    Using T1X/T1Y: ({inX},{inY}), dir={inDir}");
        }
        else
        {
            var (inputPos, _) = TerminalLayouts.ParseLayout(node.TerminalLayout);
            (inX, inY, inDir) = TerminalLayouts.GetPositionCoords(inputPos);
            Console.WriteLine($"    Using TerminalLayout for input: pos={inputPos}, coords=({inX},{inY}), dir={inDir}");
        }

        // Check input terminal - calculate position then apply rotation
        var inLocalX = inX * node.Width;
        var inLocalY = inY * node.Height;
        var (inStickX, inStickY) = GetStickOutOffsetForDirection(inDir, stickOut);
        var inTermLocalX = inLocalX + inStickX;
        var inTermLocalY = inLocalY + inStickY;

        // Apply rotation around node center if needed
        var (inTermX, inTermY) = ApplyRotation(inTermLocalX, inTermLocalY,
            node.Width / 2, node.Height / 2, node.Rotation);
        inTermX += node.X;
        inTermY += node.Y;

        var inDist = Distance(clickX, clickY, inTermX, inTermY);
        Console.WriteLine($"    Input terminal at ({inTermX:F1},{inTermY:F1}), dist={inDist:F1}, hitRadius={TerminalHitRadius}");

        if (inDist <= TerminalHitRadius)
        {
            Console.WriteLine($"    -> HIT INPUT");
            return "input";
        }

        // Use precise T2X/T2Y coordinates if set, otherwise fall back to TerminalLayout
        double outX, outY;
        TerminalDirection outDir;
        if (node.T2X.HasValue && node.T2Y.HasValue)
        {
            outX = node.T2X.Value;
            outY = node.T2Y.Value;
            outDir = TerminalLayouts.GetDirectionFromNormalizedPosition(outX, outY);
            Console.WriteLine($"    Using T2X/T2Y: ({outX},{outY}), dir={outDir}");
        }
        else
        {
            var (_, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);
            (outX, outY, outDir) = TerminalLayouts.GetPositionCoords(outputPos);
            Console.WriteLine($"    Using TerminalLayout for output: pos={outputPos}, coords=({outX},{outY}), dir={outDir}");
        }

        // Check output terminal - calculate position then apply rotation
        var outLocalX = outX * node.Width;
        var outLocalY = outY * node.Height;
        var (outStickX, outStickY) = GetStickOutOffsetForDirection(outDir, stickOut);
        var outTermLocalX = outLocalX + outStickX;
        var outTermLocalY = outLocalY + outStickY;

        // Apply rotation around node center if needed
        var (outTermX, outTermY) = ApplyRotation(outTermLocalX, outTermLocalY,
            node.Width / 2, node.Height / 2, node.Rotation);
        outTermX += node.X;
        outTermY += node.Y;

        var outDist = Distance(clickX, clickY, outTermX, outTermY);
        Console.WriteLine($"    Output terminal at ({outTermX:F1},{outTermY:F1}), dist={outDist:F1}");

        if (outDist <= TerminalHitRadius)
        {
            Console.WriteLine($"    -> HIT OUTPUT");
            return "output";
        }

        // Check third terminal if enabled
        if (node.HasThirdTerminal)
        {
            // Use precise T3X/T3Y coordinates if set, otherwise default to bottom center
            double thirdX = node.T3X ?? 0.5;
            double thirdY = node.T3Y ?? 1.0;
            var thirdDir = TerminalLayouts.GetDirectionFromNormalizedPosition(thirdX, thirdY);

            var thirdLocalX = thirdX * node.Width;
            var thirdLocalY = thirdY * node.Height;
            var (thirdStickX, thirdStickY) = GetStickOutOffsetForDirection(thirdDir, stickOut);
            var thirdTermLocalX = thirdLocalX + thirdStickX;
            var thirdTermLocalY = thirdLocalY + thirdStickY;

            // Apply rotation
            var (thirdTermX, thirdTermY) = ApplyRotation(thirdTermLocalX, thirdTermLocalY,
                node.Width / 2, node.Height / 2, node.Rotation);
            thirdTermX += node.X;
            thirdTermY += node.Y;

            if (Distance(clickX, clickY, thirdTermX, thirdTermY) <= TerminalHitRadius)
            {
                return "third";
            }
        }

        // Check extra terminals
        foreach (var extra in node.ExtraTerminals)
        {
            var (px, py, dir) = GetExtraTerminalPositionAbsolute(node, extra);
            var (stickX, stickY) = GetStickOutOffsetForDirection(dir, stickOut);

            // px, py are already absolute, convert to local for rotation
            var localX = px - node.X + stickX;
            var localY = py - node.Y + stickY;

            // Apply rotation
            var (termX, termY) = ApplyRotation(localX, localY,
                node.Width / 2, node.Height / 2, node.Rotation);
            termX += node.X;
            termY += node.Y;

            if (Distance(clickX, clickY, termX, termY) <= TerminalHitRadius)
            {
                return $"extra:{extra.Id}";
            }
        }

        return null;
    }

    /// <summary>
    /// Get the absolute position of an extra terminal
    /// </summary>
    private (double x, double y, TerminalDirection dir) GetExtraTerminalPositionAbsolute(Node node, ExtraTerminal terminal)
    {
        var offset = terminal.Position * 20; // 20px per position unit

        return terminal.Side.ToLower() switch
        {
            "left" => (node.X, node.Y + node.Height / 2 + offset, TerminalDirection.Left),
            "right" => (node.X + node.Width, node.Y + node.Height / 2 + offset, TerminalDirection.Right),
            "top" => (node.X + node.Width / 2 + offset, node.Y, TerminalDirection.Top),
            "bottom" => (node.X + node.Width / 2 + offset, node.Y + node.Height, TerminalDirection.Bottom),
            _ => (node.X, node.Y + node.Height / 2 + offset, TerminalDirection.Left)
        };
    }

    /// <summary>
    /// Get stick-out offset for a given direction
    /// </summary>
    private static (double x, double y) GetStickOutOffsetForDirection(TerminalDirection dir, double stickOut)
    {
        return dir switch
        {
            TerminalDirection.Left => (-stickOut, 0),
            TerminalDirection.Right => (stickOut, 0),
            TerminalDirection.Top => (0, -stickOut),
            TerminalDirection.Bottom => (0, stickOut),
            _ => (0, 0)
        };
    }

    /// <summary>
    /// Calculate distance between two points
    /// </summary>
    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Apply rotation transformation to a point around a center point.
    /// Matches the SVG transform="rotate(angle, cx, cy)" behavior.
    /// </summary>
    private static (double x, double y) ApplyRotation(double x, double y, double centerX, double centerY, int rotationDegrees)
    {
        if (rotationDegrees == 0) return (x, y);

        // Convert to radians
        var radians = rotationDegrees * Math.PI / 180.0;

        // Translate to origin (relative to center)
        var relX = x - centerX;
        var relY = y - centerY;

        // Rotate
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var rotatedX = relX * cos - relY * sin;
        var rotatedY = relX * sin + relY * cos;

        // Translate back
        return (rotatedX + centerX, rotatedY + centerY);
    }

    /// <summary>
    /// Get the connection side for a terminal type, accounting for node rotation
    /// </summary>
    private string GetTerminalSide(Node node, string terminalType)
    {
        string baseSide;

        if (terminalType == "input")
        {
            var (inputPos, _) = TerminalLayouts.ParseLayout(node.TerminalLayout);
            baseSide = inputPos;
        }
        else if (terminalType == "output")
        {
            var (_, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);
            baseSide = outputPos;
        }
        else if (terminalType == "third")
        {
            // Third terminal is always at bottom
            baseSide = "bottom";
        }
        else if (terminalType.StartsWith("extra:"))
        {
            var extraId = int.Parse(terminalType.Substring(6));
            var extra = node.ExtraTerminals.FirstOrDefault(t => t.Id == extraId);
            baseSide = extra?.Side ?? "left";
        }
        else
        {
            baseSide = "right";
        }

        // Apply rotation to get actual side
        return GetRotatedSide(baseSide, node.Rotation);
    }

    /// <summary>
    /// Get optimal connection points, using terminal positions when nodes have terminals enabled.
    /// This is the main entry point for determining edge connection points.
    /// Accounts for node rotation when determining terminal sides.
    /// </summary>
    private (ConnectionPoint from, ConnectionPoint to) GetSmartConnectionPoints(Node fromNode, Node toNode)
    {
        // If both nodes have terminals, use terminal-based connection points
        if (fromNode.ShowTerminals && toNode.ShowTerminals)
        {
            return GetTerminalConnectionPoints(fromNode, toNode);
        }

        // If only from node has terminals
        if (fromNode.ShowTerminals)
        {
            var (_, outputPos) = TerminalLayouts.ParseLayout(fromNode.TerminalLayout);
            // Apply rotation to get actual side
            var fromSide = GetRotatedSide(outputPos, fromNode.Rotation);
            var (_, toConn) = GeometryService.GetOptimalConnectionPoints(fromNode, toNode);
            return (new ConnectionPoint { Side = fromSide, Position = 0 }, toConn);
        }

        // If only to node has terminals
        if (toNode.ShowTerminals)
        {
            var (inputPos, _) = TerminalLayouts.ParseLayout(toNode.TerminalLayout);
            // Apply rotation to get actual side
            var toSide = GetRotatedSide(inputPos, toNode.Rotation);
            var (fromConn, _) = GeometryService.GetOptimalConnectionPoints(fromNode, toNode);
            return (fromConn, new ConnectionPoint { Side = toSide, Position = 0 });
        }

        // Neither has terminals, use standard geometry
        return GeometryService.GetOptimalConnectionPoints(fromNode, toNode);
    }

    /// <summary>
    /// Check if a connection between two terminals is allowed based on their types.
    /// Rules:
    /// - Input terminals can only connect TO Output terminals
    /// - Output terminals can only receive FROM Input terminals
    /// - Bidirectional terminals can connect to any terminal type
    /// </summary>
    /// <param name="fromNode">Source node</param>
    /// <param name="toNode">Target node</param>
    /// <param name="fromTerminal">Terminal type on source (null = use output terminal)</param>
    /// <param name="toTerminal">Terminal type on target (null = use input terminal)</param>
    /// <returns>True if connection is allowed, false otherwise</returns>
    private static bool IsConnectionAllowed(Node fromNode, Node toNode, string? fromTerminal = null, string? toTerminal = null)
    {
        // Polarity restrictions only apply when BOTH nodes have terminals showing
        // If either node doesn't have terminals, treat the connection as bidirectional (always allowed)
        if (!fromNode.ShowTerminals || !toNode.ShowTerminals)
        {
            return true;
        }

        var fromType = GetTerminalType(fromNode, fromTerminal ?? "output");
        var toType = GetTerminalType(toNode, toTerminal ?? "input");

        return IsTerminalConnectionAllowed(fromType, toType);
    }

    /// <summary>
    /// Check if two terminal types can connect
    /// </summary>
    private static bool IsTerminalConnectionAllowed(TerminalType fromType, TerminalType toType)
    {
        // Bidirectional can connect to anything
        if (fromType == TerminalType.Bidirectional || toType == TerminalType.Bidirectional)
        {
            return true;
        }

        // Input can only connect to Output
        if (fromType == TerminalType.Input)
        {
            return toType == TerminalType.Output;
        }

        // Output can only receive from Input
        if (fromType == TerminalType.Output)
        {
            return toType == TerminalType.Input;
        }

        return false;
    }

    /// <summary>
    /// Get the terminal type for a specific terminal on a node
    /// </summary>
    private static TerminalType GetTerminalType(Node node, string terminalId)
    {
        if (terminalId == "input")
        {
            return node.InputTerminalType;
        }
        else if (terminalId == "output")
        {
            return node.OutputTerminalType;
        }
        else if (terminalId == "third")
        {
            return node.ThirdTerminalType;
        }
        else if (terminalId.StartsWith("extra:"))
        {
            var extraId = int.Parse(terminalId.Substring(6));
            var extra = node.ExtraTerminals.FirstOrDefault(t => t.Id == extraId);
            return extra?.Type ?? TerminalType.Bidirectional;
        }

        return TerminalType.Bidirectional;
    }

    /// <summary>
    /// Get a user-friendly message explaining why a connection is not allowed
    /// </summary>
    private static string GetConnectionBlockedReason(Node fromNode, Node toNode, string? fromTerminal = null, string? toTerminal = null)
    {
        // If either node doesn't show terminals, connections are always allowed
        if (!fromNode.ShowTerminals || !toNode.ShowTerminals)
        {
            return ""; // Should never reach here since IsConnectionAllowed returns true
        }

        var fromType = GetTerminalType(fromNode, fromTerminal ?? "output");
        var toType = GetTerminalType(toNode, toTerminal ?? "input");

        if (fromType == TerminalType.Input && toType == TerminalType.Input)
        {
            return "Input terminals cannot connect to each other";
        }

        if (fromType == TerminalType.Output && toType == TerminalType.Output)
        {
            return "Output terminals cannot connect to each other";
        }

        if (fromType == TerminalType.Input && toType != TerminalType.Output)
        {
            return "Input terminals can only connect to Output terminals";
        }

        if (fromType == TerminalType.Output && toType != TerminalType.Input)
        {
            return "Output terminals can only receive from Input terminals";
        }

        return "Connection not allowed";
    }

    // ============================================
    // RUBBERBANDING - Terminal-to-terminal connections
    // ============================================

    /// <summary>
    /// Start a new connection from a terminal (rubberbanding)
    /// </summary>
    private void StartTerminalConnection(int nodeId, string terminalType)
    {
        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) return;

        // Get the terminal position as the starting point
        var termPos = GetTerminalPosition(node, terminalType);
        if (!termPos.HasValue) return;

        isRubberbanding = true;
        rubberbandFromNodeId = nodeId;
        rubberbandFromTerminal = terminalType;
        rubberbandEndX = termPos.Value.x;
        rubberbandEndY = termPos.Value.y;
        rubberbandEdgeId = null;  // New connection, not reconnecting
        rubberbandEdgeEnd = null;

        Console.WriteLine($"Started rubberbanding from node {nodeId}, terminal {terminalType}");
        StateHasChanged();
    }

    /// <summary>
    /// Detach an existing edge from a terminal and start rubberbanding
    /// </summary>
    private void DetachEdgeFromTerminal(int edgeId, string end)
    {
        var edge = edges.FirstOrDefault(e => e.Id == edgeId);
        if (edge == null) return;

        UndoService.SaveState(nodes, edges, edgeLabels);

        if (end == "from")
        {
            // Detaching from source - rubberband will reconnect the "from" end
            var toNode = nodes.FirstOrDefault(n => n.Id == edge.To);
            if (toNode == null) return;

            var toTermPos = GetTerminalPosition(toNode, edge.ToTerminal ?? "input");
            if (!toTermPos.HasValue) return;

            isRubberbanding = true;
            rubberbandFromNodeId = edge.To;
            rubberbandFromTerminal = edge.ToTerminal ?? "input";
            rubberbandEndX = toTermPos.Value.x;
            rubberbandEndY = toTermPos.Value.y;
            rubberbandEdgeId = edgeId;
            rubberbandEdgeEnd = "from";  // We're reconnecting the "from" end
        }
        else // "to"
        {
            // Detaching from target - rubberband will reconnect the "to" end
            var fromNode = nodes.FirstOrDefault(n => n.Id == edge.From);
            if (fromNode == null) return;

            var fromTermPos = GetTerminalPosition(fromNode, edge.FromTerminal ?? "output");
            if (!fromTermPos.HasValue) return;

            isRubberbanding = true;
            rubberbandFromNodeId = edge.From;
            rubberbandFromTerminal = edge.FromTerminal ?? "output";
            rubberbandEndX = fromTermPos.Value.x;
            rubberbandEndY = fromTermPos.Value.y;
            rubberbandEdgeId = edgeId;
            rubberbandEdgeEnd = "to";  // We're reconnecting the "to" end
        }

        Console.WriteLine($"Detached edge {edgeId} from {end} end, rubberbanding...");
        StateHasChanged();
    }

    /// <summary>
    /// Complete the rubberbanding connection to a terminal
    /// </summary>
    private void CompleteTerminalConnection(int targetNodeId, string targetTerminal)
    {
        Console.WriteLine($"CompleteTerminalConnection: targetNode={targetNodeId}, targetTerminal={targetTerminal}");
        Console.WriteLine($"  isRubberbanding={isRubberbanding}, rubberbandFromNodeId={rubberbandFromNodeId}");

        if (!isRubberbanding || !rubberbandFromNodeId.HasValue)
        {
            Console.WriteLine("  Early return: not rubberbanding or no from node");
            return;
        }

        var fromNode = nodes.FirstOrDefault(n => n.Id == rubberbandFromNodeId.Value);
        var toNode = nodes.FirstOrDefault(n => n.Id == targetNodeId);
        Console.WriteLine($"  fromNode={fromNode?.Id}, toNode={toNode?.Id}");

        if (fromNode == null || toNode == null)
        {
            Console.WriteLine("  Canceling: fromNode or toNode is null");
            CancelRubberbanding();
            return;
        }

        // Determine actual from/to based on what we're doing
        int actualFromNodeId, actualToNodeId;
        string? actualFromTerminal, actualToTerminal;

        if (rubberbandEdgeId.HasValue)
        {
            // Reconnecting an existing edge
            var edge = edges.FirstOrDefault(e => e.Id == rubberbandEdgeId.Value);
            if (edge == null)
            {
                CancelRubberbanding();
                return;
            }

            if (rubberbandEdgeEnd == "from")
            {
                // We're reconnecting the "from" end to the target terminal
                actualFromNodeId = targetNodeId;
                actualFromTerminal = targetTerminal;
                actualToNodeId = edge.To;
                actualToTerminal = edge.ToTerminal;
            }
            else // "to"
            {
                // We're reconnecting the "to" end to the target terminal
                actualFromNodeId = edge.From;
                actualFromTerminal = edge.FromTerminal;
                actualToNodeId = targetNodeId;
                actualToTerminal = targetTerminal;
            }

            // Check if connection is allowed
            var actualFromNode = nodes.FirstOrDefault(n => n.Id == actualFromNodeId);
            var actualToNode = nodes.FirstOrDefault(n => n.Id == actualToNodeId);
            if (actualFromNode == null || actualToNode == null ||
                !IsConnectionAllowed(actualFromNode, actualToNode, actualFromTerminal, actualToTerminal))
            {
                Console.WriteLine("Connection not allowed");
                CancelRubberbanding();
                return;
            }

            // Update the edge
            edge.From = actualFromNodeId;
            edge.To = actualToNodeId;
            edge.FromTerminal = actualFromTerminal;
            edge.ToTerminal = actualToTerminal;

            // Update connection points based on terminal positions
            var fromSide = GetTerminalSideFromPosition(actualFromNode!, actualFromTerminal);
            var toSide = GetTerminalSideFromPosition(actualToNode!, actualToTerminal);
            edge.FromConnection = new ConnectionPoint { Side = fromSide, Position = 0 };
            edge.ToConnection = new ConnectionPoint { Side = toSide, Position = 0 };
            edge.PathData = PathService.GetEdgePath(edge, nodes);

            Console.WriteLine($"Reconnected edge {edge.Id}: {actualFromNodeId}:{actualFromTerminal} -> {actualToNodeId}:{actualToTerminal}");
        }
        else
        {
            // Creating a new edge
            actualFromNodeId = rubberbandFromNodeId.Value;
            actualFromTerminal = rubberbandFromTerminal;
            actualToNodeId = targetNodeId;
            actualToTerminal = targetTerminal;

            // Don't create self-loops
            if (actualFromNodeId == actualToNodeId && actualFromTerminal == actualToTerminal)
            {
                CancelRubberbanding();
                return;
            }

            // Check if connection is allowed
            var fromTermType = GetTerminalType(fromNode, actualFromTerminal ?? "output");
            var toTermType = GetTerminalType(toNode, actualToTerminal ?? "input");
            Console.WriteLine($"  Checking connection: from {actualFromTerminal} (type={fromTermType}) -> to {actualToTerminal} (type={toTermType})");

            // TEMPORARILY DISABLED for debugging - allow all connections
            // if (!IsConnectionAllowed(fromNode, toNode, actualFromTerminal, actualToTerminal))
            // {
            //     Console.WriteLine($"  Connection NOT allowed: {fromTermType} -> {toTermType}");
            //     CancelRubberbanding();
            //     return;
            // }
            Console.WriteLine($"  Connection allowed (connectivity check DISABLED for debugging)");

            // Check for duplicate edge
            if (edges.Any(e => e.From == actualFromNodeId && e.To == actualToNodeId &&
                              e.FromTerminal == actualFromTerminal && e.ToTerminal == actualToTerminal))
            {
                Console.WriteLine("Edge already exists");
                CancelRubberbanding();
                return;
            }

            UndoService.SaveState(nodes, edges, edgeLabels);

            // Get actual terminal positions for smart routing
            var fromTermPos = GetTerminalPosition(fromNode, actualFromTerminal ?? "output");
            var toTermPos = GetTerminalPosition(toNode, actualToTerminal ?? "input");

            // Calculate optimal connection sides based on terminal positions
            // This ensures direct routing instead of going around when terminals are on same side
            var (fromSide, toSide) = GetOptimalSidesForTerminalConnection(
                fromNode, toNode, actualFromTerminal, actualToTerminal,
                fromTermPos, toTermPos);

            Console.WriteLine($"  Terminal positions: from=({fromTermPos?.x:F0},{fromTermPos?.y:F0}), to=({toTermPos?.x:F0},{toTermPos?.y:F0})");
            Console.WriteLine($"  Calculated sides: from={fromSide}, to={toSide}");

            var newEdge = CreateEdgeWithDefaults(actualFromNodeId, actualToNodeId,
                new ConnectionPoint { Side = fromSide, Position = 0 },
                new ConnectionPoint { Side = toSide, Position = 0 });
            newEdge.FromTerminal = actualFromTerminal;
            newEdge.ToTerminal = actualToTerminal;
            newEdge.PathData = PathService.GetEdgePath(newEdge, nodes);
            edges.Add(newEdge);

            Console.WriteLine($"Created new edge: {actualFromNodeId}:{actualFromTerminal} -> {actualToNodeId}:{actualToTerminal}");
            Console.WriteLine($"  Edge PathData: {newEdge.PathData ?? "(null)"}");
            Console.WriteLine($"  Edge FromConnection: {newEdge.FromConnection?.Side}, ToConnection: {newEdge.ToConnection?.Side}");
        }

        CancelRubberbanding();
        StateHasChanged();
    }

    /// <summary>
    /// Cancel the current rubberbanding operation
    /// </summary>
    private void CancelRubberbanding()
    {
        isRubberbanding = false;
        rubberbandFromNodeId = null;
        rubberbandFromTerminal = null;
        rubberbandEdgeId = null;
        rubberbandEdgeEnd = null;
        StateHasChanged();
    }

    /// <summary>
    /// Update the rubberband end position during mouse move
    /// </summary>
    private void UpdateRubberbandPosition(double x, double y)
    {
        if (!isRubberbanding) return;
        rubberbandEndX = x;
        rubberbandEndY = y;
        StateHasChanged();
    }

    /// <summary>
    /// Get optimal connection sides for terminal-to-terminal connections.
    /// Instead of always using the terminal's physical side, calculate based on
    /// relative positions to ensure direct routing (especially when both terminals
    /// are on the same side of their respective nodes).
    /// </summary>
    private (string fromSide, string toSide) GetOptimalSidesForTerminalConnection(
        Node fromNode, Node toNode,
        string? fromTerminal, string? toTerminal,
        (double x, double y)? fromTermPos, (double x, double y)? toTermPos)
    {
        // Fall back to physical terminal sides if positions not available
        if (!fromTermPos.HasValue || !toTermPos.HasValue)
        {
            return (
                GetTerminalSideFromPosition(fromNode, fromTerminal),
                GetTerminalSideFromPosition(toNode, toTerminal)
            );
        }

        var fromX = fromTermPos.Value.x;
        var fromY = fromTermPos.Value.y;
        var toX = toTermPos.Value.x;
        var toY = toTermPos.Value.y;

        // Calculate relative direction from source terminal to target terminal
        var dx = toX - fromX;
        var dy = toY - fromY;

        // Determine the best exit direction from source based on target position
        string fromSide;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            // Horizontal dominant - exit left or right
            fromSide = dx > 0 ? "right" : "left";
        }
        else
        {
            // Vertical dominant - exit top or bottom
            fromSide = dy > 0 ? "bottom" : "top";
        }

        // Determine the best entry direction to target based on source position
        string toSide;
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            // Horizontal dominant - enter from left or right
            toSide = dx > 0 ? "left" : "right";
        }
        else
        {
            // Vertical dominant - enter from top or bottom
            toSide = dy > 0 ? "top" : "bottom";
        }

        return (fromSide, toSide);
    }

    /// <summary>
    /// Get the side name based on terminal position (using precise coords if available)
    /// </summary>
    private string GetTerminalSideFromPosition(Node node, string? terminalType)
    {
        if (string.IsNullOrEmpty(terminalType)) terminalType = "output";

        double normX, normY;

        if (terminalType == "input")
        {
            if (node.T1X.HasValue && node.T1Y.HasValue)
            {
                normX = node.T1X.Value;
                normY = node.T1Y.Value;
            }
            else
            {
                var (inputPos, _) = TerminalLayouts.ParseLayout(node.TerminalLayout);
                return GetRotatedSide(inputPos, node.Rotation);
            }
        }
        else if (terminalType == "output")
        {
            if (node.T2X.HasValue && node.T2Y.HasValue)
            {
                normX = node.T2X.Value;
                normY = node.T2Y.Value;
            }
            else
            {
                var (_, outputPos) = TerminalLayouts.ParseLayout(node.TerminalLayout);
                return GetRotatedSide(outputPos, node.Rotation);
            }
        }
        else if (terminalType == "third")
        {
            normX = node.T3X ?? 0.5;
            normY = node.T3Y ?? 1.0;
        }
        else if (terminalType.StartsWith("extra:"))
        {
            var extraId = int.Parse(terminalType.Substring(6));
            var extra = node.ExtraTerminals.FirstOrDefault(t => t.Id == extraId);
            return GetRotatedSide(extra?.Side ?? "left", node.Rotation);
        }
        else
        {
            return "right";
        }

        var side = TerminalLayouts.GetSideFromNormalizedPosition(normX, normY);
        return GetRotatedSide(side, node.Rotation);
    }

    /// <summary>
    /// Find if clicking on a terminal that's already connected should detach that edge
    /// Returns the edge and which end ("from" or "to") if found
    /// </summary>
    private (Edge? edge, string? end) GetEdgeConnectedToTerminal(int nodeId, string terminalType)
    {
        // Check if there's an edge connected FROM this terminal
        var fromEdge = edges.FirstOrDefault(e => e.From == nodeId &&
            (e.FromTerminal == terminalType || (e.FromTerminal == null && terminalType == "output")));
        if (fromEdge != null)
            return (fromEdge, "from");

        // Check if there's an edge connected TO this terminal
        var toEdge = edges.FirstOrDefault(e => e.To == nodeId &&
            (e.ToTerminal == terminalType || (e.ToTerminal == null && terminalType == "input")));
        if (toEdge != null)
            return (toEdge, "to");

        return (null, null);
    }

    /// <summary>
    /// Get friendly terminal name (T1, T2, T3) from terminal type
    /// </summary>
    private static string GetTerminalLabel(string terminalType)
    {
        return terminalType switch
        {
            "input" => "T1 (input)",
            "output" => "T2 (output)",
            "third" => "T3",
            _ when terminalType.StartsWith("extra:") => $"Extra:{terminalType.Substring(6)}",
            _ => terminalType
        };
    }

    /// <summary>
    /// Handle a terminal click - either start new connection, detach existing, or complete connection
    /// </summary>
    private void HandleTerminalClick(int nodeId, string terminalType)
    {
        var terminalLabel = GetTerminalLabel(terminalType);
        Console.WriteLine($"");
        Console.WriteLine($"========================================");
        Console.WriteLine($"TERMINAL CLICKED: Node {nodeId}, {terminalLabel}");
        Console.WriteLine($"========================================");

        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null)
        {
            var termType = GetTerminalType(node, terminalType);
            Console.WriteLine($"  Node text: '{node.Text}'");
            Console.WriteLine($"  Terminal type: {termType}");
            Console.WriteLine($"  isRubberbanding: {isRubberbanding}");
            if (isRubberbanding && rubberbandFromNodeId.HasValue)
            {
                var fromNode = nodes.FirstOrDefault(n => n.Id == rubberbandFromNodeId.Value);
                Console.WriteLine($"  Rubberbanding FROM: Node {rubberbandFromNodeId} ({fromNode?.Text}), {GetTerminalLabel(rubberbandFromTerminal ?? "output")}");
            }
        }
        else
        {
            Console.WriteLine($"  ERROR: Node {nodeId} not found!");
            return;
        }

        if (isRubberbanding)
        {
            Console.WriteLine($"  -> COMPLETING connection to {terminalLabel}");
            Console.WriteLine($"  Current edges count BEFORE: {edges.Count}");
            // Complete the connection
            CompleteTerminalConnection(nodeId, terminalType);
            Console.WriteLine($"  Current edges count AFTER: {edges.Count}");
            Console.WriteLine($"========================================");
        }
        else
        {
            // Check if there's an edge connected to this terminal
            var (existingEdge, edgeEnd) = GetEdgeConnectedToTerminal(nodeId, terminalType);

            if (existingEdge != null && edgeEnd != null)
            {
                Console.WriteLine($"  -> DETACHING existing edge {existingEdge.Id} from {edgeEnd} end");
                // Detach the existing edge and start rubberbanding
                DetachEdgeFromTerminal(existingEdge.Id, edgeEnd);
            }
            else
            {
                Console.WriteLine($"  -> STARTING new connection from {terminalLabel}");
                // Start a new connection
                StartTerminalConnection(nodeId, terminalType);
            }
            Console.WriteLine($"========================================");
        }
    }
}
