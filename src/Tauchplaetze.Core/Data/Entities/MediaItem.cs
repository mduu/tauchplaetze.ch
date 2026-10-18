using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Core.Data.Entities;

internal class MediaItem
{
    public Guid Id { get; set; }

    public Guid? DiveSiteId { get; set; }

    public int? WaterBodyId { get; set; }

    public MediaSection Section { get; set; }

    public Guid? EntryPointId { get; set; }

    public MediaKind Kind { get; set; }

    public required string BlobPath { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public DateOnly? TakenAt { get; set; }

    public int? Depth { get; set; }

    public required string Photographer { get; set; }

    public string? PhotographerUrl { get; set; }

    public string? SourceUrl { get; set; }

    /// <summary>Position im Abschnitt der freigegebenen Version. <c>null</c>: Die freigegebene Version verwendet das Foto nicht.</summary>
    public int? SortOrder { get; set; }

    public string? UploadedBy { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public bool RightsConfirmed { get; set; }

    public bool IsPublic { get; set; }

    public List<MediaItemTranslation> Translations { get; set; } = [];
}

internal class MediaItemTranslation
{
    public Guid MediaItemId { get; set; }

    public required string Culture { get; set; }

    public string? Caption { get; set; }

    public string? AltText { get; set; }
}
