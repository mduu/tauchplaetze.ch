using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Features.Echolot.Queries;
using System.Net;
using System.Text.RegularExpressions;
using Tauchplaetze.Core.Features.Echolot;

namespace Tauchplaetze.Web.Tests;

public class EcholotPageTests : IClassFixture<WebApp>
{
    private readonly WebApp app;

    public EcholotPageTests(WebApp app)
    {
        this.app = app;
        var now = DateTimeOffset.UtcNow;
        app.Echolot.Answer = _ => new EcholotReport(
            [
                new DataSourceStatus(DataSource.Forecast, DataHealth.Current, now.AddHours(-2), 318),
                new DataSourceStatus(DataSource.LakeModels, DataHealth.Current, now.AddHours(-2)),
                new DataSourceStatus(DataSource.Weather, DataHealth.Current, now.AddHours(-2)),
                new DataSourceStatus(DataSource.Satellite, DataHealth.Late, now.AddHours(-2), ObservedOn: new DateOnly(2026, 11, 15)),
                new DataSourceStatus(DataSource.Discharge, DataHealth.Current, now.AddMinutes(-12), 34),
                new DataSourceStatus(DataSource.LakeLevel, DataHealth.Unknown, null),
            ],
            [
                new EcholotEntry(new DateOnly(2026, 11, 21), EcholotEntryKind.Feature, "Hinweis bei sehr tiefem Seepegel", "Text zur Funktion.", []),
                new EcholotEntry(new DateOnly(2026, 11, 19), EcholotEntryKind.Described, null, null,
                    [new DescribedSite("park-horn", "Park Horn", "Zürichsee"), new DescribedSite("chappeli-oberaegeri", "Chappeli", "Ägerisee")]),
            ],
            HasOlderEntries: true,
            new EcholotSounding("riff-williams-herrliberg", "Riff Williams", "Zürichsee", new DateOnly(2026, 10, 12), 12m, 16m, 15.2m, 6.1m));
    }

    [Theory]
    [InlineData("/de/echolot", "Datenstand und Neuigkeiten", "Verspätet: Satellit.", "Neuestes Bild vom 15.11.", "Neu beschrieben · 2 Plätze", "Hier stehen die letzten 2 Neuigkeiten.")]
    [InlineData("/en/sonar", "Data status and news", "Delayed: Satellite.", "Latest image from 15.11.", "Newly described · 2 sites", "This shows the latest 2 news items.")]
    [InlineData("/fr/sondeur", "État des données et nouveautés", "En retard : Satellite.", "Dernière image du 15.11.", "Nouvellement décrits · 2 sites", "Voici les 2 dernières nouveautés.")]
    [InlineData("/it/ecoscandaglio", "Stato dei dati e novità", "In ritardo: Satellite.", "Ultima immagine del 15.11.", "Descritti per la prima volta · 2 siti", "Qui ci sono le ultime 2 novità.")]
    public async Task Das_Echolot_zeigt_den_Datenstand_mit_Zusammenfassung_und_die_Neuigkeiten_mit_Hinweis_auf_aeltere(
        string path, string subtitle, string summary, string satellite, string described, string older)
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Contains(subtitle, text);
        Assert.Contains(summary, text);
        Assert.Contains(satellite, text);
        Assert.Contains(described, text);
        Assert.Contains(older, text);
        Assert.Contains("data-health=\"Unknown\"", html);
        Assert.Contains($"href=\"{path[..3]}/", html);
    }

    [Fact]
    public async Task Die_Anzeige_zeigt_die_Sprungschicht_eines_Beispiels_und_nennt_Platz_und_Tag_der_Prognose()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var html = await browser.GetStringAsync("/de/echolot", TestContext.Current.CancellationToken);
        var text = VisibleText.Of(html);

        // Assert
        Assert.Matches(@"Beispiel: Riff Williams ?, Zürichsee · Prognose vom 12\.10\.2026", text);
        Assert.Contains("15.2 °C", text);
        Assert.Contains("Sprungschicht 12–16 m", text);
        Assert.Contains("href=\"/de/tauchplaetze/riff-williams-herrliberg\"", html);
        Assert.Contains("data-sounder=\"thermocline\"", html);
    }

    [Fact]
    public async Task Echolot_und_Ueber_verlinken_sich_gegenseitig()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        var echolot = await browser.GetStringAsync("/fr/sondeur", TestContext.Current.CancellationToken);
        var about = await browser.GetStringAsync("/fr/a-propos", TestContext.Current.CancellationToken);

        // Assert
        Assert.Matches("href=\"/fr/a-propos#prognose\"[^>]*>Comment fonctionne tauchplaetze.ch", echolot);
        Assert.Matches("href=\"/fr/sondeur\"[^>]*>Le Sondeur montre si chaque source", about);
    }

    [Theory]
    [InlineData("/de/tauchplaetze", "Über tauchplaetze.ch", "Teilweise verspätet", "/de/echolot")]
    [InlineData("/en/dive-sites", "About tauchplaetze.ch", "Partly delayed", "/en/sonar")]
    [InlineData("/fr/sites-de-plongee", "À propos de tauchplaetze.ch", "En partie en retard", "/fr/sondeur")]
    [InlineData("/it/siti-di-immersione", "Chi siamo", "In parte in ritardo", "/it/ecoscandaglio")]
    public async Task Das_Info_Menue_im_Kopf_fuehrt_zu_Ueber_und_zum_Echolot_mit_dem_Gesamtzustand_auch_im_Menue_fuer_schmale_Bildschirme(
        string path, string about, string health, string echolot)
    {
        // Arrange
        using var browser = app.Browser();
        var answer = app.DataHealth.Answer;
        app.DataHealth.Answer = _ => DataHealth.Late;
        var cache = app.Services.GetRequiredService<IMemoryCache>();
        cache.Remove(nameof(GetDataHealthQuery));

        // Act
        var html = await browser.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        app.DataHealth.Answer = answer;
        cache.Remove(nameof(GetDataHealthQuery));
        var header = html[..html.IndexOf("</header>", StringComparison.Ordinal)];
        Assert.Equal(2, Regex.Matches(VisibleText.Of(header + "</body>"), Regex.Escape(about)).Count);
        Assert.Equal(2, Regex.Matches(header, $"href=\"{echolot}\"").Count);
        Assert.Equal(2, Regex.Matches(header, "data-health=\"Late\"").Count);
        Assert.Contains(health, WebUtility.HtmlDecode(header));
    }
}
