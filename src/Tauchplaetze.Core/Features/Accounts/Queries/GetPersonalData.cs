using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Accounts.Queries;

/// <summary>Alle gespeicherten Daten eines Kontos (nDSG Art. 25 und 28, `cms.md`, «Auskunft und Herausgabe»). <c>null</c> für ein unbekanntes Konto.</summary>
public record GetPersonalDataQuery(string UserId);

/// <summary>Die Daten eines Kontos mit seinen Beiträgen und Freigaben.</summary>
public record PersonalData(
    string Email,
    string DisplayName,
    bool ShowNamePublicly,
    string PreferredCulture,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    DateTimeOffset? BlockedAt,
    IReadOnlyList<RevisionReference> Contributions,
    IReadOnlyList<RevisionReference> Reviews);

/// <summary>Eine Version mit Platz, Status, Datum und Notiz.</summary>
public record RevisionReference(string SiteSlug, string SiteName, int Number, RevisionStatus Status, DateTimeOffset At, string? Comment);

internal class GetPersonalDataHandler(TauchplaetzeDbContext dbContext, UserManager<User> users) : IQueryHandler<GetPersonalDataQuery, PersonalData?>
{
    public async Task<PersonalData?> Handle(GetPersonalDataQuery query, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(query.UserId);
        if (user is null)
        {
            return null;
        }

        var contributions = await References(r => r.AuthorId == user.Id, ct);
        var reviews = await References(r => r.ReviewerId == user.Id && r.AuthorId != user.Id, ct);

        return new PersonalData(
            user.Email ?? "",
            user.DisplayName,
            user.ShowNamePublicly,
            user.PreferredCulture,
            [.. await users.GetRolesAsync(user)],
            user.EmailConfirmed,
            user.BlockedAt,
            contributions,
            reviews);
    }

    private async Task<IReadOnlyList<RevisionReference>> References(System.Linq.Expressions.Expression<Func<DiveSiteRevision, bool>> filter, CancellationToken ct) =>
        await dbContext.DiveSiteRevisions
            .AsNoTracking()
            .Where(filter)
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (r, d) => new { d.Slug, d.Name, r.Number, r.Status, At = r.ReviewedAt ?? r.CreatedAt, r.Comment })
            .OrderByDescending(r => r.At)
            .Select(r => new RevisionReference(r.Slug, r.Name, r.Number, r.Status, r.At, r.Comment))
            .ToListAsync(ct);
}
