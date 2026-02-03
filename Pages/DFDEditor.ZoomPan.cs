namespace dfd2wasm.Pages;

public partial class DFDEditor
{
    // Note: zoomLevel and zoomLevels defined in DFDEditor.razor.cs
    // zoomLevels = { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0 }

    private void ZoomIn()
    {
        // Find the next level up from current zoom
        var nextLevel = zoomLevels.FirstOrDefault(z => z > zoomLevel + 0.001);
        if (nextLevel > 0)
        {
            zoomLevel = nextLevel;
        }
        else if (zoomLevel < 3.0)
        {
            zoomLevel = 3.0;
        }
        StateHasChanged();
    }

    private void ZoomOut()
    {
        // Find the next level down from current zoom
        var prevLevel = zoomLevels.LastOrDefault(z => z < zoomLevel - 0.001);
        if (prevLevel > 0)
        {
            zoomLevel = prevLevel;
        }
        else if (zoomLevel > 0.25)
        {
            zoomLevel = 0.25;
        }
        StateHasChanged();
    }

    private void ResetZoom()
    {
        zoomLevel = 1.0;
        StateHasChanged();
    }

    private void ZoomToFit()
    {
        if (nodes.Count == 0) return;

        // Find bounding box of all nodes
        double minX = nodes.Min(n => n.X);
        double minY = nodes.Min(n => n.Y);
        double maxX = nodes.Max(n => n.X + n.Width);
        double maxY = nodes.Max(n => n.Y + n.Height);

        double contentWidth = maxX - minX + 100;
        double contentHeight = maxY - minY + 100;

        // Assume canvas viewport roughly 1200x800
        double vpWidth = 1200;
        double vpHeight = 800;

        // Calculate zoom to fit
        double idealZoom = Math.Min(vpWidth / contentWidth, vpHeight / contentHeight);
        
        // Clamp to valid range
        idealZoom = Math.Max(0.25, Math.Min(3.0, idealZoom));
        
        // Find closest zoom level that fits
        zoomLevel = zoomLevels
            .Where(z => z <= idealZoom || z == zoomLevels[0])
            .Max();
        
        StateHasChanged();
    }
}
