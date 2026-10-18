using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Die Prognose eines Platzes von heute an (Lokalzeit).</summary>
public record GetSiteForecastQuery(Guid DiveSiteId);

internal class GetSiteForecastHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetSiteForecastQuery, SiteForecast>
{
    public async Task<SiteForecast> Handle(GetSiteForecastQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var rows = await dbContext.DailyForecasts
            .AsNoTracking()
            .Where(f => f.DiveSiteId == query.DiveSiteId && f.Date >= today)
            .OrderBy(f => f.Date)
            .ToListAsync(ct);

        var days = rows.Select(f => new ForecastDay(
                f.Date,
                f.DataLevel,
                f.DiveIndex,
                f.ProfileSource,
                f.SurfaceTemperature,
                f.TemperatureAt10m,
                f.TemperatureAt20m,
                f.TemperatureAt30m,
                f.ThermoclineTop,
                f.ThermoclineBottom,
                f.SecchiDepth,
                f.SecchiObservedAt,
                f.VisibilityIndicator,
                f.VisibilityGoodBelow,
                f.WindBeaufort,
                f.WindSpeedKmh,
                f.WindGustsKmh,
                f.PrecipitationLast72h,
                f.AlgaeLevel,
                ParseReasons(f.ReasonCodes)))
            .ToList();

        return new SiteForecast(days, rows.Count > 0 ? rows.Max(f => f.ComputedAt) : null);
    }

    private static IReadOnlyList<ReasonCode> ParseReasons(string codes) =>
        codes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => Enum.TryParse<ReasonCode>(code, out var reason) ? reason : (ReasonCode?)null)
            .OfType<ReasonCode>()
            .ToList();
}
