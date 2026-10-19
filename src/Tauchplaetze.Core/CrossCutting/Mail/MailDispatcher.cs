using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;

namespace Tauchplaetze.Core.CrossCutting.Mail;

/// <summary>Reiht Mails in die Queue ein oder sendet sie sofort (ADR-010). Commands nutzen nur diese Klasse, nie den Sender direkt.</summary>
internal class MailDispatcher(
    IMailQueue queue,
    IMailSender sender,
    MailTemplates templates,
    IOptions<PublicSiteOptions> site,
    TimeProvider time,
    ILogger<MailDispatcher> logger)
{
    private static readonly HashSet<string> Cultures = ["de", "en", "fr", "it"];

    /// <summary>
    /// Reiht die Mail ein. Scheitert die Queue, loggt die Methode den Fehler und gibt <c>false</c> zurück:
    /// Der Command, der die Mail auslöst, ist schon gespeichert und soll deswegen nicht scheitern.
    /// </summary>
    public async Task<bool> Enqueue<TModel>(string to, string culture, TModel model, CancellationToken ct, string? replyTo = null)
        where TModel : notnull
    {
        var envelope = Envelope(to, culture, model, replyTo);
        try
        {
            await queue.Enqueue(JsonSerializer.Serialize(envelope, MailJson.Options), ct);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Mail {MessageId} ({Template}) could not be queued", envelope.MessageId, envelope.Template);
            return false;
        }
    }

    /// <summary>
    /// Sendet sofort, für Mails, auf die jemand wartet (Bestätigung, Passwort). Scheitert der Versand zweimal, geht die Mail in die Queue.
    /// </summary>
    public async Task SendNow<TModel>(string to, string culture, TModel model, CancellationToken ct, string? replyTo = null)
        where TModel : notnull
    {
        var envelope = Envelope(to, culture, model, replyTo);
        var message = Render(envelope);
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                await sender.Send(message, ct);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Immediate mail {MessageId} ({Template}) failed, attempt {Attempt}", envelope.MessageId, envelope.Template, attempt);
            }
        }

        try
        {
            await queue.Enqueue(JsonSerializer.Serialize(envelope, MailJson.Options), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Mail {MessageId} ({Template}) could neither be sent nor queued", envelope.MessageId, envelope.Template);
        }
    }

    /// <summary>Macht aus dem Umschlag die fertige Mail. Unbekannte Vorlage oder unlesbares Modell wirft <see cref="MailTemplateException"/>.</summary>
    public MailMessage Render(MailEnvelope envelope)
    {
        var template = templates.ByName(envelope.Template) ?? throw new MailTemplateException($"Unbekannte Vorlage «{envelope.Template}».");
        object model;
        try
        {
            model = envelope.Model.Deserialize(template.ModelType, MailJson.Options) ?? throw new MailTemplateException("Modell ist leer.");
        }
        catch (JsonException exception)
        {
            throw new MailTemplateException($"Modell für «{envelope.Template}» ist unlesbar: {exception.Message}");
        }

        var culture = CultureInfo.GetCultureInfo(envelope.Culture);
        var content = template.Render(model, culture);
        return MailLayout.Render(content, culture, envelope.To, site.Value.Link("/"), envelope.ReplyTo);
    }

    private MailEnvelope Envelope<TModel>(string to, string culture, TModel model, string? replyTo) where TModel : notnull
    {
        var template = templates.ByType(typeof(TModel)) ?? throw new InvalidOperationException($"Keine Mail-Vorlage für {typeof(TModel).Name}.");
        return new MailEnvelope(
            Guid.CreateVersion7(time.GetUtcNow()),
            template.Name,
            to,
            Cultures.Contains(culture) ? culture : "de",
            JsonSerializer.SerializeToElement(model, MailJson.Options),
            replyTo,
            time.GetUtcNow());
    }
}

/// <summary>Die Mail lässt sich nicht rendern. Ein neuer Versuch hilft nicht, darum geht sie sofort in die Dead-Letter-Queue.</summary>
internal class MailTemplateException(string message) : Exception(message);

/// <summary>Alle Vorlagen. Nur deren Modell-Typen werden aus der Queue gelesen (Whitelist).</summary>
internal class MailTemplates(IEnumerable<IMailTemplate> templates)
{
    private readonly Dictionary<string, IMailTemplate> byName = templates.ToDictionary(t => t.Name);

    public IMailTemplate? ByName(string name) => byName.GetValueOrDefault(name);

    public IMailTemplate? ByType(Type type) => byName.Values.FirstOrDefault(t => t.ModelType == type);
}
