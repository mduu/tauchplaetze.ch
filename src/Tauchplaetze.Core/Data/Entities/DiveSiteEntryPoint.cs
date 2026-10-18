using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Data.Entities;

internal class DiveSiteEntryPoint
{
    public Guid Id { get; set; }

    public Guid DiveSiteId { get; set; }

    public required Point Location { get; set; }

    public EntryPointKind Kind { get; set; }

    public int SortOrder { get; set; }

    public List<DiveSiteEntryPointTranslation> Translations { get; set; } = [];
}

internal class DiveSiteEntryPointTranslation
{
    public Guid EntryPointId { get; set; }

    public required string Culture { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }
}
