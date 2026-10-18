using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>Die Platzliste im CMS mit Status, Ausbau- und Datenstufe und offenen Vorschlägen (`cms.md`, «Platzliste»).</summary>
public record GetDiveSitesForEditingQuery(string? Search = null);

/// <summary>Ein Platz in der Liste des CMS.</summary>
public record EditableDiveSite(Guid Id, string Slug, string Name, string WaterBodyName, DiveSiteStatus Status, byte ContentLevel, byte? DataLevel, int PendingRevisions, DateTimeOffset UpdatedAt);

internal class GetDiveSitesForEditingHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>>
{
    public async Task<IReadOnlyList<EditableDiveSite>> Handle(GetDiveSitesForEditingQuery query, CancellationToken ct = default) =>
        await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => query.Search == null || d.Name.Contains(query.Search))
            .OrderBy(d => d.Name)
            .Select(d => new EditableDiveSite(
                d.Id,
                d.Slug,
                d.Name,
                d.WaterBody!.NameDe,
                d.Status,
                d.ContentLevel,
                d.DataLevel,
                dbContext.DiveSiteRevisions.Count(r => r.DiveSiteId == d.Id && r.Status == RevisionStatus.Pending),
                d.UpdatedAt))
            .ToListAsync(ct);
}
