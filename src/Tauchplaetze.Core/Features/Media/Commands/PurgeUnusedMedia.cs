using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media.Commands;

/// <summary>
/// Löscht private Fotos, die älter als <see cref="MediaOptions.PurgeAfterDays"/> sind und kein offener Vorschlag verwendet
/// (`cms.md`, «Medien»). Eintrag und Dateien. Ein zweiter Lauf findet nichts mehr.
/// </summary>
public record PurgeUnusedMediaCommand;

/// <summary>Ergebnis: Zahl der gelöschten Fotos. <see cref="Skipped"/>: Ein anderer Lauf hielt den Lock.</summary>
public record PurgeUnusedMediaResult(int Deleted, bool Skipped = false);

internal class PurgeUnusedMediaHandler(
    TauchplaetzeDbContext dbContext,
    RevisionWriter revisions,
    IMediaStore store,
    IRunLock runLock,
    IOptions<MediaOptions> options,
    TimeProvider time) : ICommandHandler<PurgeUnusedMediaCommand, PurgeUnusedMediaResult>
{
    public const string LockName = "media-purge";

    public async Task<PurgeUnusedMediaResult> Handle(PurgeUnusedMediaCommand command, CancellationToken ct = default)
    {
        await using var acquired = await runLock.TryAcquire(LockName, ct);
        if (acquired is null)
        {
            return new PurgeUnusedMediaResult(0, Skipped: true);
        }

        var cutoff = time.GetUtcNow().AddDays(-options.Value.PurgeAfterDays);
        var candidates = await dbContext.MediaItems
            .Where(m => !m.IsPublic && m.UploadedAt < cutoff)
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            return new PurgeUnusedMediaResult(0);
        }

        var inProposals = await UsedInPendingRevisions(candidates.Select(m => m.DiveSiteId).OfType<Guid>().Distinct().ToList(), ct);
        var unused = candidates.Where(m => !inProposals.Contains(m.Id)).ToList();
        foreach (var item in unused)
        {
            await store.DeletePending(item.Id, ct);
            dbContext.MediaItems.Remove(item);
        }

        await dbContext.SaveChangesAsync(ct);
        return new PurgeUnusedMediaResult(unused.Count);
    }

    private async Task<HashSet<Guid>> UsedInPendingRevisions(List<Guid> siteIds, CancellationToken ct)
    {
        var pending = await dbContext.DiveSiteRevisions
            .AsNoTracking()
            .Where(r => siteIds.Contains(r.DiveSiteId) && r.Status == RevisionStatus.Pending)
            .ToListAsync(ct);

        var used = new HashSet<Guid>();
        foreach (var revision in pending)
        {
            var content = await revisions.Load(revision, ct);
            used.UnionWith(content.MediaList.Select(m => m.MediaId));
            if (content.HeroMediaId is { } hero)
            {
                used.Add(hero);
            }
        }

        return used;
    }
}
