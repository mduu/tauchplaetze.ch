using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Echolot.Queries;

/// <summary>
/// Die Seite «Echolot» (`ux.md`, «Echolot»): der Stand jeder Datenquelle und die letzten <see cref="EcholotRules.MaxEntries"/> Neuigkeiten,
/// neueste zuerst. Neuigkeiten sind neue Funktionen aus `news.json` und Plätze mit erster Beschreibung, pro Tag gruppiert.
/// </summary>
public record GetEcholotQuery(string Culture);

internal class GetEcholotHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetEcholotQuery, EcholotReport>
{
    /// <summary>So viele Plätze liest die Query höchstens für die Neuigkeiten. Reicht für <see cref="EcholotRules.MaxEntries"/> Tage.</summary>
    private const int DescribedSitesLimit = 500;

    public async Task<EcholotReport> Handle(GetEcholotQuery query, CancellationToken ct = default)
    {
        var sources = await DataSourceStatuses.Read(dbContext, time, ct);
        var entries = await Entries(query.Culture, ct);
        return new EcholotReport(sources, [.. entries.Take(EcholotRules.MaxEntries)], entries.Count > EcholotRules.MaxEntries, await Sounding(query.Culture, ct));
    }

    /// <summary>
    /// Der neueste Tag bis heute mit Sprungschicht an einem veröffentlichten Platz. Bei mehreren Plätzen zählt zuerst die Datenstufe, dann die dickere Sprungschicht.
    /// Im Winter ist das oft ein Tag aus dem Herbst.
    /// </summary>
    private async Task<EcholotSounding?> Sounding(string culture, CancellationToken ct)
    {
        var today = SwissTime.Today(time);
        var row = await dbContext.DailyForecasts.AsNoTracking()
            .Where(f => f.Date <= today && f.ThermoclineTop != null && f.ThermoclineBottom != null)
            .Join(dbContext.DiveSites.Where(s => s.Status == DiveSiteStatus.Published), f => f.DiveSiteId, s => s.Id, (f, s) => new { Forecast = f, Site = s })
            .OrderByDescending(x => x.Forecast.Date)
            .ThenByDescending(x => x.Forecast.DataLevel)
            .ThenByDescending(x => x.Forecast.ThermoclineBottom - x.Forecast.ThermoclineTop)
            .ThenBy(x => x.Site.Slug)
            .Select(x => new
            {
                x.Site.Slug,
                x.Site.Name,
                x.Site.WaterBody!.NameDe,
                x.Site.WaterBody.NameEn,
                x.Site.WaterBody.NameFr,
                x.Site.WaterBody.NameIt,
                x.Forecast.Date,
                Top = x.Forecast.ThermoclineTop!.Value,
                Bottom = x.Forecast.ThermoclineBottom!.Value,
                x.Forecast.SurfaceTemperature,
                x.Forecast.TemperatureAt30m,
            })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new EcholotSounding(row.Slug, row.Name, WaterBodyNames.In(culture, row.NameDe, row.NameEn, row.NameFr, row.NameIt), row.Date,
                row.Top, row.Bottom, row.SurfaceTemperature, row.TemperatureAt30m);
    }

    /// <summary>Am selben Tag steht eine neue Funktion vor den Plätzen.</summary>
    private async Task<List<EcholotEntry>> Entries(string culture, CancellationToken ct)
    {
        var features = EcholotNews.All.Select(n => Feature(n.Date, n.In(culture)));
        var described = await DescribedSites(culture, ct);
        return [.. features.Concat(described).OrderByDescending(e => e.Date).ThenBy(e => e.Kind)];
    }

    private static EcholotEntry Feature(DateOnly date, NewsText text) => new(date, EcholotEntryKind.Feature, text.Title, text.Text, []);

    private async Task<IEnumerable<EcholotEntry>> DescribedSites(string culture, CancellationToken ct)
    {
        var sites = await dbContext.DiveSites.AsNoTracking()
            .Where(s => s.Status == DiveSiteStatus.Published && s.FirstDescribedAt != null)
            .OrderByDescending(s => s.FirstDescribedAt)
            .Take(DescribedSitesLimit)
            .Select(s => new
            {
                s.Slug,
                s.Name,
                DescribedAt = s.FirstDescribedAt!.Value,
                s.WaterBody!.NameDe,
                s.WaterBody.NameEn,
                s.WaterBody.NameFr,
                s.WaterBody.NameIt,
            })
            .ToListAsync(ct);
        return sites
            .GroupBy(s => SwissTime.DayOf(s.DescribedAt))
            .Select(g => new EcholotEntry(g.Key, EcholotEntryKind.Described, null, null,
                [.. g.OrderBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase)
                    .Select(s => new DescribedSite(s.Slug, s.Name, WaterBodyNames.In(culture, s.NameDe, s.NameEn, s.NameFr, s.NameIt)))]));
    }
}
