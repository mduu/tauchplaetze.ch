using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Susv;

namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Eintrag des SUSV, importiert von `ImportSusvEntries` (ADR-013). Nicht Teil der Versionen eines Platzes.</summary>
internal class SusvEntry
{
    public Guid Id { get; set; }

    public int SourceId { get; set; }

    public SusvTopic Topic { get; set; }

    public required Point Location { get; set; }

    public DateTimeOffset? SourcePublishedAt { get; set; }

    public required string ContentHash { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public byte MissedRuns { get; set; }

    public DateTimeOffset? RemovedAt { get; set; }

    public SusvReviewStatus ReviewStatus { get; set; }

    public bool ChangedSinceReview { get; set; }

    public NoticeKind? NoticeKind { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public bool RecursYearly { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    /// <summary>Für die Detailseiten: Montag des letzten vollen Abrufs aller Texte.</summary>
    public DateOnly? TextsFetchedOn { get; set; }

    public List<SusvEntryTranslation> Translations { get; set; } = [];

    public List<SusvEntrySite> Sites { get; set; } = [];
}

internal class SusvEntryTranslation
{
    public Guid EntryId { get; set; }

    public required string Culture { get; set; }

    public required string Title { get; set; }

    public string? Teaser { get; set; }

    public string? Text { get; set; }

    public required string DetailUrl { get; set; }

    public bool IsMachineTranslated { get; set; }

    /// <summary>Titel zum Zeitpunkt der letzten Prüfung. Für den Vergleich «geändert seit der Prüfung».</summary>
    public string? ReviewedTitle { get; set; }

    /// <summary>Text zum Zeitpunkt der letzten Prüfung.</summary>
    public string? ReviewedText { get; set; }
}

internal class SusvEntrySite
{
    public Guid EntryId { get; set; }

    public Guid DiveSiteId { get; set; }
}
