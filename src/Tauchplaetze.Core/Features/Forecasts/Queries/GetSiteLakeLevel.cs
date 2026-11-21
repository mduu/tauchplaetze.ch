using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>
/// Der Pegel am See eines Platzes mit Einordnung gegen den üblichen Bereich des Kalendertags (`forecast.md`, «Seepegel»).
/// <c>null</c> an einem Fluss, ohne Station, ohne frischen Messwert oder ohne aktuelle Statistik.
/// </summary>
public record GetSiteLakeLevelQuery(Guid DiveSiteId);

internal class GetSiteLakeLevelHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetSiteLakeLevelQuery, SiteLakeLevel?>
{
    /// <summary>Ein älterer Messwert zählt nicht. Der Pegel ändert sich langsam, der Import läuft jede Stunde.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    /// <summary>So alt darf die Statistik sein, wenn das BAFU sie heute nicht liefert. Der übliche Bereich ändert sich von Tag zu Tag kaum.</summary>
    public const int NormMaxAgeDays = 3;

    public async Task<SiteLakeLevel?> Handle(GetSiteLakeLevelQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites
            .AsNoTracking()
            .Include(s => s.WaterBody).ThenInclude(w => w!.HydroStations)
            .SingleOrDefaultAsync(s => s.Id == query.DiveSiteId, ct);
        if (site is null || LakeLevelStations.For(site.Location, site.WaterBody!) is not { } station)
        {
            return null;
        }

        var reading = await dbContext.LakeLevelReadings.AsNoTracking().SingleOrDefaultAsync(r => r.StationId == station.StationId, ct);
        var today = SwissTime.Today(time);
        if (reading is not { NormDay: { } normDay, NormLow: { } low, NormMedian: { } median, NormHigh: { } high }
            || time.GetUtcNow() - reading.MeasuredAt > StaleAfter
            || today.DayNumber - normDay.DayNumber > NormMaxAgeDays)
        {
            return null;
        }

        return new SiteLakeLevel(
            station.StationId,
            station.Name,
            reading.Level,
            reading.MeasuredAt,
            LakeLevelRules.DeviationOf(reading.Level, low, high),
            reading.Level - median);
    }
}
