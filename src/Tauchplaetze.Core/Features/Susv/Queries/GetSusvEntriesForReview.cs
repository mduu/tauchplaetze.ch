using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Susv.Queries;

/// <summary>
/// Die Einträge des SUSV für `/admin/susv`: zuerst offene, dann bestätigte mit Änderung der Quelle, dann die übrigen.
/// Texte in <see cref="Culture"/>, sonst Deutsch. Nur Moderation und Admin, sonst <c>null</c>.
/// </summary>
public record GetSusvEntriesForReviewQuery(string UserId, string Culture);

/// <summary>Die Einträge und alle veröffentlichten Plätze für die Suche nach weiteren Plätzen.</summary>
public record SusvReviewList(IReadOnlyList<SusvReviewItem> Entries, IReadOnlyList<SusvSiteOption> Sites, double SuggestionRadiusMeters = SusvRules.SuggestionRadiusMeters);

/// <summary>
/// Ein Eintrag zur Prüfung. <see cref="PreviousTitle"/> und <see cref="PreviousText"/>: Stand der letzten Prüfung, nur bei «geändert seit der Prüfung».
/// <see cref="Suggestions"/>: veröffentlichte Plätze im Umkreis, der nächste zuerst.
/// </summary>
public record SusvReviewItem(
    Guid Id,
    SusvTopic Topic,
    SusvReviewStatus Status,
    bool ChangedSinceReview,
    DateTimeOffset? RemovedAt,
    DateTimeOffset? SourcePublishedAt,
    DateTimeOffset FirstSeenAt,
    string Title,
    string? Text,
    string DetailUrl,
    string? PreviousTitle,
    string? PreviousText,
    double Latitude,
    double Longitude,
    NoticeKind? Kind,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool RecursYearly,
    IReadOnlyList<Guid> SiteIds,
    IReadOnlyList<SusvSiteSuggestion> Suggestions,
    SusvReviewState State = SusvReviewState.Open);

/// <summary>Ein Platz im Umkreis eines Eintrags, mit Distanz in Metern.</summary>
public record SusvSiteSuggestion(Guid Id, string Name, string WaterBodyName, int DistanceMeters);

/// <summary>Ein veröffentlichter Platz zur Auswahl, mit Koordinate für die Karte der Prüfung.</summary>
public record SusvSiteOption(Guid Id, string Name, string WaterBodyName, double Latitude, double Longitude);

internal class GetSusvEntriesForReviewHandler(TauchplaetzeDbContext dbContext, AccountRoles roles)
    : IQueryHandler<GetSusvEntriesForReviewQuery, SusvReviewList?>
{
    public async Task<SusvReviewList?> Handle(GetSusvEntriesForReviewQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.UserId)).CanModerate)
        {
            return null;
        }

        var entries = await dbContext.SusvEntries.AsNoTracking().Include(e => e.Translations).Include(e => e.Sites).ToListAsync(ct);
        var sites = await dbContext.DiveSites.AsNoTracking()
            .Where(s => s.Status == DiveSiteStatus.Published)
            .Select(s => new { s.Id, s.Name, s.Location, s.WaterBody!.NameDe, s.WaterBody.NameEn, s.WaterBody.NameFr, s.WaterBody.NameIt })
            .ToListAsync(ct);

        var items = entries
            .OrderBy(StateOf)
            .ThenBy(e => e.Topic)
            .ThenBy(e => Title(e, "de"))
            .Select(e =>
            {
                var translation = Translation(e, query.Culture);
                var german = Translation(e, "de");
                var suggestions = sites
                    .Select(s => new SusvSiteSuggestion(s.Id, s.Name, WaterBodyNames.In(query.Culture, s.NameDe, s.NameEn, s.NameFr, s.NameIt), (int)Math.Round(Distance.Km(e.Location, s.Location) * 1000)))
                    .Where(s => s.DistanceMeters <= SusvRules.SuggestionRadiusMeters)
                    .OrderBy(s => s.DistanceMeters)
                    .ToList();
                return new SusvReviewItem(
                    e.Id, e.Topic, e.ReviewStatus, e.ChangedSinceReview, e.RemovedAt, e.SourcePublishedAt, e.FirstSeenAt,
                    translation.Title, translation.Text ?? translation.Teaser, translation.DetailUrl,
                    e.ChangedSinceReview ? german.ReviewedTitle : null,
                    e.ChangedSinceReview ? german.ReviewedText : null,
                    e.Location.Y, e.Location.X, e.NoticeKind, e.ValidFrom, e.ValidUntil, e.RecursYearly,
                    [.. e.Sites.Select(s => s.DiveSiteId)],
                    suggestions,
                    StateOf(e));
            })
            .ToList();

        return new SusvReviewList(items, [.. sites.OrderBy(s => s.Name).Select(s => new SusvSiteOption(s.Id, s.Name, WaterBodyNames.In(query.Culture, s.NameDe, s.NameEn, s.NameFr, s.NameIt), s.Location.Y, s.Location.X))]);
    }

    private static SusvReviewState StateOf(SusvEntry entry) => entry switch
    {
        { RemovedAt: not null } => SusvReviewState.Removed,
        { ReviewStatus: SusvReviewStatus.Pending } => SusvReviewState.Open,
        { ReviewStatus: SusvReviewStatus.Ignored } => SusvReviewState.Ignored,
        { ChangedSinceReview: true } => SusvReviewState.Changed,
        _ => SusvReviewState.Confirmed,
    };

    private static SusvEntryTranslation Translation(SusvEntry entry, string culture) =>
        entry.Translations.FirstOrDefault(t => t.Culture == culture) ?? entry.Translations.First(t => t.Culture == "de");

    private static string Title(SusvEntry entry, string culture) => Translation(entry, culture).Title;
}
