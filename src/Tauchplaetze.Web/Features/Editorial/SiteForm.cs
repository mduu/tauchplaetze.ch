using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>Bearbeitbare Fassung eines <see cref="DiveSiteContent"/> für die Formulare des CMS. Texte gibt es für alle vier Sprachen.</summary>
public class SiteForm
{
    public string Name { get; set; } = "";

    public string Slug { get; set; } = "";

    public int WaterBodyId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? Municipality { get; set; }

    public string? Canton { get; set; }

    /// <summary>Weitere Namen, getrennt mit Komma.</summary>
    public string? OtherNames { get; set; }

    public int? MaxDepth { get; set; }

    public Dictionary<string, byte?> Ratings { get; } = [];

    public DiveSiteAccess? Access { get; set; }

    public HashSet<DiveSiteFeatures> Features { get; } = [];

    public bool HasDivingBan { get; set; }

    public string? InflowStationOverride { get; set; }

    public double? SatelliteLatitude { get; set; }

    public double? SatelliteLongitude { get; set; }

    public bool ForecastDisabled { get; set; }

    public decimal? DischargeLowFrom { get; set; }

    public decimal? DischargeMediumFrom { get; set; }

    public decimal? DischargeStrongFrom { get; set; }

    public Dictionary<string, TextForm> Texts { get; } = Languages.All.ToDictionary(l => l, _ => new TextForm());

    public List<EntryForm> EntryPoints { get; } = [];

    public List<NoticeForm> Notices { get; } = [];

    public List<SketchForm> Sketch { get; } = [];

    public List<MediaUseForm> Media { get; } = [];

    public Guid? HeroMediaId { get; set; }

    public string? Comment { get; set; }

    public static readonly string[] RatingFields = ["DifficultyRating", "WallRating", "MarineLifeRating", "WrecksRating", "CurrentRating", "SurroundingsRating"];

    public static readonly DiveSiteFeatures[] AllFeatures = [DiveSiteFeatures.Plateau, DiveSiteFeatures.Cave, DiveSiteFeatures.River, DiveSiteFeatures.IceDiving, DiveSiteFeatures.Training];

    public static SiteForm From(DiveSiteContent content)
    {
        var form = new SiteForm
        {
            Name = content.Name,
            Slug = content.Slug,
            WaterBodyId = content.WaterBodyId,
            Latitude = content.Latitude,
            Longitude = content.Longitude,
            Municipality = content.Municipality,
            Canton = content.Canton,
            MaxDepth = content.MaxDepth,
            Access = content.Access,
            HasDivingBan = content.HasDivingBan,
            InflowStationOverride = content.InflowStationOverride,
            SatelliteLatitude = content.SatelliteLatitude,
            SatelliteLongitude = content.SatelliteLongitude,
            ForecastDisabled = content.ForecastDisabled,
            DischargeLowFrom = content.Discharge?.LowFrom,
            DischargeMediumFrom = content.Discharge?.MediumFrom,
            DischargeStrongFrom = content.Discharge?.StrongFrom,
        };
        form.Ratings["DifficultyRating"] = content.DifficultyRating;
        form.Ratings["WallRating"] = content.WallRating;
        form.Ratings["MarineLifeRating"] = content.MarineLifeRating;
        form.Ratings["WrecksRating"] = content.WrecksRating;
        form.Ratings["CurrentRating"] = content.CurrentRating;
        form.Ratings["SurroundingsRating"] = content.SurroundingsRating;
        form.Features.UnionWith(AllFeatures.Where(f => content.Features.HasFlag(f)));

        foreach (var text in content.Translations.Where(t => form.Texts.ContainsKey(t.Culture)))
        {
            form.Texts[text.Culture] = TextForm.From(text);
        }

        form.EntryPoints.AddRange(content.EntryPointList.Select(e => EntryForm.From(e)));
        form.Notices.AddRange(content.NoticeList.Select(NoticeForm.From));
        form.Sketch.AddRange(content.SketchList.Select(SketchForm.From));
        form.Media.AddRange(content.MediaList.Select(m => new MediaUseForm { MediaId = m.MediaId, Section = m.Section, EntryPointId = m.EntryPointId }));
        form.HeroMediaId = content.HeroMediaId;
        form.OtherNames = content.OtherNameList.Count > 0 ? string.Join(", ", content.OtherNameList) : null;
        return form;
    }

    public DiveSiteContent ToContent() => new(
        Name,
        Slug,
        WaterBodyId,
        Latitude,
        Longitude,
        Municipality,
        Canton,
        MaxDepth,
        Ratings.GetValueOrDefault("DifficultyRating"),
        Ratings.GetValueOrDefault("WallRating"),
        Ratings.GetValueOrDefault("MarineLifeRating"),
        Ratings.GetValueOrDefault("WrecksRating"),
        Ratings.GetValueOrDefault("CurrentRating"),
        Ratings.GetValueOrDefault("SurroundingsRating"),
        Access,
        Features.Aggregate(DiveSiteFeatures.None, (all, f) => all | f),
        HasDivingBan,
        InflowStationOverride,
        SatelliteLatitude,
        SatelliteLongitude,
        ForecastDisabled,
        [.. Texts.Where(t => t.Value.HasText).Select(t => t.Value.ToContent(t.Key))],
        [.. EntryPoints.Select(e => e.ToContent())],
        [.. Notices.Select(n => n.ToContent())],
        [.. Sketch.Select(f => f.ToContent())],
        [.. Media.Select(m => new MediaUse(m.MediaId, m.Section, m.Section == MediaSection.EntryPoint ? m.EntryPointId : null))],
        Media.Any(m => m.MediaId == HeroMediaId) ? HeroMediaId : null,
        (OtherNames ?? "").Split(','),
        Discharge);

    /// <summary>Leer ohne Schwellen. Fehlt nur eine, gilt sie als 0 und die Prüfung im Core meldet den Fehler.</summary>
    private DischargeThresholds? Discharge => DischargeLowFrom is null && DischargeMediumFrom is null && DischargeStrongFrom is null
        ? null
        : new DischargeThresholds(DischargeLowFrom ?? 0, DischargeMediumFrom ?? 0, DischargeStrongFrom ?? 0);
}

public class TextForm
{
    public string? UnderwaterDescription { get; set; }

    public string? SurroundingsInfo { get; set; }

    public string? ApproachInfo { get; set; }

    public string? HazardsInfo { get; set; }

    public string? FacilitiesInfo { get; set; }

    public string? DivingBanNote { get; set; }

    public bool IsMachineTranslated { get; set; }

    public bool HasText => new[] { UnderwaterDescription, SurroundingsInfo, ApproachInfo, HazardsInfo, FacilitiesInfo, DivingBanNote }.Any(s => !string.IsNullOrWhiteSpace(s));

    public static TextForm From(DiveSiteContentTranslation text) => new()
    {
        UnderwaterDescription = text.UnderwaterDescription,
        SurroundingsInfo = text.SurroundingsInfo,
        ApproachInfo = text.ApproachInfo,
        HazardsInfo = text.HazardsInfo,
        FacilitiesInfo = text.FacilitiesInfo,
        DivingBanNote = text.DivingBanNote,
        IsMachineTranslated = text.IsMachineTranslated,
    };

    public DiveSiteContentTranslation ToContent(string culture) =>
        new(culture, UnderwaterDescription, SurroundingsInfo, ApproachInfo, HazardsInfo, FacilitiesInfo, DivingBanNote, IsMachineTranslated);
}

public class EntryForm
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public EntryPointKind Kind { get; set; }

    public Dictionary<string, (string? Name, string? Description)> Texts { get; } = Languages.All.ToDictionary(l => l, _ => ((string?)null, (string?)null));

    public static EntryForm From(EntryPointContent entry)
    {
        var form = new EntryForm { Id = entry.Id, Latitude = entry.Latitude, Longitude = entry.Longitude, Kind = entry.Kind };
        foreach (var text in entry.Texts.Where(t => form.Texts.ContainsKey(t.Culture)))
        {
            form.Texts[text.Culture] = (text.Name, text.Description);
        }

        return form;
    }

    public EntryPointContent ToContent() => new(
        Id, Latitude, Longitude, Kind,
        [.. Texts.Where(t => !string.IsNullOrWhiteSpace(t.Value.Name) || !string.IsNullOrWhiteSpace(t.Value.Description))
            .Select(t => new EntryPointText(t.Key, t.Value.Name, t.Value.Description))]);
}

public class NoticeForm
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public NoticeKind Kind { get; set; } = NoticeKind.Info;

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidUntil { get; set; }

    public Dictionary<string, string?> Texts { get; } = Languages.All.ToDictionary(l => l, _ => (string?)null);

    public static NoticeForm From(NoticeContent notice)
    {
        var form = new NoticeForm
        {
            Id = notice.Id,
            Kind = notice.Kind,
            ValidFrom = notice.ValidFrom?.ToDateTime(TimeOnly.MinValue),
            ValidUntil = notice.ValidUntil?.ToDateTime(TimeOnly.MinValue),
        };
        foreach (var text in notice.Texts.Where(t => form.Texts.ContainsKey(t.Culture)))
        {
            form.Texts[text.Culture] = text.Text;
        }

        return form;
    }

    public NoticeContent ToContent() => new(
        Id,
        Kind,
        ValidFrom is { } from ? DateOnly.FromDateTime(from) : null,
        ValidUntil is { } until ? DateOnly.FromDateTime(until) : null,
        [.. Texts.Where(t => !string.IsNullOrWhiteSpace(t.Value)).Select(t => new NoticeText(t.Key, t.Value))]);
}

/// <summary>Bearbeitbares Element der Tauchskizze. Öffentlich, weil der Editor es als Parameter bekommt.</summary>
public class SketchForm
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SketchKind Kind { get; set; }

    public List<SketchPoint> Points { get; set; } = [];

    public int? Depth { get; set; }

    public Dictionary<string, string?> Labels { get; } = Languages.All.ToDictionary(l => l, _ => (string?)null);

    public static SketchForm From(SketchFeature feature)
    {
        var form = new SketchForm { Id = feature.Id, Kind = feature.Kind, Points = [.. feature.Points], Depth = feature.Depth };
        foreach (var text in feature.Texts.Where(t => form.Labels.ContainsKey(t.Culture)))
        {
            form.Labels[text.Culture] = text.Label;
        }

        return form;
    }

    public SketchFeature ToContent() => new(
        Id, Kind, [.. Points], Depth,
        [.. Labels.Where(l => !string.IsNullOrWhiteSpace(l.Value)).Select(l => new SketchText(l.Key, l.Value))]);
}

/// <summary>Ein Foto in der bearbeiteten Version. Öffentlich, weil der Foto-Editor es als Parameter bekommt.</summary>
public class MediaUseForm
{
    public Guid MediaId { get; set; }

    public MediaSection Section { get; set; }

    public Guid? EntryPointId { get; set; }
}
