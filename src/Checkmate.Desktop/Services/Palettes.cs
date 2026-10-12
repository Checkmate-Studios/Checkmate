namespace Checkmate.Desktop.Services;

/// <summary>A named set of colours ("#RRGGBB").</summary>
public sealed class PaletteSet
{
    public PaletteSet(string name, IEnumerable<string> colors, bool isCustom = false)
    {
        Name = name;
        Colors = colors.ToList();
        IsCustom = isCustom;
    }

    public string Name { get; }
    public List<string> Colors { get; }
    /// <summary>The user's own palette ("My colors"); the only one that can be edited.</summary>
    public bool IsCustom { get; }
}

public static class Palettes
{
    public static IReadOnlyList<PaletteSet> BuiltIn { get; } = new[]
    {
        new PaletteSet("Classic", new[]
        {
            "#000000", "#2B2B2B", "#555555", "#808080", "#AAAAAA", "#D5D5D5", "#FFFFFF",
            "#FFE0D0", "#F7C8A8", "#E8A87C", "#C68642", "#8D5524",
            "#FF6B6B", "#E63946", "#B5121B", "#7A0C1B", "#FFA94D", "#F77F00", "#FFE066", "#FCBF49",
            "#A7E57A", "#4CAF50", "#2D6A4F", "#0B3D2E",
            "#7FE5E5", "#00B4D8", "#6EA8FE", "#3A86FF", "#1D3FA6", "#0B1D51",
            "#B388FF", "#7B2CBF", "#4B1A7A", "#FF9EC4", "#FF4D9D", "#C2185B",
            "#A47148", "#6B4226", "#3E2723",
        }),

        new PaletteSet("Anime: skin, hair & eyes", new[]
        {
            // skin
            "#FFF1E6", "#FFE0D0", "#F7C8A8", "#EDB48E", "#E0A07A", "#C68642", "#A9693B", "#8D5524", "#5C3A21",
            // blush and lips
            "#FF9AA2", "#F28B9B", "#E5707E", "#C94F5B",
            // hair
            "#0A0A0F", "#1C1C28", "#3B2A20", "#5B3A29", "#8A5A3C", "#C9884C", "#F2D16B", "#F7E7A1",
            "#E8E8F0", "#B8B8C8", "#E63946", "#FF6F91", "#6EA8FE", "#2A5CAA", "#3DDAD7", "#7B4FD1",
            "#B388FF", "#4CAF50", "#FF9F43",
            // eyes
            "#1B3A6B", "#2E86AB", "#3DDAD7", "#2E7D32", "#8BC34A", "#FFB703", "#B5651D", "#C2185B", "#7B1FA2", "#E63946",
        }),

        new PaletteSet("Pastel", new[]
        {
            "#FFD6E0", "#FFC8DD", "#FFAFCC", "#BDE0FE", "#A2D2FF", "#CDB4DB", "#E2CFF4", "#D0F4DE",
            "#B9FBC0", "#98F5E1", "#A9DEF9", "#CFF7FF", "#FFF1A8", "#FDFFB6", "#FFE5B4", "#FFD6A5",
            "#FFC9B5", "#FDE4CF", "#E4C1F9", "#F1C0E8", "#CFBAF0", "#B5EAD7", "#C7F9CC", "#EAE4E9",
        }),

        new PaletteSet("Vivid pop", new[]
        {
            "#FF0054", "#FF5400", "#FFBD00", "#FFE600", "#9EF01A", "#38B000", "#00F5D4", "#00BBF9",
            "#0077FF", "#3A0CA3", "#7209B7", "#F15BB5", "#FF006E", "#FB5607", "#FFBE0B", "#8338EC",
            "#3A86FF", "#06D6A0", "#EF476F", "#FFD166", "#118AB2", "#073B4C", "#000000", "#FFFFFF",
        }),

        new PaletteSet("Neon cyber", new[]
        {
            "#0D0221", "#261447", "#541388", "#2E294E", "#1B1B3A", "#320D6D", "#F1E9DA", "#FF2E63",
            "#FF6FB5", "#08F7FE", "#09FBD3", "#FE53BB", "#F5D300", "#7DF9FF", "#39FF14", "#FF073A",
            "#BC13FE", "#00FFC8", "#FF00A0", "#00B2FF",
        }),

        new PaletteSet("Sunset", new[]
        {
            "#1F0F3C", "#2B1055", "#4B1D6B", "#7B2A7E", "#A83279", "#D1477A",
            "#EE6A6F", "#F9906F", "#FDB36E", "#FFD37A", "#FFE9A0", "#FFF4D1",
        }),

        new PaletteSet("Ocean & sky", new[]
        {
            "#03045E", "#023E8A", "#0077B6", "#0096C7", "#00B4D8", "#48CAE4", "#90E0EF", "#ADE8F4",
            "#CAF0F8", "#E9F8FF", "#5FA8D3", "#1B4965", "#62B6CB", "#BEE9E8", "#CAE9FF", "#FFFFFF",
        }),

        new PaletteSet("Forest & earth", new[]
        {
            "#0B3D2E", "#1B4332", "#2D6A4F", "#40916C", "#52B788", "#74C69D", "#95D5B2", "#B7E4C7", "#D8F3DC",
            "#3E2723", "#6B4226", "#7F5539", "#8B5E3C", "#A47148", "#C69C6D", "#DDB892", "#E6CCB2", "#EDE0D4",
        }),

        new PaletteSet("Sakura & spring", new[]
        {
            "#FFF0F5", "#FFE4EC", "#FFD1DC", "#FFB7C5", "#FF9EB5", "#F78FB3", "#E4678D", "#C9456F",
            "#8E2C48", "#F6F7D3", "#E3F0AF", "#B6D98B", "#8CC084", "#5A9E6F", "#355E3B", "#A0522D",
        }),

        new PaletteSet("Night city", new[]
        {
            "#05060F", "#0B1026", "#141B3C", "#1F2A5C", "#2D3B7A", "#3F4F9C", "#5C6BC0", "#7986CB",
            "#FF6B9D", "#C44569", "#F8B500", "#FFE66D", "#6BF0FF", "#00A8E8", "#9D4EDD", "#3C096C",
        }),

        new PaletteSet("Retro 8-bit", new[]
        {
            "#000000", "#FCFCFC", "#BCBCBC", "#7C7C7C", "#F83800", "#E40058", "#A80020", "#503000",
            "#FCA044", "#F8B800", "#00B800", "#007800", "#0058F8", "#3CBCFC", "#6844FC", "#D800CC",
        }),

        new PaletteSet("Grayscale", Enumerable.Range(0, 16).Select(i => $"#{i * 17:X2}{i * 17:X2}{i * 17:X2}")),
    };
}
