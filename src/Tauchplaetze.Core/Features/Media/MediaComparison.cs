using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>Wie sich ein Foto von einer Version zur nächsten verändert hat (`cms.md`, «Vorschläge prüfen»).</summary>
public enum PhotoChange
{
    /// <summary>Gleich wie in der Basis.</summary>
    Unchanged,

    /// <summary>Neu in dieser Version.</summary>
    Added,

    /// <summary>Anderer Abschnitt, Einstieg, Platz in der Reihenfolge oder Titelbild.</summary>
    Changed,

    /// <summary>Die Basis verwendete es, diese Version nicht mehr.</summary>
    Removed,
}

/// <summary>Vergleicht die Fotos zweier Inhalte. Zuerst die Fotos der neuen Version in ihrer Reihenfolge, danach die entfernten.</summary>
internal static class MediaComparison
{
    public static IReadOnlyList<(MediaUse Use, bool IsHero, PhotoChange Change)> Between(DiveSiteContent? old, DiveSiteContent current)
    {
        var before = old?.MediaList.Select((use, index) => (use, index)).ToDictionary(x => x.use.MediaId) ?? [];
        var after = current.MediaList.Select((use, index) => (Use: use, IsHero: use.MediaId == current.HeroMediaId, Change: ChangeOf(use, index)));
        var removed = (old?.MediaList ?? [])
            .Where(use => current.MediaList.All(u => u.MediaId != use.MediaId))
            .Select(use => (Use: use, IsHero: use.MediaId == old!.HeroMediaId, Change: PhotoChange.Removed));
        return [.. after.Concat(removed)];

        PhotoChange ChangeOf(MediaUse use, int index)
        {
            if (!before.TryGetValue(use.MediaId, out var previous))
            {
                return old is null ? PhotoChange.Unchanged : PhotoChange.Added;
            }

            var heroChanged = (use.MediaId == old!.HeroMediaId) != (use.MediaId == current.HeroMediaId);
            return previous.use != use || previous.index != index || heroChanged ? PhotoChange.Changed : PhotoChange.Unchanged;
        }
    }
}
