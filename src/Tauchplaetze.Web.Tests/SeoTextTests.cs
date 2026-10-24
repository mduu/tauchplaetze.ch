using Tauchplaetze.Web.CrossCutting.Seo;

namespace Tauchplaetze.Web.Tests;

public class SeoTextTests
{
    [Fact]
    public void Eine_Beschreibung_haengt_Saetze_nur_an_solange_sie_in_160_Zeichen_passt()
    {
        // Arrange
        var first = new string('a', 100) + ".";
        var second = new string('b', 50) + ".";
        var third = new string('c', 30) + ".";

        // Act
        var text = SeoText.Join(first, null, second, third);

        // Assert
        Assert.Equal($"{first} {second}", text);
        Assert.True(text.Length <= SeoText.MaxDescription);
    }

    [Fact]
    public void Der_erste_Satz_kommt_immer_auch_wenn_er_zu_lang_ist()
    {
        // Arrange
        var first = new string('a', 200);

        // Act
        var text = SeoText.Join(first, "Zweiter Satz.");

        // Assert
        Assert.Equal(first, text);
    }
}
