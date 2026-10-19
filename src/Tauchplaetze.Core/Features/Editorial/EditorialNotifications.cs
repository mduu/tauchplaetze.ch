using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Schaltet die Benachrichtigungen der Redaktion (`Notifications`, `cms.md`, «Benachrichtigungen»).</summary>
public class NotificationOptions
{
    public const string Section = "Notifications";

    /// <summary>Admins erfahren von jeder Änderung der Moderation ohne Freigabe. Gedacht für die Anfangsphase.</summary>
    public bool NotifyAdminsOnModeratorChanges { get; set; } = true;
}

/// <summary>
/// Wer bei welchem Ereignis eine Mail bekommt (`cms.md`, «Benachrichtigungen»). Nie die Person, die gehandelt hat,
/// und nur aktive Konten: bestätigt, nicht gesperrt. Die Mails gehen in die Queue, nach dem Speichern.
/// </summary>
internal class EditorialNotifications(UserManager<User> users, MailDispatcher mail, IOptions<PublicSiteOptions> site, IOptions<NotificationOptions> options)
{
    /// <summary>Ein Vorschlag von Mitwirkenden wartet: an Moderation und Admins.</summary>
    public async Task ProposalSubmitted(DiveSiteRevision revision, string siteName, bool isNewSite, CancellationToken ct)
    {
        var author = await users.FindByIdAsync(revision.AuthorId ?? "");
        var reviewers = (await users.GetUsersInRoleAsync(Roles.Moderator)).Concat(await users.GetUsersInRoleAsync(Roles.Admin)).DistinctBy(u => u.Id);
        foreach (var reviewer in reviewers.Where(r => IsActive(r) && r.Id != revision.AuthorId))
        {
            await mail.Enqueue(reviewer.Email!, reviewer.PreferredCulture,
                new ProposalPendingMail(reviewer.DisplayName, author?.DisplayName ?? "–", siteName, isNewSite, revision.Comment, site.Value.Link("/admin/vorschlaege")), ct);
        }
    }

    /// <summary>Freigegeben: an die Autorin oder den Autor, ausser sie hat selbst freigegeben.</summary>
    public async Task Approved(DiveSiteRevision revision, string siteName, string slug, CancellationToken ct)
    {
        if (await AuthorToInform(revision) is { } author)
        {
            await mail.Enqueue(author.Email!, author.PreferredCulture,
                new ProposalApprovedMail(author.DisplayName, siteName, site.Value.Link($"/{author.PreferredCulture}/site/{slug}")), ct);
        }
    }

    /// <summary>Abgelehnt: an die Autorin oder den Autor, mit Begründung.</summary>
    public async Task Rejected(DiveSiteRevision revision, string siteName, CancellationToken ct)
    {
        if (await AuthorToInform(revision) is { } author)
        {
            await mail.Enqueue(author.Email!, author.PreferredCulture, new ProposalRejectedMail(author.DisplayName, siteName, revision.ReviewComment), ct);
        }
    }

    /// <summary>Die Moderation hat ohne Freigabe geändert: an die übrigen Admins, solange eingeschaltet.</summary>
    public async Task ModeratorChanged(DiveSiteRevision revision, Guid siteId, string siteName, CancellationToken ct)
    {
        if (!options.Value.NotifyAdminsOnModeratorChanges)
        {
            return;
        }

        var moderator = await users.FindByIdAsync(revision.AuthorId ?? "");
        foreach (var admin in (await users.GetUsersInRoleAsync(Roles.Admin)).Where(a => IsActive(a) && a.Id != revision.AuthorId))
        {
            await mail.Enqueue(admin.Email!, admin.PreferredCulture,
                new ModeratorChangedMail(admin.DisplayName, moderator?.DisplayName ?? "–", siteName, revision.Number, site.Value.Link($"/admin/plaetze/{siteId}/versionen")), ct);
        }
    }

    private async Task<User?> AuthorToInform(DiveSiteRevision revision) =>
        revision.AuthorId is { } authorId && authorId != revision.ReviewerId && await users.FindByIdAsync(authorId) is { } author && IsActive(author)
            ? author
            : null;

    private static bool IsActive(User user) => user is { EmailConfirmed: true, BlockedAt: null, Email: not null };
}
