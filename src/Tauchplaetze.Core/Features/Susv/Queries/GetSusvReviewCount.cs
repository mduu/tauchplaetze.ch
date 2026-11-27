using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Susv.Queries;

/// <summary>Zahl der Einträge, die die Moderation ansehen soll: offen oder geändert seit der Prüfung, nicht entfernt. Für die Navigation. Ohne Recht 0.</summary>
public record GetSusvReviewCountQuery(string UserId);

internal class GetSusvReviewCountHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : IQueryHandler<GetSusvReviewCountQuery, int>
{
    public async Task<int> Handle(GetSusvReviewCountQuery query, CancellationToken ct = default) =>
        (await roles.Of(query.UserId)).CanModerate
            ? await dbContext.SusvEntries.CountAsync(e => e.RemovedAt == null
                && (e.ReviewStatus == SusvReviewStatus.Pending || (e.ReviewStatus == SusvReviewStatus.Confirmed && e.ChangedSinceReview)), ct)
            : 0;
}
