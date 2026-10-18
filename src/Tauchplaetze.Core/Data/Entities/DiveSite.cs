using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Data.Entities;

internal class DiveSite
{
    public Guid Id { get; set; }

    public required string Slug { get; set; }

    public required string Name { get; set; }

    public int WaterBodyId { get; set; }

    public WaterBody? WaterBody { get; set; }

    public required Point Location { get; set; }

    public string? Municipality { get; set; }

    public string? Canton { get; set; }

    public int? MaxDepth { get; set; }

    public byte? DifficultyRating { get; set; }

    public byte? WallRating { get; set; }

    public byte? MarineLifeRating { get; set; }

    public byte? WrecksRating { get; set; }

    public byte? CurrentRating { get; set; }

    public byte? SurroundingsRating { get; set; }

    public DiveSiteAccess? Access { get; set; }

    public DiveSiteFeatures Features { get; set; }

    public bool HasDivingBan { get; set; }

    public string? InflowStationOverride { get; set; }

    public Point? SatellitePoint { get; set; }

    public bool ForecastDisabled { get; set; }

    public byte? DataLevel { get; set; }

    public DiveSiteStatus Status { get; set; }

    public byte ContentLevel { get; set; }

    public Guid? HeroMediaId { get; set; }

    /// <summary>Die Tauchskizze als JSON (Liste von <c>SketchFeature</c>), <c>null</c> ohne Skizze.</summary>
    public string? Sketch { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public List<DiveSiteTranslation> Translations { get; set; } = [];

    public List<DiveSiteEntryPoint> EntryPoints { get; set; } = [];

    public List<DiveSiteNotice> Notices { get; set; } = [];
}
