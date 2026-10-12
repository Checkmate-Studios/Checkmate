using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Checkmate.Desktop.Services;
using Checkmate.Desktop.ViewModels;

namespace Checkmate.Desktop;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ProjectType =
        new("Checkmate project") { Patterns = new[] { "*.checkmate" } };

    public MainWindow()
    {
        InitializeComponent();
        TryLoadIcon();
    }

    /// <summary>Window / title-bar / taskbar icon. Silently skipped if the icon file has not been added yet.</summary>
    private void TryLoadIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://Checkmate/Assets/Checkmate.ico"));
            Icon = new WindowIcon(stream);
        }
        catch { /* no icon file yet */ }
    }

    private MainViewModel Vm => (MainViewModel)DataContext!;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Vm.TextPlacementRequested += async point => await PlaceTextAsync(point);
    }

    private async Task PlaceTextAsync(Point documentPoint)
    {
        var item = await TextDialog.ShowAsync(this);
        if (item == null) return;
        item.X = (float)documentPoint.X;
        item.Y = (float)documentPoint.Y;
        Vm.AddText(item);
    }

    private Task<(int From, int To)?> AskRange(string title) =>
        Dialogs.AskRangeAsync(this, title, Vm.Editor.Project.FrameCount, Vm.Editor.Project.Fps, Vm.Editor.CurrentFrame + 1);

    private void OnFitClick(object? sender, RoutedEventArgs e) => DrawingCanvas.FitToView();

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save project",
            SuggestedFileName = "Untitled.checkmate",
            DefaultExtension = "checkmate",
            FileTypeChoices = new[] { ProjectType },
        });
        var path = file?.TryGetLocalPath();
        if (path != null) Vm.SaveTo(path);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var range = await AskRange("Export PNG frames");
        if (range == null) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder for the PNG sequence",
            AllowMultiple = false,
        });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (path != null) await Vm.ExportPngSequenceAsync(path, range.Value.From - 1, range.Value.To - 1);
    }

    private async void OnExportMp4Click(object? sender, RoutedEventArgs e)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg == null)
        {
            bool locate = await Dialogs.AskAsync(this, "FFmpeg is needed for MP4",
                "MP4 export uses FFmpeg, a free video tool that is not bundled with Checkmate.\n\n" +
                "Install it once:\n" +
                "   Windows:  winget install Gyan.FFmpeg\n" +
                "   macOS:  brew install ffmpeg\n" +
                "   Linux:  sudo apt install ffmpeg\n\n" +
                "Already have it somewhere? Point Checkmate to the ffmpeg program.",
                "Locate FFmpeg...");
            if (!locate) return;

            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select the ffmpeg program",
                AllowMultiple = false,
            });
            var chosen = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (chosen == null) return;

            if (!await Task.Run(() => FfmpegLocator.Verify(chosen)))
            {
                await Dialogs.AskAsync(this, "Not FFmpeg", "That file did not respond like FFmpeg.", "OK", null);
                return;
            }
            var settings = AppSettings.Load();
            settings.FfmpegPath = chosen;
            settings.Save();
            ffmpeg = chosen;
        }

        var range = await AskRange("Export MP4");
        if (range == null) return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export MP4",
            SuggestedFileName = $"{Vm.Editor.Project.Name}.mp4",
            DefaultExtension = "mp4",
            FileTypeChoices = new[] { new FilePickerFileType("MP4 video") { Patterns = new[] { "*.mp4" } } },
        });
        var path = file?.TryGetLocalPath();
        if (path != null) await Vm.ExportMp4Async(ffmpeg, path, range.Value.From - 1, range.Value.To - 1);
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open project",
            AllowMultiple = false,
            FileTypeFilter = new[] { ProjectType },
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path == null) return;
        Vm.OpenFrom(path);
        DrawingCanvas.FitToView();
    }
}
