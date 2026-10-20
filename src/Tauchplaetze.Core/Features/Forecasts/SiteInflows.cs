using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Eine Station, deren Abfluss am Platz zählt, mit Gewicht 0 bis 1.</summary>
internal record SiteInflow(string StationId, double Weight);

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

    /// <summary>Der Quotient am Platz für einen Tag: heute gemessen, an künftigen Tagen die Vorhersage, ohne Vorhersage weiter der gemessene.</summary>
    public static double? Ratio(
        IReadOnlyList<SiteInflow> inflows,
        DateOnly day,
        DateOnly today,
        IReadOnlyDictionary<string, double> measured,
        IReadOnlyDictionary<(string StationId, DateOnly Day), double> forecast) =>
        InflowWeights.Effective(inflows
            .Select(i => (i.Weight, Ratio: day > today && forecast.TryGetValue((i.StationId, day), out var predicted)
                ? predicted
                : measured.TryGetValue(i.StationId, out var current) ? current : (double?)null))
            .Where(i => i.Ratio is not null)
            .Select(i => new WeightedInflow(i.Ratio!.Value, i.Weight)));
}
