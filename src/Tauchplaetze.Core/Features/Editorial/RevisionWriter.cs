using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Schreibt Versionen: zuerst den Inhalt in Blob Storage, dann die Row (ADR-008). Speichert nicht selbst. Das tut der Command.</summary>
internal class RevisionWriter(TauchplaetzeDbContext dbContext, IRevisionStore store, TimeProvider time)
{
    /// <summary>Ein neuer Platz als Entwurf, mit leerem Inhalt in den Tabellen bis zur Freigabe.</summary>
    public DiveSite NewDraft(DiveSiteContent content, string editorId)
    {
        var now = time.GetUtcNow();
        var site = new DiveSite
        {
            Id = Guid.CreateVersion7(now),
            Slug = content.Slug,
            Name = content.Name.Trim(),
            WaterBodyId = content.WaterBodyId,
            Location = Point.Empty,
            Status = DiveSiteStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = editorId,
            UpdatedBy = editorId,
        };
        DiveSiteContentMapping.ApplyTo(site, content);
        dbContext.DiveSites.Add(site);
        return site;
    }

    /// <summary>Hängt eine neue Version an. Freigegeben, wenn <paramref name="approve"/>.</summary>
    public async Task<DiveSiteRevision> Append(DiveSite site, DiveSiteContent content, int? baseNumber, string authorId, string? comment, bool approve, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var number = await NextNumber(site.Id, ct);
        var json = DiveSiteSnapshot.ToJson(content);
        var path = $"{site.Id}/{number}.json";
        await store.Save(path, json, ct);

        var revision = new DiveSiteRevision
        {
            Id = Guid.CreateVersion7(now),
            DiveSiteId = site.Id,
            Number = number,
            BaseNumber = baseNumber,
            Status = RevisionStatus.Pending,
            SnapshotPath = path,
            SnapshotHash = DiveSiteSnapshot.HashOf(json),
            SnapshotVersion = DiveSiteSnapshot.CurrentVersion,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            AuthorId = authorId,
            CreatedAt = now,
        };
        dbContext.DiveSiteRevisions.Add(revision);

        if (approve)
        {
            await Approve(site, revision, content, authorId, ct);
        }

        return revision;
    }

    /// <summary>Gibt eine Version frei: Ihr Inhalt geht online. Ein geänderter Slug hinterlässt einen Redirect (301).</summary>
    public async Task Approve(DiveSite site, DiveSiteRevision revision, DiveSiteContent content, string reviewerId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var oldSlug = site.Status == DiveSiteStatus.Draft ? null : site.Slug;

        DiveSiteContentMapping.ApplyTo(site, content);
        site.ContentLevel = ContentLevelRule.For(content, site.HeroMediaId);
        site.Status = site.Status == DiveSiteStatus.Draft ? DiveSiteStatus.Published : site.Status;
        site.UpdatedAt = now;
        site.UpdatedBy = reviewerId;

        revision.Status = RevisionStatus.Approved;
        revision.ReviewerId = reviewerId;
        revision.ReviewedAt = now;

        await UpdateRedirects(site, oldSlug, ct);
    }

    public async Task<DiveSiteContent> Load(DiveSiteRevision revision, CancellationToken ct) =>
        DiveSiteSnapshot.FromJson(await store.Load(revision.SnapshotPath, ct));

    private async Task<int> NextNumber(Guid diveSiteId, CancellationToken ct)
    {
        var stored = await dbContext.DiveSiteRevisions.Where(r => r.DiveSiteId == diveSiteId).MaxAsync(r => (int?)r.Number, ct) ?? 0;
        var added = dbContext.DiveSiteRevisions.Local.Where(r => r.DiveSiteId == diveSiteId).Select(r => r.Number).DefaultIfEmpty(0).Max();
        return Math.Max(stored, added) + 1;
    }

    private async Task UpdateRedirects(DiveSite site, string? oldSlug, CancellationToken ct)
    {
        var pointingToNewSlug = await dbContext.SlugRedirects.Where(r => r.OldSlug == site.Slug).ToListAsync(ct);
        dbContext.SlugRedirects.RemoveRange(pointingToNewSlug);

        if (oldSlug is not null && oldSlug != site.Slug && !await dbContext.SlugRedirects.AnyAsync(r => r.OldSlug == oldSlug, ct))
        {
            dbContext.SlugRedirects.Add(new SlugRedirect { OldSlug = oldSlug, DiveSiteId = site.Id });
        }
    }
}
