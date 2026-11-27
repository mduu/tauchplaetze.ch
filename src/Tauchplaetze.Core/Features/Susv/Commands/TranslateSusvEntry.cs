using Tauchplaetze.Core.CrossCutting.UseCases;

namespace Tauchplaetze.Core.Features.Susv.Commands;

/// <summary>
/// Übersetzt einen bestätigten Eintrag ins Englische (Story 002, «Sprachen»). Läuft nach dem Bestätigen im Hintergrund,
/// weil ein Call an die Claude API einige Sekunden dauert. Ein Eintrag ohne sichtbare Einschränkung bleibt unverändert.
/// </summary>
public record TranslateSusvEntryCommand(Guid EntryId);

internal class TranslateSusvEntryHandler(SusvEnglish english) : ICommandHandler<TranslateSusvEntryCommand, Unit>
{
    public async Task<Unit> Handle(TranslateSusvEntryCommand command, CancellationToken ct = default)
    {
        await english.Update(command.EntryId, ct);
        return Unit.Value;
    }
}
