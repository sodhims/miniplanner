using dfd2wasm.Models;
namespace dfd2wasm.Services
{
    using dfd2wasm.Services;
    using System.Text.Json;

    public class UndoService
    {
        private readonly Stack<EditorState> _undoStack = new();
        private const int MaxUndoSteps = 50;

        public void SaveState(List<Node> nodes, List<Edge> edges, List<EdgeLabel> labels)
        {
            SaveState(nodes, edges, labels, new List<FreehandStroke>());
        }

        public void SaveState(List<Node> nodes, List<Edge> edges, List<EdgeLabel> labels, List<FreehandStroke> strokes, List<DrawingShape>? shapes = null)
        {
            var state = new EditorState
            {
                Nodes = DeepCopy(nodes),
                Edges = DeepCopy(edges),
                EdgeLabels = DeepCopy(labels),
                FreehandStrokes = DeepCopy(strokes),
                DrawingShapes = DeepCopy(shapes ?? new List<DrawingShape>())
            };

            _undoStack.Push(state);

            while (_undoStack.Count > MaxUndoSteps)
            {
                var temp = new Stack<EditorState>();
                for (int i = 0; i < MaxUndoSteps; i++)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack.Clear();
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }
        }

        public EditorState? Undo()
        {
            return _undoStack.Count > 0 ? _undoStack.Pop() : null;
        }

        public bool CanUndo => _undoStack.Count > 0;

        public bool TryUndo(out EditorState? state)
        {
            if (_undoStack.Count > 0)
            {
                state = _undoStack.Pop();
                return true;
            }
            state = null;
            return false;
        }

        private T DeepCopy<T>(T obj)
        {
            if (obj is null) return default!;
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json)!;
        }
    }
}
