using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Susv;

namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Ein Hinweis der Redaktion oder eine bestätigte Einschränkung des SUSV, mit Art und Zeitraum.</summary>
internal record SiteRestriction(Guid SiteId, NoticeKind Kind, NoticePeriod Period);

/// <summary>
/// Hinweise und Einschränkungen des SUSV aller Plätze, für Listen, Karte und Rangliste. Eine Sperrung des SUSV wirkt wie eine der Redaktion (Story 003).
/// Der Zeitraum «jedes Jahr» lässt sich nicht einfach in SQL prüfen. Darum lädt die Query alle Zeiträume (wenige hundert) und prüft sie im Speicher.
/// </summary>
internal static class SiteRestrictions
{
    public static async Task<ILookup<Guid, SiteRestriction>> Load(TauchplaetzeDbContext dbContext, CancellationToken ct)
    {
        var notices = await dbContext.DiveSiteNotices
            .AsNoTracking()
            .Select(n => new { n.DiveSiteId, n.Kind, n.ValidFrom, n.ValidUntil, n.RecursYearly })
            .ToListAsync(ct);
        var susv = await dbContext.SusvEntries
            .AsNoTracking()
            .Where(SusvRules.ShownRestriction)
            .SelectMany(e => e.Sites.Select(s => new { s.DiveSiteId, Kind = e.NoticeKind!.Value, e.ValidFrom, e.ValidUntil, e.RecursYearly }))
            .ToListAsync(ct);

        return notices
            .Select(n => new SiteRestriction(n.DiveSiteId, n.Kind, new NoticePeriod(n.ValidFrom, n.ValidUntil, n.RecursYearly)))
            .Concat(susv.Select(n => new SiteRestriction(n.DiveSiteId, n.Kind, new NoticePeriod(n.ValidFrom, n.ValidUntil, n.RecursYearly))))
            .ToLookup(r => r.SiteId);
    }

    /// <summary>Die schwerste am Tag gültige Art. Null: keine.</summary>
    public static NoticeKind? WorstOn(this IEnumerable<SiteRestriction> restrictions, DateOnly day) =>
        restrictions.Where(r => r.Period.IsActive(day)).Select(r => (NoticeKind?)r.Kind).Max();

    public static bool IsClosedOn(this IEnumerable<SiteRestriction> restrictions, DateOnly day) =>
        restrictions.Any(r => r.Kind == NoticeKind.Closure && r.Period.IsActive(day));
}
