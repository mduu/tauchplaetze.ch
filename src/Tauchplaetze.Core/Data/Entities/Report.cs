using Tauchplaetze.Core.Features.Reports;

namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Meldung einer Person nach einem Tauchgang (`dive-site.md`, «Meldungen»). Eine pro Konto, Platz und Tag.</summary>
internal class Report
{
    public Guid Id { get; set; }

    public Guid DiveSiteId { get; set; }

    /// <summary>Schweizer Kalendertag des Tauchgangs.</summary>
    public DateOnly Date { get; set; }

    public decimal Visibility { get; set; }

    public decimal? Temperature { get; set; }

    public int? TemperatureDepth { get; set; }

    public string? Comment { get; set; }

    public ReportCommentStatus CommentStatus { get; set; }

    /// <summary><c>null</c> nach der Löschung des Kontos.</summary>
    public string? AuthorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? HiddenAt { get; set; }

    public string? HiddenBy { get; set; }

    /// <summary>Index der Prognose für Platz und Tag beim Melden. Für das Sichtmodell.</summary>
    public decimal? ForecastIndex { get; set; }

    public string? ForecastModelVersion { get; set; }
}
