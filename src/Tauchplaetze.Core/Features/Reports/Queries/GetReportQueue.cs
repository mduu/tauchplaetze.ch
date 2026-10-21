using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Reports.Queries;

/// <summary>
/// Meldungen für die Moderation: zuerst die Kommentare, die auf Freigabe warten (älteste zuerst), dann die neuesten Meldungen.
/// <c>null</c> ohne Recht zur Moderation.
/// </summary>
public record GetReportQueueQuery(string ModeratorId);

/// <summary>Was die Moderation sieht.</summary>
public record ReportQueue(IReadOnlyList<QueuedReport> PendingComments, IReadOnlyList<QueuedReport> Recent);

internal class GetReportQueueHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : IQueryHandler<GetReportQueueQuery, ReportQueue?>
{
    private const int RecentCount = 50;

    public async Task<ReportQueue?> Handle(GetReportQueueQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.ModeratorId)).CanModerate)
        {
            return null;
        }

        var pending = await Rows(dbContext.Reports.Where(r => r.CommentStatus == ReportCommentStatus.Pending && r.HiddenAt == null), ct);
        var recentIds = await dbContext.Reports.OrderByDescending(r => r.UpdatedAt).Take(RecentCount).Select(r => r.Id).ToListAsync(ct);
        var recent = await Rows(dbContext.Reports.Where(r => recentIds.Contains(r.Id)), ct);
        return new ReportQueue([.. pending.OrderBy(r => r.UpdatedAt)], [.. recent.OrderByDescending(r => r.UpdatedAt)]);
    }

    private async Task<IReadOnlyList<QueuedReport>> Rows(IQueryable<Data.Entities.Report> reports, CancellationToken ct) =>
        await reports.AsNoTracking()
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (r, d) => new QueuedReport(
                r.Id, r.DiveSiteId, d.Name, d.Slug, r.Date, r.Visibility, r.Temperature, r.Comment, r.CommentStatus,
                dbContext.Users.Where(u => u.Id == r.AuthorId).Select(u => u.DisplayName).FirstOrDefault(),
                r.UpdatedAt, r.HiddenAt != null))
            .ToListAsync(ct);
}
