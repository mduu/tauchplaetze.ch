using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Features.Forecasts;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.WaterBodies;

public class SeedWaterBodiesTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Der_Seed_legt_die_Gewaesser_an_und_ein_zweiter_Lauf_aendert_nichts()
    {
        // Act
        var first = await Seed();
        var second = await Seed();

        // Assert
        Assert.Equal(0, second.Added);
        var waterBodies = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"));
        Assert.Equal(first.Added, waterBodies.Count);
        Assert.Subset(waterBodies.Select(w => w.Name).ToHashSet(), new HashSet<string> { "Rhein", "Zugersee", "Zürichsee" });
        Assert.Equal(WaterBodyType.River, waterBodies.Single(w => w.Slug == "rhein").Type);
    }

    [SqlFact]
    public async Task Der_Seed_entfernt_eine_ausgemusterte_Station_und_ergaenzt_die_Muendung_bestehender_Zufluesse()
    {
        // Arrange: ein Stand von vor rules-1.5, mit dem Aabach am Hallwilersee und ohne Mündung der Linth.
        await Seed();
        await Sql.Arrange(async db =>
        {
            var hallwilersee = await db.WaterBodies.Include(w => w.HydroStations).SingleAsync(w => w.Slug == "hallwilersee");
            hallwilersee.HydroStations.Add(new WaterBodyHydroStation { StationId = "2416", Name = "Aabach – Hitzkirch, Richensee", Role = HydroStationRole.Inflow });
            var linth = await db.Set<WaterBodyHydroStation>().SingleAsync(s => s.StationId == "2104");
            linth.Mouth = null;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // Act
        await Seed();

        // Assert
        await Sql.Arrange(async db =>
        {
            Assert.False(await db.Set<WaterBodyHydroStation>().AnyAsync(s => s.StationId == "2416"));
            Assert.NotNull((await db.Set<WaterBodyHydroStation>().SingleAsync(s => s.StationId == "2104")).Mouth);
        });
    }

    [Fact]
    public void Jeder_Schluessel_fuer_Alplakes_im_Seed_steht_im_Angebot_von_Alplakes()
    {
        // Arrange
        using var seed = JsonDocument.Parse(SeedJson());
        var models3d = Names(TestData.Read("alplakes", "metadata-3d-20261019.json"));
        var simstrat = Names(TestData.Read("alplakes", "metadata-1d-20261019.json"));
        using var remoteSensing = JsonDocument.Parse(TestData.Read("alplakes", "metadata-remotesensing-20261019.json"));

        // Act
        var unknown = seed.RootElement.EnumerateArray()
            .SelectMany(w => Problems(w, models3d, simstrat, remoteSensing.RootElement))
            .ToList();

        // Assert
        Assert.Empty(unknown);
    }

    private static IEnumerable<string> Problems(JsonElement waterBody, HashSet<string> models3d, HashSet<string> simstrat, JsonElement remoteSensing)
    {
        var slug = waterBody.GetProperty("slug").GetString();
        var lake = Text(waterBody, "alplakesLakeKey");
        if (lake is not null && !remoteSensing.TryGetProperty(lake, out _))
        {
            yield return $"{slug}: Satellit {lake}";
        }

        if (Text(waterBody, "alplakesModel3d") is { } model && !models3d.Contains($"{model}/{lake}"))
        {
            yield return $"{slug}: 3D {model}/{lake}";
        }

        if (Text(waterBody, "alplakesSimstratKey") is { } key && !simstrat.Contains($"simstrat/{key}"))
        {
            yield return $"{slug}: Simstrat {key}";
        }
    }

    private static HashSet<string> Names(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .SelectMany(m => m.GetProperty("lakes").EnumerateArray().Select(l => $"{m.GetProperty("model").GetString()}/{l.GetProperty("name").GetString()}"))
            .ToHashSet();
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string SeedJson()
    {
        using var stream = typeof(SeedWaterBodiesCommand).Assembly.GetManifestResourceStream("Tauchplaetze.Core.Features.WaterBodies.Seed.water-bodies.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private Task<SeedWaterBodiesResult> Seed() =>
        Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
}
