using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Core.Features.Editorial.Commands;

/// <summary>
/// Speichert eine Änderung an einem Platz als neue Version auf der Basis <paramref name="BaseNumber"/> (`cms.md`, «Ablauf»).
/// Mitwirkende: Vorschlag, wartet auf Freigabe. Moderation und Admin: sofort freigegeben. Ohne Änderung entsteht keine Version.
/// </summary>
public record ProposeChangeCommand(string EditorId, Guid DiveSiteId, int BaseNumber, DiveSiteContent Content, string? Comment);

internal class ProposeChangeHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, AccountRoles roles, MediaPublisher media, EditorialNotifications notifications) : ICommandHandler<ProposeChangeCommand, EditorialResult>
{
    public async Task<EditorialResult> Handle(ProposeChangeCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.EditorId);
        if (!rights.CanWrite)
        {
            return EditorialResult.Fail(EditorialError.Blocked);
        }

        var site = await dbContext.DiveSites.Include(d => d.Translations).Include(d => d.EntryPoints).ThenInclude(e => e.Translations).Include(d => d.Notices).ThenInclude(n => n.Translations)
            .SingleOrDefaultAsync(d => d.Id == command.DiveSiteId && d.Status != DiveSiteStatus.Archived, ct);
        var baseRevision = await dbContext.DiveSiteRevisions.SingleOrDefaultAsync(r => r.DiveSiteId == command.DiveSiteId && r.Number == command.BaseNumber, ct);
        if (site is null || baseRevision is null)
        {
            return EditorialResult.Fail(EditorialError.NotFound);
        }

        if (!rights.IsModerator && await dbContext.DiveSiteRevisions.CountAsync(r => r.AuthorId == command.EditorId && r.Status == RevisionStatus.Pending, ct) >= Proposals.MaxPending)
        {
            return EditorialResult.Fail(EditorialError.TooManyPending);
        }

        var current = DiveSiteContentMapping.FromEntity(site);
        var content = Proposals.ClearMachineTranslationOfEditedTexts(
            rights.IsModerator ? command.Content : Proposals.ProtectModeratorFields(command.Content, current), current);
        var errors = ContentValidation.Errors(content, await dbContext.WaterBodies.AnyAsync(w => w.Id == content.WaterBodyId, ct)).ToList();
        if (content.Slug != site.Slug && await dbContext.DiveSites.AnyAsync(d => d.Slug == content.Slug && d.Id != site.Id, ct))
        {
            errors.Add(EditorialError.InvalidSlug);
        }

        content = await media.WithoutDeleted(content, ct);
        if (await UsesForbiddenMedia(content, site.Id, command.EditorId, rights.IsModerator, ct) && !errors.Contains(EditorialError.InvalidMedia))
        {
            errors.Add(EditorialError.InvalidMedia);
        }

        if (errors.Count > 0)
        {
            return new EditorialResult(errors);
        }

        if (DiveSiteSnapshot.HashOf(DiveSiteSnapshot.ToJson(content)) == baseRevision.SnapshotHash)
        {
            return new EditorialResult([], site.Id, baseRevision.Number, baseRevision.Status, NoChange: true);
        }

        var revision = await revisions.Append(site, content, command.BaseNumber, command.EditorId, command.Comment, rights.IsModerator, ct);
        await dbContext.SaveChangesAsync(ct);
        await (rights.IsModerator
            ? notifications.ModeratorChanged(revision, site.Id, content.Name, ct)
            : notifications.ProposalSubmitted(revision, content.Name, isNewSite: false, ct));
        return new EditorialResult([], site.Id, revision.Number, revision.Status);
    }

    /// <summary>Nur Fotos dieses Platzes. Mitwirkende verwenden nur öffentliche oder eigene Fotos (`cms.md`, «Medien»).</summary>
    private async Task<bool> UsesForbiddenMedia(DiveSiteContent content, Guid siteId, string editorId, bool isModerator, CancellationToken ct)
    {
        var ids = content.MediaList.Select(m => m.MediaId).ToList();
        var used = await dbContext.MediaItems.Where(m => ids.Contains(m.Id)).Select(m => new { m.DiveSiteId, m.IsPublic, m.UploadedBy }).ToListAsync(ct);
        return used.Any(m => m.DiveSiteId != siteId || (!isModerator && !m.IsPublic && m.UploadedBy != editorId));
    }
}
