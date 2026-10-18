using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Informiert die Autorin oder den Autor über Freigabe oder Ablehnung (`cms.md`, «Ablauf»). Nicht bei eigenen Änderungen.</summary>
internal class ReviewMails(UserManager<User> users, IMailSender mail, IOptions<PublicSiteOptions> site)
{
    public async Task Send(DiveSiteRevision revision, string siteName, string key, string detail, CancellationToken ct)
    {
        if (revision.AuthorId is null || revision.AuthorId == revision.ReviewerId || await users.FindByIdAsync(revision.AuthorId) is not { Email: { } email } author)
        {
            return;
        }

        await mail.Send(AccountMails.Create(email, author.PreferredCulture, key, author.DisplayName, siteName, detail), ct);
    }

    /// <summary>Link auf einen Platz. Die Web-App leitet `/{lang}/site/{slug}` auf den Pfad der Sprache weiter.</summary>
    public string SiteLink(string culture, string slug) => site.Value.Link($"/{culture}/site/{slug}");
}
