using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>Regeln für die Angaben zu einem Foto (`cms.md`, «Medien»).</summary>
internal static class MediaRules
{
    /// <summary>Höchstens so viele Uploads pro Stunde und Konto. Moderation ausgenommen.</summary>
    public const int MaxPerHour = 30;

    /// <summary>Höchstens so viele Uploads pro Tag und Konto. Moderation ausgenommen.</summary>
    public const int MaxPerDay = 100;

    /// <summary>Fehler bei Fotograf und Link. Der Link muss absolut sein und mit http oder https beginnen, sonst wäre er ein Einfallstor für Skripte.</summary>
    public static IEnumerable<EditorialError> PhotographerErrors(string? photographer, string? url)
    {
        if (string.IsNullOrWhiteSpace(photographer) || photographer.Trim().Length > 120)
        {
            yield return EditorialError.InvalidPhotographer;
        }

        if (!string.IsNullOrWhiteSpace(url) && !IsWebUrl(url.Trim()))
        {
            yield return EditorialError.InvalidPhotographerUrl;
        }
    }

    private static bool IsWebUrl(string url) =>
        url.Length <= 300
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
