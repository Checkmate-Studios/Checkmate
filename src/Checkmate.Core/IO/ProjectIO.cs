using System.Text.Json;
using Checkmate.Core.Model;

namespace Checkmate.Core.IO;

/// <summary>
/// Milestone 1 format: plain JSON (easy to debug and diff). Milestone 2 moves to a zip container
/// holding project.json plus audio/media, keeping the same model.
/// </summary>
public static class ProjectIO
{
    private sealed class Envelope
    {
        public string Format { get; set; } = "checkmate";
        public int Version { get; set; } = 1;
        public Project Project { get; set; } = new();
    }

    public static void Save(Project project, string path)
    {
        var json = JsonSerializer.Serialize(new Envelope { Project = project });
        File.WriteAllText(path, json);
    }

    public static Project Load(string path)
    {
        var env = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path))
                  ?? throw new InvalidDataException("Not a Checkmate project.");
        if (env.Format != "checkmate") throw new InvalidDataException("Not a Checkmate project.");
        return env.Project;
    }
}
