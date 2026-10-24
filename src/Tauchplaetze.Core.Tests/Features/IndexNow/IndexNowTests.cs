using System.Net;
using System.Text.Json;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.IndexNow.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.IndexNow;

public class IndexNowTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Eine_Freigabe_meldet_Platz_und_Gewaesser_in_allen_vier_Sprachen_mit_Schluessel()
    {
        // Arrange
        Sql.Sources.Statuses["indexnow"] = HttpStatusCode.OK;
        var moderator = await Moderator();

        // Act
        await AddRiffWilliams(moderator);
        var result = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());

        // Assert
        var post = Assert.Single(Sql.Sources.Posts, p => p.Url.Contains("indexnow", StringComparison.Ordinal));
        using var json = JsonDocument.Parse(post.Body);
        var urls = json.RootElement.GetProperty("urlList").EnumerateArray().Select(u => u.GetString()).ToList();
        Assert.Equal(
        [
            "https://tauchplaetze.test/de/tauchplaetze/riff-williams-herrliberg",
            "https://tauchplaetze.test/en/dive-sites/riff-williams-herrliberg",
            "https://tauchplaetze.test/fr/sites-de-plongee/riff-williams-herrliberg",
            "https://tauchplaetze.test/it/siti-di-immersione/riff-williams-herrliberg",
            "https://tauchplaetze.test/de/gewaesser/zuerichsee",
            "https://tauchplaetze.test/en/waters/zuerichsee",
            "https://tauchplaetze.test/fr/eaux/zuerichsee",
            "https://tauchplaetze.test/it/acque/zuerichsee",
        ], urls);
        Assert.Equal("tauchplaetze.test", json.RootElement.GetProperty("host").GetString());
        Assert.Equal(SqlServer.IndexNowKey, json.RootElement.GetProperty("key").GetString());
        Assert.Equal($"https://tauchplaetze.test/{SqlServer.IndexNowKey}.txt", json.RootElement.GetProperty("keyLocation").GetString());
        Assert.Equal((8, HttpStatusCode.OK), (result.Urls, result.Status));
    }

    [SqlFact]
    public async Task Mehrere_Aenderungen_am_selben_Platz_ergeben_eine_Meldung_ohne_doppelte_Adressen()
    {
        // Arrange
        Sql.Sources.Statuses["indexnow"] = HttpStatusCode.Accepted;
        var moderator = await Moderator();
        var site = await AddRiffWilliams(moderator);

        // Act
        await Command<ArchiveDiveSiteCommand, EditorialResult>(new ArchiveDiveSiteCommand(moderator, site, true));
        await Command<ArchiveDiveSiteCommand, EditorialResult>(new ArchiveDiveSiteCommand(moderator, site, false));
        var first = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());
        var second = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());

        // Assert
        Assert.Equal(8, first.Urls);
        Assert.Equal((0, (HttpStatusCode?)null), (second.Urls, second.Status));
        Assert.Single(Sql.Sources.Posts, p => p.Url.Contains("indexnow", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Antwortet_IndexNow_mit_429_geht_die_Meldung_im_naechsten_Intervall_noch_einmal_hinaus()
    {
        // Arrange
        var moderator = await Moderator();
        await AddRiffWilliams(moderator);
        Sql.Sources.Statuses["indexnow"] = HttpStatusCode.TooManyRequests;

        // Act
        var failed = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());
        Sql.Sources.Statuses["indexnow"] = HttpStatusCode.OK;
        var retried = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());

        // Assert
        Assert.Equal((0, HttpStatusCode.TooManyRequests), (failed.Urls, failed.Status));
        Assert.Equal((8, HttpStatusCode.OK), (retried.Urls, retried.Status));
    }

    [SqlFact]
    public async Task Eine_abgelehnte_Meldung_wird_nicht_wiederholt_und_die_Freigabe_bleibt_gueltig()
    {
        // Arrange
        Sql.Sources.Statuses["indexnow"] = HttpStatusCode.Forbidden;
        var moderator = await Moderator();

        // Act
        var site = await AddRiffWilliams(moderator);
        var rejected = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());
        var next = await Command<SubmitIndexNowCommand, SubmitIndexNowResult>(new SubmitIndexNowCommand());

        // Assert
        Assert.NotEqual(Guid.Empty, site);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.Status);
        Assert.Null(next.Status);
    }

    private async Task<string> Moderator()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        return (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
    }
}
