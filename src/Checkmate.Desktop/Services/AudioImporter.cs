using System.Diagnostics;
using Checkmate.Core.Audio;
using NLayer;

namespace Checkmate.Desktop.Services;

/// <summary>
/// Loads a sound file into memory. WAV and MP3 work out of the box; every other format
/// (ogg, flac, m4a, aac, wma, opus...) is converted with FFmpeg, the same tool used for MP4 export.
/// </summary>
public static class AudioImporter
{
    public static AudioData Load(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".wav")
        {
            try { return WavFile.Read(path); }
            catch { /* unusual WAV flavour: let FFmpeg try */ }
        }
        else if (ext == ".mp3")
        {
            try { return LoadMp3(path); }
            catch { /* odd MP3: let FFmpeg try */ }
        }

        return LoadWithFfmpeg(path);
    }

    private static AudioData LoadMp3(string path)
    {
        using var mpeg = new MpegFile(path);
        int channels = mpeg.Channels;
        var samples = new List<short>(mpeg.SampleRate * channels * 60);
        var buffer = new float[8192];
        int read;
        while ((read = mpeg.ReadSamples(buffer, 0, buffer.Length)) > 0)
            for (int i = 0; i < read; i++)
                samples.Add((short)Math.Clamp(buffer[i] * 32767f, -32768f, 32767f));
        if (samples.Count == 0) throw new InvalidDataException("The MP3 contains no sound.");
        return new AudioData(mpeg.SampleRate, channels, samples.ToArray());
    }

    private static AudioData LoadWithFfmpeg(string path)
    {
        var ffmpeg = FfmpegLocator.Find();
        if (ffmpeg == null)
            throw new InvalidOperationException(
                "This sound format needs FFmpeg. Install it once (Windows: winget install Gyan.FFmpeg, macOS: brew install ffmpeg, " +
                "Linux: sudo apt install ffmpeg), or use a WAV or MP3 file instead.");

        string temp = Path.Combine(Path.GetTempPath(), $"checkmate_{Guid.NewGuid():N}.wav");
        try
        {
            var psi = new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (var a in new[] { "-y", "-hide_banner", "-loglevel", "error", "-i", path, "-vn", "-ac", "2", "-ar", "44100", "-c:a", "pcm_s16le", temp })
                psi.ArgumentList.Add(a);

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start FFmpeg.");
            var stderr = proc.StandardError.ReadToEndAsync();
            _ = proc.StandardOutput.ReadToEndAsync();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                var err = stderr.GetAwaiter().GetResult().Trim();
                throw new InvalidOperationException(string.IsNullOrEmpty(err) ? "FFmpeg could not read that file." : err);
            }
            return WavFile.Read(temp);
        }
        finally
        {
            try { File.Delete(temp); } catch { /* temp file, not important */ }
        }
    }
}
