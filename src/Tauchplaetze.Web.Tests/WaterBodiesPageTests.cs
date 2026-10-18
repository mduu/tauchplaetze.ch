using System.Net;
using System.Text.RegularExpressions;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Web.Tests;

public class WaterBodiesPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public WaterBodiesPageTests(WebApp app)
    {
        this.app = app;
        app.WaterBodyGroups.Answer = _ =>
        [
            new WaterBodyGroup(WaterBodyGroupKind.LargeLakes, null, [Water("zuerichsee", "lac de Zurich", WaterBodyType.Lake, 45)]),
            new WaterBodyGroup(WaterBodyGroupKind.Region, SwissRegion.Mittelland, [Water("bielersee", "lac de Bienne", WaterBodyType.Lake, 7)]),
            new WaterBodyGroup(WaterBodyGroupKind.Region, SwissRegion.Ticino, [Water("lago-di-vogorno", "Lago di Vogorno", WaterBodyType.Lake, 2)]),
            new WaterBodyGroup(WaterBodyGroupKind.Rivers, null, [Water("aare", "Aar", WaterBodyType.River, 14)]),
        ];
    }

    [Fact]
    public async Task Die_Gewaesser_stehen_in_Gruppen_grosse_Seen_zuerst_dann_Regionen_dann_Fluesse_mit_Sprungnavigation()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr/eaux", TestContext.Current.CancellationToken);

        // Assert
        var titles = Regex.Matches(html, "class=\"water-group-title\"[^>]*>([^<]+)<").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value));
        Assert.Equal(["Les grands lacs", "Espace Mittelland", "Tessin", "Rivières"], titles);
        Assert.Contains("href=\"/fr/eaux#region-ticino\"", html);
        Assert.Contains("id=\"region-ticino\"", html);
        Assert.Contains("href=\"/fr/eaux/lago-di-vogorno\"", html);
        Assert.Contains("2 sites de plongée", VisibleText.Of(html));
    }

    private static WaterBodySummary Water(string slug, string name, WaterBodyType type, int sites) => new(slug.Length, slug, name, type, 46.5, 8.0, sites);
}
