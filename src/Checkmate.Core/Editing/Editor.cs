using Checkmate.Core.Model;

namespace Checkmate.Core.Editing;

/// <summary>
/// All editing operations live here, free of any UI code.
/// The renderer reads the model on another thread, so every mutation happens inside lock(SyncRoot).
/// </summary>
public sealed class Editor
{
    public object SyncRoot { get; } = new();
    public Project Project { get; private set; } = Project.CreateDefault();
    public UndoStack History { get; } = new();
    public BrushSettings Brush { get; } = new();
    public OnionSkinSettings Onion { get; } = new();
    /// <summary>0 = raw input, up to 0.95 = very smooth lines.</summary>
    public double Stabilizer { get; set; } = 0.5;
    /// <summary>When true, clicking the canvas picks a colour instead of drawing.</summary>
    public bool EyedropperActive { get; set; }
    /// <summary>When true, clicking the canvas places text instead of drawing.</summary>
    public bool TextToolActive { get; set; }
    /// <summary>While playing, ghosts are hidden so you see the real animation.</summary>
    public bool IsPlaying { get; private set; }
    public int CurrentFrame { get; private set; }
    public int ActiveLayerIndex { get; private set; }
    public Layer ActiveLayer => Project.Layers[ActiveLayerIndex];

    /// <summary>The picture changed: repaint.</summary>
    public event Action? Redraw;
    /// <summary>Current frame / active layer / drawing keys changed.</summary>
    public event Action? FrameChanged;
    /// <summary>Layers or frame count changed.</summary>
    public event Action? Structure;
    /// <summary>A sound clip was added, removed, moved or changed (or the frame rate / length changed).</summary>
    public event Action? AudioChanged;
    /// <summary>The user moved to another frame (also raised during playback).</summary>
    public event Action<int>? FrameSet;
    /// <summary>Goes up whenever the audible result may have changed, so mixes can be cached.</summary>
    public int AudioRevision { get; private set; }
    /// <summary>A stroke was finished (used for the recent-colors list).</summary>
    public event Action<Stroke>? StrokeCommitted;

    private Stroke? _stroke;
    private Cel? _strokeCel;
    private Layer? _strokeLayer;
    private int _strokeFrame;
    private bool _strokeCreatedCel;
    private int _layerCounter = 1;

    private void RaiseStructure() { Structure?.Invoke(); FrameChanged?.Invoke(); Redraw?.Invoke(); AudioChanged?.Invoke(); }
    private void Clamp()
    {
        CurrentFrame = Math.Clamp(CurrentFrame, 0, Project.FrameCount - 1);
        ActiveLayerIndex = Math.Clamp(ActiveLayerIndex, 0, Project.Layers.Count - 1);
    }

    public void Load(Project project)
    {
        lock (SyncRoot)
        {
            _stroke = null;
            if (project.Layers.Count == 0) project.Layers.Add(new Layer { Name = "Layer 1" });
            if (project.FrameCount < 1) project.FrameCount = 1;
            Project = project;
            History.Clear();
            CurrentFrame = 0;
            ActiveLayerIndex = 0;
            _layerCounter = project.Layers.Count;
            AudioRevision++;
        }
        RaiseStructure();
    }

    // ---------- navigation ----------

    public void SetFrame(int frame)
    {
        lock (SyncRoot)
        {
            frame = Math.Clamp(frame, 0, Project.FrameCount - 1);
            if (frame == CurrentFrame) return;
            CurrentFrame = frame;
        }
        FrameChanged?.Invoke();
        Redraw?.Invoke();
        FrameSet?.Invoke(frame);
    }

    public void SetActiveLayer(int index)
    {
        lock (SyncRoot) ActiveLayerIndex = Math.Clamp(index, 0, Project.Layers.Count - 1);
        FrameChanged?.Invoke();
    }

    public void SetOnion(bool enabled, int before, int after)
    {
        lock (SyncRoot)
        {
            Onion.Enabled = enabled;
            Onion.Before = Math.Clamp(before, 0, 8);
            Onion.After = Math.Clamp(after, 0, 8);
        }
        Redraw?.Invoke();
    }

    public void SetPlaying(bool playing)
    {
        lock (SyncRoot) IsPlaying = playing;
        Redraw?.Invoke();
    }

    public void SetLayerVisible(Layer layer, bool visible)
    {
        lock (SyncRoot) layer.Visible = visible;
        Redraw?.Invoke();
    }

    // ---------- drawing ----------

    public bool BeginStroke(StrokePoint first, bool forceEraser = false)
    {
        lock (SyncRoot)
        {
            if (_stroke != null) return false;
            var layer = ActiveLayer;
            if (!layer.Visible) return false;

            var cel = layer.GetCelAt(CurrentFrame);
            _strokeCreatedCel = false;
            if (cel == null)
            {
                cel = new Cel();
                layer.Cels[CurrentFrame] = cel;
                _strokeCreatedCel = true;
            }

            var brush = Brush.Clone();
            if (forceEraser && !brush.IsEraser) brush.Type = BrushType.HardEraser;
            _stroke = new Stroke { Brush = brush, Complete = false };
            _stroke.Points.Add(first);
            cel.Strokes.Add(_stroke);
            _strokeCel = cel; _strokeLayer = layer; _strokeFrame = CurrentFrame;
        }
        Redraw?.Invoke();
        return true;
    }

    public void ExtendStroke(StrokePoint p)
    {
        lock (SyncRoot) { _stroke?.Points.Add(p); }
        Redraw?.Invoke();
    }

    public void EndStroke()
    {
        Stroke done;
        lock (SyncRoot)
        {
            if (_stroke == null) return;
            done = _stroke;
            done.Complete = true;
            History.Push(new StrokeCommand(_strokeLayer!, _strokeFrame, _strokeCel!, done, _strokeCreatedCel));
            _stroke = null; _strokeCel = null; _strokeLayer = null;
        }
        StrokeCommitted?.Invoke(done);
        FrameChanged?.Invoke();
        Redraw?.Invoke();
    }

    /// <summary>Adds a piece of text (current brush colour and opacity) to the active layer as ONE undoable step.</summary>
    public bool AddText(TextItem text)
    {
        if (string.IsNullOrWhiteSpace(text.Value)) return false;
        Stroke stroke;
        lock (SyncRoot)
        {
            if (_stroke != null) return false;
            var layer = ActiveLayer;
            if (!layer.Visible) return false;

            var cel = layer.GetCelAt(CurrentFrame);
            bool created = false;
            if (cel == null) { cel = new Cel(); created = true; }

            var brush = Brush.Clone();
            if (brush.IsEraser) brush.Type = BrushType.Pen;      // text is never an eraser
            stroke = new Stroke { Brush = brush, Text = text };
            History.Execute(new StrokeCommand(layer, CurrentFrame, cel, stroke, created));
        }
        StrokeCommitted?.Invoke(stroke);
        FrameChanged?.Invoke();
        Redraw?.Invoke();
        return true;
    }

    // ---------- sound ----------

    public void AddAudioClip(AudioClip clip)
    {
        lock (SyncRoot)
        {
            History.Execute(new ActionCommand("Add sound",
                () => { Project.Audio.Add(clip); AudioRevision++; },
                () => { Project.Audio.Remove(clip); AudioRevision++; }));
        }
        RaiseStructure();
    }

    public void RemoveAudioClip(AudioClip clip)
    {
        lock (SyncRoot)
        {
            int index = Project.Audio.IndexOf(clip);
            if (index < 0) return;
            History.Execute(new ActionCommand("Remove sound",
                () => { Project.Audio.Remove(clip); AudioRevision++; },
                () => { Project.Audio.Insert(Math.Min(index, Project.Audio.Count), clip); AudioRevision++; }));
        }
        RaiseStructure();
    }

    public void SetClipStart(AudioClip clip, int startFrame)
    {
        startFrame = Math.Max(0, startFrame);
        lock (SyncRoot)
        {
            int before = clip.StartFrame;
            if (before == startFrame) return;
            History.Execute(new ActionCommand("Move sound",
                () => { clip.StartFrame = startFrame; AudioRevision++; },
                () => { clip.StartFrame = before; AudioRevision++; }));
        }
        RaiseStructure();
    }

    public void SetClipVolume(AudioClip clip, double volume)
    {
        lock (SyncRoot) { clip.Volume = Math.Clamp(volume, 0, 2); AudioRevision++; }
        AudioChanged?.Invoke();
    }

    public void SetClipMuted(AudioClip clip, bool muted)
    {
        lock (SyncRoot) { clip.Muted = muted; AudioRevision++; }
        AudioChanged?.Invoke();
    }

    /// <summary>Tell listeners the audible result changed for another reason (for example the frame rate).</summary>
    public void NotifyAudioChanged()
    {
        lock (SyncRoot) AudioRevision++;
        AudioChanged?.Invoke();
    }

    /// <summary>Make sure the animation has at least this many frames (for example to fit a long song).</summary>
    public void EnsureFrameCount(int frames)
    {
        EditTimeline("Extend animation", () => { if (frames > Project.FrameCount) Project.FrameCount = frames; });
    }

    // ---------- timeline ----------

    /// <summary>Append a frame. With newDrawing=true the active layer gets a blank drawing there; otherwise the previous drawing is held.</summary>
    public void AddFrame(bool newDrawing)
    {
        lock (SyncRoot)
        {
            int at = Project.FrameCount;
            var layer = ActiveLayer;
            var cel = new Cel();
            History.Execute(new ActionCommand(newDrawing ? "Add drawing" : "Add hold",
                () => { Project.FrameCount = at + 1; if (newDrawing) layer.Cels[at] = cel; CurrentFrame = at; },
                () => { layer.Cels.Remove(at); Project.FrameCount = at; Clamp(); }));
        }
        RaiseStructure();
    }

    /// <summary>
    /// Start a new drawing on the current frame (used when the frame is only a hold).
    /// With copyHeld the new drawing starts as a copy of the held one, handy for the next pose.
    /// </summary>
    public void NewDrawingHere(bool copyHeld = false)
    {
        lock (SyncRoot)
        {
            var layer = ActiveLayer; int frame = CurrentFrame;
            if (layer.Cels.ContainsKey(frame)) return;
            var held = layer.GetCelAt(frame);
            var cel = copyHeld && held != null ? new Cel { Strokes = new List<Stroke>(held.Strokes) } : new Cel();
            History.Execute(new ActionCommand(copyHeld ? "Copy drawing" : "New drawing",
                () => layer.Cels[frame] = cel,
                () => layer.Cels.Remove(frame)));
        }
        RaiseStructure();
    }

    // ---------- X-sheet / exposure ----------

    private sealed record TimelineState(int FrameCount, List<(Layer Layer, SortedDictionary<int, Cel> Cels)> Layers);

    private TimelineState CaptureTimeline() => new(
        Project.FrameCount,
        Project.Layers.Select(l => (Layer: l, Cels: new SortedDictionary<int, Cel>(l.Cels))).ToList());

    private void RestoreTimeline(TimelineState s)
    {
        Project.FrameCount = s.FrameCount;
        foreach (var (layer, cels) in s.Layers) layer.Cels = new SortedDictionary<int, Cel>(cels);
    }

    private static bool SameTimeline(TimelineState a, TimelineState b)
    {
        if (a.FrameCount != b.FrameCount || a.Layers.Count != b.Layers.Count) return false;
        for (int i = 0; i < a.Layers.Count; i++)
        {
            var x = a.Layers[i].Cels; var y = b.Layers[i].Cels;
            if (x.Count != y.Count) return false;
            foreach (var kv in x)
                if (!y.TryGetValue(kv.Key, out var v) || !ReferenceEquals(v, kv.Value)) return false;
        }
        return true;
    }

    /// <summary>Runs a timeline edit and records it as ONE undo step (snapshots all layers' keys + frame count).</summary>
    private void EditTimeline(string name, Action edit)
    {
        lock (SyncRoot)
        {
            if (_stroke != null) return;
            var before = CaptureTimeline();
            edit();
            var after = CaptureTimeline();
            if (SameTimeline(before, after)) return;      // nothing changed: no undo entry
            History.Push(new ActionCommand(name, () => RestoreTimeline(after), () => RestoreTimeline(before)));
            Clamp();
        }
        RaiseStructure();
    }

    /// <summary>Change how long the drawing starting at <paramref name="key"/> is held. Later drawings on that layer ripple along.</summary>
    public void SetExposureOf(int key, int length)
    {
        EditTimeline("Change exposure", () =>
        {
            var layer = ActiveLayer;
            if (!layer.Cels.ContainsKey(key)) return;
            length = Math.Max(1, length);
            int delta = length - layer.GetExposure(key, Project.FrameCount);
            if (delta == 0) return;

            var shifted = new SortedDictionary<int, Cel>();
            foreach (var kv in layer.Cels) shifted[kv.Key > key ? kv.Key + delta : kv.Key] = kv.Value;
            layer.Cels = shifted;
            Project.FrameCount = Math.Max(Project.FrameCount, Math.Max(shifted.Keys.Max() + 1, key + length));
        });
    }

    /// <summary>Make the drawing showing on the current frame last <paramref name="delta"/> frames longer (or shorter).</summary>
    public void AdjustHold(int delta)
    {
        int key, length;
        lock (SyncRoot)
        {
            var layer = ActiveLayer;
            var k = layer.GetKeyAt(CurrentFrame);
            if (k == null) return;
            key = k.Value;
            length = layer.GetExposure(key, Project.FrameCount);
        }
        SetExposureOf(key, length + delta);
    }

    /// <summary>"Animate on 2s": every drawing on the active layer is shown for exactly <paramref name="length"/> frames.</summary>
    public void SetAllExposure(int length)
    {
        EditTimeline($"Animate on {length}s", () =>
        {
            var layer = ActiveLayer;
            if (layer.Cels.Count == 0) return;
            length = Math.Max(1, length);

            int start = layer.Cels.Keys.First();
            var rebuilt = new SortedDictionary<int, Cel>();
            int index = 0;
            foreach (var kv in layer.Cels)
            {
                rebuilt[start + index * length] = kv.Value;
                index++;
            }
            layer.Cels = rebuilt;
            Project.FrameCount = Math.Max(Project.FrameCount, start + index * length);   // last drawing gets a full exposure
        });
    }

    /// <summary>Insert a frame after the current one on ALL layers. With newDrawing the active layer starts a fresh drawing there.</summary>
    public void InsertFrame(bool newDrawing)
    {
        EditTimeline("Insert frame", () =>
        {
            int at = CurrentFrame + 1;
            foreach (var layer in Project.Layers)
            {
                var moved = new SortedDictionary<int, Cel>();
                foreach (var kv in layer.Cels) moved[kv.Key >= at ? kv.Key + 1 : kv.Key] = kv.Value;
                layer.Cels = moved;
            }
            Project.FrameCount++;
            if (newDrawing) ActiveLayer.Cels[at] = new Cel();
            CurrentFrame = at;
        });
    }

    /// <summary>Delete the current frame on ALL layers; later frames move up. A drawing held longer than one frame just gets shorter.</summary>
    public void DeleteFrame()
    {
        EditTimeline("Delete frame", () =>
        {
            if (Project.FrameCount <= 1) return;
            int f = CurrentFrame;
            int newCount = Project.FrameCount - 1;
            foreach (var layer in Project.Layers)
            {
                var d = new SortedDictionary<int, Cel>();
                foreach (var kv in layer.Cels)
                {
                    if (kv.Key < f) d[kv.Key] = kv.Value;
                    else if (kv.Key == f) { if (!layer.Cels.ContainsKey(f + 1)) d[f] = kv.Value; }   // one-frame drawing disappears
                    else d[kv.Key - 1] = kv.Value;
                }
                foreach (var stray in d.Keys.Where(k => k >= newCount).ToList()) d.Remove(stray);
                layer.Cels = d;
            }
            Project.FrameCount = newCount;
            CurrentFrame = Math.Min(f, newCount - 1);
        });
    }

    /// <summary>Remove the drawing that starts on the current frame; the previous drawing is held through instead.</summary>
    public void RemoveDrawing()
    {
        EditTimeline("Delete drawing", () => ActiveLayer.Cels.Remove(CurrentFrame));
    }

    // ---------- layers ----------

    public void AddLayer()
    {
        lock (SyncRoot)
        {
            var layer = new Layer { Name = $"Layer {++_layerCounter}" };
            int idx = ActiveLayerIndex + 1;
            History.Execute(new ActionCommand("Add layer",
                () => { Project.Layers.Insert(idx, layer); ActiveLayerIndex = idx; },
                () => { Project.Layers.Remove(layer); Clamp(); }));
        }
        RaiseStructure();
    }

    public void DeleteActiveLayer()
    {
        lock (SyncRoot)
        {
            if (Project.Layers.Count <= 1) return;
            var layer = ActiveLayer; int idx = ActiveLayerIndex;
            History.Execute(new ActionCommand("Delete layer",
                () => { Project.Layers.RemoveAt(idx); ActiveLayerIndex = Math.Min(idx, Project.Layers.Count - 1); },
                () => { Project.Layers.Insert(idx, layer); ActiveLayerIndex = idx; }));
        }
        RaiseStructure();
    }

    // ---------- history ----------

    public void Undo() { lock (SyncRoot) { if (_stroke != null || !History.Undo()) return; Clamp(); } RaiseStructure(); }
    public void Redo() { lock (SyncRoot) { if (_stroke != null || !History.Redo()) return; Clamp(); } RaiseStructure(); }

    private sealed class StrokeCommand : IUndoableCommand
    {
        private readonly Layer _layer; private readonly int _frame; private readonly Cel _cel;
        private readonly Stroke _stroke; private readonly bool _created;
        public string Name => "Stroke";
        public StrokeCommand(Layer layer, int frame, Cel cel, Stroke stroke, bool created)
        { _layer = layer; _frame = frame; _cel = cel; _stroke = stroke; _created = created; }

        public void Do()
        {
            if (_created) _layer.Cels[_frame] = _cel;
            if (!_cel.Strokes.Contains(_stroke)) _cel.Strokes.Add(_stroke);
        }
        public void Undo()
        {
            _cel.Strokes.Remove(_stroke);
            _cel.Revision++;
            if (_created) _layer.Cels.Remove(_frame);
        }
    }
}
