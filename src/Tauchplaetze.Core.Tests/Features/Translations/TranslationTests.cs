using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Translations;
using Tauchplaetze.Core.Features.Translations.Commands;
using Tauchplaetze.Core.Features.Translations.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Translations;

/// <remarks>Ausgangslage: ein Admin, eine Mitwirkende «Anna» und Riff Williams. Der Translator ist ein Double, Limite wie in <see cref="TranslationOptions"/>.</remarks>
public class TranslationTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DiveSiteContentTranslation German = new("de", "## Steilwand\n\n- Felsen bis 30 m\n- **Hechte**", null, "Parkplatz beim Hafen.", null, null, null, false);

    [SqlFact]
    public async Task Eine_Mitwirkende_uebersetzt_hoechstens_50_Mal_am_Tag_und_ein_Treffer_im_Cache_zaehlt_nicht()
    {
        // Arrange
        var (_, anna) = await Prepare();
        await PaidTranslations(anna, 49);
        var last = await Translate(anna, German, "fr");

        // Act
        var cached = await Translate(anna, German, "fr");
        var other = await Translate(anna, German, "it");
        var allowance = await Query<GetTranslationAllowanceQuery, TranslationAllowance>(new GetTranslationAllowanceQuery(anna));

        // Assert
        Assert.Null(last.Error);
        Assert.Equal(last.Text, cached.Text);
        Assert.Equal(TranslationError.DailyLimit, other.Error);
        Assert.Equal(1, Sql.Translator.Calls);
        Assert.Equal(new TranslationAllowance(TranslationError.DailyLimit, 0), allowance);
    }

    [SqlFact]
    public async Task Admins_haben_kein_Tageslimit_aber_das_Monatsbudget_gilt_fuer_alle()
    {
        // Arrange
        var (admin, _) = await Prepare();
        await PaidTranslations(admin, 600);
        var underBudget = await Translate(admin, German, "fr");
        await PaidTranslations(admin, 1, inputTokens: 50_000_000);

        // Act
        var overBudget = await Translate(admin, German, "it");

        // Assert
        Assert.Null(underBudget.Error);
        Assert.Equal(TranslationError.BudgetExhausted, overBudget.Error);
    }

    [SqlFact]
    public async Task Eine_Uebersetzung_mit_anderem_Markdown_wird_verworfen_und_zaehlt_trotzdem()
    {
        // Arrange
        var (_, anna) = await Prepare();
        Sql.Translator.Translate = (text, _) => text.Replace("- ", "", StringComparison.Ordinal);

        // Act
        var broken = await Translate(anna, German, "fr");
        var allowance = await Query<GetTranslationAllowanceQuery, TranslationAllowance>(new GetTranslationAllowanceQuery(anna));

        // Assert
        Assert.Equal(TranslationError.Failed, broken.Error);
        Assert.Equal(49, allowance.RemainingToday);
    }

    [SqlFact]
    public async Task Uebersetzt_wird_nur_in_eine_andere_Sprache_mit_Text_in_der_erlaubten_Laenge_und_nicht_von_gesperrten_Konten()
    {
        // Arrange
        var (admin, anna) = await Prepare();
        var tooLong = German with { HazardsInfo = new string('x', 2001) };

        // Act
        var sameLanguage = await Translate(anna, German, "de");
        var unknownLanguage = await Translate(anna, German, "es");
        var empty = await Translate(anna, new DiveSiteContentTranslation("de", " ", null, null, null, null, null, false), "fr");
        var longText = await Translate(anna, tooLong, "fr");
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));
        var blocked = await Translate(anna, German, "fr");

        // Assert
        Assert.Equal(
            [TranslationError.InvalidInput, TranslationError.InvalidInput, TranslationError.InvalidInput, TranslationError.InvalidInput, TranslationError.NotAllowed],
            [sameLanguage.Error, unknownLanguage.Error, empty.Error, longText.Error, blocked.Error]);
        Assert.Equal(0, Sql.Translator.Calls);
    }

    [SqlFact]
    public async Task Ein_Vorschlag_mit_der_unveraenderten_Uebersetzung_bleibt_automatisch_uebersetzt_ein_geaenderter_nicht()
    {
        // Arrange
        var (admin, anna) = await Prepare();
        var site = await AddRiffWilliams(admin);
        var french = (await Translate(anna, German, "fr")).Text!;
        var italian = (await Translate(anna, German, "it")).Text!;
        var edit = (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(admin, site, edit.BaseNumber, edit.Content with
        {
            Translations = [German, french, italian with { ApproachInfo = "Parcheggio al porto." }],
        }, null));

        // Assert
        var after = (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!.Content;
        Assert.True(after.TextIn("fr")!.IsMachineTranslated);
        Assert.False(after.TextIn("it")!.IsMachineTranslated);
    }

    private async Task<(string Admin, string Anna)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        return (admin, anna);
    }

    private Task<TranslationResult> Translate(string userId, DiveSiteContentTranslation source, string target) =>
        Command<TranslateSiteTextCommand, TranslationResult>(new TranslateSiteTextCommand(userId, source, target));

    private Task PaidTranslations(string userId, int count, int inputTokens = 1000) => Sql.Arrange(dbContext =>
    {
        dbContext.MachineTranslations.AddRange(Enumerable.Range(0, count).Select(i => new MachineTranslation
        {
            Id = Guid.CreateVersion7(),
            RequestedBy = userId,
            CreatedAt = Sql.Time.GetUtcNow(),
            SourceCulture = "de",
            TargetCulture = "en",
            SourceHash = $"{i:D64}",
            ResultHash = $"{i:D64}",
            ResultJson = "{}",
            Model = "fake/1",
            InputTokens = inputTokens,
            OutputTokens = 1000,
        }));
        return Task.CompletedTask;
    });
}
