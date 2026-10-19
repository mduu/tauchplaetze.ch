using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Editorial.Queries;

/// <summary>
/// Die eigenen Vorschläge einer Person, neuste zuerst (`cms.md`, «Was Mitwirkende sehen»). Eigene Änderungen, die sie selbst
/// freigegeben hat (Moderation), sind keine Vorschläge. Optional nur ein Platz oder nur wartende.
/// </summary>
public record GetMyProposalsQuery(string UserId, Guid? DiveSiteId = null, bool PendingOnly = false);

/// <summary>Ein eigener Vorschlag mit Status und, bei Ablehnung, der Begründung.</summary>
public record MyProposal(
    Guid RevisionId,
    Guid DiveSiteId,
    string SiteName,
    int Number,
    RevisionStatus Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? Comment,
    string? ReviewComment);

internal class GetMyProposalsHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetMyProposalsQuery, IReadOnlyList<MyProposal>>
{
    public async Task<IReadOnlyList<MyProposal>> Handle(GetMyProposalsQuery query, CancellationToken ct = default)
    {
        var revisions = dbContext.DiveSiteRevisions
            .AsNoTracking()
            .Where(r => r.AuthorId == query.UserId && (r.ReviewerId == null || r.ReviewerId != query.UserId));
        if (query.DiveSiteId is { } siteId)
        {
            revisions = revisions.Where(r => r.DiveSiteId == siteId);
        }

        if (query.PendingOnly)
        {
            revisions = revisions.Where(r => r.Status == RevisionStatus.Pending);
        }

        return await revisions
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Number)
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (r, d) => new MyProposal(
                r.Id, d.Id, d.Name, r.Number, r.Status, r.CreatedAt, r.ReviewedAt, r.Comment, r.ReviewComment))
            .ToListAsync(ct);
    }
}
