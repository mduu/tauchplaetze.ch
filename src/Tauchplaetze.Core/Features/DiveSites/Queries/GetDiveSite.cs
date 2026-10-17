using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Ein öffentlicher Platz über seinen Slug, in der Sprache <paramref name="Culture"/>. Ein alter Slug ergibt eine Weiterleitung.</summary>
public record GetDiveSiteQuery(string Slug, string Culture);

internal class GetDiveSiteHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>
{
    public async Task<DiveSiteLookup> Handle(GetDiveSiteQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Slug == query.Slug)
            .Select(d => new
            {
                d.Id,
                d.Slug,
                d.Name,
                d.Status,
                d.Municipality,
                d.Canton,
                d.Location,
                d.ContentLevel,
                d.UpdatedAt,
                WaterBody = new { d.WaterBody!.Slug, d.WaterBody.NameDe, d.WaterBody.NameEn, d.WaterBody.NameFr, d.WaterBody.NameIt, d.WaterBody.WaterBodyType },
            })
            .SingleOrDefaultAsync(ct);

        return site?.Status switch
        {
            DiveSiteStatus.Published => new DiveSiteLookup.Found(new DiveSiteDetails(
                site.Id,
                site.Slug,
                site.Name,
                site.WaterBody.Slug,
                WaterBodyNames.In(query.Culture, site.WaterBody.NameDe, site.WaterBody.NameEn, site.WaterBody.NameFr, site.WaterBody.NameIt),
                site.WaterBody.WaterBodyType,
                site.Municipality,
                site.Canton,
                site.Location.Y,
                site.Location.X,
                site.ContentLevel,
                site.UpdatedAt)),
            DiveSiteStatus.Archived => new DiveSiteLookup.Gone(),
            DiveSiteStatus.Draft => new DiveSiteLookup.NotFound(),
            _ => await FollowRedirect(query.Slug, ct),
        };
    }

    private async Task<DiveSiteLookup> FollowRedirect(string slug, CancellationToken ct)
    {
        var current = await dbContext.SlugRedirects
            .AsNoTracking()
            .Where(r => r.OldSlug == slug)
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (_, d) => new { d.Slug, d.Status })
            .SingleOrDefaultAsync(ct);

        return current?.Status == DiveSiteStatus.Published ? new DiveSiteLookup.Moved(current.Slug) : new DiveSiteLookup.NotFound();
    }
}
