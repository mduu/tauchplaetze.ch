using System.Globalization;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;

namespace Tauchplaetze.Core.Features.Contact;

/// <summary>Modell der Mail an den Betreiber. <see cref="Topic"/> ist schon einzeilig, er steht im Betreff.</summary>
internal record ContactMail(string? Name, string Email, string? Reference, string Topic, string SenderCulture, string Message);

internal class ContactMailTemplate : MailTemplate<ContactMail>
{
    public override string Name => "ContactMessage";

    protected override MailContent Render(ContactMail model, CultureInfo culture) => new(
        T(culture, "Contact_Subject", model.Topic),
        T(culture, "Contact_Greeting"),
        [
            T(culture, "Contact_Intro"),
            $"{T(culture, "Contact_From", model.Name ?? T(culture, "Contact_NoName"), model.Email)} · {model.SenderCulture.ToUpperInvariant()}",
            T(culture, "Contact_About", model.Reference ?? T(culture, "Contact_General")),
        ],
        Quote: model.Message,
        AfterButton: [T(culture, "Contact_Reply")],
        FooterKey: "Layout_Footer_Operator");
}
