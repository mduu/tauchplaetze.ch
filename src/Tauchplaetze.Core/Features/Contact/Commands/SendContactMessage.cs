using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;

namespace Tauchplaetze.Core.Features.Contact.Commands;

/// <summary>
/// Schickt eine Nachricht aus dem Kontaktformular per Mail an den Betreiber (`ux.md`, «Über», Kontakt). Antworten gehen an <paramref name="Email"/>.
/// Die Nachricht wird nicht gespeichert. <paramref name="Reference"/>: worauf sie sich bezieht, z.B. der Name eines Platzes.
/// </summary>
public record SendContactMessageCommand(string? Name, string Email, string Message, string? Reference, string Culture);

/// <summary>Ergebnis: leer heisst gesendet.</summary>
public record ContactResult(IReadOnlyList<ContactError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>Fehler im Kontaktformular. Die Oberfläche bildet daraus Texte.</summary>
public enum ContactError
{
    /// <summary>Die E-Mail-Adresse fehlt oder ist ungültig.</summary>
    InvalidEmail,

    /// <summary>Die Nachricht ist kürzer als 10 oder länger als 4000 Zeichen.</summary>
    InvalidMessage,

    /// <summary>Name oder Bezug ist zu lang.</summary>
    TooLong,

    /// <summary>Es ist kein Empfänger konfiguriert, oder der Versand scheiterte.</summary>
    NotSent,
}

/// <summary>Grenzen des Kontaktformulars.</summary>
public static class ContactRules
{
    public const int MinMessage = 10;

    public const int MaxMessage = 4000;

    public const int MaxName = 100;

    public const int MaxReference = 200;

    public const int MaxEmail = 254;
}

internal class SendContactMessageHandler(MailDispatcher mail, IOptions<ContactOptions> options, ILogger<SendContactMessageHandler> logger)
    : ICommandHandler<SendContactMessageCommand, ContactResult>
{
    public async Task<ContactResult> Handle(SendContactMessageCommand command, CancellationToken ct = default)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
        {
            return new ContactResult(errors);
        }

        if (string.IsNullOrWhiteSpace(options.Value.Email))
        {
            logger.LogWarning("Contact message not sent: {Section}:Email is missing", ContactOptions.Section);
            return new ContactResult([ContactError.NotSent]);
        }

        var email = command.Email.Trim();
        var model = new ContactMail(OneLine(command.Name), email, OneLine(command.Reference), Topic(command), command.Culture, command.Message.Trim());
        return await mail.Enqueue(options.Value.Email, "de", model, ct, replyTo: email)
            ? new ContactResult([])
            : new ContactResult([ContactError.NotSent]);
    }

    private static List<ContactError> Validate(SendContactMessageCommand command)
    {
        var errors = new List<ContactError>();
        var email = command.Email?.Trim() ?? "";
        if (email.Length is 0 or > ContactRules.MaxEmail || !email.Contains('@') || !MailboxAddress.TryParse(email, out _) || email.Any(char.IsWhiteSpace))
        {
            errors.Add(ContactError.InvalidEmail);
        }

        if (command.Message?.Trim().Length is not (>= ContactRules.MinMessage and <= ContactRules.MaxMessage))
        {
            errors.Add(ContactError.InvalidMessage);
        }

        if (command.Name?.Trim().Length > ContactRules.MaxName || command.Reference?.Trim().Length > ContactRules.MaxReference)
        {
            errors.Add(ContactError.TooLong);
        }

        return errors;
    }

    private static string Topic(SendContactMessageCommand command) => OneLine(command.Reference) ?? Shorten(OneLine(command.Message)!, 60);

    private static string? OneLine(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Shorten(string text, int length) => text.Length <= length ? text : text[..length] + "…";
}
