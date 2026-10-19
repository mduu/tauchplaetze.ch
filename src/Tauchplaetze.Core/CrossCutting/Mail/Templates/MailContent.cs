namespace Tauchplaetze.Core.CrossCutting.Mail.Templates;

/// <summary>
/// Inhalt einer Mail als Bausteine. Das Layout macht daraus HTML und Text, darum sehen alle Mails gleich aus
/// und die Textfassung entsteht ohne Umweg über HTML (ADR-010).
/// </summary>
internal record MailContent(
    string Subject,
    string Greeting,
    IReadOnlyList<string> Paragraphs,
    MailButton? Button = null,
    string? Quote = null,
    IReadOnlyList<string>? AfterButton = null,
    string FooterKey = "Layout_Footer");

/// <summary>Ein Knopf mit absolutem Link.</summary>
internal record MailButton(string Text, string Url);
