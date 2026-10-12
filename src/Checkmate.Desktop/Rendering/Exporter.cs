using System.Diagnostics;
using System.Globalization;
using Checkmate.Core.Editing;
using SkiaSharp;

namespace Checkmate.Desktop.Rendering;

public static class Exporter
{
    /// <summary>
    /// Renders frames <paramref name="firstFrame"/>..<paramref name="lastFrame"/> (0-based, inclusive) and streams the raw
    /// pixels into FFmpeg, which encodes an H.264 MP4 (yuv420p, plays everywhere). No temporary image files.
    /// Safe to call off the UI thread. Low frame rates (e.g. 12 fps) are written as 24 fps with repeated frames so every
    /// player and social site accepts the file; the timing is identical.
    /// </summary>
    public static int ExportMp4(Editor editor, string ffmpegPath, string outputPath,
                                int firstFrame, int lastFrame, IProgress<string>? progress = null)
    {
        int count, w, h, fps;
        lock (editor.SyncRoot)
        {
            count = editor.Project.FrameCount;
            w = editor.Project.Width;
            h = editor.Project.Height;
            fps = Math.Max(1, editor.Project.Fps);
        }
        firstFrame = Math.Clamp(firstFrame, 0, count - 1);
        lastFrame = Math.Clamp(lastFrame, firstFrame, count - 1);
        int total = lastFrame - firstFrame + 1;
        int outFps = Math.Max(24, fps);

        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in new[]
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "rawvideo", "-pix_fmt", "rgba", "-s", $"{w}x{h}",
            "-framerate", fps.ToString(CultureInfo.InvariantCulture), "-i", "-",
            "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",        // H.264 needs even dimensions
            "-c:v", "libx264", "-preset", "medium", "-crf", "16",
            "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-r", outFps.ToString(CultureInfo.InvariantCulture),
            outputPath,
        })
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start FFmpeg.");
        // drain output so FFmpeg can never block on a full pipe
        var stderrTask = proc.StandardError.ReadToEndAsync();
        _ = proc.StandardOutput.ReadToEndAsync();

        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            var stdin = proc.StandardInput.BaseStream;

            for (int f = firstFrame; f <= lastFrame; f++)
            {
                lock (editor.SyncRoot)
                    Compositor.DrawPageVector(surface.Canvas, editor.Project, f);   // paper is opaque: overwrites the last frame

                using (var pixmap = surface.PeekPixels())
                {
                    var bytes = pixmap.GetPixelSpan();
                    if (pixmap.RowBytes == w * 4) stdin.Write(bytes);
                    else for (int y = 0; y < h; y++) stdin.Write(bytes.Slice(y * pixmap.RowBytes, w * 4));
                }
                progress?.Report($"Encoding MP4: frame {f - firstFrame + 1} / {total}...");
            }
            stdin.Flush();
            proc.StandardInput.Close();
        }
        catch (IOException)
        {
            // FFmpeg quit early (closed pipe). Its error message, read below, explains why.
        }
        catch
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch { }
            throw;
        }

        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            var err = stderrTask.GetAwaiter().GetResult().Trim();
            if (err.Length > 400) err = err[..400];
            throw new InvalidOperationException(string.IsNullOrEmpty(err)
                ? $"FFmpeg exited with code {proc.ExitCode}." : err);
        }
        return total;
    }

    /// <summary>Writes name_0001.png, name_0002.png ... for frames firstFrame..lastFrame (0-based, inclusive). Numbers are the real frame numbers.</summary>
    public static int ExportPngSequence(Editor editor, string folder, string baseName,
                                        int firstFrame, int lastFrame, IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(folder);
        int count; int w; int h;
        lock (editor.SyncRoot)
        {
            count = editor.Project.FrameCount;
            w = editor.Project.Width;
            h = editor.Project.Height;
        }
        firstFrame = Math.Clamp(firstFrame, 0, count - 1);
        lastFrame = Math.Clamp(lastFrame, firstFrame, count - 1);
        int total = lastFrame - firstFrame + 1;

        for (int f = firstFrame; f <= lastFrame; f++)
        {
            using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
            lock (editor.SyncRoot)
                Compositor.DrawPageVector(surface.Canvas, editor.Project, f);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(folder, $"{baseName}_{f + 1:D4}.png"));
            data.SaveTo(file);
            progress?.Report($"Exporting {f - firstFrame + 1} / {total}...");
        }
        return total;
    }
}
