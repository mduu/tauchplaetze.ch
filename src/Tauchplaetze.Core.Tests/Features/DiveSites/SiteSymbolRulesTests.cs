using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Tests.Features.DiveSites;

public class SiteSymbolRulesTests
{
    [Theory]
    [InlineData((byte)1, DifficultyBand.Beginner)]
    [InlineData((byte)2, DifficultyBand.Beginner)]
    [InlineData((byte)3, DifficultyBand.Advanced)]
    [InlineData((byte)4, DifficultyBand.Expert)]
    [InlineData((byte)5, DifficultyBand.Expert)]
    [InlineData(null, null)]
    public void Die_Schwierigkeit_wird_zu_Einsteiger_Fortgeschrittene_oder_Erfahrene_ohne_Bewertung_fehlt_sie(byte? rating, DifficultyBand? expected)
    {
        // Arrange
        var facts = Facts() with { Difficulty = rating };

        // Act
        var symbols = SiteSymbolRules.Of(facts);

        // Assert
        Assert.Equal(expected, symbols.Difficulty);
    }

    [Theory]
    [InlineData(NoticeKind.Closure, NoticeKind.Closure)]
    [InlineData(NoticeKind.Warning, NoticeKind.Warning)]
    [InlineData(NoticeKind.Info, null)]
    [InlineData(null, null)]
    public void Nur_eine_Sperrung_oder_Warnung_von_heute_ergibt_ein_Symbol(NoticeKind? today, NoticeKind? expected)
    {
        // Arrange
        var facts = Facts() with { NoticeToday = today };

        // Act
        var symbols = SiteSymbolRules.Of(facts);

        // Assert
        Assert.Equal(expected, symbols.Notice);
    }

    [Fact]
    public void Die_Toilette_kommt_vom_SUSV_oder_vom_Ort_der_Redaktion()
    {
        // Arrange
        var fromSusv = Facts() with { SusvProjects = new HashSet<SusvTopic> { SusvTopic.MobileToilet } };
        var fromPlace = Facts() with { Places = new HashSet<EntryPointKind> { EntryPointKind.Toilet } };

        // Act
        var susv = SiteSymbolRules.Of(fromSusv);
        var place = SiteSymbolRules.Of(fromPlace);

        // Assert
        Assert.Equal([SiteSymbol.Toilet], susv.Items);
        Assert.Equal([SiteSymbol.Toilet], place.Items);
    }

    [Fact]
    public void Alle_Symbole_stehen_in_der_festen_Reihenfolge()
    {
        // Arrange
        var facts = new SiteSymbolFacts(
            null, null, 42, 3, 4, DiveSiteFeatures.Training | DiveSiteFeatures.River, WaterBodyType.Lake, 1200,
            new HashSet<SusvTopic> { SusvTopic.InclusiveSite, SusvTopic.Defibrillator },
            new HashSet<EntryPointKind> { EntryPointKind.FlagHolder, EntryPointKind.Parking, EntryPointKind.Toilet });

        // Act
        var symbols = SiteSymbolRules.Of(facts);

        // Assert
        Assert.Equal(
            [SiteSymbol.Defibrillator, SiteSymbol.Toilet, SiteSymbol.Parking, SiteSymbol.FlagHolder, SiteSymbol.Inclusive,
             SiteSymbol.Training, SiteSymbol.Wall, SiteSymbol.Wreck, SiteSymbol.River, SiteSymbol.MountainLake],
            symbols.Items);
        Assert.Equal(42, symbols.MaxDepth);
        Assert.Equal(1200, symbols.MountainLakeElevation);
    }

    [Theory]
    [InlineData((byte)2, false)]
    [InlineData((byte)3, true)]
    [InlineData(null, false)]
    public void Steilwand_und_Wrack_ab_Bewertung_3(byte? rating, bool expected)
    {
        // Arrange
        var facts = Facts() with { Wall = rating, Wrecks = rating };

        // Act
        var symbols = SiteSymbolRules.Of(facts);

        // Assert
        Assert.Equal(expected, symbols.Items.Contains(SiteSymbol.Wall));
        Assert.Equal(expected, symbols.Items.Contains(SiteSymbol.Wreck));
    }

    [Theory]
    [InlineData(WaterBodyType.Lake, 699, false, false)]
    [InlineData(WaterBodyType.Lake, 700, true, false)]
    [InlineData(WaterBodyType.River, 900, false, true)]
    public void Bergsee_nur_bei_Seen_ab_700_m_ein_Fluss_ist_ein_Fluss(WaterBodyType type, int elevation, bool mountainLake, bool river)
    {
        // Arrange
        var facts = Facts() with { WaterBodyType = type, Elevation = elevation };

        // Act
        var symbols = SiteSymbolRules.Of(facts);

        // Assert
        Assert.Equal(mountainLake, symbols.Items.Contains(SiteSymbol.MountainLake));
        Assert.Equal(river, symbols.Items.Contains(SiteSymbol.River));
    }

    [Fact]
    public void Ohne_Daten_hat_ein_Platz_keine_Symbole()
    {
        // Act
        var symbols = SiteSymbolRules.Of(Facts());

        // Assert
        Assert.True(symbols.IsEmpty);
    }

    private static SiteSymbolFacts Facts() =>
        new(null, null, null, null, null, DiveSiteFeatures.None, WaterBodyType.Lake, 400, new HashSet<SusvTopic>(), new HashSet<EntryPointKind>());
}
