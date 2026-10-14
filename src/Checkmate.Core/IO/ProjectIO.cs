using System.Text.Json;
using Checkmate.Core.Audio;
using Checkmate.Core.Model;

namespace Checkmate.Core.IO;

/// <summary>
/// Project format: a JSON file (easy to debug and diff). Sound clips are stored as WAV files in a folder
/// next to it called "&lt;project name&gt;_audio" (keep the two together when you move a project).
/// A compact single-file container is planned.
/// </summary>
public static class ProjectIO
{
    private sealed class Envelope
    {
        public string Format { get; set; } = "checkmate";
        public int Version { get; set; } = 1;
        public Project Project { get; set; } = new();
    }

    public static string AudioFolderFor(string projectPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? ".",
                     Path.GetFileNameWithoutExtension(projectPath) + "_audio");

    public static void Save(Project project, string path)
    {
        SaveAudio(project, AudioFolderFor(path));
        var json = JsonSerializer.Serialize(new Envelope { Project = project });
        File.WriteAllText(path, json);
    }

    public static Project Load(string path)
    {
        var env = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path))
                  ?? throw new InvalidDataException("Not a Checkmate project.");
        if (env.Format != "checkmate") throw new InvalidDataException("Not a Checkmate project.");

        var project = env.Project;
        string folder = AudioFolderFor(path);
        foreach (var clip in project.Audio)
        {
            if (clip.File == null) { clip.Missing = true; continue; }
            try
            {
                string file = Path.Combine(folder, clip.File);
                if (File.Exists(file)) clip.Data = WavFile.Read(file);
                else clip.Missing = true;
            }
            catch { clip.Missing = true; }
        }
        return project;
    }

    private static void SaveAudio(Project project, string folder)
    {
        bool anyData = project.Audio.Any(c => c.Data != null);
        if (anyData) Directory.CreateDirectory(folder);

        foreach (var clip in project.Audio)
        {
            if (clip.Data == null) continue;                           // missing on disk: keep the reference as it was
            clip.File ??= $"{Sanitize(clip.Name)}_{Guid.NewGuid().ToString("N")[..8]}.wav";
            string full = Path.Combine(folder, clip.File);
            if (!File.Exists(full)) WavFile.Write(full, clip.Data);     // clips never change, so an existing file is current
        }

        // tidy up sounds that were removed from the project
        if (Directory.Exists(folder))
        {
            var used = new HashSet<string>(project.Audio.Where(c => c.File != null).Select(c => c.File!),
                                           StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(folder, "*.wav"))
                if (!used.Contains(Path.GetFileName(file)))
                {
                    try { File.Delete(file); } catch { /* not important */ }
                }
        }
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var s = new string(chars).Trim('_');
        return s.Length == 0 ? "sound" : (s.Length > 40 ? s[..40] : s);
    }
}
