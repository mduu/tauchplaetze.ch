using System.Text.Json;

namespace Tauchplaetze.Core.CrossCutting.Mail.Queue;

/// <summary>
/// Eine Mail in der Queue: Vorlage, Modell als JSON, Empfänger und Sprache. Gerendert wird erst beim Senden,
/// so wirken Korrekturen am Layout auch auf wartende Mails. <see cref="MessageId"/> bleibt über Versuche und Dead-Letter gleich.
/// </summary>
internal record MailEnvelope(Guid MessageId, string Template, string To, string Culture, JsonElement Model, string? ReplyTo, DateTimeOffset CreatedAt);

/// <summary>Eine Mail in der Dead-Letter-Queue: der Umschlag (oder der unlesbare Text), die Zahl der Versuche und der letzte Fehler.</summary>
internal record DeadLetter(Guid? MessageId, MailEnvelope? Envelope, string? RawBody, int Attempts, string Reason, DateTimeOffset FailedAt);

/// <summary>Eine empfangene Nachricht. <see cref="DequeueCount"/> zählt die Versuche.</summary>
internal record QueuedMessage(string Id, string PopReceipt, string Body, long DequeueCount);

internal static class MailJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
