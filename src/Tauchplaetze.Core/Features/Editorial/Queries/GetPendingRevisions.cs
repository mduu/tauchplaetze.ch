using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>Alle offenen Vorschläge, älteste zuerst, mit Platz und Alter der Basis (`cms.md`, «Vorschläge prüfen»).</summary>
public record GetPendingRevisionsQuery;

/// <summary>Ein offener Vorschlag. <see cref="BaseIsOutdated"/>: Seit der Basis wurde eine andere Version freigegeben.</summary>
public record PendingRevision(Guid DiveSiteId, string SiteName, string SiteSlug, RevisionSummary Revision, bool BaseIsOutdated);

internal class GetPendingRevisionsHandler(TauchplaetzeDbContext dbContext, RevisionNames names) : IQueryHandler<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>
{
    public async Task<IReadOnlyList<PendingRevision>> Handle(GetPendingRevisionsQuery query, CancellationToken ct = default)
    {
        var pending = await dbContext.DiveSiteRevisions.AsNoTracking().Where(r => r.Status == RevisionStatus.Pending).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        var siteIds = pending.Select(r => r.DiveSiteId).Distinct().ToList();
        var sites = await dbContext.DiveSites.AsNoTracking().Where(d => siteIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => new { d.Name, d.Slug }, ct);
        var latestApproved = await dbContext.DiveSiteRevisions.AsNoTracking()
            .Where(r => siteIds.Contains(r.DiveSiteId) && r.Status == RevisionStatus.Approved)
            .GroupBy(r => r.DiveSiteId)
            .Select(g => new { DiveSiteId = g.Key, Number = g.Max(r => r.Number) })
            .ToDictionaryAsync(x => x.DiveSiteId, x => x.Number, ct);
        var summaries = await names.Summaries(pending, publicView: false, ct);

        return pending.Zip(summaries, (r, s) => new PendingRevision(
                r.DiveSiteId,
                sites[r.DiveSiteId].Name,
                sites[r.DiveSiteId].Slug,
                s,
                r.BaseNumber is { } baseNumber && latestApproved.TryGetValue(r.DiveSiteId, out var latest) && latest > baseNumber))
            .ToList();
    }
}
