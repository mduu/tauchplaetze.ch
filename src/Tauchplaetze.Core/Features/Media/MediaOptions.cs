namespace Tauchplaetze.Core.Features.Media;

/// <summary>Konfiguration der Fotos (Abschnitt `Media`).</summary>
public class MediaOptions
{
    public const string Section = "Media";

    /// <summary>Basis der öffentlichen Bild-URLs, z.B. die Adresse von Cloudflare vor dem Container `media`. Ohne Wert: die Adresse des Containers.</summary>
    public Uri? PublicBaseUri { get; set; }

    /// <summary>Private Fotos, die keine Version verwendet, verschwinden nach so vielen Tagen (`cms.md`, «Medien»).</summary>
    public int PurgeAfterDays { get; set; } = 30;
}
