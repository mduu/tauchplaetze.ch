using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>
/// Ein Platz zum Bearbeiten: Inhalt der neuesten freigegebenen Version (die Basis) und die offenen Vorschläge.
/// Ein Entwurf ohne freigegebene Version liefert den Inhalt des ersten Vorschlags.
/// </summary>
public record GetDiveSiteForEditingQuery(Guid DiveSiteId);

/// <summary>Inhalt und Basis zum Bearbeiten.</summary>
public record DiveSiteForEditing(Guid Id, DiveSiteStatus Status, int BaseNumber, DiveSiteContent Content, IReadOnlyList<RevisionSummary> Pending);

internal class GetDiveSiteForEditingHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, RevisionNames names)
    : IQueryHandler<GetDiveSiteForEditingQuery, DiveSiteForEditing?>
{
    public async Task<DiveSiteForEditing?> Handle(GetDiveSiteForEditingQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites.AsNoTracking().SingleOrDefaultAsync(d => d.Id == query.DiveSiteId, ct);
        if (site is null)
        {
            return null;
        }

        var all = await dbContext.DiveSiteRevisions.AsNoTracking().Where(r => r.DiveSiteId == site.Id).OrderBy(r => r.Number).ToListAsync(ct);
        var baseRevision = all.LastOrDefault(r => r.Status == RevisionStatus.Approved) ?? all.First();
        var pending = all.Where(r => r.Status == RevisionStatus.Pending).ToList();

        return new DiveSiteForEditing(
            site.Id,
            site.Status,
            baseRevision.Number,
            await revisions.Load(baseRevision, ct),
            await names.Summaries(pending, publicView: false, ct));
    }
}
