using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Translations;

namespace Tauchplaetze.Core.Features.Susv;

/// <summary>
/// Englisch der sichtbaren Einschränkungen: automatisch aus dem Deutschen (Story 002, «Sprachen»).
/// Gleicher deutscher Text kommt aus dem Cache der Übersetzungen und kostet nichts. Ist das Budget des Monats aufgebraucht,
/// bleibt der bisherige englische Text, sonst zeigt die Seite Deutsch.
/// </summary>
internal class SusvEnglish(TauchplaetzeDbContext dbContext, ITextTranslator translator, TranslationQuota quota, TimeProvider time, ILogger<SusvEnglish> logger)
{
    private const string Title = "title";

    private const string Text = "text";

    /// <summary>
    /// Bringt das Englisch der sichtbaren Einschränkungen auf den Stand des Deutschen, nach dem Bestätigen nur das eine (<paramref name="entryId"/>).
    /// Speichert selbst. Eine gescheiterte Übersetzung versucht erst der nächste Abgleich wieder.
    /// </summary>
    public async Task Update(Guid? entryId, CancellationToken ct)
    {
        var entries = await dbContext.SusvEntries
            .Where(SusvRules.ShownRestriction)
            .Where(e => entryId == null || e.Id == entryId)
            .Include(e => e.Translations)
            .ToListAsync(ct);
        foreach (var entry in entries)
        {
            var german = entry.Translations.Single(t => t.Culture == "de");
            var source = new Dictionary<string, string> { [Title] = german.Title, [Text] = german.Text ?? german.Teaser ?? "" };
            var result = await Translation(source, ct);
            if (result is null)
            {
                continue;
            }

            var english = entry.Translations.SingleOrDefault(t => t.Culture == "en");
            if (english is null)
            {
                english = new SusvEntryTranslation { EntryId = entry.Id, Culture = "en", Title = "", DetailUrl = german.DetailUrl };
                entry.Translations.Add(english);
            }

            english.Title = SusvRules.Shorten(result[Title], SusvRules.TitleLength);
            english.Text = string.IsNullOrWhiteSpace(result[Text]) ? null : SusvRules.Shorten(result[Text], SusvRules.TextLength);
            english.DetailUrl = german.DetailUrl;
            english.IsMachineTranslated = true;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyDictionary<string, string>?> Translation(Dictionary<string, string> source, CancellationToken ct)
    {
        var hash = SiteTextFields.HashOf("de", source, $"en|{translator.Version}");
        var cached = await dbContext.MachineTranslations.AsNoTracking().Where(t => t.SourceHash == hash).Select(t => t.ResultJson).FirstOrDefaultAsync(ct);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(cached);
        }

        if (!await quota.WithinBudget(ct))
        {
            return null;
        }

        TranslatorReply reply;
        try
        {
            reply = await translator.Translate("de", "en", source, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "SUSV translation to English failed");
            return null;
        }

        var result = reply.Fields is { } fields && fields.Keys.ToHashSet().SetEquals(source.Keys) && !string.IsNullOrWhiteSpace(fields[Title]) ? fields : null;
        dbContext.MachineTranslations.Add(new MachineTranslation
        {
            Id = Guid.CreateVersion7(),
            RequestedBy = null,
            CreatedAt = time.GetUtcNow(),
            SourceCulture = "de",
            TargetCulture = "en",
            SourceHash = result is null ? SiteTextFields.NoHash : hash,
            ResultHash = result is null ? SiteTextFields.NoHash : SiteTextFields.HashOf("en", result),
            ResultJson = JsonSerializer.Serialize(result ?? new Dictionary<string, string>()),
            Model = translator.Version,
            InputTokens = reply.InputTokens,
            OutputTokens = reply.OutputTokens,
        });
        await dbContext.SaveChangesAsync(ct);
        return result;
    }
}
