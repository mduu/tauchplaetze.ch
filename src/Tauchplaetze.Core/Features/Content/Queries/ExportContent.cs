using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Core.Features.Content.Queries;

/// <summary>
/// Schreibt das Inhaltspaket nach <paramref name="Output"/> (ADR-011): Gewässer, öffentliche und archivierte Plätze mit dem Inhalt der neuesten
/// freigegebenen Version, Slug-Redirects und öffentliche Fotos mit ihren Bildgrössen. Keine Konten, keine offenen Vorschläge, keine privaten Fotos.
/// Erlaubt für einen Admin (<paramref name="AdminId"/>) oder mit dem Schlüssel aus `Content:ExportKey` (<paramref name="ExportKey"/>).
/// </summary>
public record ExportContentQuery(string? AdminId, string? ExportKey, Stream Output);

internal class ExportContentHandler(
    TauchplaetzeDbContext dbContext,
    IRevisionStore revisions,
    IMediaStore media,
    AccountRoles roles,
    IOptions<ContentOptions> options,
    IOptions<PublicSiteOptions> site,
    TimeProvider time) : IQueryHandler<ExportContentQuery, ExportContentResult>
{
    public async Task<ExportContentResult> Handle(ExportContentQuery query, CancellationToken ct = default)
    {
        if (await Refusal(query) is { } error)
        {
            return new ExportContentResult(error);
        }

        var waterBodies = await WaterBodies(ct);
        var slugs = await dbContext.WaterBodies.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.Slug, ct);
        var sites = await dbContext.DiveSites.AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published || d.Status == DiveSiteStatus.Archived)
            .OrderBy(d => d.Id)
            .Select(d => new { d.Id, d.WaterBodyId, d.Status })
            .ToListAsync(ct);
        var siteIds = sites.Select(s => s.Id).ToList();
        var approved = (await dbContext.DiveSiteRevisions.AsNoTracking()
                .Where(r => siteIds.Contains(r.DiveSiteId) && r.Status == RevisionStatus.Approved)
                .Select(r => new { r.DiveSiteId, r.Number, r.SnapshotPath })
                .ToListAsync(ct))
            .GroupBy(r => r.DiveSiteId)
            .ToDictionary(g => g.Key, g => g.MaxBy(r => r.Number)!.SnapshotPath);
        var redirects = (await dbContext.SlugRedirects.AsNoTracking().Where(r => siteIds.Contains(r.DiveSiteId)).ToListAsync(ct))
            .GroupBy(r => r.DiveSiteId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(r => r.OldSlug).Order(StringComparer.Ordinal)]);
        var photos = await Photos(siteIds, ct);

        using var zip = new ZipArchive(query.Output, ZipArchiveMode.Create, leaveOpen: true);
        var exported = 0;
        foreach (var entry in sites.Where(s => approved.ContainsKey(s.Id)))
        {
            var snapshot = JsonNode.Parse(await revisions.Load(approved[entry.Id], ct))!.AsObject();
            await ContentPackage.Write(zip, ContentPackage.SiteEntry(entry.Id),
                new PackageSite(entry.Id, slugs[entry.WaterBodyId], entry.Status, redirects.GetValueOrDefault(entry.Id) ?? [], snapshot), ct);
            exported++;
        }

        await ContentPackage.Write(zip, ContentPackage.WaterBodiesEntry, waterBodies, ct);
        await ContentPackage.Write(zip, ContentPackage.PhotosEntry, photos, ct);
        foreach (var photo in photos)
        {
            foreach (var name in MediaFiles.WidthsFor(photo.Width).Select(MediaFiles.Rendition))
            {
                if (await media.LoadPublic(photo.Id, name, ct) is { } file)
                {
                    await ContentPackage.WriteBytes(zip, ContentPackage.PhotoFileEntry(photo.Id, name), file, ct);
                }
            }
        }

        await ContentPackage.Write(zip, ContentPackage.ManifestEntry,
            new PackageManifest(ContentPackage.FormatVersion, time.GetUtcNow(), site.Value.BaseUri, waterBodies.Count, exported, photos.Count), ct);

        return new ExportContentResult(null, waterBodies.Count, exported, photos.Count);
    }

    private async Task<ContentError?> Refusal(ExportContentQuery query)
    {
        if (query.AdminId is { } adminId)
        {
            return (await roles.Of(adminId)).CanAdminister ? null : ContentError.NotAllowed;
        }

        if (string.IsNullOrEmpty(options.Value.ExportKey))
        {
            return ContentError.Disabled;
        }

        return query.ExportKey is { } key && KeysMatch(key, options.Value.ExportKey) ? null : ContentError.InvalidKey;
    }

    private static bool KeysMatch(string given, string expected) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    private async Task<IReadOnlyList<PackageWaterBody>> WaterBodies(CancellationToken ct) =>
        [.. (await dbContext.WaterBodies.AsNoTracking().Include(w => w.HydroStations).OrderBy(w => w.Slug).ToListAsync(ct))
            .Select(w => new PackageWaterBody(
                w.Slug,
                w.NameDe,
                w.NameEn,
                w.NameFr,
                w.NameIt,
                w.WaterBodyType,
                new PackagePoint(w.Centroid.Y, w.Centroid.X),
                w.SatellitePoint is { } point ? new PackagePoint(point.Y, point.X) : null,
                w.AlplakesLakeKey,
                w.AlplakesModel3d,
                w.AlplakesSimstratKey,
                w.IsActive,
                [.. w.HydroStations.OrderBy(h => h.StationId, StringComparer.Ordinal).Select(h => new PackageHydroStation(h.StationId, h.Name, h.Role, h.Weight))]))];

    private async Task<IReadOnlyList<PackagePhoto>> Photos(IReadOnlyList<Guid> siteIds, CancellationToken ct) =>
        [.. (await dbContext.MediaItems.AsNoTracking()
                .Include(m => m.Translations)
                .Where(m => m.IsPublic && m.DiveSiteId != null && siteIds.Contains(m.DiveSiteId.Value))
                .OrderBy(m => m.Id)
                .ToListAsync(ct))
            .Select(m => new PackagePhoto(
                m.Id,
                m.DiveSiteId!.Value,
                m.Section,
                m.EntryPointId,
                m.Kind,
                m.Width,
                m.Height,
                m.TakenAt,
                m.Depth,
                m.Photographer,
                m.PhotographerUrl,
                m.SourceUrl,
                m.SortOrder,
                [.. m.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal).Select(t => new PackagePhotoText(t.Culture.Trim(), t.Caption, t.AltText))]))];
}
