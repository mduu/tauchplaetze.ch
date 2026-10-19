using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Tests.Features.Mail;

public class MailTemplateTests
{
    [Fact]
    public async Task Eine_Mail_kommt_in_der_Sprache_der_Person_mit_absolutem_Link_und_maskiertem_HTML()
    {
        // Arrange
        var mail = new TestMail();
        await mail.Dispatcher.Enqueue("anne@example.org", "fr", new ProposalRejectedMail("Anne", "Café <Plage>", "Profondeur\nfausse"), TestContext.Current.CancellationToken);

        // Act
        await mail.Process();

        // Assert
        var sent = Assert.Single(mail.Sender.Sent);
        Assert.Equal("Ta contribution à «Café <Plage>» n’a pas été reprise", sent.Subject);
        Assert.Contains("Bonjour Anne", sent.Body);
        Assert.Contains("> Profondeur\n> fausse", sent.Body);
        Assert.Contains("lang=\"fr\"", sent.Html);
        Assert.Contains("Café &lt;Plage&gt;", sent.Html);
        Assert.DoesNotContain("<Plage>", sent.Html);
        Assert.Contains("https://tauchplaetze.test/", sent.Body);
    }

    [Theory]
    [InlineData("de", "Neuer Vorschlag zu «Ermitage» wartet auf Freigabe")]
    [InlineData("en", "New proposal for «Ermitage» awaits review")]
    [InlineData("it", "Nuova proposta per «Ermitage» in attesa di approvazione")]
    [InlineData("xx", "Neuer Vorschlag zu «Ermitage» wartet auf Freigabe")]
    public async Task Ohne_bekannte_Sprache_kommt_die_Mail_auf_Deutsch(string culture, string subject)
    {
        // Arrange
        var mail = new TestMail();
        await mail.Dispatcher.Enqueue("bert@example.org", culture, new ProposalPendingMail("Bert", "Anna", "Ermitage", false, null, "https://tauchplaetze.test/admin/vorschlaege"), TestContext.Current.CancellationToken);

        // Act
        await mail.Process();

        // Assert
        Assert.Equal(subject, Assert.Single(mail.Sender.Sent).Subject);
    }
}
