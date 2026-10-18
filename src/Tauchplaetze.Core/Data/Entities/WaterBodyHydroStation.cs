using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Data.Entities;

internal class WaterBodyHydroStation
{
    public int WaterBodyId { get; set; }

    public required string StationId { get; set; }

    public required string Name { get; set; }

    public HydroStationRole Role { get; set; }

    public decimal? Weight { get; set; }
}
