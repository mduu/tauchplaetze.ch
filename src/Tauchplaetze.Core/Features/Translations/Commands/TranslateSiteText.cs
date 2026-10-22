using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Translations.Commands;

/// <summary>
/// Übersetzt die Texte eines Platzes von <c>Source.Culture</c> nach <paramref name="TargetCulture"/> (`cms.md`, «Automatisch übersetzen»).
/// Das Ergebnis ist nur ein Vorschlag für das Formular. Gleicher Text kommt aus dem Cache und zählt nicht.
/// </summary>
public record TranslateSiteTextCommand(string UserId, DiveSiteContentTranslation Source, string TargetCulture);

internal class TranslateSiteTextHandler(
    TauchplaetzeDbContext dbContext,
    AccountRoles roles,
    TranslationQuota quota,
    ITextTranslator translator,
    TimeProvider time,
    ILogger<TranslateSiteTextHandler> logger) : ICommandHandler<TranslateSiteTextCommand, TranslationResult>
{
    /// <summary>Hash einer unbrauchbaren Antwort. Sie zählt für Limit und Budget, ist aber weder Cache noch Beleg.</summary>
    private static readonly string NoHash = new('0', 64);

    public async Task<TranslationResult> Handle(TranslateSiteTextCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.UserId);
        if (!rights.CanWrite)
        {
            return TranslationResult.Fail(TranslationError.NotAllowed);
        }

        var source = SiteTextFields.Of(command.Source);
        if (!SiteTextFields.Cultures.Contains(command.Source.Culture) || !SiteTextFields.Cultures.Contains(command.TargetCulture)
            || command.Source.Culture == command.TargetCulture || !SiteTextFields.AreValid(source))
        {
            return TranslationResult.Fail(TranslationError.InvalidInput);
        }

        var sourceHash = SiteTextFields.HashOf(command.Source.Culture, source, $"{command.TargetCulture}|{translator.Version}");
        var cached = await dbContext.MachineTranslations.AsNoTracking().Where(t => t.SourceHash == sourceHash).Select(t => t.ResultJson).FirstOrDefaultAsync(ct);
        if (cached is not null)
        {
            return new TranslationResult(SiteTextFields.ToText(command.TargetCulture, JsonSerializer.Deserialize<Dictionary<string, string>>(cached)!), null);
        }

        if (await quota.Check(command.UserId, rights, ct) is { } error)
        {
            return TranslationResult.Fail(error);
        }

        var reply = await Translate(command, source, ct);
        if (reply is null)
        {
            return TranslationResult.Fail(TranslationError.Failed);
        }

        var result = IsComplete(source, reply.Fields) ? reply.Fields! : null;
        dbContext.MachineTranslations.Add(new MachineTranslation
        {
            Id = Guid.CreateVersion7(),
            RequestedBy = command.UserId,
            CreatedAt = time.GetUtcNow(),
            SourceCulture = command.Source.Culture,
            TargetCulture = command.TargetCulture,
            SourceHash = result is null ? NoHash : sourceHash,
            ResultHash = result is null ? NoHash : SiteTextFields.HashOf(command.TargetCulture, result),
            ResultJson = JsonSerializer.Serialize(result ?? new Dictionary<string, string>()),
            Model = translator.Version,
            InputTokens = reply.InputTokens,
            OutputTokens = reply.OutputTokens,
        });
        await dbContext.SaveChangesAsync(ct);

        return result is null ? TranslationResult.Fail(TranslationError.Failed) : new TranslationResult(SiteTextFields.ToText(command.TargetCulture, result), null);
    }

    private async Task<TranslatorReply?> Translate(TranslateSiteTextCommand command, IReadOnlyDictionary<string, string> source, CancellationToken ct)
    {
        try
        {
            return await translator.Translate(command.Source.Culture, command.TargetCulture, source, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Machine translation {Source} to {Target} failed", command.Source.Culture, command.TargetCulture);
            return null;
        }
    }

    /// <summary>Jedes Feld der Quelle ist übersetzt, kein anderes, und das Markdown hat dieselbe Struktur.</summary>
    private static bool IsComplete(IReadOnlyDictionary<string, string> source, IReadOnlyDictionary<string, string>? translated) =>
        translated is not null
        && translated.Keys.ToHashSet().SetEquals(source.Keys)
        && source.All(f => !string.IsNullOrWhiteSpace(translated[f.Key]) && MarkdownStructure.Matches(f.Value, translated[f.Key]));
}
