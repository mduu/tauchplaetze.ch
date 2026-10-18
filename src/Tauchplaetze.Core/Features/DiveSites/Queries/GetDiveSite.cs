using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Ein öffentlicher Platz über seinen Slug, in der Sprache <paramref name="Culture"/>. Ein alter Slug ergibt eine Weiterleitung.</summary>
public record GetDiveSiteQuery(string Slug, string Culture);

internal class GetDiveSiteHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>
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
                site.UpdatedAt,
                await Notices(site.Id, query.Culture, ct),
                await EntryPoints(site.Id, query.Culture, ct))),
            DiveSiteStatus.Archived => new DiveSiteLookup.Gone(),
            DiveSiteStatus.Draft => new DiveSiteLookup.NotFound(),
            _ => await FollowRedirect(query.Slug, ct),
        };
    }

    private async Task<IReadOnlyList<SiteNotice>> Notices(Guid siteId, string culture, CancellationToken ct)
    {
        var notices = await dbContext.DiveSiteNotices
            .AsNoTracking()
            .Where(n => n.DiveSiteId == siteId)
            .Where(NoticeRules.ActiveOn(SwissTime.Today(time)))
            .Include(n => n.Translations)
            .ToListAsync(ct);

        return notices
            .OrderByDescending(n => n.Kind)
            .ThenBy(n => n.SortOrder)
            .Select(n =>
            {
                var text = n.Translations.FirstOrDefault(t => t.Culture == culture) ?? n.Translations.FirstOrDefault(t => t.Culture == "de") ?? n.Translations.First();
                return new SiteNotice(n.Kind, text.Text, n.ValidUntil, text.Culture != culture);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SiteEntryPoint>> EntryPoints(Guid siteId, string culture, CancellationToken ct)
    {
        var entryPoints = await dbContext.DiveSiteEntryPoints
            .AsNoTracking()
            .Where(e => e.DiveSiteId == siteId)
            .OrderBy(e => e.SortOrder)
            .Include(e => e.Translations)
            .ToListAsync(ct);

        return entryPoints
            .Select(e => new SiteEntryPoint(
                e.Kind,
                e.Location.Y,
                e.Location.X,
                e.Translations.FirstOrDefault(t => t.Culture == culture && !string.IsNullOrWhiteSpace(t.Name))?.Name
                    ?? e.Translations.FirstOrDefault(t => t.Culture == "de" && !string.IsNullOrWhiteSpace(t.Name))?.Name))
            .ToList();
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
