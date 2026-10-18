namespace Tauchplaetze.Core.Features.Media;

/// <summary>Legende und Alternativtext eines Fotos in einer Sprache.</summary>
public record MediaText(string Culture, string? Caption, string? AltText);

/// <summary>Ergebnis eines Uploads: die Id des Fotos oder Fehler.</summary>
public record UploadMediaResult(IReadOnlyList<Editorial.EditorialError> Errors, Guid? MediaId = null)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>Eine Bildgrösse mit Adresse.</summary>
public record MediaRendition(int Width, string Url);

/// <summary>Ein Foto für das CMS. <see cref="IsPublic"/> false: noch privat, die Adressen zeigen auf den geschützten Endpunkt.</summary>
public record SiteMediaItem(
    Guid Id,
    MediaSection Section,
    int Width,
    int Height,
    DateOnly? TakenAt,
    int? Depth,
    string Photographer,
    string? PhotographerUrl,
    bool IsPublic,
    bool IsOwn,
    DateTimeOffset UploadedAt,
    IReadOnlyList<MediaRendition> Renditions,
    IReadOnlyList<MediaText> Texts);

/// <summary>Ein öffentliches Foto in der Sprache der Seite (Fallback Deutsch).</summary>
public record PublicPhoto(
    Guid Id,
    MediaSection Section,
    Guid? EntryPointId,
    int Width,
    int Height,
    int? Depth,
    string Photographer,
    string? PhotographerUrl,
    string? Caption,
    string? AltText,
    IReadOnlyList<MediaRendition> Renditions);
