using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Die öffentliche History eines Platzes: nur freigegebene Versionen, Namen nur mit Einwilligung (`cms.md`, «History»).</summary>
public record GetPublicRevisionsQuery(string Slug);

/// <summary>Name des Platzes und seine freigegebenen Versionen, neueste zuerst. <c>null</c> für einen unbekannten oder nicht öffentlichen Platz.</summary>
public record PublicRevisions(string SiteName, string Slug, IReadOnlyList<RevisionSummary> Revisions);

internal class GetPublicRevisionsHandler(TauchplaetzeDbContext dbContext, RevisionNames names) : IQueryHandler<GetPublicRevisionsQuery, PublicRevisions?>
{
    public async Task<PublicRevisions?> Handle(GetPublicRevisionsQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites.AsNoTracking().SingleOrDefaultAsync(d => d.Slug == query.Slug && d.Status == DiveSiteStatus.Published, ct);
        if (site is null)
        {
            return null;
        }

        var approved = await dbContext.DiveSiteRevisions.AsNoTracking()
            .Where(r => r.DiveSiteId == site.Id && r.Status == RevisionStatus.Approved && r.RedactedAt == null)
            .OrderByDescending(r => r.Number)
            .ToListAsync(ct);
        return new PublicRevisions(site.Name, site.Slug, await names.Summaries(approved, publicView: true, ct));
    }
}
