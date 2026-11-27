using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Susv;

/// <summary>Eine Mail pro Lauf an Moderation und Admins, wenn Einträge des SUSV neu, geändert oder entfernt sind (`cms.md`, «Benachrichtigungen»).</summary>
internal class SusvNotifications(UserManager<User> users, MailDispatcher mail, IOptions<PublicSiteOptions> site)
{
    public async Task ImportChanged(IReadOnlyList<string> added, IReadOnlyList<string> changed, IReadOnlyList<string> removed, CancellationToken ct)
    {
        if (added.Count + changed.Count + removed.Count == 0)
        {
            return;
        }

        var reviewers = (await users.GetUsersInRoleAsync(Roles.Moderator)).Concat(await users.GetUsersInRoleAsync(Roles.Admin)).DistinctBy(u => u.Id);
        foreach (var reviewer in reviewers.Where(r => r is { EmailConfirmed: true, BlockedAt: null, Email: not null }))
        {
            await mail.Enqueue(reviewer.Email!, reviewer.PreferredCulture,
                new SusvImportMail(reviewer.DisplayName, [.. added], [.. changed], [.. removed], site.Value.Link("/admin/susv")), ct);
        }
    }
}

/// <summary>Modell: Der Import des SUSV hat Einträge gefunden, die die Moderation prüfen soll.</summary>
internal record SusvImportMail(string RecipientName, List<string> Added, List<string> Changed, List<string> Removed, string ReviewLink);

internal class SusvImportTemplate : MailTemplate<SusvImportMail>
{
    /// <summary>So viele Titel nennt die Mail pro Gruppe. Der Rest steht auf der Seite.</summary>
    private const int MaxTitles = 15;

    public override string Name => "SusvImport";

    protected override MailContent Render(SusvImportMail model, CultureInfo culture) => new(
        T(culture, "Susv_Subject", model.Added.Count + model.Changed.Count + model.Removed.Count),
        T(culture, "Greeting", model.RecipientName),
        [
            T(culture, "Susv_Text"),
            .. Group(culture, "Susv_Added", model.Added),
            .. Group(culture, "Susv_Changed", model.Changed),
            .. Group(culture, "Susv_Removed", model.Removed),
        ],
        new MailButton(T(culture, "Susv_Button"), model.ReviewLink));

    private static IEnumerable<string> Group(CultureInfo culture, string key, List<string> titles)
    {
        if (titles.Count == 0)
        {
            yield break;
        }

        var named = string.Join(", ", titles.Take(MaxTitles).Select(t => $"«{t}»"));
        yield return titles.Count > MaxTitles
            ? T(culture, key, titles.Count, named) + " " + T(culture, "Susv_More", titles.Count - MaxTitles)
            : T(culture, key, titles.Count, named);
    }
}
