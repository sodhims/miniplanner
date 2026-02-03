using dfd2wasm.Models;
namespace dfd2wasm.Services
{
    using dfd2wasm.Services;
    using System.Text;

    public class ExportService
    {
        private readonly GeometryService _geometryService;
        private readonly PathService _pathService;

        public ExportService(GeometryService geometryService, PathService pathService)
        {
            _geometryService = geometryService;
            _pathService = pathService;
        }

        public string ExportToSVG(List<Node> nodes, List<Edge> edges, List<EdgeLabel> labels)
        {
            var svg = new StringBuilder();
            svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1920\" height=\"1080\">");
            svg.AppendLine("  <defs>");
            svg.AppendLine("    <marker id=\"arrowhead\" markerWidth=\"10\" markerHeight=\"10\" refX=\"5\" refY=\"3\" orient=\"auto\">");
            svg.AppendLine("      <polygon points=\"0 0, 10 3, 0 6\" fill=\"#374151\" />");
            svg.AppendLine("    </marker>");
            svg.AppendLine("  </defs>");

            // Export edges
            foreach (var edge in edges)
            {
                var path = _pathService.GetEdgePath(edge, nodes);
                svg.AppendLine($"  <path d=\"{path}\" stroke=\"#374151\" stroke-width=\"2\" fill=\"none\" />");
            }

            // Export nodes
            foreach (var node in nodes)
            {
                ExportNode(svg, node);
            }

            // Export labels
            foreach (var label in labels)
            {
                ExportLabel(svg, label);
            }

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        public string ExportToSVG(List<Node> nodes, List<Edge> edges, List<EdgeLabel> labels, 
            string background, int swimlaneCount, List<string> swimlaneLabels, 
            int columnCount, List<string> columnLabels)
        {
            var svg = new StringBuilder();
            svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"2000\" height=\"2000\" viewBox=\"0 0 2000 2000\">");
            
            // Add white background rectangle
            svg.AppendLine("  <rect x=\"0\" y=\"0\" width=\"2000\" height=\"2000\" fill=\"white\" />");
            
            svg.AppendLine("  <defs>");
            svg.AppendLine("    <marker id=\"arrowhead\" markerWidth=\"10\" markerHeight=\"7\" refX=\"9\" refY=\"3.5\" orient=\"auto\" markerUnits=\"strokeWidth\">");
            svg.AppendLine("      <polygon points=\"0 0, 10 3.5, 0 7\" fill=\"#374151\" />");
            svg.AppendLine("    </marker>");
            svg.AppendLine("  </defs>");

            // Export background FIRST (so it's behind everything else)
            ExportBackground(svg, background, swimlaneCount, swimlaneLabels, columnCount, columnLabels);

            // Export edges
            foreach (var edge in edges)
            {
                var path = _pathService.GetEdgePath(edge, nodes);
                var strokeColor = edge.StrokeColor ?? "#374151";
                var strokeWidth = edge.StrokeWidth;
                var dashArray = string.IsNullOrEmpty(edge.StrokeDashArray) ? "none" : edge.StrokeDashArray;
                svg.AppendLine($"  <path d=\"{path}\" stroke=\"{strokeColor}\" stroke-width=\"{strokeWidth}\" fill=\"none\" stroke-dasharray=\"{dashArray}\" marker-end=\"url(#arrowhead)\" />");
            }

            // Export nodes
            foreach (var node in nodes)
            {
                ExportNode(svg, node);
            }

            // Export labels
            foreach (var label in labels)
            {
                ExportLabel(svg, label);
            }

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        private void ExportBackground(StringBuilder svg, string background, int swimlaneCount, 
            List<string> swimlaneLabels, int columnCount, List<string> columnLabels)
        {
            if (background == "swimlanes-h")
            {
                var laneHeight = 2000.0 / swimlaneCount;
                var labelColumnWidth = 80.0;

                // Label column background
                svg.AppendLine($"  <rect x=\"0\" y=\"0\" width=\"{labelColumnWidth}\" height=\"2000\" fill=\"#e5e7eb\" stroke=\"#9ca3af\" stroke-width=\"2\" />");

                for (int i = 0; i < swimlaneCount; i++)
                {
                    var y = i * laneHeight;
                    var fillColor = i % 2 == 0 ? "#f9fafb" : "#ffffff";
                    var labelText = swimlaneLabels.Count > i ? swimlaneLabels[i] : $"Lane {i + 1}";
                    var escapedLabel = labelText.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

                    // Swimlane background
                    svg.AppendLine($"  <rect x=\"{labelColumnWidth}\" y=\"{y}\" width=\"{2000 - labelColumnWidth}\" height=\"{laneHeight}\" fill=\"{fillColor}\" stroke=\"#e5e7eb\" stroke-width=\"2\" />");

                    // Horizontal separator in label column
                    svg.AppendLine($"  <line x1=\"0\" y1=\"{y}\" x2=\"{labelColumnWidth}\" y2=\"{y}\" stroke=\"#9ca3af\" stroke-width=\"2\" />");

                    // Vertical text label
                    var textX = labelColumnWidth / 2;
                    var textY = y + laneHeight / 2;
                    svg.AppendLine($"  <text x=\"{textX}\" y=\"{textY}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"14\" font-weight=\"bold\" fill=\"#374151\" transform=\"rotate(-90 {textX} {textY})\">{escapedLabel}</text>");
                }
            }
            else if (background == "swimlanes-v")
            {
                var laneWidth = 2000.0 / swimlaneCount;
                var labelRowHeight = 40.0;

                // Label row background
                svg.AppendLine($"  <rect x=\"0\" y=\"0\" width=\"2000\" height=\"{labelRowHeight}\" fill=\"#e5e7eb\" stroke=\"#9ca3af\" stroke-width=\"2\" />");

                for (int i = 0; i < swimlaneCount; i++)
                {
                    var x = i * laneWidth;
                    var fillColor = i % 2 == 0 ? "#f9fafb" : "#ffffff";
                    var labelText = swimlaneLabels.Count > i ? swimlaneLabels[i] : $"Lane {i + 1}";
                    var escapedLabel = labelText.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

                    // Swimlane background
                    svg.AppendLine($"  <rect x=\"{x}\" y=\"{labelRowHeight}\" width=\"{laneWidth}\" height=\"{2000 - labelRowHeight}\" fill=\"{fillColor}\" stroke=\"#e5e7eb\" stroke-width=\"2\" />");

                    // Vertical separator in label row
                    svg.AppendLine($"  <line x1=\"{x}\" y1=\"0\" x2=\"{x}\" y2=\"{labelRowHeight}\" stroke=\"#9ca3af\" stroke-width=\"2\" />");

                    // Text label
                    svg.AppendLine($"  <text x=\"{x + laneWidth / 2}\" y=\"{labelRowHeight / 2}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"14\" font-weight=\"bold\" fill=\"#374151\">{escapedLabel}</text>");
                }
            }
            else if (background == "columns")
            {
                var columnWidth = 2000.0 / columnCount;
                var labelRowHeight = 40.0;

                // Label row background
                svg.AppendLine($"  <rect x=\"0\" y=\"0\" width=\"2000\" height=\"{labelRowHeight}\" fill=\"#e5e7eb\" stroke=\"#9ca3af\" stroke-width=\"2\" />");

                for (int i = 0; i < columnCount; i++)
                {
                    var x = i * columnWidth;
                    var labelText = columnLabels.Count > i ? columnLabels[i] : $"Column {i + 1}";
                    var escapedLabel = labelText.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

                    // Column guide line
                    svg.AppendLine($"  <line x1=\"{x}\" y1=\"{labelRowHeight}\" x2=\"{x}\" y2=\"2000\" stroke=\"#e5e7eb\" stroke-width=\"2\" stroke-dasharray=\"10,5\" />");

                    // Vertical separator in label row
                    svg.AppendLine($"  <line x1=\"{x}\" y1=\"0\" x2=\"{x}\" y2=\"{labelRowHeight}\" stroke=\"#9ca3af\" stroke-width=\"2\" />");

                    // Text label
                    svg.AppendLine($"  <text x=\"{x + columnWidth / 2}\" y=\"{labelRowHeight / 2}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"14\" font-weight=\"bold\" fill=\"#374151\">{escapedLabel}</text>");
                }
            }
        }

        private void ExportNode(StringBuilder svg, Node node)
        {
            // Use node properties or defaults
            var strokeColor = node.StrokeColor ?? "#374151";
            var fillColor = node.FillColor ?? "white";
            var strokeWidth = node.StrokeWidth ?? 2;
            var cornerRadius = node.CornerRadius ?? 4;
            var dashArray = node.StrokeDashArray;
            var dashAttr = string.IsNullOrEmpty(dashArray) ? "" : $" stroke-dasharray=\"{dashArray}\"";

            // If stroke width is 0, use "none" for stroke
            var strokeAttr = strokeWidth == 0 ? "none" : strokeColor;

            switch (node.Shape)
            {
                case NodeShape.Rectangle:
                    svg.AppendLine($"  <rect x=\"{node.X}\" y=\"{node.Y}\" width=\"{node.Width}\" height=\"{node.Height}\" fill=\"{fillColor}\" stroke=\"{strokeAttr}\" stroke-width=\"{strokeWidth}\" rx=\"{cornerRadius}\"{dashAttr} />");
                    break;
                case NodeShape.Ellipse:
                    svg.AppendLine($"  <ellipse cx=\"{node.X + node.Width/2}\" cy=\"{node.Y + node.Height/2}\" rx=\"{node.Width/2}\" ry=\"{node.Height/2}\" fill=\"{fillColor}\" stroke=\"{strokeAttr}\" stroke-width=\"{strokeWidth}\"{dashAttr} />");
                    break;
                case NodeShape.Diamond:
                    var diamondMidX = node.X + node.Width/2;
                    var diamondMidY = node.Y + node.Height/2;
                    svg.AppendLine($"  <polygon points=\"{diamondMidX},{node.Y} {node.X + node.Width},{diamondMidY} {diamondMidX},{node.Y + node.Height} {node.X},{diamondMidY}\" fill=\"{fillColor}\" stroke=\"{strokeAttr}\" stroke-width=\"{strokeWidth}\"{dashAttr} />");
                    break;
                case NodeShape.Parallelogram:
                    var skew = 15.0;
                    svg.AppendLine($"  <polygon points=\"{node.X + skew},{node.Y} {node.X + node.Width},{node.Y} {node.X + node.Width - skew},{node.Y + node.Height} {node.X},{node.Y + node.Height}\" fill=\"{fillColor}\" stroke=\"{strokeAttr}\" stroke-width=\"{strokeWidth}\"{dashAttr} />");
                    break;
                case NodeShape.Cylinder:
                    var rx = node.Width / 2;
                    var ellipseRy = 10.0;
                    var cy1 = node.Y + ellipseRy;
                    var cy2 = node.Y + node.Height - ellipseRy;
                    var cx = node.X + rx;
                    // Main body path
                    svg.AppendLine($"  <path d=\"M {node.X},{cy1} Q {node.X},{cy1 - ellipseRy} {cx},{cy1 - ellipseRy} Q {node.X + node.Width},{cy1 - ellipseRy} {node.X + node.Width},{cy1} L {node.X + node.Width},{cy2} Q {node.X + node.Width},{cy2 + ellipseRy} {cx},{cy2 + ellipseRy} Q {node.X},{cy2 + ellipseRy} {node.X},{cy2} Z\" fill=\"{fillColor}\" stroke=\"{strokeAttr}\" stroke-width=\"{strokeWidth}\"{dashAttr} />");
                    // Top arc visible part
                    svg.AppendLine($"  <path d=\"M {node.X},{cy1} Q {node.X},{cy1 + ellipseRy / 2} {cx},{cy1 + ellipseRy / 2} Q {node.X + node.Width},{cy1 + ellipseRy / 2} {node.X + node.Width},{cy1}\" fill=\"none\" stroke=\"{strokeAttr}\" stroke-width=\"{strokeWidth}\"{dashAttr} />");
                    break;
            }

            // Export embedded image/SVG attachments
            var imageAttachment = node.Attachments?.FirstOrDefault(a => a.FileType == AttachmentType.Svg || a.FileType == AttachmentType.Image);
            if (imageAttachment != null)
            {
                // Calculate image position inside node with padding
                var padding = 4.0;
                var imgX = node.X + padding;
                var imgY = node.Y + padding;
                var imgWidth = node.Width - (padding * 2);
                var imgHeight = node.Height - (padding * 2);

                // Leave room for text at bottom if node has text
                if (!string.IsNullOrWhiteSpace(node.Text))
                {
                    imgHeight = imgHeight * 0.7; // Use 70% for image, 30% for text
                }

                svg.AppendLine($"  <image x=\"{imgX}\" y=\"{imgY}\" width=\"{imgWidth}\" height=\"{imgHeight}\" href=\"{imageAttachment.DataUri}\" preserveAspectRatio=\"xMidYMid meet\" />");
            }

            // Export text with proper handling for multiline
            var escapedText = node.Text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var lines = escapedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Adjust text Y position if there's an image attachment
            double textYCenter = node.Y + node.Height / 2;
            if (imageAttachment != null && !string.IsNullOrWhiteSpace(node.Text))
            {
                // Position text in the bottom 30% of the node
                textYCenter = node.Y + node.Height * 0.85;
            }

            if (lines.Length <= 1)
            {
                svg.AppendLine($"  <text x=\"{node.X + node.Width / 2}\" y=\"{textYCenter}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-family=\"Arial, sans-serif\" font-size=\"14\" fill=\"#374151\">{escapedText}</text>");
            }
            else
            {
                // Handle multiline text
                var lineHeight = 18;
                var totalHeight = lines.Length * lineHeight;
                var startY = textYCenter - totalHeight / 2 + lineHeight / 2;

                for (int i = 0; i < lines.Length; i++)
                {
                    svg.AppendLine($"  <text x=\"{node.X + node.Width / 2}\" y=\"{startY + i * lineHeight}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-family=\"Arial, sans-serif\" font-size=\"14\" fill=\"#374151\">{lines[i]}</text>");
                }
            }
        }

        private void ExportLabel(StringBuilder svg, EdgeLabel label)
        {
            svg.AppendLine($"  <rect x=\"{label.X}\" y=\"{label.Y}\" width=\"{label.Width}\" height=\"{label.Height}\" fill=\"white\" stroke=\"#374151\" stroke-width=\"1\" rx=\"4\" />");
            var escapedText = label.Text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            svg.AppendLine($"  <text x=\"{label.X + label.Width / 2}\" y=\"{label.Y + label.Height / 2}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-family=\"Arial, sans-serif\" font-size=\"12\" fill=\"#374151\">{escapedText}</text>");
        }
    }
}
