using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Data.Entities;

internal class WaterBody
{
    public int Id { get; set; }

    public required string Slug { get; set; }

    public required string NameDe { get; set; }

    public string? NameEn { get; set; }

    public string? NameFr { get; set; }

    public string? NameIt { get; set; }

    public WaterBodyType WaterBodyType { get; set; }

    public Geometry? Outline { get; set; }

    public required Point Centroid { get; set; }

    public string? SwissTlmId { get; set; }

    public string? AlplakesLakeKey { get; set; }

    public string? AlplakesModel3d { get; set; }

    public string? AlplakesSimstratKey { get; set; }

    public Point? SatellitePoint { get; set; }

    public bool IsActive { get; set; }
}
