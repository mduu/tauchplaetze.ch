using System.Linq.Expressions;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Regeln für Hinweise zum Platz (`dive-site.md`, «Hinweise»).</summary>
public static class NoticeRules
{
    /// <summary>Höchstlänge eines Hinweistexts pro Sprache.</summary>
    public const int TextLength = 500;

    /// <summary>Ein Hinweis gilt vom Tag «ab» bis und mit dem Tag «bis». Ein fehlendes Datum lässt die Seite offen.</summary>
    internal static Expression<Func<DiveSiteNotice, bool>> ActiveOn(DateOnly day) =>
        n => (n.ValidFrom == null || n.ValidFrom <= day) && (n.ValidUntil == null || n.ValidUntil >= day);

    /// <summary>Am Tag gesperrt: ein gültiger Hinweis der Art <see cref="NoticeKind.Closure"/>.</summary>
    internal static Expression<Func<DiveSiteNotice, bool>> ClosureOn(DateOnly day) =>
        n => n.Kind == NoticeKind.Closure && (n.ValidFrom == null || n.ValidFrom <= day) && (n.ValidUntil == null || n.ValidUntil >= day);
}
