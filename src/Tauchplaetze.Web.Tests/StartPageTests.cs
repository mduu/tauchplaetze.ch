using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.Tests;

public class StartPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public StartPageTests(WebApp app)
    {
        this.app = app;
        app.BestSites.Ranking =
        [
            new RankedSite("riff-williams-herrliberg", "Riff Williams", "Herrliberg", "Zürichsee", 8.4m, 3, 15.2m, 11.4m, 12m, 18m, 2, [ReasonCode.ClearBelowThermocline], null),
            new RankedSite("zweiter-platz", "Zweiter Platz", "Zug", "Zugersee", 6.1m, 2, 14m, 12m, null, null, 4, [ReasonCode.Windy], null),
        ];
    }

    [Fact]
    public async Task Die_Startseite_zeigt_die_beste_Wahl_und_die_Rangliste_mit_Link_auf_den_Platz()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de", TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(html);
        Assert.Contains("Beste Wahl Riff Williams", text);
        Assert.Contains("Zweiter Platz", text);
        Assert.Contains("href=\"/de/tauchplaetze/riff-williams-herrliberg\"", html);
    }

    [Fact]
    public async Task Ein_anderer_Tag_und_ein_Ort_kommen_aus_der_URL()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/fr?tag=2026-10-20&ort=zuerich", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new DateOnly(2026, 10, 20), app.BestSites.LastQuery!.Date);
        Assert.Equal(47.3769, app.BestSites.LastQuery.Area!.Latitude);
        Assert.Contains("Où plonger mardi ?", VisibleText.Of(html));
    }

    [Fact]
    public async Task Ohne_Plaetze_mit_Index_erklaert_die_Startseite_wann_die_Prognose_rechnet()
    {
        // Arrange
        using var browser = app.Browser();
        var ranking = app.BestSites.Ranking;
        app.BestSites.Ranking = [];

        // Act
        var html = await browser.GetStringAsync("/it", TestContext.Current.CancellationToken);

        // Assert
        app.BestSites.Ranking = ranking;
        Assert.Contains("Ancora nessun indice per questo giorno.", VisibleText.Of(html));
    }
}
