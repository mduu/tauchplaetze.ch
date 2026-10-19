using System.Globalization;
using System.Resources;
using System.Text;

namespace Tauchplaetze.Core.CrossCutting.Mail.Templates;

/// <summary>
/// Gemeinsames Layout aller Mails: Tabelle mit 600 px Breite und Inline-CSS, weil Mail-Programme kein Stylesheet lesen.
/// Hell statt dunkel wie die Website: Viele Programme kehren dunkle Mails sonst unlesbar um.
/// </summary>
internal static class MailLayout
{
    /// <summary>Farben aus `design-system.md` («Kaltwasser-Nacht»), für helle Mails abgedunkelt.</summary>
    private const string Ink = "#071417";
    private const string Brand = "#0E5A54";
    private const string Accent = "#FF9A4D";
    private const string Muted = "#5E6F6C";
    private const string Paper = "#F3F7F6";
    private const string Line = "#D8E3E1";

    private static readonly ResourceManager Texts = new("Tauchplaetze.Core.CrossCutting.Mail.MailTexts", typeof(MailLayout).Assembly);

    public static string Text(CultureInfo culture, string key, params object[] values) =>
        values.Length == 0 ? Texts.GetString(key, culture)! : string.Format(culture, Texts.GetString(key, culture)!, values);

    public static MailMessage Render(MailContent content, CultureInfo culture, string to, string siteUri, string? replyTo = null) =>
        new(to, content.Subject, PlainText(content, culture, siteUri), replyTo, Html(content, culture, siteUri));

    private static string PlainText(MailContent content, CultureInfo culture, string siteUri)
    {
        var text = new StringBuilder();
        text.Append(content.Greeting).Append("\n\n");
        foreach (var paragraph in content.Paragraphs)
        {
            text.Append(paragraph).Append("\n\n");
        }

        if (content.Quote is { } quote)
        {
            text.Append("> ").Append(quote.Replace("\n", "\n> ", StringComparison.Ordinal)).Append("\n\n");
        }

        if (content.Button is { } button)
        {
            text.Append(button.Text).Append(":\n").Append(button.Url).Append("\n\n");
        }

        foreach (var paragraph in content.AfterButton ?? [])
        {
            text.Append(paragraph).Append("\n\n");
        }

        text.Append("tauchplaetze.ch\n").Append(siteUri).Append("\n\n").Append(Text(culture, content.FooterKey));
        return text.ToString();
    }

    /// <summary>Maskiert nur die Zeichen mit Bedeutung in HTML. Umlaute bleiben lesbar, die Mail ist UTF-8.</summary>
    private static string Encode(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);

    private static string Html(MailContent content, CultureInfo culture, string siteUri)
    {
        static string E(string value) => Encode(value);
        static string Paragraph(string value) =>
            $"<p style=\"margin:0 0 16px;font-size:16px;line-height:1.55;color:{Ink};\">{E(value).Replace("\n", "<br>", StringComparison.Ordinal)}</p>";

        var body = new StringBuilder();
        body.Append(Paragraph(content.Greeting));
        foreach (var paragraph in content.Paragraphs)
        {
            body.Append(Paragraph(paragraph));
        }

        if (content.Quote is { } quote)
        {
            body.Append($"<blockquote style=\"margin:0 0 16px;padding:12px 16px;border-left:4px solid {Accent};background:{Paper};font-size:15px;line-height:1.5;color:{Ink};\">")
                .Append(E(quote).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</blockquote>");
        }

        if (content.Button is { } button)
        {
            body.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:8px 0 24px;\"><tr>")
                .Append($"<td style=\"border-radius:999px;background:{Brand};\">")
                .Append($"<a href=\"{E(button.Url)}\" style=\"display:inline-block;padding:12px 24px;font-size:16px;font-weight:600;color:#FFFFFF;text-decoration:none;border-radius:999px;\">{E(button.Text)}</a>")
                .Append("</td></tr></table>")
                .Append($"<p style=\"margin:0 0 16px;font-size:13px;line-height:1.5;color:{Muted};\">{E(Text(culture, "Layout_LinkHint"))}<br><a href=\"{E(button.Url)}\" style=\"color:{Brand};word-break:break-all;\">{E(button.Url)}</a></p>");
        }

        foreach (var paragraph in content.AfterButton ?? [])
        {
            body.Append(Paragraph(paragraph));
        }

        return $"""
            <!doctype html>
            <html lang="{culture.TwoLetterISOLanguageName}">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(content.Subject)}</title></head>
            <body style="margin:0;padding:0;background:{Paper};font-family:-apple-system,'Segoe UI',Helvetica,Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Paper};"><tr><td align="center" style="padding:24px 12px;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#FFFFFF;border:1px solid {Line};border-radius:16px;">
            <tr><td style="padding:20px 28px;background:{Ink};border-radius:16px 16px 0 0;">
            <a href="{E(siteUri)}" style="font-size:20px;font-weight:800;color:#E4EFEC;text-decoration:none;">tauchplaetze<span style="color:{Accent};">.</span>ch</a>
            </td></tr>
            <tr><td style="padding:28px;">{body}</td></tr>
            <tr><td style="padding:16px 28px 24px;border-top:1px solid {Line};font-size:12px;line-height:1.5;color:{Muted};">{E(Text(culture, content.FooterKey))}</td></tr>
            </table>
            </td></tr></table>
            </body>
            </html>
            """;
    }
}
