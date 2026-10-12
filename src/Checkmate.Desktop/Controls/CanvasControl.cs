using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Checkmate.Core.Editing;
using Checkmate.Core.Model;
using Checkmate.Desktop.Rendering;

namespace Checkmate.Desktop.Controls;

/// <summary>
/// The drawing surface: draws the current frame, handles pen/mouse input and the view
/// (zoom = wheel, rotate = Shift+wheel, pan = middle or right mouse drag).
/// </summary>
public sealed class CanvasControl : Control
{
    public static readonly StyledProperty<Editor?> EditorProperty =
        AvaloniaProperty.Register<CanvasControl, Editor?>(nameof(Editor));

    public Editor? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>Executed with an Avalonia Point (document coordinates) when the text tool is clicked.</summary>
    public static readonly StyledProperty<ICommand?> PlaceTextCommandProperty =
        AvaloniaProperty.Register<CanvasControl, ICommand?>(nameof(PlaceTextCommand));

    public ICommand? PlaceTextCommand
    {
        get => GetValue(PlaceTextCommandProperty);
        set => SetValue(PlaceTextCommandProperty, value);
    }

    /// <summary>Executed with an Avalonia Color when the eyedropper picks a colour.</summary>
    public static readonly StyledProperty<ICommand?> PickColorCommandProperty =
        AvaloniaProperty.Register<CanvasControl, ICommand?>(nameof(PickColorCommand));

    public ICommand? PickColorCommand
    {
        get => GetValue(PickColorCommandProperty);
        set => SetValue(PickColorCommandProperty, value);
    }

    private static readonly IBrush Backdrop = new SolidColorBrush(Color.Parse("#232329"));

    private readonly CelCache _cache = new();
    private double _zoom = 1, _rotation;      // rotation in degrees
    private Point _pan;
    private bool _fitted, _drawing, _panning, _picking, _hasHover;
    private Point _hover;
    private Point _panStart, _panOrigin, _smooth;
    private float _smoothPressure;

    static CanvasControl()
    {
        EditorProperty.Changed.AddClassHandler<CanvasControl>((c, e) =>
            c.OnEditorChanged(e.OldValue as Editor, e.NewValue as Editor));
    }

    public CanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    private void OnEditorChanged(Editor? oldEditor, Editor? newEditor)
    {
        if (oldEditor != null) oldEditor.Redraw -= OnRedraw;
        if (newEditor != null) newEditor.Redraw += OnRedraw;
        _fitted = false;
        FitToView();
    }

    private void OnRedraw() => InvalidateVisual();

    // ---------- view ----------

    public void FitToView()
    {
        var ed = Editor;
        if (ed == null || Bounds.Width < 64 || Bounds.Height < 64) return;
        double zoom = Math.Min((Bounds.Width - 48) / ed.Project.Width, (Bounds.Height - 48) / ed.Project.Height);
        _zoom = Math.Max(zoom, 0.05);
        _rotation = 0;
        _pan = default;
        _fitted = true;
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_fitted) FitToView(); else InvalidateVisual();
    }

    // document -> screen. Avalonia matrices apply left to right.
    private Matrix ViewMatrix()
    {
        var p = Editor!.Project;
        return Matrix.CreateTranslation(-p.Width / 2.0, -p.Height / 2.0)
             * Matrix.CreateScale(_zoom, _zoom)
             * Matrix.CreateRotation(_rotation * Math.PI / 180.0)
             * Matrix.CreateTranslation(Bounds.Width / 2 + _pan.X, Bounds.Height / 2 + _pan.Y);
    }

    // ---------- rendering ----------

    public override void Render(DrawingContext context)
    {
        // Filling the backdrop also makes the whole control hit-testable.
        context.FillRectangle(Backdrop, new Rect(Bounds.Size));
        var ed = Editor;
        if (ed == null) return;

        double zoom = _zoom, rot = _rotation;
        Point pan = _pan;
        Size size = Bounds.Size;

        context.Custom(new SkiaDrawOp(new Rect(size), canvas =>
        {
            canvas.Translate((float)(size.Width / 2 + pan.X), (float)(size.Height / 2 + pan.Y));
            canvas.RotateDegrees((float)rot);
            canvas.Scale((float)zoom);
            // The render thread reads the model while the UI thread edits it, hence the lock.
            lock (ed.SyncRoot)
            {
                var project = ed.Project;
                canvas.Translate(-project.Width / 2f, -project.Height / 2f);
                Compositor.DrawShadow(canvas, project);
                Compositor.DrawPageCached(canvas, project, ed.CurrentFrame, ed.ActiveLayerIndex,
                                          ed.IsPlaying ? null : ed.Onion, _cache);
            }
        }));

        // brush-size ring under the pointer
        if (_hasHover && !_panning && !ed.EyedropperActive && !ed.TextToolActive)
        {
            double r = Math.Max(1.5, ed.Brush.Size * zoom / 2);
            context.DrawEllipse(null, new Pen(Brushes.Black, 2.5), _hover, r, r);
            context.DrawEllipse(null, new Pen(Brushes.White, 1), _hover, r, r);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hasHover = false;
        InvalidateVisual();
    }

    // ---------- input ----------

    /// <summary>Eyedropper: pick the visible colour under a screen point.</summary>
    private void PickAt(Point screen)
    {
        var ed = Editor;
        if (ed == null) return;
        var d = ViewMatrix().Invert().Transform(screen);
        int x = (int)Math.Floor(d.X), y = (int)Math.Floor(d.Y);

        Color color;
        lock (ed.SyncRoot)
        {
            var p = ed.Project;
            if (x < 0 || y < 0 || x >= p.Width || y >= p.Height) return;
            var s = Compositor.SampleColor(p, ed.CurrentFrame, x, y);
            color = Color.FromRgb(s.Red, s.Green, s.Blue);
        }
        PickColorCommand?.Execute(color);
    }

    private static float PressureOf(IPointer pointer, PointerPointProperties props) =>
        pointer.Type == PointerType.Mouse ? 0.6f : Math.Clamp(props.Pressure, 0.05f, 1f);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var ed = Editor;
        if (ed == null) return;

        Focus();
        var pt = e.GetCurrentPoint(this);
        var props = pt.Properties;

        if (props.IsMiddleButtonPressed || props.IsRightButtonPressed)
        {
            _panning = true;
            _panStart = pt.Position;
            _panOrigin = _pan;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed && (ed.EyedropperActive || e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _picking = true;
            PickAt(pt.Position);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed && ed.TextToolActive)
        {
            PlaceTextCommand?.Execute(ViewMatrix().Invert().Transform(pt.Position));
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed || props.IsEraser)
        {
            var c = ViewMatrix().Invert().Transform(pt.Position);
            _smooth = c;
            _smoothPressure = PressureOf(e.Pointer, props);
            if (ed.BeginStroke(new StrokePoint((float)c.X, (float)c.Y, _smoothPressure), props.IsEraser))
            {
                _drawing = true;
                e.Pointer.Capture(this);
            }
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var ed = Editor;
        if (ed == null) return;

        _hover = e.GetPosition(this);
        _hasHover = true;

        if (_picking)
        {
            PickAt(_hover);      // click-and-drag keeps sampling
            return;
        }

        if (_panning)
        {
            var cur = e.GetPosition(this);
            _pan = new Point(_panOrigin.X + cur.X - _panStart.X, _panOrigin.Y + cur.Y - _panStart.Y);
            InvalidateVisual();
            return;
        }

        if (!_drawing) return;

        var inverse = ViewMatrix().Invert();
        double k = 1 - Math.Clamp(ed.Stabilizer, 0, 0.95);   // 1 = raw input, lower = smoother
        foreach (var sample in e.GetIntermediatePoints(this))
        {
            var c = inverse.Transform(sample.Position);
            float pressure = PressureOf(e.Pointer, sample.Properties);

            // Exponential smoothing = a simple "stabilizer" for shaky hands.
            _smooth = new Point(_smooth.X + (c.X - _smooth.X) * k,
                                _smooth.Y + (c.Y - _smooth.Y) * k);
            _smoothPressure += (pressure - _smoothPressure) * 0.5f;
            ed.ExtendStroke(new StrokePoint((float)_smooth.X, (float)_smooth.Y, _smoothPressure));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndInteraction();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndInteraction();
    }

    private void EndInteraction()
    {
        if (_drawing) Editor?.EndStroke();
        _drawing = false;
        _panning = false;
        _picking = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Editor == null) return;

        var cursor = e.GetPosition(this);
        var anchor = ViewMatrix().Invert().Transform(cursor);    // document point under the cursor
        double delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _rotation += delta * 5;
        else _zoom = Math.Clamp(_zoom * Math.Pow(1.12, delta), 0.02, 64);

        // keep the anchor point fixed under the cursor
        var after = ViewMatrix().Transform(anchor);
        _pan = new Point(_pan.X + cursor.X - after.X, _pan.Y + cursor.Y - after.Y);
        InvalidateVisual();
        e.Handled = true;
    }
}
