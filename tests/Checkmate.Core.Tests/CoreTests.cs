using Checkmate.Core.Editing;
using Checkmate.Core.IO;
using Checkmate.Core.Model;
using Xunit;

public class CoreTests
{
    [Fact]
    public void Drawing_is_held_until_next_key()
    {
        var layer = new Layer();
        var a = new Cel(); var b = new Cel();
        layer.Cels[2] = a; layer.Cels[5] = b;

        Assert.Null(layer.GetCelAt(1));
        Assert.Same(a, layer.GetCelAt(2));
        Assert.Same(a, layer.GetCelAt(4));
        Assert.Same(b, layer.GetCelAt(5));
        Assert.Same(b, layer.GetCelAt(99));
    }

    [Fact]
    public void Stroke_undo_redo_roundtrip()
    {
        var ed = new Editor();
        ed.BeginStroke(new StrokePoint(1, 1, 1));
        ed.ExtendStroke(new StrokePoint(5, 5, 1));
        ed.EndStroke();
        Assert.Single(ed.ActiveLayer.GetCelAt(0)!.Strokes);

        ed.Undo();
        Assert.Null(ed.ActiveLayer.GetCelAt(0));   // the cel created by the stroke is gone too

        ed.Redo();
        Assert.Single(ed.ActiveLayer.GetCelAt(0)!.Strokes);
    }

    [Fact]
    public void Undo_bumps_cel_revision_so_caches_rebuild()
    {
        var ed = new Editor();
        ed.BeginStroke(new StrokePoint(1, 1, 1));
        ed.EndStroke();
        var cel = ed.ActiveLayer.GetCelAt(0)!;
        int before = cel.Revision;
        ed.Undo();
        Assert.True(cel.Revision > before);
    }

    [Fact]
    public void Onion_settings_are_clamped()
    {
        var ed = new Editor();
        ed.SetOnion(true, 99, -3);
        Assert.Equal(8, ed.Onion.Before);
        Assert.Equal(0, ed.Onion.After);
    }

    [Fact]
    public void Pen_eraser_end_forces_eraser_brush()
    {
        var ed = new Editor();
        ed.BeginStroke(new StrokePoint(0, 0, 1), forceEraser: true);
        ed.EndStroke();
        Assert.True(ed.ActiveLayer.GetCelAt(0)!.Strokes[0].Brush.IsEraser);
    }

    [Fact]
    public void Stroke_is_incomplete_until_ended()
    {
        var ed = new Editor();
        ed.BeginStroke(new StrokePoint(0, 0, 1));
        var stroke = ed.ActiveLayer.GetCelAt(0)!.Strokes[0];
        Assert.False(stroke.Complete);
        ed.EndStroke();
        Assert.True(stroke.Complete);
    }

    [Fact]
    public void Brush_type_and_opacity_survive_save_and_load()
    {
        var ed = new Editor();
        ed.Brush.Type = BrushType.Marker;
        ed.Brush.Opacity = 0.5f;
        ed.BeginStroke(new StrokePoint(1, 1, 1));
        ed.EndStroke();

        var path = Path.GetTempFileName();
        ProjectIO.Save(ed.Project, path);
        var loaded = ProjectIO.Load(path);
        File.Delete(path);

        var brush = loaded.Layers[0].GetCelAt(0)!.Strokes[0].Brush;
        Assert.Equal(BrushType.Marker, brush.Type);
        Assert.Equal(0.5f, brush.Opacity);
        Assert.True(loaded.Layers[0].GetCelAt(0)!.Strokes[0].Complete);
    }

    private static Editor SheetWithKeys(int frameCount, params int[] keys)
    {
        var ed = new Editor();
        ed.Project.FrameCount = frameCount;
        foreach (var k in keys) ed.ActiveLayer.Cels[k] = new Cel();
        return ed;
    }

    private static int[] Keys(Editor ed) => ed.ActiveLayer.Cels.Keys.ToArray();

    [Fact]
    public void Exposure_change_ripples_later_drawings()
    {
        var ed = SheetWithKeys(6, 0, 2, 4);
        ed.SetExposureOf(2, 4);                       // second drawing: 2 frames -> 4 frames
        Assert.Equal(new[] { 0, 2, 6 }, Keys(ed));
        Assert.True(ed.Project.FrameCount >= 7);

        ed.Undo();
        Assert.Equal(new[] { 0, 2, 4 }, Keys(ed));
        Assert.Equal(6, ed.Project.FrameCount);
    }

    [Fact]
    public void Animate_on_twos_retimes_the_whole_layer()
    {
        var ed = SheetWithKeys(3, 0, 1, 2);
        ed.SetAllExposure(2);
        Assert.Equal(new[] { 0, 2, 4 }, Keys(ed));
        Assert.True(ed.Project.FrameCount >= 6);
    }

    [Fact]
    public void Hold_adjust_uses_the_drawing_under_the_current_frame()
    {
        var ed = SheetWithKeys(4, 0, 2);
        ed.SetFrame(1);                                // inside the first drawing's hold
        ed.AdjustHold(+1);
        Assert.Equal(new[] { 0, 3 }, Keys(ed));
        ed.AdjustHold(-2);
        Assert.Equal(new[] { 0, 1 }, Keys(ed));
    }

    [Fact]
    public void Insert_frame_shifts_later_drawings_on_every_layer()
    {
        var ed = SheetWithKeys(3, 0, 1);
        ed.InsertFrame(newDrawing: true);              // after frame 0
        Assert.Equal(new[] { 0, 1, 2 }, Keys(ed));     // new blank at 1, old drawing moved to 2
        Assert.Equal(4, ed.Project.FrameCount);
        Assert.Equal(1, ed.CurrentFrame);
        ed.Undo();
        Assert.Equal(new[] { 0, 1 }, Keys(ed));
        Assert.Equal(3, ed.Project.FrameCount);
    }

    [Fact]
    public void Delete_frame_removes_one_frame_drawings_and_shrinks_holds()
    {
        var ed = SheetWithKeys(4, 0, 2);
        ed.SetFrame(1);                                // a hold frame of drawing 0
        ed.DeleteFrame();
        Assert.Equal(new[] { 0, 1 }, Keys(ed));
        Assert.Equal(3, ed.Project.FrameCount);

        var ed2 = SheetWithKeys(4, 0, 1, 2);
        ed2.SetFrame(1);                               // a one-frame drawing
        ed2.DeleteFrame();
        Assert.Equal(new[] { 0, 1 }, Keys(ed2));
    }

    [Fact]
    public void Copy_drawing_here_duplicates_the_held_drawing()
    {
        var ed = new Editor();
        ed.BeginStroke(new StrokePoint(1, 1, 1));
        ed.EndStroke();
        ed.AddFrame(newDrawing: false);                // frame 1 holds drawing 0
        ed.SetFrame(1);
        ed.NewDrawingHere(copyHeld: true);
        Assert.Equal(new[] { 0, 1 }, Keys(ed));
        Assert.Single(ed.ActiveLayer.Cels[1].Strokes);
        Assert.NotSame(ed.ActiveLayer.Cels[0], ed.ActiveLayer.Cels[1]);
    }

    [Fact]
    public void Text_is_one_undoable_item()
    {
        var ed = new Editor();
        Assert.True(ed.AddText(new TextItem { Value = "Hi", X = 10, Y = 20 }));
        var cel = ed.ActiveLayer.GetCelAt(0)!;
        Assert.Single(cel.Strokes);
        Assert.Equal("Hi", cel.Strokes[0].Text!.Value);

        ed.Undo();
        Assert.Null(ed.ActiveLayer.GetCelAt(0));
        ed.Redo();
        Assert.Single(ed.ActiveLayer.GetCelAt(0)!.Strokes);
    }

    [Fact]
    public void Empty_text_is_ignored_and_text_is_never_an_eraser()
    {
        var ed = new Editor();
        Assert.False(ed.AddText(new TextItem { Value = "   " }));

        ed.Brush.Type = BrushType.SoftEraser;
        Assert.True(ed.AddText(new TextItem { Value = "A" }));
        Assert.False(ed.ActiveLayer.GetCelAt(0)!.Strokes[0].Brush.IsEraser);
    }

    [Fact]
    public void Text_survives_save_and_load()
    {
        var ed = new Editor();
        ed.AddText(new TextItem { Value = "Checkmate", FontFamily = "Arial", FontSize = 80, Bold = true, X = 5, Y = 6 });

        var path = Path.GetTempFileName();
        ProjectIO.Save(ed.Project, path);
        var loaded = ProjectIO.Load(path);
        File.Delete(path);

        var text = loaded.Layers[0].GetCelAt(0)!.Strokes[0].Text!;
        Assert.Equal("Checkmate", text.Value);
        Assert.True(text.Bold);
        Assert.Equal(80f, text.FontSize);
    }

    [Fact]
    public void Add_frame_and_undo()
    {
        var ed = new Editor();
        int before = ed.Project.FrameCount;
        ed.AddFrame(newDrawing: true);
        Assert.Equal(before + 1, ed.Project.FrameCount);
        Assert.Equal(before, ed.CurrentFrame);
        ed.Undo();
        Assert.Equal(before, ed.Project.FrameCount);
    }

    [Fact]
    public void Project_survives_save_and_load()
    {
        var ed = new Editor();
        ed.BeginStroke(new StrokePoint(1, 2, 0.5f));
        ed.ExtendStroke(new StrokePoint(3, 4, 0.7f));
        ed.EndStroke();

        var path = Path.GetTempFileName();
        ProjectIO.Save(ed.Project, path);
        var loaded = ProjectIO.Load(path);
        File.Delete(path);

        var stroke = loaded.Layers[0].GetCelAt(0)!.Strokes[0];
        Assert.Equal(2, stroke.Points.Count);
        Assert.Equal(0.7f, stroke.Points[1].Pressure);
    }
}
