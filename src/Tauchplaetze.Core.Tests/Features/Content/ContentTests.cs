using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Content;
using Tauchplaetze.Core.Features.Content.Commands;
using Tauchplaetze.Core.Features.Content.Queries;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Media.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Features.Media;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Content;

public class ContentTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Der_Export_enthaelt_nur_oeffentliche_Inhalte_ohne_Konten_private_Fotos_und_offene_Vorschlaege()
    {
        // Arrange
        var (admin, anna, site, photo) = await Prepare();
        var privatePhoto = (await Upload(anna, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 99 }, "Vorschlag von Anna"));

        // Act
        var (result, package) = await Export(admin);

        // Assert
        Assert.Equal((null, 1, 1), (result.Error, result.DiveSites, result.Photos));
        Assert.Contains($"photos/{photo}/480.webp", package.Keys);
        Assert.DoesNotContain(package.Keys, k => k.Contains(privatePhoto.ToString(), StringComparison.Ordinal));
        var text = string.Concat(package.Where(p => p.Key.EndsWith(".json", StringComparison.Ordinal)).Select(p => System.Text.Encoding.UTF8.GetString(p.Value)));
        Assert.DoesNotContain("@example.org", text);
        Assert.DoesNotContain(anna, text);
        Assert.DoesNotContain(admin, text);
        Assert.DoesNotContain("Vorschlag von Anna", text);
        Assert.DoesNotContain("\"maxDepth\":99", text);
    }

    [SqlFact]
    public async Task Ein_Paket_aus_Prod_ergibt_in_einer_leeren_Datenbank_dieselben_oeffentlichen_Inhalte_mit_Fotos()
    {
        // Arrange
        var (admin, _, site, photo) = await Prepare();
        var before = await ForEditing(site);
        var (_, bytes) = await ExportBytes(admin);
        await Sql.Reset();
        var localAdmin = await Admin();

        // Act
        var result = await Import(localAdmin, bytes);

        // Assert
        Assert.Equal((null, 1, 1), (result.Error, result.SitesAdded, result.PhotosAdded));
        Assert.True(result.WaterBodiesAdded >= 3);
        var after = await ForEditing(site);
        Assert.Equal(Normalized(before.Content), Normalized(after.Content));
        var details = Assert.IsType<DiveSiteLookup.Found>(await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "de"))).Site;
        Assert.Equal("Treppe beim Bootshaus", Assert.Single(details.EntryPoints).Name);
        Assert.Contains($"{photo}/480.webp", Sql.Media.Public.Keys);
        Assert.Empty(Sql.Media.Pending);
    }

    [SqlFact]
    public async Task Ein_zweiter_Import_aendert_nichts_und_der_Import_loescht_nie()
    {
        // Arrange
        var (admin, _, _, _) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        var lakes = await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var localOnly = await AddLocalSite(admin);

        // Act
        var first = await Import(admin, bytes);
        var second = await Import(admin, bytes);

        // Assert
        Assert.Equal((0, 0, 1), (first.SitesAdded, first.SitesUpdated, first.SitesUnchanged));
        Assert.Equal(new ImportContentResult(null, 0, 0, 0, 1, 0, 0, 0), second);
        Assert.NotNull(await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(localOnly)));
        Assert.Equal(0, lakes.Added);
    }

    [SqlFact]
    public async Task Weicht_der_Inhalt_lokal_ab_haengt_der_Import_eine_freigegebene_Version_mit_dem_Inhalt_von_Prod_an()
    {
        // Arrange
        var (admin, _, site, _) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(admin, site, edit.BaseNumber, edit.Content with { MaxDepth = 12 }, null));
        var versionsBefore = (await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site, admin))).Count;

        // Act
        var result = await Import(admin, bytes);

        // Assert
        Assert.Equal((1, 0), (result.SitesUpdated, result.SitesAdded));
        Assert.Equal(edit.Content.MaxDepth, (await ForEditing(site)).Content.MaxDepth);
        var versions = await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site, admin));
        Assert.Equal(versionsBefore + 1, versions.Count);
        Assert.StartsWith("Import aus Prod vom ", versions.MaxBy(v => v.Number)!.Comment);
    }

    [SqlFact]
    public async Task Nur_Admins_importieren_und_nur_wenn_der_Import_eingeschaltet_ist()
    {
        // Arrange
        var (admin, anna, _, _) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);

        // Act
        var byContributor = await Import(anna, bytes);
        var disabled = await WithOptions(o => o.ImportEnabled = false, () => Import(admin, bytes));
        var pull = await WithOptions(o => o.SourceBaseUri = null, () => Command<PullContentFromSourceCommand, ImportContentResult>(new PullContentFromSourceCommand(admin)));
        var overHttp = await WithOptions(
            o => (o.SourceBaseUri, o.ExportKey) = (new Uri("http://tauchplaetze.test/"), "geheim-und-lang"),
            () => Command<PullContentFromSourceCommand, ImportContentResult>(new PullContentFromSourceCommand(admin)));

        // Assert
        Assert.Equal(ContentError.NotAllowed, byContributor.Error);
        Assert.Equal(ContentError.Disabled, disabled.Error);
        Assert.Equal(ContentError.SourceNotConfigured, pull.Error);
        Assert.Equal(ContentError.SourceNotConfigured, overHttp.Error);
    }

    [SqlFact]
    public async Task Ohne_Anmeldung_exportiert_nur_der_richtige_Schluessel_und_ohne_Schluessel_ist_der_Abruf_aus()
    {
        // Arrange
        var (_, anna, _, _) = await Prepare();

        // Act
        var contributor = await Query<ExportContentQuery, ExportContentResult>(new ExportContentQuery(anna, null, _ => Stream.Null));
        var withoutConfiguredKey = await Query<ExportContentQuery, ExportContentResult>(new ExportContentQuery(null, "irgendwas", _ => Stream.Null));
        var (wrong, right) = await WithOptions(o => o.ExportKey = "geheim-und-lang", async () => (
            await Query<ExportContentQuery, ExportContentResult>(new ExportContentQuery(null, "falsch", _ => Stream.Null)),
            await Query<ExportContentQuery, ExportContentResult>(new ExportContentQuery(null, "geheim-und-lang", _ => new MemoryStream()))));

        // Assert
        Assert.Equal(ContentError.NotAllowed, contributor.Error);
        Assert.Equal(ContentError.Disabled, withoutConfiguredKey.Error);
        Assert.Equal(ContentError.InvalidKey, wrong.Error);
        Assert.Null(right.Error);
    }

    [SqlFact]
    public async Task Ein_kaputtes_Paket_ergibt_einen_Fehler_und_aendert_nichts()
    {
        // Arrange
        var (admin, _, _, _) = await Prepare();

        // Act
        var result = await Import(admin, "kein ZIP"u8.ToArray());

        // Assert
        Assert.Equal(ContentError.InvalidPackage, result.Error);
    }

    [SqlFact]
    public async Task Der_Export_schreibt_in_einen_Stream_ohne_Positionierung_und_ohne_synchrones_Schreiben()
    {
        // Arrange
        var (admin, _, _, photo) = await Prepare();
        var body = new ResponseBody();

        // Act
        var result = await Query<ExportContentQuery, ExportContentResult>(new ExportContentQuery(admin, null, _ => body));

        // Assert
        Assert.Null(result.Error);
        using var zip = new ZipArchive(new MemoryStream(body.Written.ToArray()), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry($"photos/{photo}/480.webp"));
    }

    [SqlFact]
    public async Task Archivstatus_und_alte_Slugs_aus_Prod_uebernimmt_der_Import()
    {
        // Arrange
        var (admin, _, site, _) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(admin, site, edit.BaseNumber, edit.Content with { Slug = "riff-williams" }, null));
        await Sql.Arrange(async db => await db.DiveSites.Where(d => d.Id == site).ExecuteUpdateAsync(d => d.SetProperty(x => x.Status, DiveSiteStatus.Archived)));
        var (_, bytes) = await ExportBytes(admin);
        await Sql.Reset();

        // Act
        var result = await Import(await Admin(), bytes);

        // Assert
        Assert.Equal(1, result.SitesAdded);
        var status = DiveSiteStatus.Draft;
        await Sql.Arrange(async db => status = (await db.DiveSites.SingleAsync(d => d.Id == site)).Status);
        Assert.Equal(DiveSiteStatus.Archived, status);
        Assert.Contains("riff-williams-herrliberg", await Redirects(site));
    }

    [SqlFact]
    public async Task Einen_neuen_Platz_dessen_Slug_lokal_ein_anderer_Platz_oder_Redirect_belegt_ueberspringt_der_Import()
    {
        // Arrange
        var (admin, _, _, _) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        await Sql.Reset();
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var localAdmin = await Admin();
        var local = await AddLocalSite(localAdmin);
        await SetSlug(local, "riff-williams-herrliberg");

        // Act
        var bySite = await Import(localAdmin, bytes);
        await SetSlug(local, "nur-lokal");
        await Sql.Arrange(db =>
        {
            db.SlugRedirects.Add(new SlugRedirect { OldSlug = "riff-williams-herrliberg", DiveSiteId = local });
            return db.SaveChangesAsync();
        });
        var byRedirect = await Import(localAdmin, bytes);

        // Assert
        Assert.Equal((0, 1), (bySite.SitesAdded, bySite.SitesSkipped));
        Assert.Equal((0, 1), (byRedirect.SitesAdded, byRedirect.SitesSkipped));
    }

    [SqlFact]
    public async Task Belegt_lokal_ein_anderer_Platz_den_Slug_eines_vorhandenen_Platzes_bleibt_dieser_unveraendert()
    {
        // Arrange
        var (admin, _, site, _) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(admin, site, edit.BaseNumber, edit.Content with { Slug = "riff-williams", MaxDepth = 12 }, null));
        await Sql.Arrange(db => db.SlugRedirects.Where(r => r.DiveSiteId == site).ExecuteDeleteAsync());
        var other = await AddLocalSite(admin);
        await SetSlug(other, "riff-williams-herrliberg");

        // Act
        var result = await Import(admin, bytes);

        // Assert
        Assert.Equal((0, 1), (result.SitesUpdated, result.SitesSkipped));
        Assert.Equal(12, (await ForEditing(site)).Content.MaxDepth);
    }

    [SqlFact]
    public async Task Ein_neues_Foto_an_einem_unveraenderten_Platz_zaehlt_als_Aktualisierung()
    {
        // Arrange
        var (admin, _, _, photo) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        await Sql.Arrange(db => db.MediaItems.Where(m => m.Id == photo).ExecuteDeleteAsync());
        Sql.Media.Public.Clear();

        // Act
        var result = await Import(admin, bytes);

        // Assert
        Assert.Equal((1, 0, 1), (result.SitesUpdated, result.SitesUnchanged, result.PhotosAdded));
        Assert.Contains($"{photo}/480.webp", Sql.Media.Public.Keys);
    }

    [SqlFact]
    public async Task Fehlt_eine_Bildgroesse_legt_der_Import_das_Foto_nicht_an()
    {
        // Arrange
        var (admin, _, _, photo) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        var package = Rewrite(bytes, zip => zip.GetEntry($"photos/{photo}/480.webp")!.Delete());
        await Sql.Reset();

        // Act
        var result = await Import(await Admin(), package);

        // Assert
        Assert.Equal((1, 0, 1), (result.SitesAdded, result.PhotosAdded, result.PhotosSkipped));
        Assert.Empty(Sql.Media.Public);
    }

    [SqlFact]
    public async Task Ungueltige_oder_unlesbare_Plaetze_ueberspringt_der_Import_und_zaehlt_sie()
    {
        // Arrange
        var (admin, _, site, _) = await Prepare();
        var (_, bytes) = await ExportBytes(admin);
        var package = Rewrite(bytes, zip =>
        {
            var entry = zip.GetEntry($"dive-sites/{site}.json")!;
            JsonNode node;
            using (var stream = entry.Open())
            {
                node = JsonNode.Parse(stream)!;
            }

            entry.Delete();
            node["content"]!["slug"] = "neu";
            using (var writer = new StreamWriter(zip.CreateEntry($"dive-sites/{site}.json").Open()))
            {
                writer.Write(node.ToJsonString());
            }

            using var broken = new StreamWriter(zip.CreateEntry($"dive-sites/{Guid.NewGuid()}.json").Open());
            broken.Write("{ kaputt");
        });
        await Sql.Reset();

        // Act
        var result = await Import(await Admin(), package);

        // Assert
        Assert.Equal((null, 0, 2), (result.Error, result.SitesAdded, result.SitesSkipped));
    }

    private static byte[] Rewrite(byte[] package, Action<ZipArchive> change)
    {
        using var buffer = new MemoryStream();
        buffer.Write(package);
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Update, leaveOpen: true))
        {
            change(zip);
        }

        return buffer.ToArray();
    }

    private Task SetSlug(Guid site, string slug) =>
        Sql.Arrange(db => db.DiveSites.Where(d => d.Id == site).ExecuteUpdateAsync(d => d.SetProperty(x => x.Slug, slug)));

    private async Task<IReadOnlyList<string>> Redirects(Guid site)
    {
        IReadOnlyList<string> slugs = [];
        await Sql.Arrange(async db => slugs = await db.SlugRedirects.Where(r => r.DiveSiteId == site).Select(r => r.OldSlug).ToListAsync());
        return slugs;
    }

    private static string Normalized(DiveSiteContent content) =>
        System.Text.Encoding.UTF8.GetString(DiveSiteSnapshot.ToJson(content with { WaterBodyId = 0 }));

    private async Task<T> WithOptions<T>(Action<ContentOptions> change, Func<Task<T>> action)
    {
        var options = Sql.Services.GetRequiredService<IOptions<ContentOptions>>().Value;
        var (importEnabled, key, source) = (options.ImportEnabled, options.ExportKey, options.SourceBaseUri);
        change(options);
        try
        {
            return await action();
        }
        finally
        {
            (options.ImportEnabled, options.ExportKey, options.SourceBaseUri) = (importEnabled, key, source);
        }
    }

    private async Task<(ExportContentResult Result, byte[] Bytes)> ExportBytes(string admin)
    {
        using var output = new MemoryStream();
        var result = await Query<ExportContentQuery, ExportContentResult>(new ExportContentQuery(admin, null, _ => output));
        return (result, output.ToArray());
    }

    private async Task<(ExportContentResult Result, Dictionary<string, byte[]> Entries)> Export(string admin)
    {
        var (result, bytes) = await ExportBytes(admin);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entries = new Dictionary<string, byte[]>();
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            entries[entry.FullName] = buffer.ToArray();
        }

        return (result, entries);
    }

    private Task<ImportContentResult> Import(string admin, byte[] package) =>
        Command<ImportContentCommand, ImportContentResult>(new ImportContentCommand(admin, new MemoryStream(package)));

    private Task<UploadMediaResult> Upload(string uploader, Guid site) =>
        Command<UploadMediaCommand, UploadMediaResult>(new UploadMediaCommand(
            uploader, site, TestImages.Jpeg(), "image/jpeg", "Anna Muster", "https://tauchfotos.ch", MediaSection.Underwater, 18, true,
            [new MediaText("de", "Hecht an der Wand", "Felswand mit Hecht")]));

    private async Task<DiveSiteForEditing> ForEditing(Guid site) =>
        (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

    private async Task<string> Admin() =>
        (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];

    private async Task<Guid> AddLocalSite(string admin)
    {
        var waterBodies = await Query<Tauchplaetze.Core.Features.WaterBodies.Queries.GetWaterBodiesQuery, IReadOnlyList<Tauchplaetze.Core.Features.WaterBodies.WaterBodySummary>>(
            new Tauchplaetze.Core.Features.WaterBodies.Queries.GetWaterBodiesQuery("de"));
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(
            admin, "Nur lokal", waterBodies.Single(w => w.Slug == "zuerichsee").Id, 47.30, 8.58, "Küsnacht", "ZH", null));
        return result.DiveSiteId!.Value;
    }

    /// <summary>Riff Williams mit einem Einstieg und einem öffentlichen Foto, freigegeben vom Admin. Anna ist Mitwirkende.</summary>
    private async Task<(string Admin, string Anna, Guid Site, Guid Photo)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = await Admin();
        var site = await AddRiffWilliams(admin);
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        var photo = (await Upload(admin, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(admin, site, edit.BaseNumber, edit.Content with
        {
            MaxDepth = 30,
            EntryPoints = [new EntryPointContent(Guid.NewGuid(), 47.2851, 8.6081, EntryPointKind.EntryAndExit, [new EntryPointText("de", "Treppe beim Bootshaus", "Über die Treppe ins Wasser.")])],
            Media = [new MediaUse(photo, MediaSection.Underwater)],
            HeroMediaId = photo,
        }, null));
        return (admin, anna, site, photo);
    }
}

/// <summary>Wie der Body einer Antwort in Kestrel: ohne Positionierung, synchrones Schreiben verboten.</summary>
internal sealed class ResponseBody : Stream
{
    public MemoryStream Written { get; } = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() => throw new InvalidOperationException("Synchronous operations are disallowed.");

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous operations are disallowed.");

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => Written.WriteAsync(buffer, cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Written.WriteAsync(buffer, offset, count, cancellationToken);
}
