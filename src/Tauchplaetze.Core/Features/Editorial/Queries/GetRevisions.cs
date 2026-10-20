using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>Alle Versionen eines Platzes im CMS, neueste zuerst, mit Status und Namen. Nur für die Moderation, sonst leer.</summary>
public record GetRevisionsQuery(Guid DiveSiteId, string RequesterId);

internal class GetRevisionsHandler(TauchplaetzeDbContext dbContext, RevisionNames names, AccountRoles roles) : IQueryHandler<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>
{
    public async Task<IReadOnlyList<RevisionSummary>> Handle(GetRevisionsQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.RequesterId)).CanModerate)
        {
            return [];
        }

        return await names.Summaries(
            await dbContext.DiveSiteRevisions.AsNoTracking().Where(r => r.DiveSiteId == query.DiveSiteId).OrderByDescending(r => r.Number).ToListAsync(ct),
            publicView: false,
            ct);
    }
}
