using Tauchplaetze.Core.Features.Translations;

namespace Tauchplaetze.Core.Tests.Features.Translations;

public class MarkdownStructureTests
{
    [Theory]
    [InlineData("## Steilwand\n\n- Felsen\n- **Hecht**", "## Tombant\n\n- Rochers\n- **Brochet**", true)]
    [InlineData("Zeile eins\nZeile zwei", "Une seule ligne", true)]
    [InlineData("1. Parkplatz\n2. Einstieg", "1) Parking\n2) Entrée", true)]
    [InlineData("", "  ", true)]
    [InlineData("## Steilwand", "### Tombant", false)]
    [InlineData("- Felsen\n- Hecht", "- Rochers, brochet", false)]
    [InlineData("**Achtung** Schiffe", "Attention aux bateaux", false)]
    [InlineData("Absatz\n\nZweiter Absatz", "Paragraphe", false)]
    [InlineData("Text", "", false)]
    public void Eine_Uebersetzung_behaelt_Zwischentitel_Listen_Absaetze_und_Fett_bei(string source, string translation, bool expected)
    {
        // Act
        var matches = MarkdownStructure.Matches(source, translation);

        // Assert
        Assert.Equal(expected, matches);
    }
}
