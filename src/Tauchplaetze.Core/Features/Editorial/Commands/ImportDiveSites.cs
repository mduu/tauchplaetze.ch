using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Text;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>Legt die Plätze aus `Seed/dive-sites.json` an, die noch fehlen. Jeder neue Platz bekommt eine freigegebene Version 1 mit <paramref name="EditorId"/> als Autor.</summary>
public record ImportDiveSitesCommand(string EditorId);

/// <summary>Ergebnis des Imports: Zahl der neu angelegten Plätze.</summary>
public record ImportDiveSitesResult(int Added);

internal class ImportDiveSitesHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions)
    : ICommandHandler<ImportDiveSitesCommand, ImportDiveSitesResult>
{
    public const string Comment = "Erstimport";

    private const string ResourceName = "Tauchplaetze.Core.Features.Editorial.Seed.dive-sites.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ImportDiveSitesResult> Handle(ImportDiveSitesCommand command, CancellationToken ct = default)
    {
        var waterBodies = await dbContext.WaterBodies.ToDictionaryAsync(w => w.Slug, w => w.Id, ct);
        var existingSlugs = await dbContext.DiveSites.Select(d => d.Slug).ToListAsync(ct);
        var missing = (await ReadSeed(ct))
            .Select(seed => (seed, slug: Slugs.From($"{seed.Name} {seed.Municipality}")))
            .Where(s => !existingSlugs.Contains(s.slug))
            .ToList();

        foreach (var (seed, slug) in missing)
        {
            var waterBodyId = waterBodies.TryGetValue(seed.WaterBody, out var id)
                ? id
                : throw new InvalidOperationException($"Gewässer «{seed.WaterBody}» fehlt für «{seed.Name}».");
            await revisions.CreateApprovedSite(ToSnapshot(seed, slug, waterBodyId), command.EditorId, Comment, ct);
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

    private static DiveSiteSnapshot ToSnapshot(DiveSiteSeed seed, string slug, int waterBodyId) => new(
        SnapshotVersion: DiveSiteSnapshot.CurrentVersion,
        Name: seed.Name,
        Slug: slug,
        WaterBodyId: waterBodyId,
        Latitude: seed.Latitude,
        Longitude: seed.Longitude,
        Municipality: seed.Municipality,
        Canton: seed.Canton,
        MaxDepth: null,
        DifficultyRating: null,
        WallRating: null,
        MarineLifeRating: null,
        WrecksRating: null,
        CurrentRating: null,
        SurroundingsRating: null,
        Access: null,
        Features: DiveSiteFeatures.None,
        HasDivingBan: false,
        InflowStationOverride: null,
        SatelliteLatitude: null,
        SatelliteLongitude: null,
        ForecastDisabled: false,
        Translations: []);

    private sealed record DiveSiteSeed(string Name, string WaterBody, double Latitude, double Longitude, string? Municipality, string? Canton);
}
