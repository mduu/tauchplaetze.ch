using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Media.Queries;

/// <summary>Eine Bildgrösse eines privaten Fotos für die Vorschau im CMS. Nur für die Person, die hochgeladen hat, und die Moderation.</summary>
public record GetPendingMediaFileQuery(string RequesterId, Guid MediaId, string File);

/// <summary>Inhalt und MIME-Type einer Datei.</summary>
public record MediaFile(byte[] Content, string ContentType);

internal class GetPendingMediaFileHandler(TauchplaetzeDbContext dbContext, IMediaStore store, AccountRoles roles) : IQueryHandler<GetPendingMediaFileQuery, MediaFile?>
{
    public async Task<MediaFile?> Handle(GetPendingMediaFileQuery query, CancellationToken ct = default)
    {
        var item = await dbContext.MediaItems.AsNoTracking().SingleOrDefaultAsync(m => m.Id == query.MediaId, ct);
        if (item is null || !MediaFiles.WidthsFor(item.Width).Select(MediaFiles.Rendition).Contains(query.File))
        {
            return null;
        }

        var rights = await roles.Of(query.RequesterId);
        if (!rights.Exists || (!rights.IsModerator && item.UploadedBy != query.RequesterId))
        {
            return null;
        }

        var content = await store.LoadPending(item.Id, query.File, ct);
        return content is null ? null : new MediaFile(content, MediaFiles.ContentTypeOf(query.File));
    }
}
