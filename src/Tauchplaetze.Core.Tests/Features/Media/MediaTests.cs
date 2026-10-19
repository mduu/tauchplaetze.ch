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
    [InlineData(false, "Anna Muster", null, "image/jpeg", "jpeg", EditorialError.RightsNotConfirmed)]
    [InlineData(true, " ", null, "image/jpeg", "jpeg", EditorialError.InvalidPhotographer)]
    [InlineData(true, "Anna Muster", "javascript:alert(1)", "image/jpeg", "jpeg", EditorialError.InvalidPhotographerUrl)]
    [InlineData(true, "Anna Muster", "ftp://tauchfotos.ch/a", "image/jpeg", "jpeg", EditorialError.InvalidPhotographerUrl)]
    [InlineData(true, "Anna Muster", "tauchfotos.ch", "image/jpeg", "jpeg", EditorialError.InvalidPhotographerUrl)]
    [InlineData(true, "Anna Muster", null, "image/heic", "jpeg", EditorialError.InvalidImage)]
    [InlineData(true, "Anna Muster", null, "image/jpeg", "text", EditorialError.InvalidImage)]
    [InlineData(true, "Anna Muster", null, "image/jpeg", "gif", EditorialError.InvalidImage)]
    [InlineData(true, "Anna Muster", null, "image/png", "bomb", EditorialError.InvalidImage)]
    public async Task Ein_Upload_braucht_Rechte_Fotograf_einen_Web_Link_und_ein_lesbares_JPEG_PNG_oder_WebP_von_vernuenftiger_Groesse(
        bool rightsConfirmed, string photographer, string? url, string contentType, string kind, EditorialError expected)
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var content = kind switch
        {
            "jpeg" => TestImages.Jpeg(),
            "gif" => TestImages.Gif(),
            "bomb" => TestImages.PixelBomb(),
            _ => "kein Bild"u8.ToArray(),
        };

        // Act
        var result = await Command<UploadMediaCommand, UploadMediaResult>(new UploadMediaCommand(
            anna, site, content, contentType, photographer, url, MediaSection.Underwater, null, rightsConfirmed, []));

        // Assert
        Assert.Equal([expected], result.Errors);
        Assert.Empty(Sql.Media.Pending);
    }

    [SqlFact]
    public async Task Mitwirkende_laden_hoechstens_100_Fotos_pro_Tag_hoch()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        await Sql.Arrange(db =>
        {
            for (var i = 0; i < 100; i++)
            {
                db.MediaItems.Add(new Tauchplaetze.Core.Data.Entities.MediaItem
                {
                    Id = Guid.NewGuid(), DiveSiteId = site, BlobPath = "x", Photographer = "Anna", Width = 480, Height = 320,
                    UploadedBy = anna, UploadedAt = FixedTime.SpikeDay.AddHours(-5), RightsConfirmed = true,
                });
            }

            return Task.CompletedTask;
        });

        // Act
        var result = await Upload(anna, site);

        // Assert
        Assert.Equal([EditorialError.TooManyUploads], result.Errors);
    }

    [SqlFact]
    public async Task Vor_dem_Lesen_der_Dateien_sagt_der_Core_wie_viele_Fotos_noch_gehen_die_Moderation_hat_kein_Limit_ein_gesperrtes_Konto_keins()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        await Sql.Arrange(db =>
        {
            for (var i = 0; i < 25; i++)
            {
                db.MediaItems.Add(new Tauchplaetze.Core.Data.Entities.MediaItem
                {
                    Id = Guid.NewGuid(), DiveSiteId = site, BlobPath = "x", Photographer = "Anna", Width = 480, Height = 320,
                    UploadedBy = anna, UploadedAt = FixedTime.SpikeDay.AddMinutes(-30), RightsConfirmed = true,
                });
            }

            return Task.CompletedTask;
        });
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(moderator, bert, true));

        // Act
        var contributor = await Query<GetUploadAllowanceQuery, UploadAllowance>(new GetUploadAllowanceQuery(anna));
        var moderation = await Query<GetUploadAllowanceQuery, UploadAllowance>(new GetUploadAllowanceQuery(moderator));
        var blocked = await Query<GetUploadAllowanceQuery, UploadAllowance>(new GetUploadAllowanceQuery(bert));

        // Assert
        Assert.Equal(new UploadAllowance(false, 5), contributor);
        Assert.False(contributor.Allows(6));
        Assert.Equal(new UploadAllowance(false, null), moderation);
        Assert.False(blocked.Allows(1));
    }

    [SqlFact]
    public async Task Ein_privates_Foto_loeschen_wer_es_hochgeladen_hat_und_die_Moderation_ein_oeffentliches_nur_die_Moderation()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        var own = (await Upload(anna, site)).MediaId!.Value;
        var published = (await Upload(anna, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(published, MediaSection.Underwater)], HeroMediaId = published }, null));

        // Act
        var byBert = await Command<DeleteMediaCommand, UploadMediaResult>(new DeleteMediaCommand(bert, own));
        var ownByAnna = await Command<DeleteMediaCommand, UploadMediaResult>(new DeleteMediaCommand(anna, own));
        var publishedByAnna = await Command<DeleteMediaCommand, UploadMediaResult>(new DeleteMediaCommand(anna, published));
        var publishedByModerator = await Command<DeleteMediaCommand, UploadMediaResult>(new DeleteMediaCommand(moderator, published));

        // Assert
        Assert.Equal([EditorialError.NotAllowed], byBert.Errors);
        Assert.True(ownByAnna.Succeeded);
        Assert.Equal([EditorialError.NotAllowed], publishedByAnna.Errors);
        Assert.True(publishedByModerator.Succeeded);
        Assert.Empty(Sql.Media.Pending);
        Assert.Empty(Sql.Media.Public);
        var after = await PublicSite();
        Assert.Empty(after.PhotoList);
        Assert.Null(after.Hero);
    }

    [SqlFact]
    public async Task Eine_Version_mit_geloeschtem_Foto_laesst_sich_wiederherstellen_und_speichern_ohne_das_Foto()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var photo = (await Upload(anna, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        var withPhoto = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(photo, MediaSection.Underwater)], HeroMediaId = photo }, null));
        await Command<DeleteMediaCommand, UploadMediaResult>(new DeleteMediaCommand(moderator, photo));

        // Act
        var restored = await Command<RestoreRevisionCommand, EditorialResult>(new RestoreRevisionCommand(moderator, site, withPhoto.Number!.Value));
        var stale = await ForEditing(site);
        var saved = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, stale.BaseNumber,
            stale.Content with { Media = [new MediaUse(photo, MediaSection.Underwater)], HeroMediaId = photo, MaxDepth = 40 }, null));

        // Assert
        Assert.True(restored.Succeeded);
        Assert.True(saved.Succeeded);
        var current = await PublicSite();
        Assert.Empty(current.PhotoList);
        Assert.Null(current.Hero);
        Assert.Empty((await ForEditing(site)).Content.MediaList);
    }

    [SqlFact]
    public async Task Mitwirkende_verwenden_nur_oeffentliche_oder_eigene_Fotos()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        var bertsPrivate = (await Upload(bert, site)).MediaId!.Value;
        var edit = await ForEditing(site);

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(bertsPrivate, MediaSection.Underwater)] }, null));

        // Assert
        Assert.Equal([EditorialError.InvalidMedia], result.Errors);
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
    public async Task Wird_ein_Konto_geloescht_verschwinden_private_Fotos_und_alle_Originale_die_oeffentlichen_Bildgroessen_bleiben_mit_dem_Fotografen()
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
        Assert.Equal("Felswand mit Hecht", before.PhotoList[0].Texts.Single().AltText);
        Assert.Empty(Sql.Media.Pending);
        Assert.NotEmpty(Sql.Media.Public);
        Assert.Equal("Anna Muster", Assert.Single((await PublicSite()).PhotoList).Photographer);
        Assert.Equal([published], (await Query<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>(new GetSiteMediaQuery(moderator, site, Pending))).Select(m => m.Id));
    }

    [SqlFact]
    public async Task Private_Fotos_ohne_Verwendung_verschwinden_nach_30_Tagen_junge_vorgeschlagene_und_oeffentliche_bleiben()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var unused = (await Upload(anna, site)).MediaId!.Value;
        var proposed = (await Upload(anna, site)).MediaId!.Value;
        var published = (await Upload(moderator, site)).MediaId!.Value;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { Media = [new MediaUse(published, MediaSection.Underwater)] }, null));
        var approved = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, approved.BaseNumber,
            approved.Content with { Media = [.. approved.Content.MediaList, new MediaUse(proposed, MediaSection.Surroundings)] }, null));
        Sql.Time.Now = FixedTime.SpikeDay.AddDays(31);
        var young = (await Upload(anna, site)).MediaId!.Value;

        // Act
        var first = await Command<PurgeUnusedMediaCommand, PurgeUnusedMediaResult>(new PurgeUnusedMediaCommand());
        var second = await Command<PurgeUnusedMediaCommand, PurgeUnusedMediaResult>(new PurgeUnusedMediaCommand());

        // Assert
        Assert.Equal((1, 0), (first.Deleted, second.Deleted));
        var remaining = (await Query<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>(new GetSiteMediaQuery(moderator, site, Pending))).Select(m => m.Id).ToHashSet();
        Assert.Equal(new HashSet<Guid> { proposed, published, young }, remaining);
        Assert.DoesNotContain(Sql.Media.Pending.Keys, key => key.StartsWith(unused.ToString(), StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Der_Vergleich_eines_Vorschlags_zeigt_neue_geaenderte_unveraenderte_und_entfernte_Fotos_mit_Vorschau()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var (same, removed, moved) = ((await Upload(moderator, site)).MediaId!.Value, (await Upload(moderator, site)).MediaId!.Value, (await Upload(moderator, site)).MediaId!.Value);
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with
        {
            Media = [new MediaUse(same, MediaSection.Underwater), new MediaUse(removed, MediaSection.Underwater), new MediaUse(moved, MediaSection.Underwater)],
            HeroMediaId = removed,
        }, null));
        var added = (await Upload(anna, site)).MediaId!.Value;
        var approved = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, approved.BaseNumber, approved.Content with
        {
            Media = [new MediaUse(same, MediaSection.Underwater), new MediaUse(moved, MediaSection.Underwater), new MediaUse(added, MediaSection.Surroundings)],
            HeroMediaId = moved,
        }, null));
        var proposal = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator))).Revision;

        // Act
        var photos = await Query<CompareRevisionMediaQuery, IReadOnlyList<RevisionPhoto>>(
            new CompareRevisionMediaQuery(moderator, site, proposal.BaseNumber, proposal.Number, Pending));

        // Assert
        Assert.Equal(
            [(same, PhotoChange.Unchanged, false), (moved, PhotoChange.Changed, true), (added, PhotoChange.Added, false), (removed, PhotoChange.Removed, true)],
            photos.Select(p => (p.MediaId, p.Change, p.IsHero)));
        Assert.Equal($"/admin/medien/{added}/480.webp", photos[2].ThumbnailUrl);
        Assert.All(photos, p => Assert.Equal("Anna Muster", p.Photographer));
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
