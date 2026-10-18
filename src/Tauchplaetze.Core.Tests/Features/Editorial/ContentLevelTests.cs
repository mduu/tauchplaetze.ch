using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Tests.Features.Editorial;

public class ContentLevelTests
{
    private static readonly DiveSiteContent Directory = new(
        "Riff Williams", "riff-williams-herrliberg", 1, 47.28, 8.6, "Herrliberg", "ZH", null, null, null, null, null, null, null, null,
        DiveSiteFeatures.None, false, null, null, null, false, [], []);

    private static readonly DiveSiteContent Basic = Directory with
    {
        MaxDepth = 30,
        DifficultyRating = 3,
        Access = DiveSiteAccess.Shore,
        Translations = [new DiveSiteContentTranslation("de", null, null, "Parkplatz beim Hafen.", null, null, null, false)],
        EntryPoints = [new EntryPointContent(Guid.NewGuid(), 47.28, 8.6, EntryPointKind.EntryAndExit, [new EntryPointText("de", "Treppe", "Über die Treppe ins Wasser.")])],
    };

    [Fact]
    public void Name_Koordinate_und_Gewaesser_ergeben_Stufe_0()
    {
        // Act
        var level = ContentLevelRule.For(Directory, null);

        // Assert
        Assert.Equal(0, level);
    }

    [Fact]
    public void Tiefe_Schwierigkeit_Zugang_ein_beschriebener_Einstieg_und_Anfahrt_ergeben_Stufe_1()
    {
        // Act
        var level = ContentLevelRule.For(Basic, null);

        // Assert
        Assert.Equal(1, level);
    }

    [Theory]
    [InlineData(EntryPointKind.Entry, "en")]
    [InlineData(EntryPointKind.Parking, "de")]
    [InlineData(EntryPointKind.Toilet, "de")]
    public void Ohne_deutsch_beschriebenen_Einstieg_bleibt_Stufe_0_ein_Parkplatz_oder_eine_Toilette_zaehlt_nicht(EntryPointKind kind, string culture)
    {
        // Arrange
        var content = Basic with { EntryPoints = [new EntryPointContent(Guid.NewGuid(), 47.28, 8.6, kind, [new EntryPointText(culture, "Beim Hafen", "Gleich neben dem Hafen.")])] };

        // Act
        var level = ContentLevelRule.For(content, null);

        // Assert
        Assert.Equal(0, level);
    }

    [Fact]
    public void Unter_Wasser_auf_Deutsch_und_ein_Hero_Foto_ergeben_Stufe_2()
    {
        // Arrange
        var full = Basic with { Translations = [new DiveSiteContentTranslation("de", "Felswand bis 30 m.", null, "Parkplatz beim Hafen.", null, null, null, false)] };

        // Act
        var withPhoto = ContentLevelRule.For(full, Guid.NewGuid());
        var withoutPhoto = ContentLevelRule.For(full, null);

        // Assert
        Assert.Equal((2, 1), (withPhoto, withoutPhoto));
    }
}
