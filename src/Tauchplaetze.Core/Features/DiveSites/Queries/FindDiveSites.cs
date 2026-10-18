using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>
/// Sucht öffentliche Plätze für das Verzeichnis (`ux.md`, «Verzeichnis»): Text in Name, Gemeinde und Gewässer (ohne Gross-/Kleinschreibung,
/// Umlaute und Akzente), optional Standort mit Umkreis, Eigenschaften, Gewässer. Ohne Angabe sortiert nach Index heute, mit Standort nach Distanz.
/// </summary>
public record FindDiveSitesQuery(
    string Culture,
    string? Text = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null,
    IReadOnlyCollection<SiteTrait>? Traits = null,
    string? WaterBodySlug = null,
    SiteSort? Sort = null,
    int Page = 1,
    int PageSize = 30);

/// <summary>Eigenschaft eines Platzes als Filter im Verzeichnis.</summary>
public enum SiteTrait
{
    /// <summary>Steilwand ab Bewertung 3.</summary>
    Wall,

    /// <summary>Wracks und Gegenstände ab Bewertung 3.</summary>
    Wreck,

    /// <summary>Schwierigkeit höchstens 2.</summary>
    Beginner,

    /// <summary>Geeignet für Ausbildung.</summary>
    Training,

    /// <summary>Platz an einem Fluss.</summary>
    River,

    /// <summary>Höhle.</summary>
    Cave,
}

/// <summary>Sortierung im Verzeichnis.</summary>
public enum SiteSort
{
    /// <summary>Index heute, höchster zuerst. Plätze ohne Index am Ende.</summary>
    Index,

    /// <summary>Name.</summary>
    Name,

    /// <summary>Distanz zum Standort. Ohne Standort wie <see cref="Index"/>.</summary>
    Distance,
}

/// <summary>
/// Eine Seite Treffer, dazu alle Treffer für die Karte. <see cref="SiteCount"/> und <see cref="WaterBodyCount"/> zählen alle öffentlichen Plätze,
/// unabhängig von der Suche.
/// </summary>
public record FoundDiveSites(
    IReadOnlyList<FoundDiveSite> Sites,
    int Total,
    int Page,
    int PageCount,
    SiteSort Sort,
    IReadOnlyList<DiveSiteListItem> MapSites,
    int SiteCount,
    int WaterBodyCount,
    string? WaterBodyName);

/// <summary>Ein Treffer mit Index heute, schwerstem Hinweis von heute, Eigenschaften und Distanz (nur mit Standort).</summary>
public record FoundDiveSite(
    string Slug,
    string Name,
    string? Municipality,
    string? Canton,
    string WaterBodyName,
    double Latitude,
    double Longitude,
    decimal? DiveIndexToday,
    NoticeKind? NoticeToday,
    int? MaxDepth,
    IReadOnlyList<SiteTrait> Traits,
    double? DistanceKm);

internal class FindDiveSitesHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<FindDiveSitesQuery, FoundDiveSites>
{
    public async Task<FoundDiveSites> Handle(FindDiveSitesQuery query, CancellationToken ct = default)
    {
        var today = SwissTime.Today(time);
        var active = NoticeRules.ActiveOn(today);
        var rows = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published)
            .Select(d => new Row(
                d.Slug,
                d.Name,
                d.Municipality,
                d.Canton,
                d.Location,
                d.ContentLevel,
                d.DataLevel,
                d.MaxDepth,
                d.WallRating,
                d.WrecksRating,
                d.DifficultyRating,
                d.Features,
                d.WaterBody!.Slug,
                d.WaterBody.NameDe,
                d.WaterBody.NameEn,
                d.WaterBody.NameFr,
                d.WaterBody.NameIt,
                d.WaterBody.WaterBodyType,
                dbContext.DailyForecasts.Where(f => f.DiveSiteId == d.Id && f.Date == today).Select(f => f.DiveIndex).FirstOrDefault(),
                d.Notices.AsQueryable().Where(active).Select(n => n.Kind).ToList()))
            .ToListAsync(ct);

        var hasLocation = query is { Latitude: not null, Longitude: not null };
        var origin = hasLocation ? GeoPoints.Create(query.Latitude!.Value, query.Longitude!.Value) : null;
        var search = SearchText.Of(query.Text);
        var traits = query.Traits ?? [];

        var matches = rows
            .Select(r => (Row: r, Traits: TraitsOf(r), Distance: origin is null ? (double?)null : Distance.Km(origin, GeoPoints.Create(r.Latitude, r.Longitude))))
            .Where(m => query.WaterBodySlug is null || m.Row.WaterSlug == query.WaterBodySlug)
            .Where(m => search.Length == 0 || SearchText.Matches(search, m.Row.Name, m.Row.Municipality, m.Row.NameDe, m.Row.NameEn, m.Row.NameFr, m.Row.NameIt))
            .Where(m => traits.All(m.Traits.Contains))
            .Where(m => query.RadiusKm is null || m.Distance is null || m.Distance <= query.RadiusKm)
            .ToList();

        var sort = query.Sort ?? (hasLocation ? SiteSort.Distance : SiteSort.Index);
        if (sort == SiteSort.Distance && !hasLocation)
        {
            sort = SiteSort.Index;
        }

        var names = StringComparer.Create(CultureInfo.GetCultureInfo(query.Culture), true);
        var sorted = sort switch
        {
            SiteSort.Name => matches.OrderBy(m => m.Row.Name, names),
            SiteSort.Distance => matches.OrderBy(m => m.Distance).ThenBy(m => m.Row.Name, names),
            _ => matches.OrderBy(m => m.Row.Index is null).ThenByDescending(m => m.Row.Index).ThenBy(m => m.Row.Name, names),
        };

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pageCount = Math.Max(1, (int)Math.Ceiling(matches.Count / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, pageCount);
        var waterName = query.WaterBodySlug is null ? null : rows.Where(r => r.WaterSlug == query.WaterBodySlug).Select(r => WaterName(r, query.Culture)).FirstOrDefault();

        return new FoundDiveSites(
            [.. sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(m => new FoundDiveSite(
                m.Row.Slug,
                m.Row.Name,
                m.Row.Municipality,
                m.Row.Canton?.Trim(),
                WaterName(m.Row, query.Culture),
                m.Row.Latitude,
                m.Row.Longitude,
                m.Row.Index,
                Worst(m.Row.Notices),
                m.Row.MaxDepth,
                m.Traits,
                m.Distance is { } km ? Math.Round(km, 1) : null))],
            matches.Count,
            page,
            pageCount,
            sort,
            [.. matches.Select(m => new DiveSiteListItem(
                m.Row.Slug, m.Row.Name, m.Row.Municipality, m.Row.WaterSlug, WaterName(m.Row, query.Culture), m.Row.WaterType,
                m.Row.Latitude, m.Row.Longitude, m.Row.ContentLevel, m.Row.DataLevel, m.Row.Index, Worst(m.Row.Notices)))],
            rows.Count,
            rows.Select(r => r.WaterSlug).Distinct().Count(),
            waterName);
    }

    private static IReadOnlyList<SiteTrait> TraitsOf(Row row)
    {
        var traits = new List<SiteTrait>();
        if (row.Wall >= 3)
        {
            traits.Add(SiteTrait.Wall);
        }

        if (row.Wrecks >= 3)
        {
            traits.Add(SiteTrait.Wreck);
        }

        if (row.Difficulty is <= 2)
        {
            traits.Add(SiteTrait.Beginner);
        }

        if (row.Features.HasFlag(DiveSiteFeatures.Training))
        {
            traits.Add(SiteTrait.Training);
        }

        if (row.WaterType == WaterBodyType.River || row.Features.HasFlag(DiveSiteFeatures.River))
        {
            traits.Add(SiteTrait.River);
        }

        if (row.Features.HasFlag(DiveSiteFeatures.Cave))
        {
            traits.Add(SiteTrait.Cave);
        }

        return traits;
    }

    private static string WaterName(Row row, string culture) => WaterBodyNames.In(culture, row.NameDe, row.NameEn, row.NameFr, row.NameIt);

    private static NoticeKind? Worst(List<NoticeKind> notices) => notices.Count == 0 ? null : notices.Max();

    private sealed record Row(
        string Slug,
        string Name,
        string? Municipality,
        string? Canton,
        Point Location,
        byte ContentLevel,
        byte? DataLevel,
        int? MaxDepth,
        byte? Wall,
        byte? Wrecks,
        byte? Difficulty,
        DiveSiteFeatures Features,
        string WaterSlug,
        string NameDe,
        string? NameEn,
        string? NameFr,
        string? NameIt,
        WaterBodyType WaterType,
        decimal? Index,
        List<NoticeKind> Notices)
    {
        public double Latitude => Location.Y;

        public double Longitude => Location.X;
    }
}

/// <summary>Suchtext ohne Gross-/Kleinschreibung, Umlaute und Akzente: «zurich» und «zuerich» finden beide «Zürichsee».</summary>
internal static class SearchText
{
    public static string Of(string? text) => Plain(text ?? "").Trim();

    public static bool Matches(string search, params string?[] fields) =>
        fields.OfType<string>().Any(field => Plain(field).Contains(search, StringComparison.Ordinal) || Expanded(field).Contains(search, StringComparison.Ordinal));

    private static string Plain(string text) =>
        string.Concat(text.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

    private static string Expanded(string text) =>
        Plain(text.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue"));
}
