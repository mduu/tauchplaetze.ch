using System.Text.Json;
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
public record ProcessMailQueueCommand(int Max = 16);

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
            await queue.Delete(message, ct);
            return Outcome.Sent;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (IsPermanent(exception) || message.DequeueCount >= MailQueueRules.MaxAttempts)
            {
                await MoveToDeadLetter(message, envelope, exception, ct);
                return Outcome.DeadLettered;
            }

            var delay = MailQueueRules.DelayAfter(message.DequeueCount);
            logger.LogWarning(exception, "Mail {MessageId} failed, attempt {Attempt}, next try in {Delay}", envelope?.MessageId, message.DequeueCount, delay);
            await queue.Postpone(message, delay, ct);
            return Outcome.Retried;
        }
    }

    private async Task MoveToDeadLetter(QueuedMessage message, MailEnvelope? envelope, Exception exception, CancellationToken ct)
    {
        var reason = $"{exception.GetType().Name}: {exception.Message}";
        var deadLetter = new DeadLetter(envelope?.MessageId, envelope, envelope is null ? message.Body : null, (int)message.DequeueCount, reason, time.GetUtcNow());
        await queue.DeadLetter(JsonSerializer.Serialize(deadLetter, MailJson.Options), ct);
        await queue.Delete(message, ct);
        logger.LogError(exception, "Mail {MessageId} ({Template}) moved to deadletter after {Attempts} attempts", envelope?.MessageId, envelope?.Template, message.DequeueCount);
    }

    /// <summary>Unlesbar, unbekannte Vorlage, ungültige Adresse oder vom Server endgültig abgelehnt: Ein neuer Versuch hilft nicht.</summary>
    private static bool IsPermanent(Exception exception) => exception switch
    {
        JsonException or MailTemplateException or ParseException => true,
        SmtpCommandException smtp => (int)smtp.StatusCode is >= 500 and < 600,
        _ => false,
    };

    private enum Outcome
    {
        Sent,
        Retried,
        DeadLettered,
    }
}
