using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Core.Features.Content;

/// <summary>
/// Liest ein Inhaltspaket und übernimmt es (ADR-011). Legt an, was fehlt, hängt eine freigegebene Version an, wo der Inhalt anders ist,
/// und lässt Gleiches stehen. Löscht nie. Ein zweiter Import desselben Pakets ändert nichts.
/// </summary>
internal class ContentImporter(
    TauchplaetzeDbContext dbContext,
    RevisionWriter revisions,
    IMediaStore media,
    TimeProvider time,
    ILogger<ContentImporter> logger)
{
    public async Task<ImportContentResult> Import(Stream package, string adminId, CancellationToken ct)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return ImportContentResult.Fail(ContentError.InvalidPackage);
        }

        using (zip)
        {
            PackageManifest? manifest;
            IReadOnlyList<PackageWaterBody> waterBodies;
            IReadOnlyList<PackagePhoto> photos;
            try
            {
                manifest = await ContentPackage.Read<PackageManifest>(zip, ContentPackage.ManifestEntry, ct);
                waterBodies = await ContentPackage.Read<List<PackageWaterBody>>(zip, ContentPackage.WaterBodiesEntry, ct) ?? [];
                photos = await ContentPackage.Read<List<PackagePhoto>>(zip, ContentPackage.PhotosEntry, ct) ?? [];
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                return ImportContentResult.Fail(ContentError.InvalidPackage);
            }

            if (manifest?.FormatVersion != ContentPackage.FormatVersion)
            {
                return ImportContentResult.Fail(ContentError.InvalidPackage);
            }

            var comment = string.Create(CultureInfo.InvariantCulture, $"Import aus Prod vom {SwissTime.DayOf(manifest.CreatedAt):dd.MM.yyyy}");
            var waterBodiesAdded = await ImportWaterBodies(waterBodies, ct);
            var waterBodyIds = await dbContext.WaterBodies.ToDictionaryAsync(w => w.Slug, w => w.Id, ct);
            var photosBySite = photos.GroupBy(p => p.DiveSiteId).ToDictionary(g => g.Key, g => g.ToList());

            var counts = new Counts();
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith(ContentPackage.SitesFolder, StringComparison.Ordinal) && e.FullName.EndsWith(".json", StringComparison.Ordinal)).ToList())
            {
                if (await ReadSite(entry, waterBodyIds, ct) is not var (site, content))
                {
                    counts.Skipped++;
                    continue;
                }

                await ImportSite(zip, site, content, photosBySite.GetValueOrDefault(site.Id) ?? [], adminId, comment, counts, ct);
                dbContext.ChangeTracker.Clear();
            }

            logger.LogInformation(
                "Content import: {WaterBodies} water bodies, {Added} sites added, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped, {Photos} photos, {PhotosSkipped} photos skipped",
                waterBodiesAdded, counts.Added, counts.Updated, counts.Unchanged, counts.Skipped, counts.Photos, counts.PhotosSkipped);
            return new ImportContentResult(null, waterBodiesAdded, counts.Added, counts.Updated, counts.Unchanged, counts.Skipped, counts.Photos, counts.PhotosSkipped);
        }
    }

    private async Task<int> ImportWaterBodies(IReadOnlyList<PackageWaterBody> waterBodies, CancellationToken ct)
    {
        var existing = await dbContext.WaterBodies.Include(w => w.HydroStations).ToDictionaryAsync(w => w.Slug, ct);
        var added = 0;
        foreach (var water in waterBodies)
        {
            if (existing.TryGetValue(water.Slug, out var local))
            {
                local.HydroStations.AddRange(water.HydroStations
                    .Where(s => local.HydroStations.All(h => h.StationId != s.Id))
                    .Select(s => new WaterBodyHydroStation { StationId = s.Id, Name = s.Name, Role = s.Role, Weight = s.Weight }));
                continue;
            }

            dbContext.WaterBodies.Add(new WaterBody
            {
                Slug = water.Slug,
                NameDe = water.NameDe,
                NameEn = water.NameEn,
                NameFr = water.NameFr,
                NameIt = water.NameIt,
                WaterBodyType = water.Type,
                Centroid = GeoPoints.Create(water.Centroid.Latitude, water.Centroid.Longitude),
                SatellitePoint = water.SatellitePoint is { } point ? GeoPoints.Create(point.Latitude, point.Longitude) : null,
                AlplakesLakeKey = water.AlplakesLakeKey,
                AlplakesModel3d = water.AlplakesModel3d,
                AlplakesSimstratKey = water.AlplakesSimstratKey,
                IsActive = water.IsActive,
                HydroStations = [.. water.HydroStations.Select(s => new WaterBodyHydroStation { StationId = s.Id, Name = s.Name, Role = s.Role, Weight = s.Weight })],
            });
            added++;
        }

        await dbContext.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>Ein lesbarer, gültiger Platz mit bekanntem Gewässer. Sonst <c>null</c> und eine Warnung im Log.</summary>
    private async Task<(PackageSite Site, DiveSiteContent Content)?> ReadSite(ZipArchiveEntry entry, IReadOnlyDictionary<string, int> waterBodyIds, CancellationToken ct)
    {
        try
        {
            var site = await ContentPackage.Read<PackageSite>(entry, ct);
            if (site is null || !waterBodyIds.TryGetValue(site.WaterBody, out var waterBodyId))
            {
                logger.LogWarning("Content import skipped {Entry}: unreadable or unknown water body", entry.FullName);
                return null;
            }

            var content = DiveSiteSnapshot.FromJson(JsonSerializer.SerializeToUtf8Bytes(site.Content)) with { WaterBodyId = waterBodyId };
            if (ContentValidation.Errors(content, waterBodyExists: true) is { Count: > 0 } errors)
            {
                logger.LogWarning("Content import skipped site {SiteId}: {Errors}", site.Id, string.Join(", ", errors));
                return null;
            }

            return (site, content);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            logger.LogWarning("Content import skipped {Entry}: {Error}", entry.FullName, exception.GetType().Name);
            return null;
        }
    }

    private async Task ImportSite(ZipArchive zip, PackageSite package, DiveSiteContent content, IReadOnlyList<PackagePhoto> photos, string adminId, string comment, Counts counts, CancellationToken ct)
    {
        var hash = DiveSiteSnapshot.HashOf(DiveSiteSnapshot.ToJson(content));
        if (await SlugTaken(content.Slug, package.Id, ct))
        {
            logger.LogWarning("Content import skipped site {SiteId}: slug {Slug} is used locally", package.Id, content.Slug);
            counts.Skipped++;
            return;
        }

        var site = await dbContext.DiveSites
            .Include(d => d.Translations)
            .Include(d => d.EntryPoints).ThenInclude(e => e.Translations)
            .Include(d => d.Notices).ThenInclude(n => n.Translations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(d => d.Id == package.Id, ct);

        if (site is null)
        {
            site = revisions.NewDraft(content, adminId, package.Id);
            counts.Photos += await ImportPhotos(zip, site.Id, photos, counts, ct);
            await dbContext.SaveChangesAsync(ct);
            await revisions.Append(site, content, null, adminId, comment, approve: true, ct);
            site.Status = package.Status;
            await AddRedirects(site.Id, package.OldSlugs, ct);
            await dbContext.SaveChangesAsync(ct);
            counts.Added++;
            return;
        }

        var latest = await dbContext.DiveSiteRevisions
            .Where(r => r.DiveSiteId == site.Id && r.Status == RevisionStatus.Approved)
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync(ct);
        var photosAdded = await ImportPhotos(zip, site.Id, photos, counts, ct);
        counts.Photos += photosAdded;
        await dbContext.SaveChangesAsync(ct);
        var changed = latest?.SnapshotHash != hash;
        if (changed)
        {
            await revisions.Append(site, content, latest?.Number, adminId, comment, approve: true, ct);
        }

        var statusChanged = site.Status != package.Status && package.Status != DiveSiteStatus.Draft;
        if (statusChanged)
        {
            site.Status = package.Status;
            site.UpdatedAt = time.GetUtcNow();
            site.UpdatedBy = adminId;
        }

        var redirectsAdded = await AddRedirects(site.Id, package.OldSlugs, ct);
        await dbContext.SaveChangesAsync(ct);

        if (changed || statusChanged || redirectsAdded > 0 || photosAdded > 0)
        {
            counts.Updated++;
        }
        else
        {
            counts.Unchanged++;
        }
    }

    private async Task<bool> SlugTaken(string slug, Guid siteId, CancellationToken ct) =>
        await dbContext.DiveSites.AnyAsync(d => d.Slug == slug && d.Id != siteId, ct)
        || await dbContext.SlugRedirects.AnyAsync(r => r.OldSlug == slug && r.DiveSiteId != siteId, ct);

    /// <summary>Legt fehlende öffentliche Fotos mit ihren Dateien an. Vorhandene bleiben unverändert. Fehlt eine Bildgrösse, fehlt das Foto.</summary>
    private async Task<int> ImportPhotos(ZipArchive zip, Guid siteId, IReadOnlyList<PackagePhoto> photos, Counts counts, CancellationToken ct)
    {
        var ids = photos.Select(p => p.Id).ToList();
        var existing = await dbContext.MediaItems.Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        var added = 0;
        foreach (var photo in photos.Where(p => !existing.Contains(p.Id)))
        {
            if (await Renditions(zip, photo, ct) is not { } files)
            {
                logger.LogWarning("Content import skipped photo {MediaId}: a rendition is missing or too large", photo.Id);
                counts.PhotosSkipped++;
                continue;
            }

            foreach (var (name, file) in files)
            {
                await media.SavePublic(photo.Id, name, file, ct);
            }

            dbContext.MediaItems.Add(new MediaItem
            {
                Id = photo.Id,
                DiveSiteId = siteId,
                Section = photo.Section,
                EntryPointId = photo.EntryPointId,
                Kind = photo.Kind,
                BlobPath = photo.Id.ToString(),
                Width = photo.Width,
                Height = photo.Height,
                TakenAt = photo.TakenAt,
                Depth = photo.Depth,
                Photographer = photo.Photographer,
                PhotographerUrl = photo.PhotographerUrl,
                SourceUrl = photo.SourceUrl,
                SortOrder = photo.SortOrder,
                UploadedAt = time.GetUtcNow(),
                RightsConfirmed = true,
                IsPublic = true,
                Translations = [.. photo.Texts.Select(t => new MediaItemTranslation { MediaItemId = photo.Id, Culture = t.Culture, Caption = t.Caption, AltText = t.AltText })],
            });
            added++;
        }

        return added;
    }

    private static async Task<IReadOnlyList<(string Name, byte[] File)>?> Renditions(ZipArchive zip, PackagePhoto photo, CancellationToken ct)
    {
        var files = new List<(string, byte[])>();
        foreach (var name in MediaFiles.WidthsFor(photo.Width).Select(MediaFiles.Rendition))
        {
            if (await ContentPackage.ReadBytes(zip, ContentPackage.PhotoFileEntry(photo.Id, name), ct) is not { } file)
            {
                return null;
            }

            files.Add((name, file));
        }

        return files;
    }

    private async Task<int> AddRedirects(Guid siteId, IReadOnlyList<string> oldSlugs, CancellationToken ct)
    {
        var taken = await dbContext.SlugRedirects.Where(r => oldSlugs.Contains(r.OldSlug)).Select(r => r.OldSlug).ToListAsync(ct);
        var current = await dbContext.DiveSites.Where(d => oldSlugs.Contains(d.Slug)).Select(d => d.Slug).ToListAsync(ct);
        var missing = oldSlugs.Except(taken).Except(current).ToList();
        dbContext.SlugRedirects.AddRange(missing.Select(slug => new SlugRedirect { OldSlug = slug, DiveSiteId = siteId }));
        return missing.Count;
    }

    private sealed class Counts
    {
        public int Added { get; set; }

        public int Updated { get; set; }

        public int Unchanged { get; set; }

        public int Skipped { get; set; }

        public int Photos { get; set; }

        public int PhotosSkipped { get; set; }
    }
}
