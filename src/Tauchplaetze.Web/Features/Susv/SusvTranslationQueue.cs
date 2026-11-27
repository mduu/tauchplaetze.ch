using System.Threading.Channels;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Susv.Commands;

namespace Tauchplaetze.Web.Features.Susv;

/// <summary>Einträge, die nach dem Bestätigen ins Englische übersetzt werden. Die Prüfseite wartet so nicht auf die Claude API.</summary>
public sealed class SusvTranslationQueue
{
    private readonly Channel<Guid> entries = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid entryId) => entries.Writer.TryWrite(entryId);

    internal ChannelReader<Guid> Reader => entries.Reader;
}

/// <summary>Ruft den Command `TranslateSusvEntry` für jeden Eintrag der Queue. Enthält keine Fachlogik (ADR-006). Ein Fehler beendet die Schleife nie.</summary>
internal class SusvTranslationWorker(SusvTranslationQueue queue, IServiceScopeFactory scopes, ILogger<SusvTranslationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entryId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICommandHandler<TranslateSusvEntryCommand, Unit>>()
                    .Handle(new TranslateSusvEntryCommand(entryId), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "SUSV translation of {EntryId} failed", entryId);
            }
        }
    }
}
