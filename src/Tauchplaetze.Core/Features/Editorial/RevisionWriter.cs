using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Schreibt Versionen: zuerst den Inhalt in Blob Storage, dann die Row (ADR-008). Speichert nicht selbst. Das tut der Command.</summary>
internal class RevisionWriter(TauchplaetzeDbContext dbContext, IRevisionStore store, TimeProvider time)
{
    /// <summary>Legt einen neuen Platz an, dessen erste Version sofort freigegeben ist (Moderation, Admin oder Import).</summary>
    public async Task<DiveSite> CreateApprovedSite(DiveSiteSnapshot snapshot, string editorId, string? comment, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var site = new DiveSite
        {
            Id = Guid.CreateVersion7(now),
            Slug = snapshot.Slug,
            Name = snapshot.Name,
            Location = Point.Empty,
            Status = DiveSiteStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = editorId,
            UpdatedBy = editorId,
        };
        snapshot.ApplyTo(site);

        var json = snapshot.ToJson();
        var path = $"{site.Id}/1.json";
        await store.Save(path, json, ct);

        dbContext.DiveSites.Add(site);
        dbContext.DiveSiteRevisions.Add(new DiveSiteRevision
        {
            Id = Guid.CreateVersion7(now),
            DiveSiteId = site.Id,
            Number = 1,
            Status = RevisionStatus.Approved,
            SnapshotPath = path,
            SnapshotHash = DiveSiteSnapshot.HashOf(json),
            SnapshotVersion = snapshot.SnapshotVersion,
            Comment = comment,
            AuthorId = editorId,
            CreatedAt = now,
            ReviewerId = editorId,
            ReviewedAt = now,
        });

        return site;
    }
}
