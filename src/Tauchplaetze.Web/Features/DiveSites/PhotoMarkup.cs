using System.Globalization;
using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Web.Features.DiveSites;

/// <summary>HTML-Attribute für Fotos.</summary>
internal static class PhotoMarkup
{
    /// <summary>`srcset` mit allen Bildgrössen, z.B. «…/480.webp 480w, …/960.webp 960w».</summary>
    public static string SrcSet(IReadOnlyList<MediaRendition> renditions) =>
        string.Join(", ", renditions.Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Url} {r.Width}w")));
}
