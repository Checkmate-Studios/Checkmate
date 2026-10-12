using System.Text.Json;

namespace Checkmate.Desktop.Services;

/// <summary>Tiny per-user settings file (remembers where FFmpeg is).</summary>
public sealed class AppSettings
{
    public string? FfmpegPath { get; set; }
    public string? PaletteName { get; set; }
    public List<string> CustomColors { get; set; } = new();

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Checkmate", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* corrupt settings: start fresh */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { /* not fatal */ }
    }
}
