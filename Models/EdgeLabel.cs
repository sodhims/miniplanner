namespace dfd2wasm.Models
{
    public class EdgeLabel
    {
        public int Id { get; set; }
        public int EdgeId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 80;
        public double Height { get; set; } = 30;
        public string Text { get; set; } = "Label";
    }
}
