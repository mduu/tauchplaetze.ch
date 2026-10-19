using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Text;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>
/// Schlägt einen neuen Platz vor: Name, Gewässer, Koordinate, Gemeinde (`cms.md`, «Was Mitwirkende dürfen»).
/// Der Platz bleibt Entwurf bis zur Freigabe. Moderation und Admin geben beim Speichern selbst frei.
/// </summary>
public record ProposeDiveSiteCommand(string EditorId, string Name, int WaterBodyId, double Latitude, double Longitude, string? Municipality, string? Canton, string? Comment);

internal class ProposeDiveSiteHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, AccountRoles roles, EditorialNotifications notifications) : ICommandHandler<ProposeDiveSiteCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(ProposeDiveSiteCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.EditorId);
        if (!rights.CanWrite)
        {
            return EditorialResult.Fail(EditorialError.Blocked);
        }

        if (!rights.IsModerator && await PendingOf(command.EditorId, ct) >= Proposals.MaxPending)
        {
            return EditorialResult.Fail(EditorialError.TooManyPending);
        }

        var content = new DiveSiteContent(
            command.Name, await FreeSlug(Slugs.From($"{command.Name} {command.Municipality}"), ct), command.WaterBodyId, command.Latitude, command.Longitude,
            command.Municipality, command.Canton, null, null, null, null, null, null, null, null, default, false, null, null, null, false, [], []);
        var errors = ContentValidation.Errors(content, await dbContext.WaterBodies.AnyAsync(w => w.Id == command.WaterBodyId, ct));
        if (errors.Count > 0)
        {
            return new EditorialResult(errors);
        }

        var site = revisions.NewDraft(content, command.EditorId);
        var revision = await revisions.Append(site, content, null, command.EditorId, command.Comment, rights.IsModerator, ct);
        await dbContext.SaveChangesAsync(ct);
        await (rights.IsModerator
            ? notifications.ModeratorChanged(revision, site.Id, content.Name, ct)
            : notifications.ProposalSubmitted(revision, content.Name, isNewSite: true, ct));
        return new EditorialResult([], site.Id, revision.Number, revision.Status);
    }

    private Task<int> PendingOf(string editorId, CancellationToken ct) =>
        dbContext.DiveSiteRevisions.CountAsync(r => r.AuthorId == editorId && r.Status == RevisionStatus.Pending, ct);

    private async Task<string> FreeSlug(string wanted, CancellationToken ct)
    {
        var slug = wanted.Length == 0 ? "platz" : wanted;
        var suffix = 2;
        var candidate = slug;
        while (await dbContext.DiveSites.AnyAsync(d => d.Slug == candidate, ct) || await dbContext.SlugRedirects.AnyAsync(r => r.OldSlug == candidate, ct))
        {
            candidate = $"{slug}-{suffix++}";
        }

        return candidate;
    }
}
