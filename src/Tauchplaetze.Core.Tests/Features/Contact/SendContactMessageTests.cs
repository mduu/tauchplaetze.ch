using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.Contact;
using Tauchplaetze.Core.Features.Contact.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Contact;

public class SendContactMessageTests
{
    private readonly InMemoryMailSender mail = new();

    [Fact]
    public async Task Eine_Nachricht_geht_an_den_Betreiber_und_Antworten_gehen_an_die_Absenderin()
    {
        // Act
        var result = await Send(new SendContactMessageCommand("Anna", " anna@example.org ", "Beim Riff Williams fehlt der Einstieg.", "Riff Williams", "de"));

        // Assert
        Assert.True(result.Succeeded);
        var sent = Assert.Single(mail.Sent);
        Assert.Equal(("betrieb@example.org", "anna@example.org", "tauchplaetze.ch Kontakt: Riff Williams"), (sent.To, sent.ReplyTo, sent.Subject));
        Assert.Contains("Beim Riff Williams fehlt der Einstieg.", sent.Body);
    }

    [Theory]
    [InlineData("", "Eine genügend lange Nachricht.", ContactError.InvalidEmail)]
    [InlineData("keine-adresse", "Eine genügend lange Nachricht.", ContactError.InvalidEmail)]
    [InlineData("anna@example.org", "zu kurz", ContactError.InvalidMessage)]
    [InlineData("anna@example.org", null, ContactError.InvalidMessage)]
    public async Task Ohne_gueltige_E_Mail_oder_mit_zu_kurzer_Nachricht_geht_nichts_raus(string email, string? message, ContactError expected)
    {
        // Act
        var result = await Send(new SendContactMessageCommand(null, email, message!, null, "fr"));

        // Assert
        Assert.Equal([expected], result.Errors);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task Eine_Nachricht_ueber_4000_Zeichen_oder_ein_zu_langer_Name_wird_abgelehnt()
    {
        // Act
        var tooLong = await Send(new SendContactMessageCommand(null, "anna@example.org", new string('a', 4001), null, "de"));
        var longName = await Send(new SendContactMessageCommand(new string('n', 101), "anna@example.org", "Eine genügend lange Nachricht.", null, "de"));

        // Assert
        Assert.Equal([ContactError.InvalidMessage], tooLong.Errors);
        Assert.Equal([ContactError.TooLong], longName.Errors);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task Ohne_Empfaenger_in_der_Konfiguration_meldet_das_Formular_einen_Fehler()
    {
        // Act
        var result = await Send(new SendContactMessageCommand(null, "anna@example.org", "Eine genügend lange Nachricht.", null, "it"), recipient: null);

        // Assert
        Assert.Equal([ContactError.NotSent], result.Errors);
        Assert.Empty(mail.Sent);
    }

    private Task<ContactResult> Send(SendContactMessageCommand command, string? recipient = "betrieb@example.org") =>
        new SendContactMessageHandler(mail, Options.Create(new ContactOptions { Email = recipient }), NullLogger<SendContactMessageHandler>.Instance)
            .Handle(command, TestContext.Current.CancellationToken);
}
