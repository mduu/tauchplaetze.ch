using System.Text.Json;
using MailKit.Net.Smtp;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Mail;

namespace Tauchplaetze.Core.Tests.Features.Mail;

public class MailQueueTests
{
    private readonly TestMail mail = new();

    [Fact]
    public async Task Eine_gesendete_Mail_verlaesst_die_Queue_mit_HTML_und_Text()
    {
        // Arrange
        await Enqueue();

        // Act
        var result = await mail.Process();

        // Assert
        Assert.Equal(1, result.Sent);
        Assert.Empty(mail.Queue.Waiting);
        var sent = Assert.Single(mail.Sender.Sent);
        Assert.Equal("anna@example.org", sent.To);
        Assert.Contains("https://tauchplaetze.test/de/site/riff-williams", sent.Body);
        Assert.Contains("href=\"https://tauchplaetze.test/de/site/riff-williams\"", sent.Html);
    }

    [Fact]
    public async Task Nach_einem_Fehler_wartet_die_Mail_1_5_15_und_60_Minuten_und_der_fuenfte_Fehler_legt_sie_in_die_Dead_Letter_Queue()
    {
        // Arrange
        await Enqueue();
        mail.Sender.Failing = new IOException("SMTP nicht erreichbar");
        var waits = new List<TimeSpan>();

        // Act
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await mail.Process();
            waits.Add(mail.Queue.VisibleAt.Single() - mail.Time.Now);
            mail.Time.Now = mail.Queue.VisibleAt.Single();
        }

        var last = await mail.Process();

        // Assert
        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)], waits);
        Assert.Equal(1, last.DeadLettered);
        Assert.Empty(mail.Queue.Waiting);
        var deadLetter = DeadLetterOf(Assert.Single(mail.Queue.DeadLetters));
        Assert.Equal(MailQueueRules.MaxAttempts, deadLetter.Attempts);
        Assert.Contains("SMTP nicht erreichbar", deadLetter.Reason);
        Assert.Equal("ProposalApproved", deadLetter.Envelope!.Template);
    }

    [Fact]
    public async Task Eine_vom_Server_endgueltig_abgelehnte_Mail_geht_sofort_in_die_Dead_Letter_Queue()
    {
        // Arrange
        await Enqueue();
        mail.Sender.Failing = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "Mailbox unavailable");

        // Act
        var result = await mail.Process();

        // Assert
        Assert.Equal(1, result.DeadLettered);
        Assert.Equal(1, DeadLetterOf(Assert.Single(mail.Queue.DeadLetters)).Attempts);
    }

    [Theory]
    [InlineData("""{"messageId":"0199f1c2-0000-7000-8000-000000000001","template":"Gibt-es-nicht","to":"anna@example.org","culture":"de","model":{},"createdAt":"2026-10-17T10:00:00+00:00"}""")]
    [InlineData("kein JSON")]
    public async Task Eine_unlesbare_Mail_oder_eine_unbekannte_Vorlage_geht_sofort_in_die_Dead_Letter_Queue(string body)
    {
        // Arrange
        await mail.Queue.Enqueue(body, TestContext.Current.CancellationToken);

        // Act
        var result = await mail.Process();

        // Assert
        Assert.Equal(1, result.DeadLettered);
        Assert.Empty(mail.Sender.Sent);
        Assert.Single(mail.Queue.DeadLetters);
    }

    [Fact]
    public async Task Ist_die_Queue_nicht_erreichbar_scheitert_das_Einreihen_still_und_meldet_false()
    {
        // Arrange
        mail.Queue.Failing = true;

        // Act
        var queued = await mail.Dispatcher.Enqueue("anna@example.org", "de", Approved(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(queued);
    }

    [Fact]
    public async Task Nur_Modelle_mit_einer_Vorlage_kommen_in_die_Queue()
    {
        // Act
        var enqueue = () => mail.Dispatcher.Enqueue("anna@example.org", "de", new { Fremd = true }, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(enqueue);
    }

    [Fact]
    public async Task Konto_Mails_gehen_sofort_raus_und_scheitert_der_Versand_landen_sie_in_der_Queue()
    {
        // Arrange
        var sent = new TestMail();
        var failing = new TestMail();
        failing.Sender.Failing = new IOException("SMTP nicht erreichbar");

        // Act
        await sent.Dispatcher.SendNow("anna@example.org", "de", Approved(), TestContext.Current.CancellationToken);
        await failing.Dispatcher.SendNow("anna@example.org", "de", Approved(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(sent.Sender.Sent);
        Assert.Empty(sent.Queue.Waiting);
        Assert.Single(failing.Queue.Waiting);
    }

    private Task Enqueue() => mail.Dispatcher.Enqueue("anna@example.org", "de", Approved(), TestContext.Current.CancellationToken);

    private static ProposalApprovedMail Approved() => new("Anna", "Riff Williams", "https://tauchplaetze.test/de/site/riff-williams");

    private static DeadLetter DeadLetterOf(string body) => JsonSerializer.Deserialize<DeadLetter>(body, MailJson.Options)!;
}
