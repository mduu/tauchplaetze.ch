using System.Net;
using Tauchplaetze.Web.CrossCutting.Hosting;

namespace Tauchplaetze.Web.Tests;

public class HostRedirectTests
{
    private static readonly HostRedirects Redirects = new(new HostRedirectOptions
    {
        Aliases = ["www.tauchplaetze.ch"],
        ShortcutHosts = ["sichtweiten.ch", "www.sichtweiten.ch"],
        ShortcutDefaultPath = "/de",
        Shortcuts = new() { ["zuerichsee"] = "zuerichsee", ["zuerisee"] = "zuerichsee" },
    }, "https://tauchplaetze.ch");

    [Theory]
    [InlineData("www.tauchplaetze.ch", "/de/tauchplaetze/riff-williams", "?tag=2", "https://tauchplaetze.ch/de/tauchplaetze/riff-williams?tag=2")]
    [InlineData("WWW.Tauchplaetze.CH", "/", "", "https://tauchplaetze.ch/")]
    [InlineData("sichtweiten.ch", "/", "", "https://tauchplaetze.ch/de")]
    [InlineData("www.sichtweiten.ch", "/ZuerichSee", "?x=1", "https://tauchplaetze.ch/de/gewaesser/zuerichsee")]
    [InlineData("sichtweiten.ch", "/zuerisee/", "", "https://tauchplaetze.ch/de/gewaesser/zuerichsee")]
    [InlineData("sichtweiten.ch", "/unbekannt", "", "https://tauchplaetze.ch/de")]
    [InlineData("sichtweiten.ch", "//evil.example/x", "", "https://tauchplaetze.ch/de")]
    public void Alias_und_Kurz_Domains_leiten_immer_auf_den_kanonischen_Host(string host, string path, string query, string expected)
    {
        // Act
        var target = Redirects.Resolve(host, path, query);

        // Assert
        Assert.Equal(expected, target);
    }

    [Theory]
    [InlineData("tauchplaetze.ch")]
    [InlineData("app-tauchplaetze-prod.azurewebsites.net")]
    [InlineData("localhost")]
    [InlineData("evil.example")]
    public void Andere_Hosts_werden_nicht_umgeleitet(string host)
    {
        // Act
        var target = Redirects.Resolve(host, "/de", "");

        // Assert
        Assert.Null(target);
    }

    [Fact]
    public async Task Die_App_leitet_www_mit_301_und_Formulare_mit_308_um()
    {
        // Arrange
        await using var app = new WebApp();
        using var client = app.Browser();
        var ct = TestContext.Current.CancellationToken;

        // Act
        using var get = new HttpRequestMessage(HttpMethod.Get, "/de/gewaesser?x=1");
        get.Headers.Host = "www.tauchplaetze.ch";
        using var getResponse = await client.SendAsync(get, ct);
        using var post = new HttpRequestMessage(HttpMethod.Post, "/de/konto/anmelden");
        post.Headers.Host = "www.tauchplaetze.ch";
        using var postResponse = await client.SendAsync(post, ct);
        using var direct = await client.GetAsync("/alive", ct);

        // Assert
        Assert.Equal(HttpStatusCode.MovedPermanently, getResponse.StatusCode);
        Assert.Equal("https://tauchplaetze.ch/de/gewaesser?x=1", getResponse.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.PermanentRedirect, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
    }
}
