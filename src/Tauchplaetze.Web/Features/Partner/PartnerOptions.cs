namespace Tauchplaetze.Web.Features.Partner;

/// <summary>
/// Seite «Partner» (Abschnitt `Partner`). Ausgeblendet, bis der SUSV den Wortlaut freigibt (Entscheid Marc, 27.11.2026):
/// Die Seite antwortet mit 404 und fehlt im Fuss, in der Sitemap und in den Links anderer Seiten.
/// </summary>
public class PartnerOptions
{
    public const string Section = "Partner";

    public bool Visible { get; set; }
}
