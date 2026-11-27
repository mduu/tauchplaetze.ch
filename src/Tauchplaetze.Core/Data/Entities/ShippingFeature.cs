using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Features.Shipping;

namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Objekt der Schifffahrt aus dem Seed (`SeedShippingFeatures`). Nicht Teil der Versionen eines Platzes.</summary>
internal class ShippingFeature
{
    public int Id { get; set; }

    public ShippingFeatureKind Kind { get; set; }

    public string? Name { get; set; }

    public required Geometry Geometry { get; set; }

    public ShippingSource Source { get; set; }

    public required string SourceKey { get; set; }
}
