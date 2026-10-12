using Checkmate.Core.Model;
using SkiaSharp;

namespace Checkmate.Desktop.Rendering;

/// <summary>
/// Keeps one document-sized bitmap per drawing. Finished strokes are painted into it once; the stroke
/// currently being drawn is NOT in the bitmap: the compositor draws it live on top, which keeps
/// translucent and textured brushes correct. Only touched from the render thread, inside lock(editor.SyncRoot).
/// </summary>
public sealed class CelCache
{
    private sealed class Entry
    {
        public required SKBitmap Bitmap;
        public int Revision;
        public int StrokeCount;   // finished strokes already painted into the bitmap
        public long LastUsed;
    }

    private const long Budget = 256L * 1024 * 1024;   // ~256 MB of cached drawings
    private readonly Dictionary<Cel, Entry> _map = new();
    private long _tick;

    public SKBitmap Get(Cel cel, int width, int height)
    {
        if (!_map.TryGetValue(cel, out var e) || e.Bitmap.Width != width || e.Bitmap.Height != height)
        {
            e?.Bitmap.Dispose();
            var bmp = new SKBitmap(width, height);
            bmp.Erase(SKColors.Transparent);
            e = new Entry { Bitmap = bmp, Revision = cel.Revision };
            _map[cel] = e;
            Evict(width, height, keep: cel);
        }

        if (e.Revision != cel.Revision) Reset(e, cel.Revision);
        Update(e, cel);
        e.LastUsed = ++_tick;
        return e.Bitmap;
    }

    private static void Reset(Entry e, int revision)
    {
        e.Bitmap.Erase(SKColors.Transparent);
        e.Revision = revision;
        e.StrokeCount = 0;
    }

    private static void Update(Entry e, Cel cel)
    {
        var strokes = cel.Strokes;
        int n = strokes.Count;
        while (n > 0 && !strokes[n - 1].Complete) n--;     // the live stroke is drawn by the compositor
        if (e.StrokeCount > n) Reset(e, e.Revision);       // safety net
        if (e.StrokeCount == n) return;

        using var canvas = new SKCanvas(e.Bitmap);
        for (int i = e.StrokeCount; i < n; i++) Compositor.DrawStroke(canvas, strokes[i]);
        e.StrokeCount = n;
    }

    private void Evict(int w, int h, Cel keep)
    {
        int max = (int)Math.Max(8, Budget / ((long)w * h * 4));
        while (_map.Count > max)
        {
            Cel? oldest = null; long oldestTick = long.MaxValue;
            foreach (var kv in _map)
                if (!ReferenceEquals(kv.Key, keep) && kv.Value.LastUsed < oldestTick)
                { oldest = kv.Key; oldestTick = kv.Value.LastUsed; }
            if (oldest == null) break;
            _map[oldest].Bitmap.Dispose();
            _map.Remove(oldest);
        }
    }
}
