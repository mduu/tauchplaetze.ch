using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Shipping;
using Tauchplaetze.Core.Features.Shipping.Commands;
using Tauchplaetze.Core.Features.Shipping.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Shipping;

/// <remarks>Story 005. Der Seed enthält die echte Landungsstelle «Herrliberg (See)» bei 47.283234 / 8.609327.</remarks>
public class ShippingTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Der_Seed_legt_die_Objekte_an_und_ein_zweiter_Lauf_aendert_nichts()
    {
        // Act
        var first = await Command<SeedShippingFeaturesCommand, SeedShippingFeaturesResult>(new SeedShippingFeaturesCommand());
        var second = await Command<SeedShippingFeaturesCommand, SeedShippingFeaturesResult>(new SeedShippingFeaturesCommand());

        // Assert
        Assert.True(first.Added > 100);
        Assert.Equal(new SeedShippingFeaturesResult(0, 0, 0), second);
    }

    [SqlFact]
    public async Task Ein_Platz_weniger_als_100_m_von_einer_Landungsstelle_nennt_sie_beim_Namen()
    {
        // Arrange
        var site = await Site(47.2836, 8.6090);

        // Act
        var shipping = await Query<GetSiteShippingQuery, SiteShipping>(new GetSiteShippingQuery(site));

        // Assert
        Assert.Equal("Herrliberg (See)", shipping.NearLandingStage);
        Assert.Contains(shipping.Features, f => f.Kind == ShippingFeatureKind.LandingStage && f.Name == "Herrliberg (See)");
    }

    [SqlFact]
    public async Task Ein_Platz_weit_weg_von_Schiffen_hat_keine_Ebene_auch_nicht_durch_einen_verdrehten_Hafen()
    {
        // Arrange
        var site = await Site(46.75, 8.25);

        // Act
        var shipping = await Query<GetSiteShippingQuery, SiteShipping>(new GetSiteShippingQuery(site));

        // Assert
        Assert.Empty(shipping.Features);
        Assert.Null(shipping.NearLandingStage);
    }

    private async Task<Guid> Site(double latitude, double longitude)
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        await Command<SeedShippingFeaturesCommand, SeedShippingFeaturesResult>(new SeedShippingFeaturesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zuerichsee").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, "Testplatz", lake, latitude, longitude, null, null, null));
        return result.DiveSiteId!.Value;
    }
}
