using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Media.Queries;

/// <summary>
/// Die Fotos eines Platzes für das CMS: alle öffentlichen, dazu die privaten der anfragenden Person, für die Moderation alle.
/// <paramref name="PendingPathTemplate"/> ist der Pfad des geschützten Endpunkts mit `{id}` und `{file}`.
/// </summary>
public record GetSiteMediaQuery(string RequesterId, Guid DiveSiteId, string PendingPathTemplate);

internal class GetSiteMediaHandler(TauchplaetzeDbContext dbContext, IMediaStore store, AccountRoles roles) : IQueryHandler<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>
{
    public async Task<IReadOnlyList<SiteMediaItem>> Handle(GetSiteMediaQuery query, CancellationToken ct = default)
    {
        var rights = await roles.Of(query.RequesterId);
        if (!rights.Exists)
        {
            return [];
        }

        var items = await dbContext.MediaItems
            .AsNoTracking()
            .Include(m => m.Translations)
            .Where(m => m.DiveSiteId == query.DiveSiteId && (m.IsPublic || rights.IsModerator || m.UploadedBy == query.RequesterId))
            .OrderBy(m => m.UploadedAt)
            .ToListAsync(ct);

        return [.. items.Select(m => new SiteMediaItem(
            m.Id,
            m.Section,
            m.Width,
            m.Height,
            m.TakenAt,
            m.Depth,
            m.Photographer,
            m.PhotographerUrl,
            m.IsPublic,
            m.UploadedBy == query.RequesterId,
            m.UploadedAt,
            [.. MediaFiles.WidthsFor(m.Width).Select(w => new MediaRendition(w, m.IsPublic
                ? store.PublicUri(m.Id, MediaFiles.Rendition(w)).ToString()
                : query.PendingPathTemplate.Replace("{id}", m.Id.ToString()).Replace("{file}", MediaFiles.Rendition(w))))],
            [.. m.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal).Select(t => new MediaText(t.Culture.Trim(), t.Caption, t.AltText))]))];
    }
}
