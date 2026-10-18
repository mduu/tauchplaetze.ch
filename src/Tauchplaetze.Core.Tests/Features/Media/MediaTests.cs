using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Media.Commands;
using Tauchplaetze.Core.Features.Media.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Media;

public class MediaTests(SqlServer sql) : SqlTest(sql)
{
    private const string Pending = "/admin/medien/{id}/{file}";

    [SqlTheory]
    [InlineData(false, "Anna Muster", "image/jpeg", true, EditorialError.RightsNotConfirmed)]
    [InlineData(true, " ", "image/jpeg", true, EditorialError.InvalidPhotographer)]
    [InlineData(true, "Anna Muster", "image/heic", true, EditorialError.InvalidImage)]
    [InlineData(true, "Anna Muster", "image/jpeg", false, EditorialError.InvalidImage)]
    public async Task Ein_Upload_braucht_die_Bestaetigung_der_Rechte_einen_Fotografen_und_ein_lesbares_JPEG_PNG_oder_WebP(
        bool rightsConfirmed, string photographer, string contentType, bool isImage, EditorialError expected)
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var content = isImage ? TestImages.Jpeg() : "kein Bild"u8.ToArray();

        // Act
        var result = await Command<UploadMediaCommand, UploadMediaResult>(new UploadMediaCommand(
            anna, site, content, contentType, photographer, null, MediaSection.Underwater, null, rightsConfirmed, []));

        // Assert
        Assert.Equal([expected], result.Errors);
        Assert.Empty(Sql.Media.Pending);
    }

    [SqlFact]
    public async Task Ein_gesperrtes_Konto_laedt_kein_Foto_hoch()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(moderator, anna, true));

        // Act
        var result = await Upload(anna, site);

        // Assert
        Assert.Equal([EditorialError.Blocked], result.Errors);
    }

    [SqlFact]
    public async Task Ein_Foto_ist_erst_oeffentlich_wenn_eine_freigegebene_Version_es_verwendet_und_eine_Wiederherstellung_kopiert_es_nicht_doppelt()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var photo = (await Upload(anna, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(photo, MediaSection.Underwater)], HeroMediaId = photo }, null));
        var before = await PublicSite();
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));

        // Act
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id));
        var publishedFiles = Sql.Media.Public.Count;
        await Command<RestoreRevisionCommand, EditorialResult>(new RestoreRevisionCommand(moderator, site, pending.Revision.Number));
        var after = await PublicSite();

        // Assert
        Assert.Empty(before.PhotoList);
        var published = Assert.Single(after.PhotoList);
        Assert.Equal((photo, "Anna Muster", "Felswand mit Hecht"), (published.Id, published.Photographer, published.AltText));
        Assert.Equal(photo, after.Hero?.Id);
        Assert.Equal([480, 960, 1200], published.Renditions.Select(r => r.Width));
        Assert.Equal(3, publishedFiles);
        Assert.Equal(publishedFiles, Sql.Media.Public.Count);
        Assert.DoesNotContain(Sql.Media.Public.Keys, key => key.Contains("original", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Ein_Foto_eines_anderen_Platzes_laesst_sich_nicht_verwenden()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var lake = (await Query<Tauchplaetze.Core.Features.WaterBodies.Queries.GetWaterBodiesQuery, IReadOnlyList<Tauchplaetze.Core.Features.WaterBodies.WaterBodySummary>>(
            new Tauchplaetze.Core.Features.WaterBodies.Queries.GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zuerichsee").Id;
        var other = (await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(moderator, "Ermitage", lake, 47.31, 8.58, "Küsnacht", "ZH", null))).DiveSiteId!.Value;
        var foreign = (await Upload(anna, other)).MediaId!.Value;
        var edit = await ForEditing(site);

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(foreign, MediaSection.Underwater)] }, null));

        // Assert
        Assert.Equal([EditorialError.InvalidMedia], result.Errors);
    }

    [SqlFact]
    public async Task Die_Bildgroessen_haben_keine_Metadaten_mehr_das_Aufnahmedatum_kommt_aus_EXIF()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var photo = TestImages.Jpeg(takenAt: new DateTime(2026, 7, 14, 10, 30, 0));
        Assert.NotEmpty(ImageMetadataReader.ReadMetadata(new MemoryStream(photo)).OfType<GpsDirectory>());

        // Act
        var result = await Command<UploadMediaCommand, UploadMediaResult>(new UploadMediaCommand(
            anna, site, photo, "image/jpeg", "Anna Muster", null, MediaSection.Surroundings, null, true, []));

        // Assert
        var item = Assert.Single(await Query<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>(new GetSiteMediaQuery(anna, site, Pending)));
        Assert.Equal((new DateOnly(2026, 7, 14), MediaSection.Surroundings, false), (item.TakenAt, item.Section, item.IsPublic));
        var renditions = Sql.Media.Pending.Where(f => f.Key.StartsWith($"{result.MediaId}/", StringComparison.Ordinal) && f.Key.EndsWith(".webp", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(renditions);
        Assert.All(renditions, r => Assert.DoesNotContain(ImageMetadataReader.ReadMetadata(new MemoryStream(r.Value)), d => d is GpsDirectory or ExifSubIfdDirectory));
    }

    [SqlFact]
    public async Task Ein_privates_Foto_sehen_nur_wer_es_hochgeladen_hat_und_die_Moderation()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        var photo = (await Upload(anna, site)).MediaId!.Value;

        // Act
        var forAnna = await Query<GetPendingMediaFileQuery, MediaFile?>(new GetPendingMediaFileQuery(anna, photo, "480.webp"));
        var forModerator = await Query<GetPendingMediaFileQuery, MediaFile?>(new GetPendingMediaFileQuery(moderator, photo, "480.webp"));
        var forBert = await Query<GetPendingMediaFileQuery, MediaFile?>(new GetPendingMediaFileQuery(bert, photo, "480.webp"));
        var original = await Query<GetPendingMediaFileQuery, MediaFile?>(new GetPendingMediaFileQuery(moderator, photo, "original.jpg"));

        // Assert
        Assert.Equal("image/webp", forAnna?.ContentType);
        Assert.NotNull(forModerator);
        Assert.Null(forBert);
        Assert.Null(original);
        Assert.Empty(await Query<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>(new GetSiteMediaQuery(bert, site, Pending)));
    }

    [SqlFact]
    public async Task Wird_ein_Konto_geloescht_verschwinden_seine_privaten_Fotos_die_oeffentlichen_bleiben_mit_dem_Fotografen()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var published = (await Upload(anna, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(published, MediaSection.Underwater)] }, null));
        var unpublished = (await Upload(anna, site)).MediaId!.Value;
        var before = await Query<GetPersonalDataQuery, PersonalData?>(new GetPersonalDataQuery(anna));

        // Act
        await Command<DeleteAccountCommand, AccountResult>(new DeleteAccountCommand(anna));

        // Assert
        Assert.Equal(2, before!.PhotoList.Count);
        Assert.DoesNotContain(Sql.Media.Pending.Keys, key => key.StartsWith($"{unpublished}/", StringComparison.Ordinal));
        Assert.Equal("Anna Muster", Assert.Single((await PublicSite()).PhotoList).Photographer);
        Assert.Equal([published], (await Query<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>(new GetSiteMediaQuery(moderator, site, Pending))).Select(m => m.Id));
    }

    private Task<UploadMediaResult> Upload(string uploader, Guid site) =>
        Command<UploadMediaCommand, UploadMediaResult>(new UploadMediaCommand(
            uploader, site, TestImages.Jpeg(), "image/jpeg", "Anna Muster", "https://tauchfotos.ch", MediaSection.Underwater, 18, true,
            [new MediaText("de", "Hecht an der Wand", "Felswand mit Hecht")]));

    private async Task<DiveSiteDetails> PublicSite() =>
        Assert.IsType<DiveSiteLookup.Found>(await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "fr"))).Site;

    private async Task<DiveSiteForEditing> ForEditing(Guid site) =>
        (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

    private async Task<(string Moderator, string Anna, Guid Site)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var moderator = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var site = await AddRiffWilliams(moderator);
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        return (moderator, anna, site);
    }
}
