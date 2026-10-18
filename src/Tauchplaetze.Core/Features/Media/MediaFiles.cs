namespace Tauchplaetze.Core.Features.Media;

/// <summary>Bildgrössen und Dateinamen eines Fotos (`cms.md`, «Medien»).</summary>
public static class MediaFiles
{
    /// <summary>Höchstens so gross darf eine hochgeladene Datei sein.</summary>
    public const long MaxBytes = 25 * 1024 * 1024;

    /// <summary>Breiten der Bildgrössen in Pixel.</summary>
    public static readonly int[] Widths = [480, 960, 1600, 2400];

    /// <summary>
    /// Die Breiten, die es für ein Foto gibt: alle kleineren Standardbreiten und die eigene Breite, höchstens 2400 px.
    /// Kein Foto wird vergrössert.
    /// </summary>
    public static IReadOnlyList<int> WidthsFor(int width) =>
        [.. Widths.Where(w => w < width).Append(Math.Min(width, Widths[^1])).Distinct().Order()];

    /// <summary>Dateiname einer Bildgrösse.</summary>
    public static string Rendition(int width) => $"{width}.webp";

    /// <summary>MIME-Type nach Dateiendung.</summary>
    public static string ContentTypeOf(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".png" => "image/png",
        _ => "image/jpeg",
    };
}
