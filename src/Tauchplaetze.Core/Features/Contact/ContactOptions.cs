namespace Tauchplaetze.Core.Features.Contact;

/// <summary>Empfänger des Kontaktformulars (`Operator:Email`). Die Adresse erscheint nie auf der Website.</summary>
public class ContactOptions
{
    public const string Section = "Operator";

    public string? Email { get; set; }
}
