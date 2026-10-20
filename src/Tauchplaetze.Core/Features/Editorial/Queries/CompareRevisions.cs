using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>
/// Die Felder, die sich von Version <paramref name="From"/> zu <paramref name="To"/> geändert haben. Ohne <paramref name="From"/>: alles von <paramref name="To"/>.
/// Nur für die Moderation, sonst null.
/// </summary>
public record CompareRevisionsQuery(Guid DiveSiteId, int? From, int To, string RequesterId);

internal class CompareRevisionsHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, AccountRoles roles) : IQueryHandler<CompareRevisionsQuery, IReadOnlyList<FieldChange>?>
{
    public async Task<IReadOnlyList<FieldChange>?> Handle(CompareRevisionsQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.RequesterId)).CanModerate)
        {
            return null;
        }

        var wanted = new[] { query.From, query.To }.OfType<int>().ToList();
        var found = await dbContext.DiveSiteRevisions.AsNoTracking()
            .Where(r => r.DiveSiteId == query.DiveSiteId && wanted.Contains(r.Number))
            .ToDictionaryAsync(r => r.Number, ct);
        if (!found.TryGetValue(query.To, out var to) || (query.From is { } from && !found.ContainsKey(from)))
        {
            return null;
        }

        var old = query.From is { } number ? await revisions.Load(found[number], ct) : null;
        return ContentDiff.Between(old, await revisions.Load(to, ct));
    }
}
