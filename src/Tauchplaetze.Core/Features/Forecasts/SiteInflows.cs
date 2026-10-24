using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Eine Station, deren Abfluss am Platz zählt, mit Gewicht 0 bis 1.</summary>
internal record SiteInflow(string StationId, double Weight);

/// <summary>Der Quotient am Platz für einen Tag. <see cref="IsCurrent"/>: frisch gemessen oder für den Tag vorhergesagt, nicht weitergetragen.</summary>
internal record InflowRatio(double? Value, bool IsCurrent);

/// <summary>Welche Stationen am Platz zählen (`forecast.md`, `rules-1.5`).</summary>
internal static class SiteInflows
{
    /// <summary>
    /// Am Fluss die Station des Platzes, dieselbe wie in der Kachel «Abfluss». Am See jeder Zufluss, gewichtet nach der Distanz zu seiner Mündung.
    /// </summary>
    public static IReadOnlyList<SiteInflow> For(DiveSite site, WaterBody waterBody)
    {
        if (waterBody.WaterBodyType == WaterBodyType.River)
        {
            return DischargeStations.For(site.Location, site.InflowStationOverride, waterBody) is { } stationId ? [new SiteInflow(stationId, 1)] : [];
        }

        return [.. waterBody.HydroStations
            .Where(s => s.Role == HydroStationRole.Inflow)
            .Select(s => new SiteInflow(s.StationId, InflowWeights.Weight(s.Mouth is { } mouth ? Distance.Km(site.Location, mouth) : null)))];
    }

    /// <summary>
    /// Der Quotient am Platz für einen Tag. Heute zuerst der frische Messwert, sonst die Vorhersage für heute (`rules-1.7`).
    /// An künftigen Tagen die Vorhersage, ohne Vorhersage weiter der gemessene (dann nicht <see cref="InflowRatio.IsCurrent"/>).
    /// <paramref name="measured"/> enthält nur frische Messwerte.
    /// </summary>
    public static InflowRatio Ratio(
        IReadOnlyList<SiteInflow> inflows,
        DateOnly day,
        DateOnly today,
        IReadOnlyDictionary<string, double> measured,
        IReadOnlyDictionary<(string StationId, DateOnly Day), double> forecast)
    {
        var ratios = inflows.Select(i => (i.Weight, Ratio: RatioOf(i.StationId, day, today, measured, forecast))).Where(i => i.Ratio is not null).ToList();
        return new InflowRatio(
            InflowWeights.Effective(ratios.Select(i => new WeightedInflow(i.Ratio!.Value.Value, i.Weight))),
            ratios.Count > 0 && ratios.All(i => i.Ratio!.Value.IsCurrent));
    }

    private static (double Value, bool IsCurrent)? RatioOf(
        string stationId,
        DateOnly day,
        DateOnly today,
        IReadOnlyDictionary<string, double> measured,
        IReadOnlyDictionary<(string StationId, DateOnly Day), double> forecast)
    {
        var hasMeasured = measured.TryGetValue(stationId, out var current);
        var hasForecast = forecast.TryGetValue((stationId, day), out var predicted);
        return (day > today, hasMeasured, hasForecast) switch
        {
            (false, true, _) => (current, true),
            (false, false, true) => (predicted, true),
            (true, _, true) => (predicted, true),
            (true, true, false) => (current, false),
            _ => null,
        };
    }
}
