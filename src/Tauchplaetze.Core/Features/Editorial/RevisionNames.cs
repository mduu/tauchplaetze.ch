using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Eine Version in einer Liste. <see cref="AuthorName"/> und <see cref="ReviewerName"/> sind <c>null</c>, wenn sie nicht gezeigt werden dürfen.</summary>
public record RevisionSummary(
    Guid Id,
    int Number,
    int? BaseNumber,
    RevisionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? Comment,
    string? ReviewComment,
    string? AuthorId,
    string? AuthorName,
    string? ReviewerName,
    bool IsRedacted);

/// <summary>
/// Namen von Autor und Approver. Im CMS immer, öffentlich nur mit Einwilligung (`cms.md`, «Datenschutz in der History»).
/// Ein gelöschtes Konto hat keinen Namen.
/// </summary>
internal class RevisionNames(TauchplaetzeDbContext dbContext)
{
    public async Task<IReadOnlyList<RevisionSummary>> Summaries(IReadOnlyList<DiveSiteRevision> revisions, bool publicView, CancellationToken ct)
    {
        var ids = revisions.SelectMany(r => new[] { r.AuthorId, r.ReviewerId }).OfType<string>().Distinct().ToList();
        var users = await dbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.ShowNamePublicly })
            .ToDictionaryAsync(u => u.Id, ct);

        string? Name(string? id) => id is not null && users.TryGetValue(id, out var user) && (!publicView || user.ShowNamePublicly) ? user.DisplayName : null;

        return revisions.Select(r => new RevisionSummary(
                r.Id, r.Number, r.BaseNumber, r.Status, r.CreatedAt, r.ReviewedAt, r.Comment, publicView ? null : r.ReviewComment,
                publicView ? null : r.AuthorId, Name(r.AuthorId), Name(r.ReviewerId), r.RedactedAt is not null))
            .ToList();
    }
}
