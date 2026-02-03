namespace dfd2wasm.Models
{
    public class EditorState
    {
        public List<Node> Nodes { get; set; } = new();
        public List<Edge> Edges { get; set; } = new();
        public List<EdgeLabel> EdgeLabels { get; set; } = new();
        public List<FreehandStroke> FreehandStrokes { get; set; } = new();
        public List<DrawingShape> DrawingShapes { get; set; } = new();
    }
}
