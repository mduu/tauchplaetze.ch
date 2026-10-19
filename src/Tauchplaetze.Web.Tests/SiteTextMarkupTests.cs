using Tauchplaetze.Web.Features.DiveSites;

namespace Tauchplaetze.Web.Tests;

public class SiteTextMarkupTests
{
    [Theory]
    [InlineData("Erster Absatz.\n\nZweiter\nmit Umbruch.", true, "<p>Erster Absatz.</p><p>Zweiter<br>mit Umbruch.</p>")]
    [InlineData("- Felsen\n* Hechte", true, "<ul><li>Felsen</li><li>Hechte</li></ul>")]
    [InlineData("Bis **40 m** tief.", true, "<p>Bis <strong>40 m</strong> tief.</p>")]
    [InlineData("Bis **40 m** tief.", false, "<p>Bis **40 m** tief.</p>")]
    [InlineData("- kein Markdown", false, "<p>- kein Markdown</p>")]
    [InlineData("<b onclick=\"x\">Text</b> & mehr", true, "<p>&lt;b onclick=&quot;x&quot;&gt;Text&lt;/b&gt; &amp; mehr</p>")]
    [InlineData("\r\n\r\n  \r\n", true, "")]
    public void Ein_Text_wird_zu_Absaetzen_Listen_und_Fett_und_alles_andere_bleibt_Text(string text, bool markdown, string expected)
    {
        // Act
        var html = SiteTextMarkup.ToHtml(text, markdown);

        // Assert
        Assert.Equal(expected, html);
    }
}
