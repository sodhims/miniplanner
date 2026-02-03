using dfd2wasm.Models;
namespace dfd2wasm.Services
{
    using dfd2wasm.Services;

    public class GeometryService
    {
        public const int GridSize = 20;
        public const int OrthoSpacing = 140;
        public const int ConnectionPointSpacing = 15;
        public const int ColumnHeightLimit = 10000;

        public double SnapToGrid(double value, bool enabled)
        {
            return enabled ? Math.Round(value / GridSize) * GridSize : value;
        }

        public (double X, double Y) GetConnectionPointCoordinates(Node node, string side, int position)
        {
            var offset = position * ConnectionPointSpacing;

            // Calculate relative position (before rotation)
            double relX, relY;
            switch (side)
            {
                case "top":
                    relX = node.Width / 2 + offset;
                    relY = 0;
                    break;
                case "bottom":
                    relX = node.Width / 2 + offset;
                    relY = node.Height;
                    break;
                case "left":
                    relX = 0;
                    relY = node.Height / 2 + offset;
                    break;
                case "right":
                    relX = node.Width;
                    relY = node.Height / 2 + offset;
                    break;
                default:
                    relX = node.Width / 2;
                    relY = node.Height / 2;
                    break;
            }

            // Apply rotation if node is rotated
            if (node.Rotation != 0)
            {
                (relX, relY) = ApplyRotation(relX, relY, node.Width / 2, node.Height / 2, node.Rotation);
            }

            return (node.X + relX, node.Y + relY);
        }

        /// <summary>
        /// Apply rotation to a point around a center
        /// </summary>
        private (double x, double y) ApplyRotation(double x, double y, double centerX, double centerY, int rotationDegrees)
        {
            double radians = rotationDegrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            // Translate to origin
            double dx = x - centerX;
            double dy = y - centerY;

            // Rotate
            double rotatedX = dx * cos - dy * sin;
            double rotatedY = dx * sin + dy * cos;

            // Translate back
            return (rotatedX + centerX, rotatedY + centerY);
        }

        /// <summary>
        /// Check if a node uses a circular shape
        /// </summary>
        public bool IsCircularShape(Node node)
        {
            return node.Shape == Models.NodeShape.Ellipse ||
                   node.TemplateShapeId == "chance" ||
                   node.TemplateShapeId == "probability" ||
                   node.TemplateShapeId == "branch-point" ||
                   node.TemplateShapeId == "start-event" ||
                   node.TemplateShapeId == "end-event" ||
                   node.TemplateShapeId == "intermediate-event" ||
                   node.TemplateShapeId == "router" ||
                   node.TemplateShapeId == "internet";
        }

        /// <summary>
        /// Calculate the intersection point where a line from an external point meets a circle's perimeter
        /// </summary>
        public (double X, double Y) GetCircleEdgeIntersection(Node circleNode, double fromX, double fromY)
        {
            var cx = circleNode.X + circleNode.Width / 2;
            var cy = circleNode.Y + circleNode.Height / 2;
            var r = Math.Min(circleNode.Width, circleNode.Height) / 2;

            // Calculate direction from circle center to the external point
            var dx = fromX - cx;
            var dy = fromY - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 0.001)
            {
                // Point is at center, default to right side
                return (cx + r, cy);
            }

            // Normalize and scale to radius to get point on circle perimeter
            return (cx + (dx / dist) * r, cy + (dy / dist) * r);
        }

        public ConnectionPoint FindClosestConnectionPoint(Node node, double clickX, double clickY)
        {
            var sides = new[] { "top", "bottom", "left", "right" };
            var positions = new[] { -2, -1, 0, 1, 2 };

            ConnectionPoint closest = null;
            double minDistance = double.MaxValue;

            foreach (var side in sides)
            {
                foreach (var pos in positions)
                {
                    var (cx, cy) = GetConnectionPointCoordinates(node, side, pos);
                    var distance = Math.Sqrt(Math.Pow(clickX - cx, 2) + Math.Pow(clickY - cy, 2));

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closest = new ConnectionPoint { Side = side, Position = pos };
                    }
                }
            }

            return closest ?? new ConnectionPoint { Side = "right", Position = 0 };
        }
        public (ConnectionPoint from, ConnectionPoint to) GetOptimalConnectionPoints(Node fromNode, Node toNode)
        {
            // Calculate centers
            var fromCenterX = fromNode.X + fromNode.Width / 2;
            var fromCenterY = fromNode.Y + fromNode.Height / 2;
            var toCenterX = toNode.X + toNode.Width / 2;
            var toCenterY = toNode.Y + toNode.Height / 2;

            // Calculate angle between nodes (in degrees)
            var dx = toCenterX - fromCenterX;
            var dy = toCenterY - fromCenterY;
            var angle = Math.Atan2(dy, dx) * 180 / Math.PI;

            // Use angle-based selection with 30° boundaries for better arrow visibility
            // This ensures arrows always approach at reasonable angles (at least 60° from the side)
            // Angle ranges:
            //   -30 to 30: right/left (horizontal)
            //   30 to 150: bottom/top (downward)
            //   150 to 180 or -180 to -150: left/right (horizontal, reversed)
            //   -150 to -30: top/bottom (upward)
            
            string fromSide, toSide;

            if (angle >= -30 && angle <= 30)
            {
                // Target is to the right
                fromSide = "right";
                toSide = "left";
            }
            else if (angle > 30 && angle <= 150)
            {
                // Target is below
                fromSide = "bottom";
                toSide = "top";
            }
            else if (angle > 150 || angle <= -150)
            {
                // Target is to the left
                fromSide = "left";
                toSide = "right";
            }
            else // angle > -150 && angle < -30
            {
                // Target is above
                fromSide = "top";
                toSide = "bottom";
            }

            return (
                new ConnectionPoint { Side = fromSide, Position = 0 },
                new ConnectionPoint { Side = toSide, Position = 0 }
            );
        }

        public (double X, double Y) CalculateOrthoPlacement(
            List<Node> nodes,
            double clickX,
            double clickY,
            bool snapEnabled)
        {
            const double epsilon = 5.0;

            // First node - center on click
            if (nodes.Count == 0)
            {
                double x = clickX - 60;
                double y = clickY - 30;
                return (SnapToGrid(x, snapEnabled), SnapToGrid(y, snapEnabled));
            }

            var lastNode = nodes[^1];
            var lastNodeBottom = lastNode.Y + lastNode.Height;

            // Check if click X is within the last node's column (with margin)
            var margin = 40.0; // pixels of margin on each side
            var isInSameColumn = clickX >= (lastNode.X - margin) &&
                                clickX <= (lastNode.X + lastNode.Width + margin);

            // Check if click Y is below the last node
            var isBelow = clickY > (lastNodeBottom + epsilon);

            double targetX, targetY;

            if (isInSameColumn && isBelow)
            {
                // Click is below and horizontally aligned - stack vertically
                targetX = lastNode.X;
                targetY = lastNodeBottom + OrthoSpacing;

                // No height limit for now - just keep stacking
                // (You can scroll if needed)
            }
            else if (isInSameColumn && !isBelow)
            {
                // Click is above or at same height in same column
                if (clickY < lastNode.Y - epsilon)
                {
                    // Place above
                    targetX = lastNode.X;
                    targetY = lastNode.Y - lastNode.Height - OrthoSpacing;
                    if (targetY < 0) targetY = 0;
                }
                else
                {
                    // Same height - place to the right
                    targetX = lastNode.X + lastNode.Width + OrthoSpacing;
                    targetY = lastNode.Y;
                }
            }
            else
            {
                // Click is in a different column
                var topY = nodes.Min(n => n.Y);

                if (clickX > lastNode.X + lastNode.Width)
                {
                    // New column to the right
                    targetX = lastNode.X + lastNode.Width + OrthoSpacing;
                    targetY = topY;
                }
                else
                {
                    // New column to the left
                    targetX = lastNode.X - lastNode.Width - OrthoSpacing;
                    targetY = topY;
                    if (targetX < 0) targetX = 0;
                }
            }

            // Apply snapping
            targetX = SnapToGrid(targetX, snapEnabled);
            targetY = SnapToGrid(targetY, snapEnabled);

            // Simple collision check
            int nudgeAttempts = 0;
            while (nudgeAttempts < 10 && nodes.Any(n =>
                Math.Abs(n.X - targetX) < epsilon &&
                Math.Abs(n.Y - targetY) < epsilon))
            {
                targetY = SnapToGrid(targetY + GridSize, snapEnabled);
                nudgeAttempts++;
            }

            // Clamp to canvas bounds
            targetX = Math.Max(0, Math.Min(1800, targetX));
            targetY = Math.Max(0, Math.Min(1800, targetY));

            return (targetX, targetY);
        }

        // Helper: Find the topmost Y position across all columns
        private double FindTopOfNewColumn(List<IGrouping<double, Node>> columns)
        {
            if (columns.Count == 0) return 0;

            var topY = columns
                .SelectMany(col => col)
                .Min(n => n.Y);

            return topY;
        }

        // Helper: Check for collisions and nudge position if needed
        private (double X, double Y) AvoidCollisions(
            double x, double y, double width, double height,
            List<Node> nodes, bool snapEnabled)
        {
            const int maxAttempts = 20;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                bool collision = nodes.Any(n =>
                    !(x + width < n.X ||
                      x > n.X + n.Width ||
                      y + height < n.Y ||
                      y > n.Y + n.Height));

                if (!collision) return (x, y);

                // Nudge down by grid size
                y = SnapToGrid(y + GridSize, snapEnabled);
                attempts++;
            }

            // If still colliding after max attempts, nudge right
            x = SnapToGrid(x + OrthoSpacing, snapEnabled);
            y = FindTopOfNewColumn(nodes.GroupBy(n => SnapToGrid(n.X, snapEnabled)).ToList());

            return (x, y);
        }

        /// <summary>
        /// Bundle edges from a single node - all edges going same direction share center point
        /// Single edges are also centered.
        /// </summary>
        public void BundleEdgesFromNode(int nodeId, List<Node> nodes, List<Edge> edges)
        {
            var sourceNode = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (sourceNode == null) return;

            var outgoingEdges = edges.Where(e => e.From == nodeId).ToList();
            if (outgoingEdges.Count == 0) return;

            // Group edges by direction to target
            var edgesByDirection = new Dictionary<string, List<Edge>>();

            foreach (var edge in outgoingEdges)
            {
                var targetNode = nodes.FirstOrDefault(n => n.Id == edge.To);
                if (targetNode == null) continue;

                // Calculate direction to target
                var dx = (targetNode.X + targetNode.Width / 2) - (sourceNode.X + sourceNode.Width / 2);
                var dy = (targetNode.Y + targetNode.Height / 2) - (sourceNode.Y + sourceNode.Height / 2);

                string direction;
                if (Math.Abs(dx) > Math.Abs(dy))
                    direction = dx > 0 ? "right" : "left";
                else
                    direction = dy > 0 ? "bottom" : "top";

                if (!edgesByDirection.ContainsKey(direction))
                    edgesByDirection[direction] = new List<Edge>();
                edgesByDirection[direction].Add(edge);
            }

            // Center all edges on their side (bundle multiple, center single)
            foreach (var group in edgesByDirection)
            {
                foreach (var edge in group.Value)
                {
                    edge.FromConnection = new ConnectionPoint { Side = group.Key, Position = 0 };
                }
            }
        }

        /// <summary>
        /// Bundle incoming edges to a single node - all edges coming from same direction share center point
        /// Single edges are also centered.
        /// </summary>
        public void BundleEdgesToNode(int nodeId, List<Node> nodes, List<Edge> edges)
        {
            var targetNode = nodes.FirstOrDefault(n => n.Id == nodeId);
            if (targetNode == null) return;

            var incomingEdges = edges.Where(e => e.To == nodeId).ToList();
            if (incomingEdges.Count == 0) return;

            // Group edges by direction from source
            var edgesByDirection = new Dictionary<string, List<Edge>>();

            foreach (var edge in incomingEdges)
            {
                var sourceNode = nodes.FirstOrDefault(n => n.Id == edge.From);
                if (sourceNode == null) continue;

                // Calculate direction FROM source (where the arrow is coming from)
                var dx = (sourceNode.X + sourceNode.Width / 2) - (targetNode.X + targetNode.Width / 2);
                var dy = (sourceNode.Y + sourceNode.Height / 2) - (targetNode.Y + targetNode.Height / 2);

                string direction;
                if (Math.Abs(dx) > Math.Abs(dy))
                    direction = dx > 0 ? "right" : "left";
                else
                    direction = dy > 0 ? "bottom" : "top";

                if (!edgesByDirection.ContainsKey(direction))
                    edgesByDirection[direction] = new List<Edge>();
                edgesByDirection[direction].Add(edge);
            }

            // Center all edges on their side (bundle multiple, center single)
            foreach (var group in edgesByDirection)
            {
                foreach (var edge in group.Value)
                {
                    edge.ToConnection = new ConnectionPoint { Side = group.Key, Position = 0 };
                }
            }
        }

        /// <summary>
        /// Bundle all edges in the diagram (both outgoing and incoming)
        /// </summary>
        public void BundleAllEdges(List<Node> nodes, List<Edge> edges)
        {
            // Bundle outgoing edges
            var sourceNodeIds = edges.Select(e => e.From).Distinct().ToList();
            foreach (var nodeId in sourceNodeIds)
            {
                BundleEdgesFromNode(nodeId, nodes, edges);
            }

            // Bundle incoming edges
            var targetNodeIds = edges.Select(e => e.To).Distinct().ToList();
            foreach (var nodeId in targetNodeIds)
            {
                BundleEdgesToNode(nodeId, nodes, edges);
            }
        }
    }
}
