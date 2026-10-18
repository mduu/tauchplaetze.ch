using System.Net;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Tests;

public class VersionsPageTests : IClassFixture<WebApp>
{
    private static readonly DateTimeOffset At = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    private readonly WebApp app;

    public VersionsPageTests(WebApp app)
    {
        this.app = app;
        app.PublicRevisions.Answer = query => query.Slug != "riff-williams" ? null : new PublicRevisions("Riff Williams", "riff-williams",
        [
            new RevisionSummary(Guid.NewGuid(), 2, 1, RevisionStatus.Approved, At, At, "Tiefe ergänzt", null, null, "Lucie", null, false),
            new RevisionSummary(Guid.NewGuid(), 1, null, RevisionStatus.Approved, At, At, "Erstimport", null, null, null, null, false),
        ]);
        app.PublicChanges.Answer = query => query is { From: 1, To: 2 } ? [new FieldChange("MaxDepth", null, "38")] : null;
    }

    [Fact]
    public async Task Die_History_zeigt_Namen_nur_mit_Einwilligung_und_ist_nicht_indexiert()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/de/tauchplaetze/riff-williams/versionen", TestContext.Current.CancellationToken);

        // Assert
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("von Lucie, freigegeben von Moderation", text);
        Assert.Contains("von Mitwirkende, freigegeben von Moderation", text);
        Assert.Contains("<meta name=\"robots\" content=\"noindex\"", html);
    }

    [Fact]
    public async Task Der_Vergleich_zeigt_die_Aenderungen_zum_Vorgaenger()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/fr/sites-de-plongee/riff-williams/versions?v=2", TestContext.Current.CancellationToken);

        // Assert
        var text = VisibleText.Of(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Modifications de la version 2", text);
        Assert.Contains("Profondeur maximale (m) 38", text);
    }

    [Fact]
    public async Task Ein_unbekannter_Platz_hat_keine_History()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/de/tauchplaetze/gibt-es-nicht/versionen", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/de/tauchplaetze/riff-williams/versionen", "it", "/it/siti-di-immersione/riff-williams/versioni")]
    [InlineData("/en/dive-sites/riff-williams", "fr", "/fr/sites-de-plongee/riff-williams")]
    [InlineData("/fr/eaux/zuerichsee", "de", "/de/gewaesser/zuerichsee")]
    [InlineData("/en/dive-sites/forecast/versions", "de", "/de/tauchplaetze/forecast/versionen")]
    public void Die_Sprachwahl_uebersetzt_alle_Segmente_des_Pfads(string path, string language, string expected)
    {
        // Act
        var switched = LanguagePaths.Switch(path, language);

        // Assert
        Assert.Equal(expected, switched);
    }
}
