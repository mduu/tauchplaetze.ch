using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Reports.Queries;

/// <summary>
/// Die Meldungen der letzten 14 Tage für die Platzseite, neueste zuerst, höchstens 5. Ausgeblendete fehlen.
/// Den Kommentar sehen alle erst nach Freigabe, vorher nur die Person, die gemeldet hat, und die Moderation.
/// </summary>
public record GetSiteReportsQuery(Guid DiveSiteId, string? ViewerId = null);

internal class GetSiteReportsHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, TimeProvider time) : IQueryHandler<GetSiteReportsQuery, IReadOnlyList<SiteReport>>
{
    public async Task<IReadOnlyList<SiteReport>> Handle(GetSiteReportsQuery query, CancellationToken ct = default)
    {
        var since = SwissTime.Today(time).AddDays(1 - ReportRules.ShownDays);
        var reports = await dbContext.Reports.AsNoTracking()
            .Where(r => r.DiveSiteId == query.DiveSiteId && r.Date >= since && r.HiddenAt == null)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.UpdatedAt)
            .Take(ReportRules.ShownCount)
            .Select(r => new
            {
                r.Id, r.Date, r.Visibility, r.Temperature, r.TemperatureDepth, r.Comment, r.CommentStatus, r.AuthorId, r.ForecastIndex,
                Name = dbContext.Users.Where(u => u.Id == r.AuthorId && u.ShowNamePublicly).Select(u => u.DisplayName).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var moderator = query.ViewerId is not null && (await roles.Of(query.ViewerId)).CanModerate;
        return reports.Select(r =>
            {
                var own = query.ViewerId is not null && r.AuthorId == query.ViewerId;
                var comment = r.CommentStatus == ReportCommentStatus.Approved || own || moderator ? r.Comment : null;
                return new SiteReport(r.Id, r.Date, r.Visibility, r.Temperature, r.TemperatureDepth, comment, r.CommentStatus, r.Name, own, r.ForecastIndex);
            })
            .ToList();
    }
}
