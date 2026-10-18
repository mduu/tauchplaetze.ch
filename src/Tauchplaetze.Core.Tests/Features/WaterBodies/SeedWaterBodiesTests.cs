using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
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

    private Task<SeedWaterBodiesResult> Seed() =>
        Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
}
