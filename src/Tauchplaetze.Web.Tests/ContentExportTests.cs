using System.Net;
using Tauchplaetze.Core.Features.Content;

namespace Tauchplaetze.Web.Tests;

public class ContentExportTests(WebApp app) : IClassFixture<WebApp>
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("falsch", HttpStatusCode.Unauthorized)]
    [InlineData("richtig", HttpStatusCode.OK)]
    public async Task Der_Endpoint_liefert_das_Paket_nur_mit_dem_richtigen_Schluessel(string? key, HttpStatusCode expected)
    {
        // Arrange
        using var browser = app.Browser();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform/content-export");
        if (key is not null)
        {
            request.Headers.Add("X-Content-Export-Key", key);
        }

        // Act
        using var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("tauchplaetze-inhalt-2026-10-19-1430.zip", response.Content.Headers.ContentDisposition?.FileNameStar);
        }
    }

    [Fact]
    public async Task Ohne_Schluessel_in_der_Konfiguration_gibt_es_den_Endpoint_nicht()
    {
        // Arrange
        using var browser = app.Browser();
        var answer = app.ContentExport.Answer;
        app.ContentExport.Answer = _ => new ExportContentResult(ContentError.Disabled);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform/content-export");
        request.Headers.Add("X-Content-Export-Key", "richtig");

        // Act
        using var response = await browser.SendAsync(request, TestContext.Current.CancellationToken);
        app.ContentExport.Answer = answer;

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Den_Download_ueber_die_Seite_gibt_es_nur_mit_Anmeldung()
    {
        // Arrange
        using var browser = app.Browser();

        // Act
        using var response = await browser.GetAsync("/platform/inhalt/export", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual("application/zip", response.Content.Headers.ContentType?.MediaType);
    }
}
