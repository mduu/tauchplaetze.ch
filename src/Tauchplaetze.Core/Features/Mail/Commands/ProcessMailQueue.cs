using System.Text.Json;
using Azure;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using MimeKit;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;

namespace Tauchplaetze.Core.Features.Mail.Commands;

/// <summary>
/// Sendet die sichtbaren Mails der Queue, höchstens <paramref name="Max"/> (ADR-010). Erfolg löscht die Mail.
/// Ein Fehler wartet nach <see cref="MailQueueRules.Backoff"/>, der fünfte und jeder nicht behebbare Fehler legt sie in die Dead-Letter-Queue.
/// </summary>
public record ProcessMailQueueCommand(int Max = MailQueueRules.BatchSize);

/// <summary>Was der Durchgang getan hat. Alles 0: Die Queue war leer.</summary>
public record ProcessMailQueueResult(int Sent, int Retried, int DeadLettered)
{
    public bool Idle => Sent + Retried + DeadLettered == 0;
}

internal class ProcessMailQueueHandler(IMailQueue queue, IMailSender sender, MailDispatcher dispatcher, TimeProvider time, ILogger<ProcessMailQueueHandler> logger)
    : ICommandHandler<ProcessMailQueueCommand, ProcessMailQueueResult>
{
    public async Task<ProcessMailQueueResult> Handle(ProcessMailQueueCommand command, CancellationToken ct = default)
    {
        var (sent, retried, dead) = (0, 0, 0);
        foreach (var message in await queue.Receive(command.Max, MailQueueRules.Processing, ct))
        {
            switch (await Process(message, ct))
            {
                case Outcome.Sent: sent++; break;
                case Outcome.Retried: retried++; break;
                default: dead++; break;
            }
        }

        return new ProcessMailQueueResult(sent, retried, dead);
    }

    private async Task<Outcome> Process(QueuedMessage message, CancellationToken ct)
    {
        MailEnvelope? envelope = null;
        try
        {
            envelope = JsonSerializer.Deserialize<MailEnvelope>(message.Body, MailJson.Options) ?? throw new MailTemplateException("Leerer Umschlag.");
            await sender.Send(dispatcher.Render(envelope), ct);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            return await Failed(message, envelope, exception, ct);
        }

        await Settle(() => queue.Delete(message, ct), envelope, "delete");
        return Outcome.Sent;
    }

    private async Task<Outcome> Failed(QueuedMessage message, MailEnvelope? envelope, Exception exception, CancellationToken ct)
    {
        if (IsPermanent(exception) || message.DequeueCount >= MailQueueRules.MaxAttempts)
        {
            var deadLetter = new DeadLetter(envelope?.MessageId, envelope, envelope is null ? message.Body : null, (int)message.DequeueCount, Reason(exception), time.GetUtcNow());
            await Settle(async () =>
            {
                await queue.DeadLetter(JsonSerializer.Serialize(deadLetter, MailJson.Options), ct);
                await queue.Delete(message, ct);
            }, envelope, "deadletter");
            logger.LogError("Mail {MessageId} ({Template}) moved to deadletter after {Attempts} attempts: {Error}",
                envelope?.MessageId, envelope?.Template, message.DequeueCount, Code(exception));
            return Outcome.DeadLettered;
        }

        var delay = MailQueueRules.DelayAfter(message.DequeueCount);
        logger.LogWarning("Mail {MessageId} failed, attempt {Attempt}, next try in {Delay}: {Error}", envelope?.MessageId, message.DequeueCount, delay, Code(exception));
        await Settle(() => queue.Postpone(message, delay, ct), envelope, "postpone");
        return Outcome.Retried;
    }

    /// <summary>
    /// Ein Fehler der Queue nach dem Versand (z.B. abgelaufener Pop Receipt) wird nur geloggt. Die Mail erscheint dann
    /// nach der Sichtbarkeit wieder: Sie kann doppelt kommen (at-least-once, ADR-010), aber der Durchgang läuft weiter.
    /// </summary>
    private async Task Settle(Func<Task> action, MailEnvelope? envelope, string step)
    {
        try
        {
            await action();
        }
        catch (RequestFailedException exception)
        {
            logger.LogWarning("Mail {MessageId}: queue {Step} failed with {Status} {Code}", envelope?.MessageId, step, exception.Status, exception.ErrorCode);
        }
    }

    /// <summary>
    /// Kein neuer Versuch hilft: unlesbar, unbekannte Vorlage, ungültige Adresse, Empfänger abgelehnt.
    /// Andere 5xx (Absender, Anmeldung, Richtlinie) sind oft Konfiguration und bekommen den Backoff.
    /// </summary>
    internal static bool IsPermanent(Exception exception) => exception switch
    {
        JsonException or MailTemplateException or ParseException => true,
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } => true,
        _ => false,
    };

    /// <summary>Für das Log: nur Typ und Code, nie die Meldung (sie kann die Adresse enthalten).</summary>
    private static string Code(Exception exception) => exception switch
    {
        SmtpCommandException smtp => $"{nameof(SmtpCommandException)} {(int)smtp.StatusCode} {smtp.ErrorCode}",
        _ => exception.GetType().Name,
    };

    /// <summary>Für die Dead-Letter-Mail: der volle Text, nur Admins sehen ihn.</summary>
    private static string Reason(Exception exception) => $"{Code(exception)}: {exception.Message}";

    private enum Outcome
    {
        Sent,
        Retried,
        DeadLettered,
    }
}
