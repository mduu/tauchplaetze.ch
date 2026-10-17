using System.Security.Cryptography;
using System.Text.Json;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Inhalt einer Version: der ganze Platz, wie die Redaktion ihn bearbeitet (`cms.md`, «Versionen»).</summary>
/// <remarks>Neue Felder nur optional anhängen. Ändert sich die Bedeutung eines Felds, steigt <see cref="CurrentVersion"/>, und das Lesen kennt beide Formate.</remarks>
internal sealed record DiveSiteSnapshot(
    int SnapshotVersion,
    string Name,
    string Slug,
    int WaterBodyId,
    double Latitude,
    double Longitude,
    string? Municipality,
    string? Canton,
    int? MaxDepth,
    byte? DifficultyRating,
    byte? WallRating,
    byte? MarineLifeRating,
    byte? WrecksRating,
    byte? CurrentRating,
    byte? SurroundingsRating,
    DiveSiteAccess? Access,
    DiveSiteFeatures Features,
    bool HasDivingBan,
    string? InflowStationOverride,
    double? SatelliteLatitude,
    double? SatelliteLongitude,
    bool ForecastDisabled,
    IReadOnlyList<DiveSiteSnapshotTranslation> Translations)
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this with { Translations = [.. Translations.OrderBy(t => t.Culture, StringComparer.Ordinal)] }, JsonOptions);

    public static string HashOf(byte[] json) => Convert.ToHexStringLower(SHA256.HashData(json));

    public void ApplyTo(DiveSite site)
    {
        site.Name = Name;
        site.Slug = Slug;
        site.WaterBodyId = WaterBodyId;
        site.Location = GeoPoints.Create(Latitude, Longitude);
        site.Municipality = Municipality;
        site.Canton = Canton;
        site.MaxDepth = MaxDepth;
        site.DifficultyRating = DifficultyRating;
        site.WallRating = WallRating;
        site.MarineLifeRating = MarineLifeRating;
        site.WrecksRating = WrecksRating;
        site.CurrentRating = CurrentRating;
        site.SurroundingsRating = SurroundingsRating;
        site.Access = Access;
        site.Features = Features;
        site.HasDivingBan = HasDivingBan;
        site.InflowStationOverride = InflowStationOverride;
        site.SatellitePoint = SatelliteLatitude is { } lat && SatelliteLongitude is { } lng ? GeoPoints.Create(lat, lng) : null;
        site.ForecastDisabled = ForecastDisabled;
        site.Translations = [.. Translations.Select(t => t.ToEntity(site.Id))];
    }
}

/// <summary>Texte einer Sprache in einer Version.</summary>
internal sealed record DiveSiteSnapshotTranslation(
    string Culture,
    string? UnderwaterDescription,
    string? SurroundingsInfo,
    string? ApproachInfo,
    string? HazardsInfo,
    string? FacilitiesInfo,
    string? DivingBanNote,
    bool IsMachineTranslated)
{
    public DiveSiteTranslation ToEntity(Guid diveSiteId) => new()
    {
        DiveSiteId = diveSiteId,
        Culture = Culture,
        UnderwaterDescription = UnderwaterDescription,
        SurroundingsInfo = SurroundingsInfo,
        ApproachInfo = ApproachInfo,
        HazardsInfo = HazardsInfo,
        FacilitiesInfo = FacilitiesInfo,
        DivingBanNote = DivingBanNote,
        IsMachineTranslated = IsMachineTranslated,
    };
}
