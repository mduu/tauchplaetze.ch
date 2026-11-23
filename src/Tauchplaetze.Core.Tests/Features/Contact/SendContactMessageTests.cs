using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.Contact;
using Tauchplaetze.Core.Features.Contact.Commands;
using Tauchplaetze.Core.Tests.Features.Mail;

namespace Tauchplaetze.Core.Tests.Features.Contact;

public class SendContactMessageTests
{
    private readonly TestMail mail = new();

    [Fact]
    public async Task Eine_Nachricht_geht_an_den_Betreiber_und_Antworten_gehen_an_die_Absenderin()
    {
        // Act
        var result = await Send(new SendContactMessageCommand("Anna", " anna@example.org ", "Beim Riff Williams fehlt der Einstieg.", "Riff Williams", "de"));

        // Assert
        Assert.True(result.Succeeded);
        var sent = Assert.Single(mail.Sender.Sent);
        Assert.Equal(("betrieb@example.org", "anna@example.org", "tauchplaetze.ch Kontakt: Riff Williams"), (sent.To, sent.ReplyTo, sent.Subject));
        Assert.Contains("Beim Riff Williams fehlt der Einstieg.", sent.Body);
    }

    [Fact]
    public async Task Zeilenumbrueche_im_Bezug_oder_in_der_Nachricht_ergeben_einen_einzeiligen_Betreff()
    {
        // Act
        await Send(new SendContactMessageCommand(null, "anna@example.org", "Erste Zeile\r\nBcc: spam@example.org\nDritte Zeile", null, "de"));
        await Send(new SendContactMessageCommand(null, "anna@example.org", "Eine genügend lange Nachricht.", "Riff\r\nWilliams", "de"));

        // Assert
        Assert.All(mail.Sender.Sent, m => Assert.DoesNotContain('\n', m.Subject));
        Assert.All(mail.Sender.Sent, m => Assert.DoesNotContain('\r', m.Subject));
        Assert.Equal("tauchplaetze.ch Kontakt: Riff Williams", mail.Sender.Sent.Last().Subject);
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
        Assert.Empty(mail.Sender.Sent);
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
        Assert.Empty(mail.Sender.Sent);
    }

    [Fact]
    public async Task Ohne_Empfaenger_in_der_Konfiguration_meldet_das_Formular_einen_Fehler()
    {
        // Act
        var result = await Send(new SendContactMessageCommand(null, "anna@example.org", "Eine genügend lange Nachricht.", null, "it"), recipient: null);

        // Assert
        Assert.Equal([ContactError.NotSent], result.Errors);
        Assert.Empty(mail.Sender.Sent);
    }

    [Theory]
    [InlineData("werbung@spam.example", "Eine genügend lange Nachricht.")]
    [InlineData("werbung@mail.SPAM.example", "Eine genügend lange Nachricht.")]
    [InlineData("anna@example.org", "Hier der Link zur Anmeldung: Spam.Example")]
    public async Task Eine_Nachricht_mit_gesperrter_Domain_in_Adresse_oder_Text_meldet_gesendet_und_geht_nicht_raus(string email, string message)
    {
        // Act
        var result = await Send(new SendContactMessageCommand(null, email, message, null, "en"), blockedDomains: "other.example, spam.example");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Empty(mail.Sender.Sent);
    }

    [Fact]
    public async Task Eine_Domain_die_nur_aehnlich_endet_ist_nicht_gesperrt_und_ein_Eintrag_der_keine_Domain_ist_sperrt_nichts()
    {
        // Act
        await Send(new SendContactMessageCommand(null, "anna@nospam.example", "Eine genügend lange Nachricht.", null, "de"), blockedDomains: "spam.example");
        await Send(new SendContactMessageCommand(null, "anna@example.org", "Eine genügend lange Nachricht.", null, "de"), blockedDomains: "@Microsoft.KeyVault(VaultName=kv;SecretName=x) e");

        // Assert
        Assert.Equal(2, mail.Sender.Sent.Count);
    }

    private async Task<ContactResult> Send(SendContactMessageCommand command, string? recipient = "betrieb@example.org", string? blockedDomains = null)
    {
        var result = await new SendContactMessageHandler(mail.Dispatcher, Options.Create(new ContactOptions { Email = recipient }),
                Options.Create(new ContactSpamOptions { BlockedDomains = blockedDomains }), NullLogger<SendContactMessageHandler>.Instance)
            .Handle(command, TestContext.Current.CancellationToken);
        await mail.Process();
        return result;
    }
}
