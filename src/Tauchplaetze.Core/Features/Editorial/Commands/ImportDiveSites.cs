using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Text;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>
/// Legt die Plätze aus `Seed/dive-sites.json` an, die noch fehlen, mit ihren Einstiegen. Jeder neue Platz bekommt eine freigegebene Version 1
/// mit <paramref name="EditorId"/> als Autor. Ein Platz, dessen Slug später umbenannt wurde, kommt nicht wieder.
/// </summary>
public record ImportDiveSitesCommand(string EditorId);

/// <summary>Ergebnis des Imports: Zahl der neu angelegten Plätze.</summary>
public record ImportDiveSitesResult(int Added);

internal class ImportDiveSitesHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions)
    : ICommandHandler<ImportDiveSitesCommand, ImportDiveSitesResult>
{
    public const string Comment = "Erstimport";

    private const string ResourceName = "Tauchplaetze.Core.Features.Editorial.Seed.dive-sites.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<ImportDiveSitesResult> Handle(ImportDiveSitesCommand command, CancellationToken ct = default)
    {
        var waterBodies = await dbContext.WaterBodies.ToDictionaryAsync(w => w.Slug, w => w.Id, ct);
        var existingSlugs = (await dbContext.DiveSites.Select(d => d.Slug).ToListAsync(ct))
            .Concat(await dbContext.SlugRedirects.Select(r => r.OldSlug).ToListAsync(ct))
            .ToHashSet();
        var missing = (await ReadSeed(ct))
            .Select(seed => (seed, slug: Slugs.From($"{seed.Name} {seed.Municipality}")))
            .Where(s => !existingSlugs.Contains(s.slug))
            .ToList();

        foreach (var (seed, slug) in missing)
        {
            var waterBodyId = waterBodies.TryGetValue(seed.WaterBody, out var id)
                ? id
                : throw new InvalidOperationException($"Gewässer «{seed.WaterBody}» fehlt für «{seed.Name}».");
            var content = ToContent(seed, slug, waterBodyId);
            var site = revisions.NewDraft(content, command.EditorId);
            await revisions.Append(site, content, null, command.EditorId, Comment, approve: true, ct);
        }

        await dbContext.SaveChangesAsync(ct);
        return new ImportDiveSitesResult(missing.Count);
    }

    private static async Task<IReadOnlyList<DiveSiteSeed>> ReadSeed(CancellationToken ct)
    {
        await using var stream = typeof(ImportDiveSitesHandler).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Ressource {ResourceName} fehlt.");
        return await JsonSerializer.DeserializeAsync<List<DiveSiteSeed>>(stream, JsonOptions, ct) ?? [];
    }

    private static DiveSiteContent ToContent(DiveSiteSeed seed, string slug, int waterBodyId) => new(
        Name: seed.Name,
        Slug: slug,
        WaterBodyId: waterBodyId,
        Latitude: seed.Latitude,
        Longitude: seed.Longitude,
        Municipality: seed.Municipality,
        Canton: seed.Canton,
        MaxDepth: seed.MaxDepth,
        DifficultyRating: (byte?)seed.DifficultyRating,
        WallRating: (byte?)seed.WallRating,
        MarineLifeRating: null,
        WrecksRating: (byte?)seed.WrecksRating,
        CurrentRating: null,
        SurroundingsRating: null,
        Access: seed.Access,
        Features: (seed.Features ?? []).Aggregate(DiveSiteFeatures.None, (all, f) => all | f),
        HasDivingBan: false,
        InflowStationOverride: null,
        SatelliteLatitude: null,
        SatelliteLongitude: null,
        ForecastDisabled: false,
        Translations: [],
        EntryPoints: [.. (seed.EntryPoints ?? []).Select(ToEntryPoint)],
        OtherNames: OtherNames.Valid(seed.Name, seed.OtherNames ?? []));

    private static EntryPointContent ToEntryPoint(EntryPointSeed seed) => new(
        Guid.CreateVersion7(),
        seed.Latitude,
        seed.Longitude,
        Enum.Parse<EntryPointKind>(seed.Kind),
        seed.Name is null ? [] : [new EntryPointText("de", seed.Name, null)]);

    private sealed record DiveSiteSeed(
        string Name,
        string WaterBody,
        double Latitude,
        double Longitude,
        string? Municipality,
        string? Canton,
        IReadOnlyList<EntryPointSeed>? EntryPoints,
        int? MaxDepth,
        int? DifficultyRating,
        int? WallRating,
        int? WrecksRating,
        DiveSiteAccess? Access,
        IReadOnlyList<DiveSiteFeatures>? Features,
        IReadOnlyList<string>? OtherNames);

    private sealed record EntryPointSeed(string Kind, string? Name, double Latitude, double Longitude);
}
