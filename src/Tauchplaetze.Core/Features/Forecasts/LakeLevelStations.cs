using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Die Pegelstation eines Seeplatzes (`forecast.md`, «Seepegel»).</summary>
internal static class LakeLevelStations
{
    /// <summary>Die nächste Station mit Rolle <c>Level</c> und Lage am See des Platzes. <c>null</c> an einem Fluss oder ohne Station.</summary>
    public static WaterBodyHydroStation? For(Point site, WaterBody waterBody) =>
        waterBody.WaterBodyType == WaterBodyType.River
            ? null
            : waterBody.HydroStations
                .Where(s => s.Role == HydroStationRole.Level && s.Location is not null)
                .MinBy(s => Distance.Km(site, s.Location!));
}
