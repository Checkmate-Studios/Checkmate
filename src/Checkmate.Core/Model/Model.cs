using System.Text.Json.Serialization;

namespace Checkmate.Core.Model;

/// <summary>One sample of a stroke, in document coordinates (pixels).</summary>
public struct StrokePoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Pressure { get; set; }

    public StrokePoint(float x, float y, float pressure)
    {
        X = x; Y = y; Pressure = pressure;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BrushType
{
    Pen,         // round pen, pressure changes width
    GPen,        // anime ink pen: strong pressure response + tapered start/end
    Pencil,      // thinner, grainy texture
    Marker,      // constant width, translucent, no overlap build-up
    Airbrush,    // soft edge
    HardEraser,
    SoftEraser,
}

public sealed class BrushSettings
{
    public BrushType Type { get; set; } = BrushType.Pen;
    public uint Color { get; set; } = 0xFF1B1B1F; // ARGB (alpha is always 255; transparency is Opacity)
    public float Size { get; set; } = 6;
    public float Opacity { get; set; } = 1f;      // 0..1

    [JsonIgnore] public bool IsEraser => Type is BrushType.HardEraser or BrushType.SoftEraser;

    public BrushSettings Clone() => (BrushSettings)MemberwiseClone();
}

/// <summary>A single vector stroke. Stored as data so it can be re-rendered at any resolution.</summary>
public sealed class Stroke
{
    public BrushSettings Brush { get; set; } = new();
    public List<StrokePoint> Points { get; set; } = new();

    /// <summary>Set for text items. Text lives in the stroke list so order, undo and erasing all just work.</summary>
    public TextItem? Text { get; set; }

    /// <summary>False while the stroke is still being drawn. Not saved (loaded strokes are complete).</summary>
    [JsonIgnore] public bool Complete { get; set; } = true;
}

/// <summary>A piece of text placed on a drawing. Colour and opacity come from the stroke's brush.</summary>
public sealed class TextItem
{
    public string Value { get; set; } = "";
    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 64;
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    /// <summary>Top-left corner of the text block, in document pixels.</summary>
    public float X { get; set; }
    public float Y { get; set; }
}

/// <summary>One drawing (what animators call a "cel").</summary>
public sealed class Cel
{
    public List<Stroke> Strokes { get; set; } = new();

    /// <summary>Bumped whenever strokes are REMOVED, so render caches know to rebuild. Appends are detected by count.</summary>
    [JsonIgnore] public int Revision { get; set; }
}

/// <summary>Ghost images of neighbouring drawings (red = before, green = after).</summary>
public sealed class OnionSkinSettings
{
    public bool Enabled { get; set; } = true;
    public int Before { get; set; } = 1;
    public int After { get; set; } = 1;
}

/// <summary>
/// A layer stores drawings keyed by the frame where they START. A drawing is held until the
/// next key. This is the exposure-sheet model used in anime production: drawing on 1s, 2s, 3s...
/// </summary>
public sealed class Layer
{
    public string Name { get; set; } = "Layer";
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    public SortedDictionary<int, Cel> Cels { get; set; } = new();

    /// <summary>The frame where the drawing showing on <paramref name="frame"/> starts (null before the first drawing).</summary>
    public int? GetKeyAt(int frame)
    {
        int? result = null;
        foreach (var key in Cels.Keys)
        {
            if (key > frame) break;
            result = key;
        }
        return result;
    }

    /// <summary>How many frames the drawing starting at <paramref name="key"/> is shown for.</summary>
    public int GetExposure(int key, int frameCount)
    {
        foreach (var k in Cels.Keys)
            if (k > key) return k - key;
        return Math.Max(1, frameCount - key);
    }

    /// <summary>The drawing showing on <paramref name="frame"/> (possibly held from an earlier frame).</summary>
    public Cel? GetCelAt(int frame)
    {
        Cel? result = null;
        foreach (var kv in Cels)
        {
            if (kv.Key > frame) break;
            result = kv.Value;
        }
        return result;
    }
}

public sealed class Project
{
    public string Name { get; set; } = "Untitled";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 12;
    public int FrameCount { get; set; } = 24;
    public List<Layer> Layers { get; set; } = new();

    public static Project CreateDefault()
    {
        var p = new Project();
        p.Layers.Add(new Layer { Name = "Layer 1" });
        return p;
    }
}
