using System.Net;

namespace Tauchplaetze.Web.Tests;

public class IndexNowKeyTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Die_Schluesseldatei_liefert_den_konfigurierten_Schluessel_als_Text()
    {
        // Arrange
        await using var app = new WebApp();
        await using var configured = app.WithWebHostBuilder(builder => builder.UseSetting("IndexNow:Key", Key));
        using var client = configured.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var response = await client.GetAsync($"/{Key}.txt", ct);
        using var other = await client.GetAsync("/fedcba9876543210fedcba9876543210.txt", ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Key, await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Ohne_Schluessel_gibt_es_keine_Schluesseldatei()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.CreateClient();

        // Act
        using var response = await client.GetAsync($"/{Key}.txt", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
