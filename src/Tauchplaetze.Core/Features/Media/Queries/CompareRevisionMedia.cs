using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media.Queries;

/// <summary>
/// Die Fotos von Version <paramref name="To"/> im Vergleich zu <paramref name="From"/>, mit Vorschaubild für die Freigabe.
/// Ein privates Foto hat ein Vorschaubild nur für die Moderation und die Person, die es hochgeladen hat.
/// <paramref name="PendingPathTemplate"/> ist der Pfad des geschützten Endpunkts mit `{id}` und `{file}`.
/// </summary>
public record CompareRevisionMediaQuery(string RequesterId, Guid DiveSiteId, int? From, int To, string PendingPathTemplate);

/// <summary>Ein Foto im Vergleich. Ohne <see cref="Photographer"/>: Das Foto ist inzwischen gelöscht.</summary>
public record RevisionPhoto(Guid MediaId, PhotoChange Change, MediaSection Section, bool IsHero, string? Photographer, string? Caption, string? ThumbnailUrl);

internal class CompareRevisionMediaHandler(TauchplaetzeDbContext dbContext, RevisionWriter revisions, IMediaStore store, AccountRoles roles)
    : IQueryHandler<CompareRevisionMediaQuery, IReadOnlyList<RevisionPhoto>>
{
    public async Task<IReadOnlyList<RevisionPhoto>> Handle(CompareRevisionMediaQuery query, CancellationToken ct = default)
    {
        var rights = await roles.Of(query.RequesterId);
        var wanted = new[] { query.From, query.To }.OfType<int>().ToList();
        var found = await dbContext.DiveSiteRevisions.AsNoTracking()
            .Where(r => r.DiveSiteId == query.DiveSiteId && wanted.Contains(r.Number))
            .ToDictionaryAsync(r => r.Number, ct);
        if (!rights.Exists || !found.TryGetValue(query.To, out var to) || (query.From is { } from && !found.ContainsKey(from)))
        {
            return [];
        }

        var old = query.From is { } number ? await revisions.Load(found[number], ct) : null;
        var compared = MediaComparison.Between(old, await revisions.Load(to, ct));
        var ids = compared.Select(c => c.Use.MediaId).ToList();
        var items = await dbContext.MediaItems.AsNoTracking().Include(m => m.Translations).Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);

        return [.. compared.Select(c =>
        {
            var item = items.GetValueOrDefault(c.Use.MediaId);
            var file = item is null ? null : MediaFiles.Rendition(MediaFiles.WidthsFor(item.Width)[0]);
            var thumbnail = item switch
            {
                null => null,
                { IsPublic: true } => store.PublicUri(item.Id, file!).ToString(),
                _ when rights.IsModerator || item.UploadedBy == query.RequesterId => query.PendingPathTemplate.Replace("{id}", item.Id.ToString()).Replace("{file}", file),
                _ => null,
            };
            var caption = item?.Translations.FirstOrDefault(t => t.Culture.Trim() == "de")?.Caption ?? item?.Translations.Select(t => t.Caption).FirstOrDefault(t => t is not null);
            return new RevisionPhoto(c.Use.MediaId, c.Change, c.Use.Section, c.IsHero, item?.Photographer, caption, thumbnail);
        })];
    }
}
