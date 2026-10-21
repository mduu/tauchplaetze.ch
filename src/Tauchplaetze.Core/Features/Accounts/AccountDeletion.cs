using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Reports;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>
/// Löscht ein Konto für die Selbstlöschung und für Admins. Die Versionen bleiben, Autor und Approver werden «gelöschtes Konto»
/// (`cms.md`, «Datenschutz in der History»). Private Fotos werden gelöscht. Öffentliche bleiben als Bildgrössen mit dem Namen des Fotografen,
/// ohne Bezug zum Konto; ihre Originale (mit EXIF) werden gelöscht.
/// Meldungen bleiben ohne Autor und ohne Kommentar (`dive-site.md`, «Meldungen»). Der letzte aktive Admin wird nie gelöscht.
/// </summary>
internal class AccountDeletion(TauchplaetzeDbContext dbContext, UserManager<User> users, AccountRoles roles, IMediaStore media)
{
    public async Task<AccountResult> Delete(User user, CancellationToken ct)
    {
        if (await roles.IsLastActiveAdmin(user.Id))
        {
            return AccountResult.Fail(AccountError.LastAdmin);
        }

        await dbContext.DiveSiteRevisions.Where(r => r.AuthorId == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.AuthorId, (string?)null), ct);
        await dbContext.DiveSiteRevisions.Where(r => r.ReviewerId == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.ReviewerId, (string?)null), ct);
        await dbContext.DiveSiteRevisions.Where(r => r.RedactedBy == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.RedactedBy, (string?)null), ct);
        await dbContext.DiveSites.Where(d => d.CreatedBy == user.Id).ExecuteUpdateAsync(d => d.SetProperty(x => x.CreatedBy, (string?)null), ct);
        await dbContext.DiveSites.Where(d => d.UpdatedBy == user.Id).ExecuteUpdateAsync(d => d.SetProperty(x => x.UpdatedBy, (string?)null), ct);
        await dbContext.Reports.Where(r => r.AuthorId == user.Id).ExecuteUpdateAsync(r => r
            .SetProperty(x => x.AuthorId, (string?)null)
            .SetProperty(x => x.Comment, (string?)null)
            .SetProperty(x => x.CommentStatus, ReportCommentStatus.None), ct);
        await dbContext.Reports.Where(r => r.HiddenBy == user.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.HiddenBy, (string?)null), ct);
        await DeletePrivatePhotos(user.Id, ct);
        await dbContext.MediaItems.Where(m => m.UploadedBy == user.Id).ExecuteUpdateAsync(m => m.SetProperty(x => x.UploadedBy, (string?)null), ct);

        return AccountErrors.From(await users.DeleteAsync(user));
    }

    private async Task DeletePrivatePhotos(string userId, CancellationToken ct)
    {
        var photos = await dbContext.MediaItems.Where(m => m.UploadedBy == userId).Select(m => new { m.Id, m.IsPublic }).ToListAsync(ct);
        foreach (var photo in photos)
        {
            await media.DeletePending(photo.Id, ct);
        }

        var privateIds = photos.Where(p => !p.IsPublic).Select(p => p.Id).ToList();
        await dbContext.MediaItems.Where(m => privateIds.Contains(m.Id)).ExecuteDeleteAsync(ct);
    }
}
