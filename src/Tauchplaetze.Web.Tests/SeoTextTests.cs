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

    [Theory]
    [InlineData("Riff Williams – Tauchplatz Zürichsee", "Riff Williams – Tauchplatz Zürichsee · tauchplaetze.ch")]
    [InlineData("Unterwasserpark beim alten Bootshaus – Tauchplatz Thunersee", "Unterwasserpark beim alten Bootshaus – Tauchplatz Thunersee")]
    public void Ein_Titel_traegt_die_Marke_nur_solange_er_in_65_Zeichen_passt(string title, string expected)
    {
        // Act
        var text = SeoText.PageTitle(title, "tauchplaetze.ch");

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Ein_zu_langer_Titel_wird_auf_65_Zeichen_gekuerzt()
    {
        // Arrange
        var title = new string('a', 80);

        // Act
        var text = SeoText.PageTitle(title, "tauchplaetze.ch");

        // Assert
        Assert.Equal(SeoText.MaxTitle, text.Length);
        Assert.EndsWith("…", text);
    }
}
