using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Checkmate.Core.Editing;
using Checkmate.Core.IO;
using Checkmate.Core.Model;
using Checkmate.Desktop.Rendering;
using Checkmate.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Checkmate.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const double MaxSize = 300;
    private const int MaxRecent = 12;

    private readonly DispatcherTimer _timer = new();
    private bool _syncing, _loadingTool, _syncSize, _loadingPalette;
    private BrushType _currentType = BrushType.Pen;
    private ToolItem? _lastDrawTool;

    // Every tool remembers its own size and opacity.
    private readonly Dictionary<BrushType, (double Size, double Opacity)> _prefs = new()
    {
        [BrushType.Pen] = (6, 1.0),
        [BrushType.GPen] = (10, 1.0),
        [BrushType.Pencil] = (5, 0.9),
        [BrushType.Marker] = (28, 0.5),
        [BrushType.Airbrush] = (60, 0.4),
        [BrushType.HardEraser] = (30, 1.0),
        [BrushType.SoftEraser] = (60, 1.0),
    };

    public Editor Editor { get; } = new();
    public ObservableCollection<LayerItem> Layers { get; } = new();
    public ObservableCollection<FrameCell> Frames { get; } = new();
    public ObservableCollection<ToolItem> Tools { get; } = new();
    public ObservableCollection<Swatch> Palette { get; } = new();
    public ObservableCollection<Swatch> Recent { get; } = new();
    public ObservableCollection<PaletteSet> PaletteSets { get; } = new();

    [ObservableProperty] private ToolItem? _selectedTool;
    [ObservableProperty] private PaletteSet? _selectedPalette;
    [ObservableProperty] private double _brushSize = 6;
    [ObservableProperty] private double _sizeSlider;          // 0..1 on a log scale (1..300 px)
    [ObservableProperty] private double _brushOpacity = 1;
    [ObservableProperty] private double _smoothing = 50;      // percent
    [ObservableProperty] private Color _brushColor = Color.Parse("#1B1B1F");

    [ObservableProperty] private double _fps = 12;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _frameText = "";
    [ObservableProperty] private LayerItem? _selectedLayer;
    [ObservableProperty] private bool _onionEnabled = true;
    [ObservableProperty] private double _onionBefore = 1;
    [ObservableProperty] private double _onionAfter = 1;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isExporting;

    public bool CanExport => !IsExporting;
    partial void OnIsExportingChanged(bool value) => OnPropertyChanged(nameof(CanExport));

    public MainViewModel()
    {
        Tools.Add(new ToolItem(BrushType.Pen, "Pen", "B"));
        Tools.Add(new ToolItem(BrushType.GPen, "G-Pen (ink)", "G"));
        Tools.Add(new ToolItem(BrushType.Pencil, "Pencil", "P"));
        Tools.Add(new ToolItem(BrushType.Marker, "Marker", "M"));
        Tools.Add(new ToolItem(BrushType.Airbrush, "Airbrush", "A"));
        Tools.Add(new ToolItem(BrushType.HardEraser, "Eraser", "E"));
        Tools.Add(new ToolItem(BrushType.SoftEraser, "Soft eraser", "Shift+E"));
        Tools.Add(new ToolItem(BrushType.Pen, "Eyedropper", "I", IsEyedropper: true));
        Tools.Add(new ToolItem(BrushType.Pen, "Text", "T", IsText: true));

        // colour palettes: the built-in sets plus the user's own "My colors" (saved between sessions)
        var settings = AppSettings.Load();
        foreach (var set in Palettes.BuiltIn) PaletteSets.Add(set);
        PaletteSets.Add(new PaletteSet("My colors", settings.CustomColors, isCustom: true));
        _loadingPalette = true;
        SelectedPalette = PaletteSets.FirstOrDefault(p => p.Name == settings.PaletteName) ?? PaletteSets[0];
        _loadingPalette = false;

        Editor.Structure += OnStructure;
        Editor.FrameChanged += RefreshTimeline;
        Editor.StrokeCommitted += OnStrokeCommitted;
        _timer.Tick += (_, _) => Editor.SetFrame((Editor.CurrentFrame + 1) % Editor.Project.FrameCount);

        SelectedTool = Tools[0];
        SizeSlider = ToSlider(BrushSize);
        ApplyBrush();
        OnStructure();
    }

    // ------------------------------------------------------------------
    //  Tools, size, opacity, colour
    // ------------------------------------------------------------------

    public bool HasOpacity => SelectedTool is { IsText: true } || _currentType is not (BrushType.HardEraser or BrushType.SoftEraser);

    private static double ToSlider(double size) =>
        Math.Log(Math.Clamp(size, 1, MaxSize)) / Math.Log(MaxSize);

    partial void OnSelectedToolChanged(ToolItem? value)
    {
        if (value == null) return;
        Editor.EyedropperActive = value.IsEyedropper;
        Editor.TextToolActive = value.IsText;
        if (value.IsEyedropper || value.IsText)    // these keep the previous brush settings untouched
        {
            OnPropertyChanged(nameof(HasOpacity));
            return;
        }
        _lastDrawTool = value;

        _loadingTool = true;
        _currentType = value.Type;
        var p = _prefs[value.Type];
        BrushSize = p.Size;
        BrushOpacity = p.Opacity;
        _loadingTool = false;
        OnPropertyChanged(nameof(HasOpacity));
        ApplyBrush();
    }

    partial void OnBrushSizeChanged(double value)
    {
        if (!_syncSize)
        {
            _syncSize = true;
            SizeSlider = ToSlider(value);
            _syncSize = false;
        }
        if (!_loadingTool) _prefs[_currentType] = (value, BrushOpacity);
        ApplyBrush();
    }

    partial void OnSizeSliderChanged(double value)
    {
        if (_syncSize) return;
        _syncSize = true;
        BrushSize = Math.Round(Math.Pow(MaxSize, value), 1);
        _syncSize = false;
    }

    partial void OnBrushOpacityChanged(double value)
    {
        if (!_loadingTool) _prefs[_currentType] = (BrushSize, value);
        ApplyBrush();
    }

    partial void OnSmoothingChanged(double value) => Editor.Stabilizer = value / 100.0;
    partial void OnBrushColorChanged(Color value) => ApplyBrush();

    private void ApplyBrush()
    {
        var b = Editor.Brush;
        b.Type = _currentType;
        b.Size = (float)BrushSize;
        b.Opacity = (float)BrushOpacity;
        var c = BrushColor;
        b.Color = 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
    }

    // ------------------------------------------------------------------
    //  Palettes
    // ------------------------------------------------------------------

    public bool IsCustomPalette => SelectedPalette is { IsCustom: true };
    public bool ShowCustomHint => IsCustomPalette && Palette.Count == 0;

    partial void OnSelectedPaletteChanged(PaletteSet? value)
    {
        RebuildPalette();
        OnPropertyChanged(nameof(IsCustomPalette));
        if (_loadingPalette || value == null) return;
        var settings = AppSettings.Load();
        settings.PaletteName = value.Name;
        settings.Save();
    }

    private void RebuildPalette()
    {
        Palette.Clear();
        if (SelectedPalette != null)
            foreach (var hex in SelectedPalette.Colors)
                if (Color.TryParse(hex, out var c)) Palette.Add(new Swatch(c, PickColor));
        OnPropertyChanged(nameof(ShowCustomHint));
    }

    private PaletteSet MyColors => PaletteSets.First(p => p.IsCustom);

    private static string HexOf(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private void SaveMyColors()
    {
        var settings = AppSettings.Load();
        settings.CustomColors = MyColors.Colors.ToList();
        settings.Save();
        if (SelectedPalette == MyColors) RebuildPalette();
    }

    [RelayCommand]
    private void AddCurrentColor()
    {
        string hex = HexOf(BrushColor);
        if (!MyColors.Colors.Contains(hex, StringComparer.OrdinalIgnoreCase)) MyColors.Colors.Add(hex);
        SaveMyColors();
        SelectedPalette = MyColors;                 // switch to it so you can see the colour was saved
        RebuildPalette();
        Status = $"Saved {hex} to My colors";
    }

    [RelayCommand]
    private void RemoveCurrentColor()
    {
        string hex = HexOf(BrushColor);
        int removed = MyColors.Colors.RemoveAll(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase));
        SaveMyColors();
        Status = removed > 0 ? $"Removed {hex} from My colors" : $"{hex} is not in My colors";
    }

    [RelayCommand]
    private void ClearMyColors()
    {
        MyColors.Colors.Clear();
        SaveMyColors();
        Status = "Cleared My colors";
    }

    private void PickColor(Color c)
    {
        BrushColor = c;
        ReturnToDrawing();
    }

    /// <summary>Picking a colour while the eyedropper or an eraser is active means "go back to drawing".</summary>
    private void ReturnToDrawing()
    {
        if (SelectedTool is { IsEyedropper: true })
        {
            var back = _lastDrawTool;
            bool wasEraser = back != null && back.Type is BrushType.HardEraser or BrushType.SoftEraser;
            SelectedTool = back == null || wasEraser ? Tools[0] : back;
        }
        else if (SelectedTool is { IsText: false } && !HasOpacity) SelectedTool = Tools[0];
    }

    [RelayCommand]
    private void PickColorFromCanvas(Color c)
    {
        BrushColor = c;
        ReturnToDrawing();
        Status = $"Picked #{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private void OnStrokeCommitted(Stroke s)
    {
        if (s.Brush.IsEraser) return;
        uint v = s.Brush.Color;
        var c = Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);

        for (int i = Recent.Count - 1; i >= 0; i--)
            if (Recent[i].Color == c) Recent.RemoveAt(i);
        Recent.Insert(0, new Swatch(c, PickColor));
        while (Recent.Count > MaxRecent) Recent.RemoveAt(Recent.Count - 1);
    }

    /// <summary>Raised when the text tool is clicked on the canvas; the window shows the text dialog.</summary>
    public event Action<Point>? TextPlacementRequested;

    [RelayCommand]
    private void PlaceText(Point documentPoint) => TextPlacementRequested?.Invoke(documentPoint);

    public void AddText(TextItem item)
    {
        if (!Editor.AddText(item)) Status = "Could not add text (is the active layer hidden?)";
    }

    [RelayCommand]
    private void SelectTool(string name)
    {
        var tool = Tools.FirstOrDefault(t => t.Id == name);
        if (tool != null) SelectedTool = tool;
    }

    [RelayCommand]
    private void SizeUp() =>
        BrushSize = Math.Min(MaxSize, Math.Max(BrushSize + 1, Math.Round(BrushSize * 1.2)));

    [RelayCommand]
    private void SizeDown() =>
        BrushSize = Math.Max(1, Math.Min(BrushSize - 1, Math.Round(BrushSize / 1.2)));

    // ------------------------------------------------------------------
    //  Onion skin, playback
    // ------------------------------------------------------------------

    partial void OnOnionEnabledChanged(bool value) => ApplyOnion();
    partial void OnOnionBeforeChanged(double value) => ApplyOnion();
    partial void OnOnionAfterChanged(double value) => ApplyOnion();

    private void ApplyOnion() =>
        Editor.SetOnion(OnionEnabled, (int)Math.Round(OnionBefore), (int)Math.Round(OnionAfter));

    partial void OnFpsChanged(double value)
    {
        Editor.Project.Fps = (int)Math.Round(value);
        _timer.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, value));
    }

    partial void OnSelectedLayerChanged(LayerItem? value)
    {
        if (!_syncing && value != null) Editor.SetActiveLayer(value.ModelIndex);
    }

    // ------------------------------------------------------------------
    //  Keeping the lists in sync with the model
    // ------------------------------------------------------------------

    private void OnStructure()
    {
        _syncing = true;
        Layers.Clear();
        var layers = Editor.Project.Layers;
        for (int i = layers.Count - 1; i >= 0; i--)           // top layer first in the UI
            Layers.Add(new LayerItem(Editor, layers[i], i));
        SelectedLayer = Layers.FirstOrDefault(l => l.ModelIndex == Editor.ActiveLayerIndex);
        Fps = Editor.Project.Fps;
        _syncing = false;
        RefreshTimeline();
    }

    private void RefreshTimeline()
    {
        int count = Editor.Project.FrameCount;
        while (Frames.Count < count) Frames.Add(new FrameCell(Frames.Count, i => Editor.SetFrame(i)));
        while (Frames.Count > count) Frames.RemoveAt(Frames.Count - 1);

        var layer = Editor.ActiveLayer;
        for (int i = 0; i < Frames.Count; i++)
        {
            Frames[i].IsCurrent = i == Editor.CurrentFrame;
            Frames[i].HasKey = layer.Cels.ContainsKey(i);
        }
        FrameText = $"Frame {Editor.CurrentFrame + 1} / {count}";
    }

    // ------------------------------------------------------------------
    //  Commands
    // ------------------------------------------------------------------

    [RelayCommand] private void ToggleOnion() => OnionEnabled = !OnionEnabled;
    [RelayCommand] private void Undo() => Editor.Undo();
    [RelayCommand] private void Redo() => Editor.Redo();
    [RelayCommand] private void AddDrawing() => Editor.AddFrame(true);
    [RelayCommand] private void AddHold() => Editor.AddFrame(false);
    [RelayCommand] private void NewDrawing() => Editor.NewDrawingHere();
    // frames (apply to every layer)
    [RelayCommand] private void AddFrame() => Editor.InsertFrame(true);        // after the current frame, blank drawing on the active layer
    [RelayCommand] private void AddHoldFrame() => Editor.InsertFrame(false);   // after the current frame, everything just holds
    [RelayCommand] private void DeleteFrame() => Editor.DeleteFrame();

    // X-sheet (apply to the active layer)
    [RelayCommand] private void AnimateOn(string length) => Editor.SetAllExposure(int.Parse(length));
    [RelayCommand] private void HoldPlus() => Editor.AdjustHold(1);
    [RelayCommand] private void HoldMinus() => Editor.AdjustHold(-1);
    [RelayCommand] private void DuplicateDrawing() => Editor.NewDrawingHere(true);
    [RelayCommand] private void DeleteDrawing() => Editor.RemoveDrawing();

    [RelayCommand] private void AddLayer() => Editor.AddLayer();
    [RelayCommand] private void DeleteLayer() => Editor.DeleteActiveLayer();
    [RelayCommand] private void PrevFrame() => Editor.SetFrame(Editor.CurrentFrame - 1);
    [RelayCommand] private void NextFrame() => Editor.SetFrame(Editor.CurrentFrame + 1);

    [RelayCommand]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;
        Editor.SetPlaying(IsPlaying);
        _timer.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, Fps));
        if (IsPlaying) _timer.Start(); else _timer.Stop();
    }

    public void SaveTo(string path)
    {
        Editor.Project.Name = Path.GetFileNameWithoutExtension(path);
        ProjectIO.Save(Editor.Project, path);
        Status = $"Saved {Path.GetFileName(path)}";
    }

    public void OpenFrom(string path) => Editor.Load(ProjectIO.Load(path));

    public async Task ExportPngSequenceAsync(string folder, int firstFrame, int lastFrame)
    {
        if (IsExporting) return;
        IsExporting = true;
        try
        {
            var progress = new Progress<string>(s => Status = s);
            string name = Editor.Project.Name;
            int n = await Task.Run(() => Exporter.ExportPngSequence(Editor, folder, name, firstFrame, lastFrame, progress));
            Status = $"Exported {n} frames to {folder}";
        }
        catch (Exception ex) { Status = "Export failed: " + ex.Message; }
        finally { IsExporting = false; }
    }

    public async Task ExportMp4Async(string ffmpegPath, string outputPath, int firstFrame, int lastFrame)
    {
        if (IsExporting) return;
        IsExporting = true;
        try
        {
            var progress = new Progress<string>(s => Status = s);
            int n = await Task.Run(() => Exporter.ExportMp4(Editor, ffmpegPath, outputPath, firstFrame, lastFrame, progress));
            Status = $"Exported {n} frames to {Path.GetFileName(outputPath)}";
        }
        catch (Exception ex) { Status = "MP4 export failed: " + ex.Message; }
        finally { IsExporting = false; }
    }
}
