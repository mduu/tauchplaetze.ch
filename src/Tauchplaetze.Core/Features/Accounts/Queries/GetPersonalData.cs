using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;

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
    IReadOnlyList<RevisionReference> Reviews,
    IReadOnlyList<PhotoReference>? Photos = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? LastSignInAt = null,
    IReadOnlyList<ReportReference>? Reports = null)
{
    [JsonIgnore]
    public IReadOnlyList<PhotoReference> PhotoList => Photos ?? [];

    [JsonIgnore]
    public IReadOnlyList<ReportReference> ReportList => Reports ?? [];
}

/// <summary>Eine eigene Meldung mit Platz, Datum, Werten und Kommentar.</summary>
public record ReportReference(string SiteSlug, string SiteName, DateOnly Date, decimal Visibility, decimal? Temperature, int? TemperatureDepth, string? Comment, bool IsHidden);

/// <summary>Ein hochgeladenes Foto mit Platz, Fotograf, Datum, Legenden und ob es öffentlich ist.</summary>
public record PhotoReference(Guid Id, string SiteSlug, string SiteName, string Photographer, string? PhotographerUrl, int? Depth, DateTimeOffset UploadedAt, bool IsPublic, IReadOnlyList<MediaText> Texts);

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
            reviews,
            await PhotosOf(user.Id, ct),
            user.CreatedAt,
            user.LastSignInAt,
            await ReportsOf(user.Id, ct));
    }

    private async Task<IReadOnlyList<PhotoReference>> PhotosOf(string userId, CancellationToken ct)
    {
        var photos = await dbContext.MediaItems
            .AsNoTracking()
            .Include(m => m.Translations)
            .Where(m => m.UploadedBy == userId)
            .Join(dbContext.DiveSites, m => m.DiveSiteId, d => d.Id, (m, d) => new { Media = m, d.Slug, d.Name })
            .OrderByDescending(m => m.Media.UploadedAt)
            .ToListAsync(ct);

        return [.. photos.Select(p => new PhotoReference(
            p.Media.Id, p.Slug, p.Name, p.Media.Photographer, p.Media.PhotographerUrl, p.Media.Depth, p.Media.UploadedAt, p.Media.IsPublic,
            [.. p.Media.Translations.OrderBy(t => t.Culture, StringComparer.Ordinal).Select(t => new MediaText(t.Culture.Trim(), t.Caption, t.AltText))]))];
    }

    private async Task<IReadOnlyList<ReportReference>> ReportsOf(string userId, CancellationToken ct) =>
        await dbContext.Reports
            .AsNoTracking()
            .Where(r => r.AuthorId == userId)
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (r, d) => new { d.Slug, d.Name, Report = r })
            .OrderByDescending(r => r.Report.Date)
            .Select(r => new ReportReference(r.Slug, r.Name, r.Report.Date, r.Report.Visibility, r.Report.Temperature, r.Report.TemperatureDepth, r.Report.Comment, r.Report.HiddenAt != null))
            .ToListAsync(ct);

    private async Task<IReadOnlyList<RevisionReference>> References(System.Linq.Expressions.Expression<Func<DiveSiteRevision, bool>> filter, CancellationToken ct) =>
        await dbContext.DiveSiteRevisions
            .AsNoTracking()
            .Where(filter)
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (r, d) => new { d.Slug, d.Name, r.Number, r.Status, At = r.ReviewedAt ?? r.CreatedAt, r.Comment })
            .OrderByDescending(r => r.At)
            .Select(r => new RevisionReference(r.Slug, r.Name, r.Number, r.Status, r.At, r.Comment))
            .ToListAsync(ct);
}
