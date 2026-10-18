using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>
/// Macht die Fotos einer freigegebenen Version öffentlich und übernimmt ihre Zuordnung (`cms.md`, «Medien»).
/// Ein Foto, das schon öffentlich ist, wird nicht nochmals kopiert. Fotos, die die Version nicht verwendet, verlieren ihren Platz in der Galerie.
/// </summary>
internal class MediaPublisher(TauchplaetzeDbContext dbContext, IMediaStore store)
{
    /// <summary>
    /// Der Inhalt ohne Fotos, die es nicht mehr gibt (gelöscht). Ein Titelbild, das fehlt, fällt weg.
    /// So lässt sich auch eine alte Version mit gelöschten Fotos freigeben oder wiederherstellen.
    /// </summary>
    public async Task<DiveSiteContent> WithoutDeleted(DiveSiteContent content, CancellationToken ct)
    {
        var ids = content.MediaList.Select(m => m.MediaId).ToList();
        var existing = await dbContext.MediaItems.Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
        return content with
        {
            Media = [.. content.MediaList.Where(m => existing.Contains(m.MediaId))],
            HeroMediaId = content.HeroMediaId is { } hero && existing.Contains(hero) ? hero : null,
        };
    }

    public async Task Publish(Guid diveSiteId, IReadOnlyList<MediaUse> uses, CancellationToken ct)
    {
        var items = await dbContext.MediaItems.Where(m => m.DiveSiteId == diveSiteId).ToListAsync(ct);
        var positions = uses.Select((use, index) => (use, index)).ToDictionary(u => u.use.MediaId);

        foreach (var item in items)
        {
            if (!positions.TryGetValue(item.Id, out var position))
            {
                item.SortOrder = null;
                continue;
            }

            if (!item.IsPublic)
            {
                await store.Publish(item.Id, [.. MediaFiles.WidthsFor(item.Width).Select(MediaFiles.Rendition)], ct);
                item.IsPublic = true;
            }

            item.Section = position.use.Section;
            item.EntryPointId = position.use.EntryPointId;
            item.SortOrder = position.index;
        }
    }
}
