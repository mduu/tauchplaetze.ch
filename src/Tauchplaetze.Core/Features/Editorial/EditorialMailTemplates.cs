using System.Globalization;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Modell: Ein Vorschlag wartet auf Freigabe (an Moderation und Admins).</summary>
internal record ProposalPendingMail(string RecipientName, string AuthorName, string SiteName, bool IsNewSite, string? Comment, string ReviewLink);

/// <summary>Modell: Der Vorschlag ist freigegeben (an die Autorin oder den Autor).</summary>
internal record ProposalApprovedMail(string RecipientName, string SiteName, string SiteLink);

/// <summary>Modell: Der Vorschlag ist abgelehnt (an die Autorin oder den Autor).</summary>
internal record ProposalRejectedMail(string RecipientName, string SiteName, string? Reason);

/// <summary>Modell: Die Moderation hat einen Platz direkt geändert (an Admins).</summary>
internal record ModeratorChangedMail(string RecipientName, string ModeratorName, string SiteName, int Version, string VersionsLink);

internal class ProposalPendingTemplate : MailTemplate<ProposalPendingMail>
{
    public override string Name => "ProposalPending";

    protected override MailContent Render(ProposalPendingMail model, CultureInfo culture) => new(
        T(culture, "Pending_Subject", model.SiteName),
        T(culture, "Greeting", model.RecipientName),
        [T(culture, model.IsNewSite ? "Pending_NewSite" : "Pending_Text", model.AuthorName, model.SiteName),
            .. model.Comment is { Length: > 0 } ? [T(culture, "Pending_Comment")] : Array.Empty<string>()],
        new MailButton(T(culture, "Pending_Button"), model.ReviewLink),
        Quote: model.Comment is { Length: > 0 } comment ? comment : null);
}

internal class ProposalApprovedTemplate : MailTemplate<ProposalApprovedMail>
{
    public override string Name => "ProposalApproved";

    protected override MailContent Render(ProposalApprovedMail model, CultureInfo culture) => new(
        T(culture, "Approved_Subject", model.SiteName),
        T(culture, "Greeting", model.RecipientName),
        [T(culture, "Approved_Text", model.SiteName)],
        new MailButton(T(culture, "Approved_Button"), model.SiteLink));
}

internal class ProposalRejectedTemplate : MailTemplate<ProposalRejectedMail>
{
    public override string Name => "ProposalRejected";

    protected override MailContent Render(ProposalRejectedMail model, CultureInfo culture) => new(
        T(culture, "Rejected_Subject", model.SiteName),
        T(culture, "Greeting", model.RecipientName),
        [T(culture, "Rejected_Text", model.SiteName)],
        Quote: model.Reason is { Length: > 0 } reason ? reason : T(culture, "Rejected_NoReason"),
        AfterButton: [T(culture, "Rejected_Thanks")]);
}

internal class ModeratorChangedTemplate : MailTemplate<ModeratorChangedMail>
{
    public override string Name => "ModeratorChanged";

    protected override MailContent Render(ModeratorChangedMail model, CultureInfo culture) => new(
        T(culture, "ModeratorChanged_Subject", model.SiteName),
        T(culture, "Greeting", model.RecipientName),
        [T(culture, "ModeratorChanged_Text", model.ModeratorName, model.SiteName, model.Version)],
        new MailButton(T(culture, "ModeratorChanged_Button"), model.VersionsLink),
        AfterButton: [T(culture, "ModeratorChanged_Why")]);
}
