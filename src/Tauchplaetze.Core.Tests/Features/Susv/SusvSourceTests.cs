using Tauchplaetze.Core.Features.Susv.Sources;
using Tauchplaetze.Core.Tests.Features.Forecasts;

namespace Tauchplaetze.Core.Tests.Features.Susv;

/// <summary>Contract Tests mit echten Antworten von susv.ch vom 26.11.2026 (`TestData/susv`). Die Seite ist im Aufbau, darum auch Varianten des Formats.</summary>
public class SusvSourceTests
{
    private static readonly Uri Base = new("https://susv.ch/");

    [Fact]
    public void Die_Karte_liefert_jeden_Eintrag_mit_Titel_Koordinate_Link_und_Datum_in_Schweizer_Zeit()
    {
        // Arrange
        var json = TestData.Read("susv", "map-restriction-de.json");

        // Act
        var map = SusvMap.Parse(json, Base);

        // Assert
        var strick = map.Entries.Single(e => e.Id == 49178);
        Assert.Equal((23, 0), (map.Entries.Count, map.Skipped));
        Assert.Equal(("Arth, Tauchplatz Strick", 47.066079, 8.524055), (strick.Title, strick.Latitude, strick.Longitude));
        Assert.Equal("https://susv.ch/de/arth-tauchplatz-strick/", strick.DetailUrl);
        Assert.Equal(new DateTimeOffset(2025, 8, 7, 11, 25, 0, TimeSpan.FromHours(2)), strick.PublishedAt);
        Assert.Null(strick.Teaser);
    }

    [Theory]
    [InlineData("""{"content":{"entries":[{"id":7,"title":"A\tB","lat":"47.1","lng":"8.5","detailUrl":"/de/a/","date":"2026-03-02 10:33:00"}]}}""")]
    [InlineData("""{"entries":[{"id":"7","title":"A  B","lat":47.1,"lng":8.5,"detailUrl":"https://susv.ch/de/a/","neu":true}]}""")]
    [InlineData("""[{"id":7,"title":" A B ","lat":47.1,"lon":8.5,"detailUrl":"https://susv.ch/de/a/","date":{"date":"2026-03-02"}}]""")]
    public void Ein_umgebautes_Format_liest_sich_weiter_solange_Nummer_Titel_Koordinate_und_Link_da_sind(string json)
    {
        // Act
        var map = SusvMap.Parse(json, Base);

        // Assert
        var entry = Assert.Single(map.Entries);
        Assert.Equal((7, "A B", 47.1, 8.5, "https://susv.ch/de/a/"), (entry.Id, entry.Title, entry.Latitude, entry.Longitude, entry.DetailUrl));
    }

    [Fact]
    public void Ein_unlesbarer_Eintrag_faellt_weg_ohne_die_uebrigen_mitzunehmen()
    {
        // Arrange
        const string json = """
            {"content":{"entries":[
              {"id":1,"title":"Gut","lat":"47.1","lng":"8.5","detailUrl":"/de/gut/"},
              {"id":2,"title":"Ohne Koordinate","detailUrl":"/de/x/"},
              {"id":3,"title":"Vertauscht","lat":"8.5","lng":"47.1","detailUrl":"/de/y/"},
              {"id":4,"title":"Fremder Link","lat":"47.1","lng":"8.5","detailUrl":"https://example.org/z/"},
              "kein Objekt"
            ]}}
            """;

        // Act
        var map = SusvMap.Parse(json, Base);

        // Assert
        Assert.Equal([1], map.Entries.Select(e => e.Id));
        Assert.Equal(4, map.Skipped);
    }

    [Theory]
    [InlineData("""{"content":{"items":[]}}""")]
    [InlineData("<html>Wartung</html>")]
    public void Ohne_Liste_der_Eintraege_scheitert_der_Call_statt_leer_zu_gelten(string body)
    {
        // Act
        var error = Record.Exception(() => SusvMap.Parse(body, Base));

        // Assert
        Assert.True(error is FormatException or System.Text.Json.JsonException);
    }

    [Fact]
    public void Die_Detailseite_liefert_den_Haupttext_ohne_Kopf_Fuss_und_Karte_und_ohne_doppelten_Lead()
    {
        // Arrange
        var html = TestData.Read("susv", "detail-49178-de.html");

        // Act
        var text = SusvDetailPage.MainText(html, "Arth, Tauchplatz Strick");

        // Assert
        Assert.NotNull(text);
        Assert.StartsWith("Am Tauchplatz Strick am Zugersee ist jeweils vom 15. Oktober – 15. Dezember das Tauchen UNTERSAGT.", text);
        Assert.Contains("Laichzeit der Zugersee Röteli", text);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "15. Oktober"));
        Assert.DoesNotContain("Legende", text);
        Assert.DoesNotContain("example.org", text);
    }

    [Fact]
    public void Die_Detailseite_liest_sich_auch_ohne_main_und_mit_anderen_Klassen()
    {
        // Arrange
        const string html = """
            <html><body><nav><p>Menü</p></nav><div class="neu"><h1>Platz</h1><p>Erster&nbsp;Satz.<br>Zweite Zeile.</p><ul><li>Punkt</li></ul></div>
            <footer><p>Kontakt</p></footer><script>var p = "<p>x</p>";</script></body></html>
            """;

        // Act
        var text = SusvDetailPage.MainText(html, "Platz");

        // Assert
        Assert.Equal("Erster Satz.\nZweite Zeile.\n\nPunkt", text);
    }

    [Fact]
    public void Eine_Detailseite_ohne_Absatz_ergibt_keinen_Text()
    {
        // Act
        var text = SusvDetailPage.MainText("<main><div>Nur Karte</div></main>", "Titel");

        // Assert
        Assert.Null(text);
    }
}
