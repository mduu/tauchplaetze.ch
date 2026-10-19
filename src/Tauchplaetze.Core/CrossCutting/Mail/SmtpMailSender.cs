using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Tauchplaetze.Core.CrossCutting.Mail;

internal class SmtpMailSender(IOptions<MailOptions> options) : IMailSender
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

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (settings.UserName is { } user)
        {
            await client.AuthenticateAsync(user, settings.Password ?? "", ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
