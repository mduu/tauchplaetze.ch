using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Susv.Queries;

/// <summary>Vorschlag für einen neuen Platz aus einem Eintrag des SUSV, für die Schnellerfassung (Story 002, «Platz anlegen»).</summary>
public record GetSusvSiteDraftQuery(string UserId, Guid EntryId);

/// <summary>
/// Name aus dem Titel der Quelle (ohne Vorspann wie «SUSV Defibrillator – »), Koordinate des Eintrags und ein Gewässer als Vorschlag.
/// Die Moderation prüft alles vor dem Speichern.
/// </summary>
public record SusvSiteDraft(string Name, double Latitude, double Longitude, int? WaterBodyId);

internal class GetSusvSiteDraftHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : IQueryHandler<GetSusvSiteDraftQuery, SusvSiteDraft?>
{
    /// <summary>Liegt ein Platz so nah, gilt sein Gewässer. Sonst der See mit dem nächsten Mittelpunkt: Der Mittelpunkt eines Flusses liegt oft nah am Ufer eines Sees. Unsicher bei grossen Seen.</summary>
    private const double NearbySiteMeters = 2000;

    public async Task<SusvSiteDraft?> Handle(GetSusvSiteDraftQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.UserId)).CanModerate)
        {
            return null;
        }

        var entry = await dbContext.SusvEntries
            .AsNoTracking()
            .Where(e => e.Id == query.EntryId)
            .Select(e => new { e.Location, Title = e.Translations.Where(t => t.Culture == "de").Select(t => t.Title).FirstOrDefault() })
            .SingleOrDefaultAsync(ct);
        if (entry is null)
        {
            return null;
        }

        var waterBody = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Status == DiveSiteStatus.Published && d.Location.IsWithinDistance(entry.Location, NearbySiteMeters))
            .OrderBy(d => d.Location.Distance(entry.Location))
            .Select(d => (int?)d.WaterBodyId)
            .FirstOrDefaultAsync(ct)
            ?? await dbContext.WaterBodies
                .AsNoTracking()
                .Where(w => w.IsActive && w.WaterBodyType == WaterBodyType.Lake)
                .OrderBy(w => w.Centroid.Distance(entry.Location))
                .Select(w => (int?)w.Id)
                .FirstOrDefaultAsync(ct);

        return new SusvSiteDraft(SiteName(entry.Title ?? ""), entry.Location.Y, entry.Location.X, waterBody);
    }

    /// <summary>Titel wie «SUSV Defibrillator – Riedtli, Bodensee» nennen den Platz nach dem Gedankenstrich.</summary>
    internal static string SiteName(string title)
    {
        var dash = title.LastIndexOf(" – ", StringComparison.Ordinal);
        return (dash >= 0 ? title[(dash + 3)..] : title).Trim();
    }
}
