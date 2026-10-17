using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.WaterBodies;

public class SeedWaterBodiesTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Der_Seed_legt_Zuerichsee_Zugersee_und_Rhein_an_und_ein_zweiter_Lauf_aendert_nichts()
    {
        // Act
        var first = await Seed();
        var second = await Seed();

        // Assert
        Assert.Equal(3, first.Added);
        Assert.Equal(0, second.Added);
        var waterBodies = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"));
        Assert.Equal(["Rhein", "Zugersee", "Zürichsee"], waterBodies.Select(w => w.Name));
        Assert.Equal(WaterBodyType.River, waterBodies.Single(w => w.Slug == "rhein").Type);
    }

    private async Task<SeedWaterBodiesResult> Seed()
    {
        await using var scope = Sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<SeedWaterBodiesCommand, SeedWaterBodiesResult>>()
            .Handle(new SeedWaterBodiesCommand(), TestContext.Current.CancellationToken);
    }
}
