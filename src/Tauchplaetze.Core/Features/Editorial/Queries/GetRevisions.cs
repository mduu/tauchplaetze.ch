using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>Alle Versionen eines Platzes im CMS, neueste zuerst, mit Status und Namen.</summary>
public record GetRevisionsQuery(Guid DiveSiteId);

internal class GetRevisionsHandler(TauchplaetzeDbContext dbContext, RevisionNames names) : IQueryHandler<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>
{
    public async Task<IReadOnlyList<RevisionSummary>> Handle(GetRevisionsQuery query, CancellationToken ct = default) =>
        await names.Summaries(
            await dbContext.DiveSiteRevisions.AsNoTracking().Where(r => r.DiveSiteId == query.DiveSiteId).OrderByDescending(r => r.Number).ToListAsync(ct),
            publicView: false,
            ct);
}
