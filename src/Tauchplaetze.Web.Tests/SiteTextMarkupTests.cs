using Tauchplaetze.Web.Features.DiveSites;

namespace Tauchplaetze.Web.Tests;

public class SiteTextMarkupTests
{
    [Theory]
    [InlineData("Erster Absatz.\n\nZweiter\nmit Umbruch.", "<p>Erster Absatz.</p><p>Zweiter<br>mit Umbruch.</p>")]
    [InlineData("- Felsen\n* Hechte", "<ul><li>Felsen</li><li>Hechte</li></ul>")]
    [InlineData("1. Abtauchen\n2. Nach links", "<ol><li>Abtauchen</li><li>Nach links</li></ol>")]
    [InlineData("3. Wrack\n4. Auftauchen", "<ol start=\"3\"><li>Wrack</li><li>Auftauchen</li></ol>")]
    [InlineData("Route:\n- Felsen\n- Wrack\nDann retour.", "<p>Route:</p><ul><li>Felsen</li><li>Wrack</li></ul><p>Dann retour.</p>")]
    [InlineData("- Felsen\n1. Wrack", "<ul><li>Felsen</li></ul><ol><li>Wrack</li></ol>")]
    [InlineData("# Nordwand\n## Riff\n### Detail\n#### zu tief", "<h4>Nordwand</h4><h5>Riff</h5><h6>Detail</h6><p>#### zu tief</p>")]
    [InlineData("#Hashtag", "<p>#Hashtag</p>")]
    [InlineData("Bis **40 m** tief.", "<p>Bis <strong>40 m</strong> tief.</p>")]
    [InlineData("Ein *Hecht* und _Egli_.", "<p>Ein <em>Hecht</em> und <em>Egli</em>.</p>")]
    [InlineData("2 * 3 * 4 und file_name_x", "<p>2 * 3 * 4 und file_name_x</p>")]
    [InlineData("## <script>x</script>\n- <b onclick=\"x\">Text</b> & mehr", "<h5>&lt;script&gt;x&lt;/script&gt;</h5><ul><li>&lt;b onclick=&quot;x&quot;&gt;Text&lt;/b&gt; &amp; mehr</li></ul>")]
    [InlineData("\r\n\r\n  \r\n", "")]
    public void Ein_Text_wird_zu_Titeln_Absaetzen_Listen_Fett_und_Kursiv_und_alles_andere_bleibt_Text(string text, string expected)
    {
        // Act
        var html = SiteTextMarkup.ToHtml(text);

        // Assert
        Assert.Equal(expected, html);
    }
}
