using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media.Commands;

/// <summary>
/// Ändert Fotograf, Tiefe, Legende und Alternativtext eines Fotos. Wer hochgeladen hat, darf das, solange das Foto privat ist.
/// Ein öffentliches Foto ändert nur die Moderation. Diese Angaben sind nicht Teil der Version.
/// </summary>
public record UpdateMediaCommand(string EditorId, Guid MediaId, string Photographer, string? PhotographerUrl, int? Depth, IReadOnlyList<MediaText> Texts);

internal class UpdateMediaHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : ICommandHandler<UpdateMediaCommand, UploadMediaResult>
{
    public async Task<UploadMediaResult> Handle(UpdateMediaCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.EditorId);
        if (!rights.CanWrite)
        {
            return new UploadMediaResult([EditorialError.Blocked]);
        }

        var item = await dbContext.MediaItems.Include(m => m.Translations).SingleOrDefaultAsync(m => m.Id == command.MediaId, ct);
        if (item is null)
        {
            return new UploadMediaResult([EditorialError.NotFound]);
        }

        if (!rights.IsModerator && (item.IsPublic || item.UploadedBy != command.EditorId))
        {
            return new UploadMediaResult([EditorialError.NotAllowed]);
        }

        if (string.IsNullOrWhiteSpace(command.Photographer) || command.Photographer.Trim().Length > 120 || command.PhotographerUrl?.Trim().Length > 300)
        {
            return new UploadMediaResult([EditorialError.InvalidPhotographer]);
        }

        if (command.Depth is < 0 or > 200 || !MediaTexts.AreValid(command.Texts))
        {
            return new UploadMediaResult([EditorialError.InvalidMedia]);
        }

        item.Photographer = command.Photographer.Trim();
        item.PhotographerUrl = string.IsNullOrWhiteSpace(command.PhotographerUrl) ? null : command.PhotographerUrl.Trim();
        item.Depth = command.Depth;
        item.Translations.Clear();
        item.Translations.AddRange(MediaTexts.ToEntities(item.Id, command.Texts));
        await dbContext.SaveChangesAsync(ct);
        return new UploadMediaResult([], item.Id);
    }
}
