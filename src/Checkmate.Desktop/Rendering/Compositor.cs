using Checkmate.Core.Model;
using SkiaSharp;

namespace Checkmate.Desktop.Rendering;

public static class Compositor
{
    private static readonly SKColor BeforeTint = new(255, 80, 80);   // red ghosts: previous drawings
    private static readonly SKColor AfterTint = new(60, 200, 90);    // green ghosts: next drawings
    private static SKShader? _grain;

    // =====================================================================
    //  Page composition
    // =====================================================================

    public static void DrawShadow(SKCanvas canvas, Project p)
    {
        using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12);
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 110), IsAntialias = true, MaskFilter = blur };
        canvas.DrawRect(new SKRect(0, 8, p.Width, p.Height + 8), shadow);
    }

    /// <summary>Fast path for the editor view: cached bitmaps, live stroke on top, onion skin.</summary>
    public static void DrawPageCached(SKCanvas canvas, Project p, int frame, int activeLayer,
                                      OnionSkinSettings? onion, CelCache cache)
    {
        int w = p.Width, h = p.Height;
        using (var paper = new SKPaint { Color = SKColors.White })
            canvas.DrawRect(new SKRect(0, 0, w, h), paper);

        using var paint = new SKPaint { FilterQuality = SKFilterQuality.Medium, Color = SKColors.White };

        for (int li = 0; li < p.Layers.Count; li++)
        {
            var layer = p.Layers[li];
            if (!layer.Visible) continue;

            if (li == activeLayer && onion is { Enabled: true })
                DrawOnion(canvas, p, layer, frame, onion, cache);

            var cel = layer.GetCelAt(frame);
            if (cel == null) continue;

            var bitmap = cache.Get(cel, w, h);
            byte alpha = (byte)Math.Clamp(layer.Opacity * 255, 0, 255);
            var live = cel.Strokes.Count > 0 && !cel.Strokes[^1].Complete ? cel.Strokes[^1] : null;

            if (live == null)
            {
                paint.ColorFilter = null;
                paint.Color = SKColors.White.WithAlpha(alpha);
                canvas.DrawBitmap(bitmap, 0, 0, paint);
            }
            else
            {
                // Draw the unfinished stroke on top of the cached drawing inside one offscreen layer,
                // so erasers cut through it and translucent brushes blend correctly.
                using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
                canvas.SaveLayer(layerPaint);
                paint.ColorFilter = null;
                paint.Color = SKColors.White;
                canvas.DrawBitmap(bitmap, 0, 0, paint);
                DrawStroke(canvas, live);
                canvas.Restore();
            }
        }
    }

    private static void DrawOnion(SKCanvas canvas, Project p, Layer layer, int frame,
                                  OnionSkinSettings onion, CelCache cache)
    {
        // A held drawing appears on several frames; never ghost the one we are already showing.
        var seen = new HashSet<Cel>();
        var current = layer.GetCelAt(frame);
        if (current != null) seen.Add(current);

        void Ghost(int f, SKColor tint, int distance)
        {
            if (f < 0 || f >= p.FrameCount) return;
            var cel = layer.GetCelAt(f);
            if (cel == null || cel.Strokes.Count == 0 || !seen.Add(cel)) return;

            using var filter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.SrcIn);
            using var paint = new SKPaint
            {
                FilterQuality = SKFilterQuality.Medium,
                ColorFilter = filter,
                Color = SKColors.White.WithAlpha((byte)(150 / distance)),
            };
            canvas.DrawBitmap(cache.Get(cel, p.Width, p.Height), 0, 0, paint);
        }

        for (int d = 1; d <= onion.Before; d++) Ghost(frame - d, BeforeTint, d);
        for (int d = 1; d <= onion.After; d++) Ghost(frame + d, AfterTint, d);
    }

    /// <summary>Exact vector render used for export. No caches, no ghosts, no shadow.</summary>
    public static void DrawPageVector(SKCanvas canvas, Project p, int frame)
    {
        var page = new SKRect(0, 0, p.Width, p.Height);
        using (var paper = new SKPaint { Color = SKColors.White })
            canvas.DrawRect(page, paper);

        canvas.Save();
        canvas.ClipRect(page);
        foreach (var layer in p.Layers)
        {
            if (!layer.Visible) continue;
            var cel = layer.GetCelAt(frame);
            if (cel == null || cel.Strokes.Count == 0) continue;

            using var layerPaint = new SKPaint
            {
                Color = SKColors.White.WithAlpha((byte)Math.Clamp(layer.Opacity * 255, 0, 255))
            };
            canvas.SaveLayer(layerPaint);   // own layer so erasers only cut this layer
            foreach (var stroke in cel.Strokes) DrawStroke(canvas, stroke);
            canvas.Restore();
        }
        canvas.Restore();
    }

    /// <summary>The colour you SEE at a document pixel (all visible layers over white paper). Used by the eyedropper.</summary>
    public static SKColor SampleColor(Project p, int frame, int x, int y)
    {
        using var bmp = new SKBitmap(1, 1);
        bmp.Erase(SKColors.White);
        using var canvas = new SKCanvas(bmp);
        canvas.Translate(-x, -y);
        DrawPageVector(canvas, p, frame);
        return bmp.GetPixel(0, 0);
    }

    // =====================================================================
    //  Brushes
    // =====================================================================

    public static void DrawStroke(SKCanvas c, Stroke s)
    {
        if (s.Text != null) { DrawTextItem(c, s); return; }

        var pts = s.Points;
        if (pts.Count == 0) return;
        var b = s.Brush;
        byte alpha = (byte)Math.Clamp(b.Opacity * 255f, 0f, 255f);
        var color = new SKColor(b.Color);

        switch (b.Type)
        {
            case BrushType.HardEraser:
                DrawConstantPath(c, pts, b.Size, SKColors.Black, SKBlendMode.Clear, 0);
                break;
            case BrushType.SoftEraser:
                DrawConstantPath(c, pts, b.Size * 0.75f, SKColors.Black, SKBlendMode.DstOut, b.Size * 0.18f);
                break;
            case BrushType.Marker:
                // one path, one draw call: opacity never builds up where the line crosses itself
                DrawConstantPath(c, pts, b.Size, color.WithAlpha(alpha), SKBlendMode.SrcOver, 0);
                break;
            case BrushType.Airbrush:
                DrawConstantPath(c, pts, b.Size * 0.75f, color.WithAlpha(alpha), SKBlendMode.SrcOver, b.Size * 0.18f);
                break;
            default:
                DrawVariableWidth(c, s, color, alpha);
                break;
        }
    }

    // typefaces are looked up by name; only used while holding the editor lock, so a plain dictionary is fine
    private static readonly Dictionary<string, SKTypeface> Typefaces = new();

    private static SKTypeface TypefaceFor(TextItem t)
    {
        string key = $"{t.FontFamily}|{t.Bold}|{t.Italic}";
        if (!Typefaces.TryGetValue(key, out var face))
        {
            face = SKTypeface.FromFamilyName(
                       t.FontFamily,
                       t.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                       SKFontStyleWidth.Normal,
                       t.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
                   ?? SKTypeface.Default;
            Typefaces[key] = face;
        }
        return face;
    }

    private static void DrawTextItem(SKCanvas c, Stroke s)
    {
        var t = s.Text!;
        var b = s.Brush;
        using var font = new SKFont(TypefaceFor(t), Math.Max(1f, t.FontSize));
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(b.Color).WithAlpha((byte)Math.Clamp(b.Opacity * 255f, 0f, 255f)),
        };

        float y = t.Y - font.Metrics.Ascent;                 // (X, Y) is the top-left of the text block
        foreach (var line in t.Value.Replace("\r", "").Split('\n'))
        {
            c.DrawText(line, t.X, y, font, paint);
            y += font.Spacing;
        }
    }

    /// <summary>A constant-width line drawn as ONE path (optionally with a soft edge).</summary>
    private static void DrawConstantPath(SKCanvas c, List<StrokePoint> pts, float width,
                                         SKColor color, SKBlendMode mode, float blurSigma)
    {
        width = Math.Max(0.5f, width);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            BlendMode = mode,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = width,
            Style = SKPaintStyle.Stroke,
        };
        using var blur = blurSigma > 0.1f ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, blurSigma) : null;
        if (blur != null) paint.MaskFilter = blur;

        if (pts.Count == 1)
        {
            paint.Style = SKPaintStyle.Fill;
            c.DrawCircle(pts[0].X, pts[0].Y, width / 2f, paint);
            return;
        }

        using var path = new SKPath();
        path.MoveTo(pts[0].X, pts[0].Y);
        for (int i = 1; i < pts.Count; i++) path.LineTo(pts[i].X, pts[i].Y);
        c.DrawPath(path, paint);
    }

    /// <summary>Pen, G-Pen and Pencil: width follows pressure, drawn segment by segment.</summary>
    private static void DrawVariableWidth(SKCanvas c, Stroke s, SKColor color, byte alpha)
    {
        var pts = s.Points;
        var b = s.Brush;
        bool grain = b.Type == BrushType.Pencil;
        bool offscreen = grain || alpha < 255;      // translucent => composite the whole stroke at once

        using var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
        if (offscreen) c.SaveLayer(layerPaint);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color.WithAlpha(255),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Style = SKPaintStyle.Stroke,
        };

        if (pts.Count == 1)
        {
            paint.Style = SKPaintStyle.Fill;
            c.DrawCircle(pts[0].X, pts[0].Y, WidthAt(b, pts[0].Pressure) / 2f, paint);
        }
        else
        {
            float[]? cum = null; float total = 0;
            if (b.Type == BrushType.GPen)
            {
                cum = new float[pts.Count];
                for (int i = 1; i < pts.Count; i++)
                {
                    float dx = pts[i].X - pts[i - 1].X, dy = pts[i].Y - pts[i - 1].Y;
                    cum[i] = cum[i - 1] + MathF.Sqrt(dx * dx + dy * dy);
                }
                total = cum[^1];
            }

            float taper = Math.Max(b.Size * 3f, 6f);   // length of the pointed start/end of an ink line
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1]; var d = pts[i];
                float w = WidthAt(b, (a.Pressure + d.Pressure) * 0.5f);
                if (cum != null)
                {
                    float k = Math.Clamp(Math.Min(cum[i] / taper, (total - cum[i]) / taper), 0f, 1f);
                    w *= 0.15f + 0.85f * k;
                }
                paint.StrokeWidth = Math.Max(0.5f, w);
                c.DrawLine(a.X, a.Y, d.X, d.Y, paint);
            }
        }

        if (grain) ApplyGrain(c, pts, b.Size);
        if (offscreen) c.Restore();
    }

    private static float WidthAt(BrushSettings b, float pressure) => b.Type switch
    {
        BrushType.GPen => b.Size * (0.08f + 0.92f * MathF.Pow(pressure, 1.5f)),
        BrushType.Pencil => b.Size * (0.35f + 0.65f * pressure),
        _ => b.Size * (0.15f + 0.85f * pressure),                       // Pen
    };

    /// <summary>Pencil texture: multiplies the stroke by paper-fixed noise (call inside the stroke's own layer).</summary>
    private static void ApplyGrain(SKCanvas c, List<StrokePoint> pts, float size)
    {
        _grain ??= SKShader.CreatePerlinNoiseFractalNoise(0.35f, 0.35f, 2, 7f);
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in pts)
        {
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
        }
        float pad = size + 2;
        using var paint = new SKPaint { Shader = _grain, BlendMode = SKBlendMode.DstIn };
        c.DrawRect(new SKRect(minX - pad, minY - pad, maxX + pad, maxY + pad), paint);
    }
}
