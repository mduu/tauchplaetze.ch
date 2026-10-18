using System.Globalization;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Ein geändertes Feld zwischen zwei Versionen. <see cref="Field"/> ist ein Code, z.B. `Name` oder `Text.de.ApproachInfo`.</summary>
public record FieldChange(string Field, string? Old, string? New);

/// <summary>Vergleicht zwei Inhalte feldweise (`cms.md`, «Versionen»).</summary>
internal static class ContentDiff
{
    public static IReadOnlyList<FieldChange> Between(DiveSiteContent? old, DiveSiteContent current)
    {
        var before = old is null ? new Dictionary<string, string?>() : Flatten(old);
        var after = Flatten(current);
        return before.Keys.Union(after.Keys)
            .Select(key => new FieldChange(key, before.GetValueOrDefault(key), after.GetValueOrDefault(key)))
            .Where(change => change.Old != change.New)
            .ToList();
    }

    public static Dictionary<string, string?> Flatten(DiveSiteContent content)
    {
        var fields = new Dictionary<string, string?>
        {
            ["Name"] = content.Name,
            ["Slug"] = content.Slug,
            ["WaterBodyId"] = content.WaterBodyId.ToString(CultureInfo.InvariantCulture),
            ["Location"] = Coordinate(content.Latitude, content.Longitude),
            ["Municipality"] = content.Municipality,
            ["Canton"] = content.Canton,
            ["MaxDepth"] = Text(content.MaxDepth),
            ["DifficultyRating"] = Text(content.DifficultyRating),
            ["WallRating"] = Text(content.WallRating),
            ["MarineLifeRating"] = Text(content.MarineLifeRating),
            ["WrecksRating"] = Text(content.WrecksRating),
            ["CurrentRating"] = Text(content.CurrentRating),
            ["SurroundingsRating"] = Text(content.SurroundingsRating),
            ["Access"] = content.Access?.ToString(),
            ["Features"] = content.Features.ToString(),
            ["HasDivingBan"] = content.HasDivingBan.ToString(),
            ["InflowStationOverride"] = content.InflowStationOverride,
            ["SatellitePoint"] = content is { SatelliteLatitude: { } lat, SatelliteLongitude: { } lng } ? Coordinate(lat, lng) : null,
            ["ForecastDisabled"] = content.ForecastDisabled.ToString(),
        };

        foreach (var text in content.Translations)
        {
            fields[$"Text.{text.Culture}.UnderwaterDescription"] = text.UnderwaterDescription;
            fields[$"Text.{text.Culture}.SurroundingsInfo"] = text.SurroundingsInfo;
            fields[$"Text.{text.Culture}.ApproachInfo"] = text.ApproachInfo;
            fields[$"Text.{text.Culture}.HazardsInfo"] = text.HazardsInfo;
            fields[$"Text.{text.Culture}.FacilitiesInfo"] = text.FacilitiesInfo;
            fields[$"Text.{text.Culture}.DivingBanNote"] = text.DivingBanNote;
        }

        foreach (var (entry, index) in content.EntryPointList.Select((e, i) => (e, i + 1)))
        {
            fields[$"EntryPoint.{index}.Location"] = Coordinate(entry.Latitude, entry.Longitude);
            fields[$"EntryPoint.{index}.Kind"] = entry.Kind.ToString();
            foreach (var text in entry.Texts)
            {
                fields[$"EntryPoint.{index}.{text.Culture}.Name"] = text.Name;
                fields[$"EntryPoint.{index}.{text.Culture}.Description"] = text.Description;
            }
        }

        foreach (var (notice, index) in content.NoticeList.Select((n, i) => (n, i + 1)))
        {
            fields[$"Notice.{index}.Kind"] = notice.Kind.ToString();
            fields[$"Notice.{index}.ValidFrom"] = notice.ValidFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            fields[$"Notice.{index}.ValidUntil"] = notice.ValidUntil?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var text in notice.Texts)
            {
                fields[$"Notice.{index}.{text.Culture}.Text"] = text.Text;
            }
        }

        foreach (var (feature, index) in content.SketchList.Select((f, i) => (f, i + 1)))
        {
            fields[$"Sketch.{index}.Kind"] = feature.Kind.ToString();
            fields[$"Sketch.{index}.Points"] = string.Join(" / ", feature.Points.Select(p => Coordinate(p.Latitude, p.Longitude)));
            fields[$"Sketch.{index}.Depth"] = Text(feature.Depth);
            foreach (var text in feature.Texts)
            {
                fields[$"Sketch.{index}.{text.Culture}.Label"] = text.Label?.Trim();
            }
        }

        foreach (var (use, index) in content.MediaList.Select((m, i) => (m, i + 1)))
        {
            fields[$"Media.{index}.Photo"] = use.MediaId.ToString();
            fields[$"Media.{index}.Section"] = use.Section.ToString();
        }

        fields["HeroMediaId"] = content.HeroMediaId?.ToString();

        return fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToDictionary(f => f.Key, f => f.Value);
    }

    private static string? Text<T>(T? value) where T : struct, IFormattable => value?.ToString(null, CultureInfo.InvariantCulture);

    private static string Coordinate(double latitude, double longitude) => string.Create(CultureInfo.InvariantCulture, $"{latitude:0.00000}, {longitude:0.00000}");
}
