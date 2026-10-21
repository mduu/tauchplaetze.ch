using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Die Seiten für die Sitemap (`seo.md`, «Sitemap und Robots»): veröffentlichte Plätze und Gewässer mit mindestens einem veröffentlichten Platz.</summary>
public record GetSitemapEntriesQuery;

/// <summary>Plätze und Gewässer der Sitemap, je nach Slug sortiert.</summary>
public record SitemapEntries(IReadOnlyList<SitemapEntry> DiveSites, IReadOnlyList<SitemapEntry> WaterBodies);

/// <summary>Ein Slug mit der letzten Änderung des Inhalts. Beim Gewässer gilt die jüngste Änderung seiner Plätze.</summary>
public record SitemapEntry(string Slug, DateTimeOffset LastModified);

internal class GetSitemapEntriesHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetSitemapEntriesQuery, SitemapEntries>
{
    public async Task<SitemapEntries> Handle(GetSitemapEntriesQuery query, CancellationToken ct = default)
    {
        var published = dbContext.DiveSites.AsNoTracking().Where(d => d.Status == DiveSiteStatus.Published);

        var sites = await published
            .OrderBy(d => d.Slug)
            .Select(d => new SitemapEntry(d.Slug, d.UpdatedAt))
            .ToListAsync(ct);

        var waterBodies = await published
            .Where(d => d.WaterBody!.IsActive)
            .GroupBy(d => d.WaterBody!.Slug)
            .Select(g => new { Slug = g.Key, LastModified = g.Max(d => d.UpdatedAt) })
            .ToListAsync(ct);

        return new SitemapEntries(sites, [.. waterBodies.OrderBy(w => w.Slug, StringComparer.Ordinal).Select(w => new SitemapEntry(w.Slug, w.LastModified))]);
    }
}
