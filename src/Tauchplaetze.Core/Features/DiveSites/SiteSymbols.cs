using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Symbol eines Platzes ohne eigenen Wert (Story 006). Die Reihenfolge ist die Reihenfolge der Anzeige.</summary>
public enum SiteSymbol
{
    Defibrillator,

    Toilet,

    Parking,

    FlagHolder,

    Inclusive,

    Training,

    Wall,

    Wreck,

    River,

    MountainLake,
}

/// <summary>Schwierigkeit als Stufe: leicht (Bewertung 1 und 2), mittel (3), schwer (4 und 5).</summary>
public enum DifficultyBand
{
    Easy = 1,

    Medium = 2,

    Hard = 3,
}

/// <summary>
/// Was ein Platz bietet, für jede Kachel gleich (Story 006): Sperrung oder Warnung heute, Schwierigkeit, maximale Tiefe, dann die übrigen Symbole.
/// Ein fehlendes Symbol heisst «unbekannt», nie «nein».
/// </summary>
public record SiteSymbols(NoticeKind? Notice, DifficultyBand? Difficulty, int? MaxDepth, int? MountainLakeElevation, IReadOnlyList<SiteSymbol> Items)
{
    public static readonly SiteSymbols None = new(null, null, null, null, []);

    public bool IsEmpty => Notice is null && Difficulty is null && MaxDepth is null && Items.Count == 0;
}

/// <summary>Die Daten eines Platzes, aus denen die Symbole entstehen.</summary>
internal record SiteSymbolFacts(
    NoticeKind? NoticeToday,
    byte? Difficulty,
    int? MaxDepth,
    byte? Wall,
    byte? Wrecks,
    DiveSiteFeatures Features,
    WaterBodyType WaterBodyType,
    int? Elevation,
    IReadOnlySet<SusvTopic> SusvProjects,
    IReadOnlySet<EntryPointKind> Places);

/// <summary>Die Regel der Symbole an einer Stelle, für alle Listen und den Hero.</summary>
internal static class SiteSymbolRules
{
    public static SiteSymbols Of(SiteSymbolFacts facts)
    {
        var items = new List<SiteSymbol>();
        Add(items, SiteSymbol.Defibrillator, facts.SusvProjects.Contains(SusvTopic.Defibrillator));
        Add(items, SiteSymbol.Toilet, facts.SusvProjects.Contains(SusvTopic.MobileToilet) || facts.Places.Contains(EntryPointKind.Toilet));
        Add(items, SiteSymbol.Parking, facts.Places.Contains(EntryPointKind.Parking));
        Add(items, SiteSymbol.FlagHolder, facts.Places.Contains(EntryPointKind.FlagHolder));
        Add(items, SiteSymbol.Inclusive, facts.SusvProjects.Contains(SusvTopic.InclusiveSite));
        Add(items, SiteSymbol.Training, facts.Features.HasFlag(DiveSiteFeatures.Training));
        Add(items, SiteSymbol.Wall, facts.Wall >= 3);
        Add(items, SiteSymbol.Wreck, facts.Wrecks >= 3);
        Add(items, SiteSymbol.River, facts.WaterBodyType == WaterBodyType.River || facts.Features.HasFlag(DiveSiteFeatures.River));
        var mountainLake = MountainLakes.Elevation(facts.WaterBodyType, facts.Elevation);
        Add(items, SiteSymbol.MountainLake, mountainLake is not null);

        return new SiteSymbols(
            facts.NoticeToday is NoticeKind.Closure or NoticeKind.Warning ? facts.NoticeToday : null,
            Band(facts.Difficulty),
            facts.MaxDepth,
            mountainLake,
            items);
    }

    private static DifficultyBand? Band(byte? difficulty) => difficulty switch
    {
        1 or 2 => DifficultyBand.Easy,
        3 => DifficultyBand.Medium,
        4 or 5 => DifficultyBand.Hard,
        _ => null,
    };

    private static void Add(List<SiteSymbol> items, SiteSymbol symbol, bool applies)
    {
        if (applies)
        {
            items.Add(symbol);
        }
    }
}

/// <summary>Bestätigte Projekte des SUSV und Orte an Land aller Plätze, für die Symbole der Listen.</summary>
internal sealed class SiteSymbolSources(ILookup<Guid, SusvTopic> projects, ILookup<Guid, EntryPointKind> places)
{
    private static readonly EntryPointKind[] PlaceKinds = [EntryPointKind.Toilet, EntryPointKind.Parking, EntryPointKind.FlagHolder];

    public static async Task<SiteSymbolSources> Load(TauchplaetzeDbContext dbContext, CancellationToken ct)
    {
        var projects = await dbContext.SusvEntries
            .AsNoTracking()
            .Where(SusvRules.ShownProject)
            .SelectMany(e => e.Sites.Select(s => new { s.DiveSiteId, e.Topic }))
            .ToListAsync(ct);
        var places = await dbContext.DiveSiteEntryPoints
            .AsNoTracking()
            .Where(p => PlaceKinds.Contains(p.Kind))
            .Select(p => new { p.DiveSiteId, p.Kind })
            .Distinct()
            .ToListAsync(ct);

        return new SiteSymbolSources(projects.ToLookup(p => p.DiveSiteId, p => p.Topic), places.ToLookup(p => p.DiveSiteId, p => p.Kind));
    }

    public IReadOnlySet<SusvTopic> ProjectsOf(Guid siteId) => projects[siteId].ToHashSet();

    public IReadOnlySet<EntryPointKind> PlacesOf(Guid siteId) => places[siteId].ToHashSet();
}
