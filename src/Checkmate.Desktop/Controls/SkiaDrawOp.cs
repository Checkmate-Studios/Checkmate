using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Checkmate.Desktop.Controls;

/// <summary>Lets us draw straight onto Avalonia's Skia canvas.</summary>
internal sealed class SkiaDrawOp : ICustomDrawOperation
{
    private readonly Rect _bounds;
    private readonly Action<SKCanvas> _paint;

    public SkiaDrawOp(Rect bounds, Action<SKCanvas> paint) { _bounds = bounds; _paint = paint; }

    public Rect Bounds => _bounds;
    public void Dispose() { }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature == null) return;
        using var lease = feature.Lease();
        var canvas = lease.SkCanvas;
        canvas.Save();
        canvas.ClipRect(new SKRect((float)_bounds.Left, (float)_bounds.Top, (float)_bounds.Right, (float)_bounds.Bottom));
        _paint(canvas);
        canvas.Restore();
    }
}
