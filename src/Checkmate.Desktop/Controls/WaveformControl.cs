using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Checkmate.Core.Audio;
using Checkmate.Core.Editing;

namespace Checkmate.Desktop.Controls;

/// <summary>
/// Shows the sound clips as waveforms directly under the frame cells, so you can see which frame a sound
/// (a word, a beat) lands on. Sits in the same scroll area as the frame cells.
/// </summary>
public sealed class WaveformControl : Control
{
    public static readonly StyledProperty<Editor?> EditorProperty =
        AvaloniaProperty.Register<WaveformControl, Editor?>(nameof(Editor));

    public Editor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>One frame cell is 38 px wide plus 3 px spacing in the timeline (see MainWindow.axaml).</summary>
    private const double FramePitch = 41;
    private const double StripHeight = 56;

    private static readonly Color[] ClipColors =
    {
        Color.Parse("#6C5CE7"), Color.Parse("#FF4D9D"), Color.Parse("#3DDAD7"), Color.Parse("#FFB86C"),
    };

    private readonly Dictionary<(AudioData, int), float[]> _peakCache = new();

    static WaveformControl()
    {
        EditorProperty.Changed.AddClassHandler<WaveformControl>((c, e) =>
            c.OnEditorChanged(e.OldValue as Editor, e.NewValue as Editor));
    }

    public WaveformControl() { ClipToBounds = true; }

    private void OnEditorChanged(Editor? oldEditor, Editor? newEditor)
    {
        if (oldEditor != null) { oldEditor.AudioChanged -= Refresh; oldEditor.Structure -= Refresh; }
        if (newEditor != null) { newEditor.AudioChanged += Refresh; newEditor.Structure += Refresh; }
        Refresh();
    }

    private void Refresh() { InvalidateMeasure(); InvalidateVisual(); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ed = Editor;
        return new Size(ed == null ? 0 : ed.Project.FrameCount * FramePitch, StripHeight);
    }

    private static FormattedText Text(string text, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

    public override void Render(DrawingContext ctx)
    {
        var ed = Editor;
        if (ed == null) return;

        var project = ed.Project;
        double total = project.FrameCount * FramePitch;
        int fps = Math.Max(1, project.Fps);

        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#141418")), null, new Rect(0, 0, total, StripHeight), 6, 6);

        if (project.Audio.Count == 0)
        {
            ctx.DrawText(Text("No sound yet. Use \"Add sound...\" or \"Record voice\".", 12, new SolidColorBrush(Color.Parse("#77778A"))),
                         new Point(10, StripHeight / 2 - 8));
            return;
        }

        if (_peakCache.Count > 64) _peakCache.Clear();

        for (int index = 0; index < project.Audio.Count; index++)
        {
            var clip = project.Audio[index];
            var data = clip.Data;
            if (data == null) continue;

            var color = ClipColors[index % ClipColors.Length];
            double x = clip.StartFrame * FramePitch;
            double width = Math.Max(2, data.Duration * fps * FramePitch);
            var box = new Rect(x, 4, width, StripHeight - 8);

            byte fill = clip.Muted ? (byte)22 : (byte)70;
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(fill, color.R, color.G, color.B)),
                              new Pen(new SolidColorBrush(color), 1), box, 5, 5);

            int buckets = Math.Max(1, (int)(width / 2));
            if (!_peakCache.TryGetValue((data, buckets), out var peaks))
                _peakCache[(data, buckets)] = peaks = data.GetPeaks(buckets);

            var linePen = new Pen(new SolidColorBrush(clip.Muted ? Color.FromArgb(120, color.R, color.G, color.B) : color), 1.2);
            double mid = box.Center.Y, half = box.Height / 2 - 3;
            for (int i = 0; i < peaks.Length; i++)
            {
                double px = x + i * 2 + 1;
                if (px > total) break;
                double amp = Math.Max(0.5, peaks[i] * half);
                ctx.DrawLine(linePen, new Point(px, mid - amp), new Point(px, mid + amp));
            }

            ctx.DrawText(Text(clip.Name, 11, Brushes.White), new Point(x + 6, 6));
        }
    }
}
