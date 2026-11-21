using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>
/// Setzt <c>FirstDescribedAt</c> bei Plätzen, die heute einen Text haben, aber noch kein Datum (`cms.md`, «Erste Beschreibung»).
/// Das Datum ist die Freigabe der ersten Version mit Text. Läuft beim Start. Ein zweiter Lauf ändert nichts.
/// </summary>
public record BackfillFirstDescribedCommand;

/// <summary>Plätze, die ein Datum bekamen.</summary>
public record BackfillFirstDescribedResult(int Sites);

internal class BackfillFirstDescribedHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions)
    : ICommandHandler<BackfillFirstDescribedCommand, BackfillFirstDescribedResult>
{
    public async Task<BackfillFirstDescribedResult> Handle(BackfillFirstDescribedCommand command, CancellationToken ct = default)
    {
        var sites = await dbContext.DiveSites
            .Where(s => s.FirstDescribedAt == null && s.Translations.Any(t => !t.IsMachineTranslated
                && (t.UnderwaterDescription != null || t.SurroundingsInfo != null || t.ApproachInfo != null || t.HazardsInfo != null || t.FacilitiesInfo != null)))
            .ToListAsync(ct);
        var updated = 0;
        foreach (var site in sites)
        {
            if (await FirstDescribed(site.Id, ct) is { } describedAt)
            {
                site.FirstDescribedAt = describedAt;
                updated++;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        return new BackfillFirstDescribedResult(updated);
    }

    private async Task<DateTimeOffset?> FirstDescribed(Guid siteId, CancellationToken ct)
    {
        var approved = await dbContext.DiveSiteRevisions
            .AsNoTracking()
            .Where(r => r.DiveSiteId == siteId && r.Status == RevisionStatus.Approved && r.ReviewedAt != null)
            .OrderBy(r => r.Number)
            .ToListAsync(ct);
        foreach (var revision in approved)
        {
            if (DescriptionRule.IsDescribed(await revisions.Load(revision, ct)))
            {
                return revision.ReviewedAt;
            }
        }

        return null;
    }
}
