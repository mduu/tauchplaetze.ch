using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Data.Entities;

internal class WaterBodyHydroStation
{
    public int WaterBodyId { get; set; }

    public required string StationId { get; set; }

    public required string Name { get; set; }

    public HydroStationRole Role { get; set; }

    public decimal? Weight { get; set; }

    /// <summary>Lage der Station. Damit wählt ein Flussplatz die nächste Station am selben Fluss.</summary>
    public Point? Location { get; set; }
}
