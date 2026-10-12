using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Checkmate.Core.Editing;

namespace Checkmate.Desktop.Controls;

/// <summary>
/// The exposure sheet: one row per frame, one column per layer (top layer on the left, like a paper X-sheet).
/// A number marks where a drawing starts; the vertical line below it is the hold; a small tick marks where it ends.
/// Click a cell to jump to that frame and layer, drag down the sheet to scrub.
/// Only the visible rows are drawn, so long animations stay fast.
/// </summary>
public sealed class XSheetControl : Control
{
    public static readonly StyledProperty<Editor?> EditorProperty =
        AvaloniaProperty.Register<XSheetControl, Editor?>(nameof(Editor));

    public Editor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    private const double RowH = 22, HeaderH = 28, GutterW = 40, ColW = 62;

    private static readonly IBrush PanelBrush = new SolidColorBrush(Color.Parse("#141418"));
    private static readonly IBrush HeaderBrush = new SolidColorBrush(Color.Parse("#23232B"));
    private static readonly IBrush ActiveHeaderBrush = new SolidColorBrush(Color.Parse("#6C5CE7"));
    private static readonly IBrush CurrentRowBrush = new SolidColorBrush(Color.Parse("#306C5CE7"));
    private static readonly IBrush ActiveColumnBrush = new SolidColorBrush(Color.Parse("#146C5CE7"));
    private static readonly IBrush KeyCellBrush = new SolidColorBrush(Color.Parse("#34344A"));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#EDEDF5"));
    private static readonly IBrush DimTextBrush = new SolidColorBrush(Color.Parse("#8A8A99"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#26262E")), 1);
    private static readonly Pen SecondPen = new(new SolidColorBrush(Color.Parse("#5A5A70")), 1);
    private static readonly Pen HoldPen = new(new SolidColorBrush(Color.Parse("#9A8CFF")), 2);
    private static readonly Pen SelectPen = new(new SolidColorBrush(Color.Parse("#FFB86C")), 2);

    private ScrollViewer? _scroll;
    private bool _dragging;

    static XSheetControl()
    {
        EditorProperty.Changed.AddClassHandler<XSheetControl>((c, e) =>
            c.OnEditorChanged(e.OldValue as Editor, e.NewValue as Editor));
    }

    public XSheetControl() { ClipToBounds = true; }

    // ---------- wiring ----------

    private void OnEditorChanged(Editor? oldEditor, Editor? newEditor)
    {
        if (oldEditor != null) { oldEditor.Structure -= OnStructure; oldEditor.FrameChanged -= OnFrameChanged; }
        if (newEditor != null) { newEditor.Structure += OnStructure; newEditor.FrameChanged += OnFrameChanged; }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnStructure() { InvalidateMeasure(); InvalidateVisual(); }

    private void OnFrameChanged()
    {
        EnsureFrameVisible();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroll = this.FindAncestorOfType<ScrollViewer>();
        if (_scroll != null) _scroll.ScrollChanged += OnScrolled;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scroll != null) _scroll.ScrollChanged -= OnScrolled;
        _scroll = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnScrolled(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    private void EnsureFrameVisible()
    {
        var ed = Editor;
        if (_scroll == null || ed == null) return;
        double y = HeaderH + ed.CurrentFrame * RowH;
        double top = _scroll.Offset.Y, viewport = _scroll.Viewport.Height;
        if (viewport <= 0) return;

        if (y < top + HeaderH) _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, y - HeaderH));
        else if (y + RowH > top + viewport) _scroll.Offset = new Vector(_scroll.Offset.X, y + RowH - viewport);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ed = Editor;
        if (ed == null) return new Size(GutterW + ColW, HeaderH + RowH);
        return new Size(GutterW + ed.Project.Layers.Count * ColW, HeaderH + ed.Project.FrameCount * RowH);
    }

    // ---------- drawing ----------

    private static void DrawCentered(DrawingContext ctx, string text, Rect box, double size, IBrush brush)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                   Typeface.Default, size, brush)
        {
            MaxTextWidth = Math.Max(1, box.Width - 4),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        ctx.DrawText(ft, new Point(box.X + (box.Width - ft.Width) / 2, box.Y + (box.Height - ft.Height) / 2));
    }

    public override void Render(DrawingContext ctx)
    {
        var ed = Editor;
        if (ed == null) return;

        var project = ed.Project;
        int frames = project.FrameCount, count = project.Layers.Count;
        int fps = Math.Max(1, project.Fps);
        double width = GutterW + count * ColW, height = HeaderH + frames * RowH;

        double top = _scroll?.Offset.Y ?? 0;
        double viewport = _scroll?.Viewport.Height is > 0 ? _scroll!.Viewport.Height : Bounds.Height;
        int first = Math.Max(0, (int)((top - HeaderH) / RowH) - 1);
        int last = Math.Min(frames - 1, (int)((top + viewport - HeaderH) / RowH) + 1);

        ctx.FillRectangle(PanelBrush, new Rect(0, 0, width, height));

        int activeCol = count - 1 - ed.ActiveLayerIndex;
        ctx.FillRectangle(ActiveColumnBrush, new Rect(GutterW + activeCol * ColW, 0, ColW, height));

        // drawing numbers per layer (1, 2, 3 ... in order of appearance)
        var ordinals = new Dictionary<int, int>[count];
        for (int c = 0; c < count; c++)
        {
            ordinals[c] = new Dictionary<int, int>();
            int n = 1;
            foreach (var key in project.Layers[count - 1 - c].Cels.Keys) ordinals[c][key] = n++;
        }

        for (int f = first; f <= last; f++)
        {
            double y = HeaderH + f * RowH;
            if (f == ed.CurrentFrame) ctx.FillRectangle(CurrentRowBrush, new Rect(0, y, width, RowH));

            // second marker: a brighter line under every fps-th frame
            ctx.DrawLine((f + 1) % fps == 0 ? SecondPen : GridPen, new Point(0, y + RowH), new Point(width, y + RowH));
            DrawCentered(ctx, (f + 1).ToString(CultureInfo.InvariantCulture), new Rect(0, y, GutterW, RowH), 11,
                         f == ed.CurrentFrame ? TextBrush : DimTextBrush);

            for (int c = 0; c < count; c++)
            {
                var layer = project.Layers[count - 1 - c];
                var key = layer.GetKeyAt(f);
                if (key == null) continue;

                double x = GutterW + c * ColW;
                if (key == f)
                {
                    ctx.FillRectangle(KeyCellBrush, new Rect(x + 2, y + 1, ColW - 4, RowH - 2), 3);
                    DrawCentered(ctx, ordinals[c][f].ToString(CultureInfo.InvariantCulture),
                                 new Rect(x, y, ColW, RowH), 12, TextBrush);
                }
                else
                {
                    ctx.DrawLine(HoldPen, new Point(x + ColW / 2, y), new Point(x + ColW / 2, y + RowH));
                }

                bool endsHere = f + 1 >= frames || layer.Cels.ContainsKey(f + 1);
                if (endsHere && key != f)
                    ctx.DrawLine(HoldPen, new Point(x + ColW / 2 - 8, y + RowH - 2), new Point(x + ColW / 2 + 8, y + RowH - 2));
            }
        }

        // column separators
        for (int c = 0; c <= count; c++)
            ctx.DrawLine(GridPen, new Point(GutterW + c * ColW, top), new Point(GutterW + c * ColW, top + viewport));

        // the selected cell: current frame on the active layer
        ctx.DrawRectangle(null, SelectPen,
            new Rect(GutterW + activeCol * ColW + 1, HeaderH + ed.CurrentFrame * RowH + 1, ColW - 2, RowH - 2), 3, 3);

        // sticky header (stays at the top while the rows scroll)
        ctx.FillRectangle(HeaderBrush, new Rect(0, top, width, HeaderH));
        DrawCentered(ctx, "#", new Rect(0, top, GutterW, HeaderH), 11, DimTextBrush);
        for (int c = 0; c < count; c++)
        {
            var layer = project.Layers[count - 1 - c];
            var box = new Rect(GutterW + c * ColW, top, ColW, HeaderH);
            if (c == activeCol) ctx.FillRectangle(ActiveHeaderBrush, box.Deflate(new Thickness(2, 3)), 4);
            DrawCentered(ctx, layer.Name, box, 11, layer.Visible ? TextBrush : DimTextBrush);
        }
    }

    // ---------- input ----------

    private void SelectAt(Point p, bool allowLayerChange)
    {
        var ed = Editor;
        if (ed == null) return;
        int frames = ed.Project.FrameCount, count = ed.Project.Layers.Count;
        double top = _scroll?.Offset.Y ?? 0;

        if (allowLayerChange && p.X >= GutterW)
        {
            int col = (int)((p.X - GutterW) / ColW);
            if (col >= 0 && col < count) ed.SetActiveLayer(count - 1 - col);
        }

        if (p.Y >= top + HeaderH)                      // clicks on the header only choose the layer
        {
            int row = Math.Clamp((int)((p.Y - HeaderH) / RowH), 0, frames - 1);
            ed.SetFrame(row);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        e.Pointer.Capture(this);
        SelectAt(e.GetPosition(this), allowLayerChange: true);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) SelectAt(e.GetPosition(this), allowLayerChange: false);   // drag = scrub through frames
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }
}
