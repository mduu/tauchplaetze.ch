using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Tauchplaetze.Core.CrossCutting.Mail;

internal class SmtpMailSender(IOptions<MailOptions> options, ILogger<SmtpMailSender> logger) : IMailSender
{
    public async Task Send(MailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(settings.From));
        mime.To.Add(MailboxAddress.Parse(message.To));
        if (message.ReplyTo is { } replyTo)
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        mime.Subject = message.Subject;
        var body = new BodyBuilder { TextBody = message.Body, HtmlBody = message.Html };
        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (settings.UserName is { } user)
        {
            await client.AuthenticateAsync(user, settings.Password ?? "", ct);
        }

        await client.SendAsync(mime, ct);
        await Disconnect(client);
    }

    /// <summary>Die Mail ist schon angenommen. Ein Fehler beim Trennen darf keinen zweiten Versand auslösen.</summary>
    private async Task Disconnect(SmtpClient client)
    {
        try
        {
            await client.DisconnectAsync(true);
        }
        catch (Exception exception) when (exception is IOException or SmtpProtocolException or OperationCanceledException or TimeoutException)
        {
            logger.LogWarning("SMTP disconnect failed after the mail was accepted: {Error}", exception.GetType().Name);
        }
    }
}
