using System.Net;

namespace Tauchplaetze.Web.Tests;

public class HeadRequestTests
{
    [Theory]
    [InlineData("/de")]
    [InlineData("/fr/a-propos")]
    public async Task HEAD_antwortet_wie_GET_ohne_Body_statt_mit_405(string path)
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path), ct);
        var body = await response.Content.ReadAsByteArrayAsync(ct);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(body);
    }
}
