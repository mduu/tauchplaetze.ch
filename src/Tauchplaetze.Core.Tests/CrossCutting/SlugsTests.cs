using Tauchplaetze.Core.CrossCutting.Text;

namespace Tauchplaetze.Core.Tests.CrossCutting;

public class SlugsTests
{
    [Theory]
    [InlineData("Riff Williams Herrliberg", "riff-williams-herrliberg")]
    [InlineData("Zürichsee", "zuerichsee")]
    [InlineData("Lac de Neuchâtel", "lac-de-neuchatel")]
    [InlineData("  Lützelau (Insel)!  ", "luetzelau-insel")]
    [InlineData("Ölhafen – Süd", "oelhafen-sued")]
    public void Der_Slug_kommt_aus_dem_deutschen_Namen_ohne_Umlaute_und_Sonderzeichen(string name, string expected)
    {
        // Act
        var slug = Slugs.From(name);

        // Assert
        Assert.Equal(expected, slug);
    }
}
