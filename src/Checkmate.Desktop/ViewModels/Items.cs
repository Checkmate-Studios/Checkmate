using System.Windows.Input;
using Avalonia.Media;
using Checkmate.Core.Editing;
using Checkmate.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Checkmate.Desktop.ViewModels;

public sealed class LayerItem : ObservableObject
{
    private readonly Editor _editor;
    private readonly Layer _layer;

    public LayerItem(Editor editor, Layer layer, int modelIndex)
    {
        _editor = editor; _layer = layer; ModelIndex = modelIndex;
    }

    public int ModelIndex { get; }
    public string Name => _layer.Name;

    public bool IsVisible
    {
        get => _layer.Visible;
        set { _editor.SetLayerVisible(_layer, value); OnPropertyChanged(); }
    }
}

public sealed class FrameCell : ObservableObject
{
    private bool _isCurrent, _hasKey;

    public FrameCell(int index, Action<int> goTo)
    {
        Index = index;
        GoToCommand = new RelayCommand(() => goTo(index));
    }

    public int Index { get; }
    public int Number => Index + 1;
    public ICommand GoToCommand { get; }

    public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }
    /// <summary>True when a new drawing starts on this frame (on the active layer).</summary>
    public bool HasKey { get => _hasKey; set => SetProperty(ref _hasKey, value); }
}

public sealed record ToolItem(BrushType Type, string Name, string Shortcut, bool IsEyedropper = false, bool IsText = false)
{
    public string Id => IsEyedropper ? "Eyedropper" : IsText ? "Text" : Type.ToString();
    public string Label => $"{Name}   ({Shortcut})";
}

/// <summary>A clickable colour square (palette or recent colours).</summary>
public sealed class Swatch
{
    public Swatch(Avalonia.Media.Color color, Action<Avalonia.Media.Color> pick)
    {
        Color = color;
        Brush = new SolidColorBrush(color);
        PickCommand = new RelayCommand(() => pick(color));
    }

    public Avalonia.Media.Color Color { get; }
    public string Hex => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
    public IBrush Brush { get; }
    public ICommand PickCommand { get; }
}

/// <summary>One sound clip in the sound list, with the controls for it.</summary>
public sealed class AudioClipItem : ObservableObject
{
    private readonly Editor _editor;

    public AudioClipItem(Editor editor, AudioClip clip) { _editor = editor; Clip = clip; }

    public AudioClip Clip { get; }

    public string Label => Clip.Data == null
        ? $"{Clip.Name}  (file missing)"
        : $"{Clip.Name}  ({Clip.Duration:0.0} s)";

    public double Volume
    {
        get => Clip.Volume;
        set { _editor.SetClipVolume(Clip, value); OnPropertyChanged(); }
    }

    public bool Muted
    {
        get => Clip.Muted;
        set { _editor.SetClipMuted(Clip, value); OnPropertyChanged(); }
    }

    /// <summary>1-based, like the frame numbers on screen.</summary>
    public decimal? StartFrame
    {
        get => Clip.StartFrame + 1;
        set
        {
            if (value is { } v) _editor.SetClipStart(Clip, (int)v - 1);
            OnPropertyChanged();
        }
    }

    /// <summary>Tell the UI that something may have changed behind its back (for example an undo).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);
}
