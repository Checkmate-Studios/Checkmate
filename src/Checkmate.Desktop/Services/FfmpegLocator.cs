using System.Diagnostics;

namespace Checkmate.Desktop.Services;

public static class FfmpegLocator
{
    /// <summary>Saved path, then next to Checkmate, then PATH, then the usual install folders.</summary>
    public static string? Find()
    {
        var saved = AppSettings.Load().FfmpegPath;
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;

        string exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        foreach (var candidate in new[]
        {
            Path.Combine(AppContext.BaseDirectory, exe),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", exe),
        })
            if (File.Exists(candidate)) return candidate;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim().Trim('"'), exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* malformed PATH entry */ }
        }

        // GUI apps on macOS/Linux often do not inherit the shell PATH
        foreach (var candidate in new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg" })
            if (File.Exists(candidate)) return candidate;

        return null;
    }

    /// <summary>True if the program really is FFmpeg (runs "ffmpeg -version").</summary>
    public static bool Verify(string path)
    {
        try
        {
            var psi = new ProcessStartInfo(path, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(5000)) { p.Kill(); return false; }
            return output.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
