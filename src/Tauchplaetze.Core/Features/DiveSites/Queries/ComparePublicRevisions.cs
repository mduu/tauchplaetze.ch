using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Vergleich zweier freigegebener Versionen eines öffentlichen Platzes. Vorschläge und Ablehnungen bleiben verborgen.</summary>
public record ComparePublicRevisionsQuery(string Slug, int? From, int To);

internal class ComparePublicRevisionsHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions) : IQueryHandler<ComparePublicRevisionsQuery, IReadOnlyList<FieldChange>?>
{
    public async Task<IReadOnlyList<FieldChange>?> Handle(ComparePublicRevisionsQuery query, CancellationToken ct = default)
    {
        var siteId = await dbContext.DiveSites.Where(d => d.Slug == query.Slug && d.Status == DiveSiteStatus.Published).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct);
        if (siteId is null)
        {
            return null;
        }

        var wanted = new[] { query.From, query.To }.OfType<int>().ToList();
        var found = await dbContext.DiveSiteRevisions.AsNoTracking()
            .Where(r => r.DiveSiteId == siteId && r.Status == RevisionStatus.Approved && wanted.Contains(r.Number))
            .ToDictionaryAsync(r => r.Number, ct);
        if (!found.TryGetValue(query.To, out var to) || (query.From is { } from && !found.ContainsKey(from)))
        {
            return null;
        }

        var old = query.From is { } number ? await revisions.Load(found[number], ct) : null;
        return ContentDiff.Between(old, await revisions.Load(to, ct));
    }
}
