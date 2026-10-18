using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media.Commands;

/// <summary>
/// Löscht ein Foto endgültig: Eintrag und Dateien in beiden Containern (`cms.md`, «Medien»). Die Moderation darf das immer,
/// die Person, die hochgeladen hat, solange das Foto privat ist. Versionen, die es noch verwenden, zeigen es nicht mehr.
/// </summary>
public record DeleteMediaCommand(string EditorId, Guid MediaId);

internal class DeleteMediaHandler(TauchplaetzeDbContext dbContext, IMediaStore store, AccountRoles roles) : ICommandHandler<DeleteMediaCommand, UploadMediaResult>
{
    public async Task<UploadMediaResult> Handle(DeleteMediaCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.EditorId);
        if (!rights.CanWrite)
        {
            return new UploadMediaResult([EditorialError.Blocked]);
        }

        var item = await dbContext.MediaItems.SingleOrDefaultAsync(m => m.Id == command.MediaId, ct);
        if (item is null)
        {
            return new UploadMediaResult([EditorialError.NotFound]);
        }

        if (!rights.IsModerator && (item.IsPublic || item.UploadedBy != command.EditorId))
        {
            return new UploadMediaResult([EditorialError.NotAllowed]);
        }

        await store.DeletePublic(item.Id, ct);
        await store.DeletePending(item.Id, ct);
        await dbContext.DiveSites.Where(d => d.HeroMediaId == item.Id).ExecuteUpdateAsync(d => d.SetProperty(x => x.HeroMediaId, (Guid?)null), ct);
        dbContext.MediaItems.Remove(item);
        await dbContext.SaveChangesAsync(ct);
        return new UploadMediaResult([], item.Id);
    }
}
