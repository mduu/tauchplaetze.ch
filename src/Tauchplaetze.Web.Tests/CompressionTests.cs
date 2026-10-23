using System.IO.Compression;

namespace Tauchplaetze.Web.Tests;

public class CompressionTests(WebApp app) : IClassFixture<WebApp>
{
    [Fact]
    public async Task Eine_Seite_kommt_ueber_HTTPS_mit_Brotli_komprimiert()
    {
        // Arrange
        using var browser = app.Browser();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/de/gewaesser");
        request.Headers.AcceptEncoding.ParseAdd("br, gzip");

        // Act
        using var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);
        await using var body = new BrotliStream(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), CompressionMode.Decompress);
        var html = await new StreamReader(body).ReadToEndAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["br"], response.Content.Headers.ContentEncoding);
        Assert.Contains("</html>", html);
    }
}
