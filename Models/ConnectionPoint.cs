namespace dfd2wasm.Models
{
    public class ConnectionPoint
    {
        public string Side { get; set; } = ""; // "top", "right", "bottom", "left"
        public int Position { get; set; }      // 0-4 for the 5 connection points per side
    }
}
