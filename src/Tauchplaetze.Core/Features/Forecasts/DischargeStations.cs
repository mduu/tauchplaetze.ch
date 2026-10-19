using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Die BAFU-Station eines Flussplatzes (`forecast.md`, «Abfluss»).</summary>
internal static class DischargeStations
{
    /// <summary>
    /// <c>InflowStationOverride</c> des Platzes, sonst die nächste Station mit Lage und Rolle <c>River</c> am selben Fluss.
    /// <c>null</c> an einem See oder ohne Station.
    /// </summary>
    public static string? For(Point site, string? stationOverride, WaterBody waterBody)
    {
        if (waterBody.WaterBodyType != WaterBodyType.River)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(stationOverride))
        {
            return stationOverride.Trim();
        }

        return waterBody.HydroStations
            .Where(s => s.Role == HydroStationRole.River && s.Location is not null)
            .MinBy(s => Distance.Km(site, s.Location!))
            ?.StationId;
    }
}
