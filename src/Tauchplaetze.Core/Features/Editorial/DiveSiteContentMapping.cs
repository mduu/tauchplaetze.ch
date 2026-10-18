using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Überträgt den Inhalt einer Version auf die Tabellen und zurück. Die öffentlichen Seiten lesen nur die Tabellen.</summary>
internal static class DiveSiteContentMapping
{
    public static DiveSiteContent FromEntity(DiveSite site) => new(
        site.Name,
        site.Slug,
        site.WaterBodyId,
        site.Location.Y,
        site.Location.X,
        site.Municipality,
        site.Canton?.Trim(),
        site.MaxDepth,
        site.DifficultyRating,
        site.WallRating,
        site.MarineLifeRating,
        site.WrecksRating,
        site.CurrentRating,
        site.SurroundingsRating,
        site.Access,
        site.Features,
        site.HasDivingBan,
        site.InflowStationOverride,
        site.SatellitePoint?.Y,
        site.SatellitePoint?.X,
        site.ForecastDisabled,
        [.. site.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal).Select(t => new DiveSiteContentTranslation(
            t.Culture.Trim(), t.UnderwaterDescription, t.SurroundingsInfo, t.ApproachInfo, t.HazardsInfo, t.FacilitiesInfo, t.DivingBanNote, t.IsMachineTranslated))],
        [.. site.EntryPoints.OrderBy(e => e.SortOrder).Select(e => new EntryPointContent(
            e.Id, e.Location.Y, e.Location.X, e.Kind,
            [.. e.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal).Select(t => new EntryPointText(t.Culture.Trim(), t.Name, t.Description))]))],
        [.. site.Notices.OrderBy(n => n.SortOrder).Select(n => new NoticeContent(
            n.Id, n.Kind, n.ValidFrom, n.ValidUntil,
            [.. n.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal).Select(t => new NoticeText(t.Culture.Trim(), t.Text))]))],
        SketchJson.Read(site.Sketch));

    public static void ApplyTo(DiveSite site, DiveSiteContent content)
    {
        site.Name = content.Name.Trim();
        site.Slug = content.Slug;
        site.WaterBodyId = content.WaterBodyId;
        site.Location = GeoPoints.Create(content.Latitude, content.Longitude);
        site.Municipality = Blank(content.Municipality);
        site.Canton = Blank(content.Canton)?.ToUpperInvariant();
        site.MaxDepth = content.MaxDepth;
        site.DifficultyRating = content.DifficultyRating;
        site.WallRating = content.WallRating;
        site.MarineLifeRating = content.MarineLifeRating;
        site.WrecksRating = content.WrecksRating;
        site.CurrentRating = content.CurrentRating;
        site.SurroundingsRating = content.SurroundingsRating;
        site.Access = content.Access;
        site.Features = content.Features;
        site.HasDivingBan = content.HasDivingBan;
        site.InflowStationOverride = Blank(content.InflowStationOverride);
        site.SatellitePoint = content is { SatelliteLatitude: { } lat, SatelliteLongitude: { } lng } ? GeoPoints.Create(lat, lng) : null;
        site.ForecastDisabled = content.ForecastDisabled;

        site.Translations.Clear();
        site.Translations.AddRange(content.Translations.Where(HasText).Select(t => new DiveSiteTranslation
        {
            DiveSiteId = site.Id,
            Culture = t.Culture,
            UnderwaterDescription = Blank(t.UnderwaterDescription),
            SurroundingsInfo = Blank(t.SurroundingsInfo),
            ApproachInfo = Blank(t.ApproachInfo),
            HazardsInfo = Blank(t.HazardsInfo),
            FacilitiesInfo = Blank(t.FacilitiesInfo),
            DivingBanNote = Blank(t.DivingBanNote),
            IsMachineTranslated = t.IsMachineTranslated,
        }));

        site.EntryPoints.Clear();
        site.EntryPoints.AddRange(content.EntryPointList.Select((e, index) => new DiveSiteEntryPoint
        {
            Id = e.Id == Guid.Empty ? Guid.CreateVersion7() : e.Id,
            DiveSiteId = site.Id,
            Location = GeoPoints.Create(e.Latitude, e.Longitude),
            Kind = e.Kind,
            SortOrder = index,
            Translations = [.. e.Texts.Where(t => Blank(t.Name) is not null || Blank(t.Description) is not null).Select(t => new DiveSiteEntryPointTranslation
            {
                Culture = t.Culture,
                Name = Blank(t.Name),
                Description = Blank(t.Description),
            })],
        }));

        ApplyNotices(site, content);
        site.Sketch = SketchJson.Write(content.SketchList);
    }

    private static void ApplyNotices(DiveSite site, DiveSiteContent content)
    {
        site.Notices.Clear();
        site.Notices.AddRange(content.NoticeList.Select((n, index) => new DiveSiteNotice
        {
            Id = n.Id == Guid.Empty ? Guid.CreateVersion7() : n.Id,
            DiveSiteId = site.Id,
            Kind = n.Kind,
            ValidFrom = n.ValidFrom,
            ValidUntil = n.ValidUntil,
            SortOrder = index,
            Translations = [.. n.Texts.Where(t => Blank(t.Text) is not null).Select(t => new DiveSiteNoticeTranslation
            {
                Culture = t.Culture,
                Text = Blank(t.Text)!,
            })],
        }));
    }

    private static bool HasText(DiveSiteContentTranslation t) =>
        new[] { t.UnderwaterDescription, t.SurroundingsInfo, t.ApproachInfo, t.HazardsInfo, t.FacilitiesInfo, t.DivingBanNote }.Any(text => Blank(text) is not null);

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
