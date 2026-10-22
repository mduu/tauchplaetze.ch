using Microsoft.AspNetCore.Components;
using Tauchplaetze.Core.Features.Translations;
using Tauchplaetze.Core.Features.Translations.Commands;
using Tauchplaetze.Core.Features.Translations.Queries;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>
/// «Automatisch übersetzen» in einem Sprach-Tab (`cms.md`, «Automatisch übersetzen»). Übersetzt alle Texte aus einer ausgefüllten Sprache
/// in die Sprache des Tabs. Das Ergebnis steht nur im Formular, gespeichert wird wie sonst. Ein vorhandener Text wird erst nach Rückfrage ersetzt.
/// </summary>
public abstract class TextTranslatorBase : AdminPage
{
    [Parameter, EditorRequired]
    public SiteForm Form { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Target { get; set; } = "";

    [Parameter]
    public EventCallback Changed { get; set; }

    protected TranslationAllowance? Allowance { get; private set; }

    protected string? Source { get; set; }

    protected bool Running { get; private set; }

    protected bool Confirming { get; private set; }

    protected string? Message { get; private set; }

    protected bool Failed { get; private set; }

    /// <summary>Sprachen mit Text, Deutsch zuerst.</summary>
    protected IReadOnlyList<string> Sources => [.. Languages.All.Where(l => l != Target && Form.Texts[l].HasText).OrderBy(l => l == Languages.Default ? 0 : 1)];

    /// <summary>Ohne Recht oder ohne Schlüssel fehlt der Button ganz. Bei Limit oder Budget steht er mit Hinweis da.</summary>
    protected bool Visible => Allowance is { Blocker: not (TranslationError.NotAllowed or TranslationError.Unavailable) } && Sources.Count > 0;

    protected string SelectedSource => Source is not null && Sources.Contains(Source) ? Source : Sources[0];

    protected string? Blocked => Allowance?.Blocker is { } blocker ? L[$"Translate_Error_{blocker}"].Value : null;

    protected override Task Load() => Refresh();

    protected async Task Start()
    {
        if (Form.Texts[Target].HasText && !Confirming)
        {
            Confirming = true;
            return;
        }

        Confirming = false;
        Running = true;
        Message = null;
        var source = SelectedSource;
        var result = await Command<TranslateSiteTextCommand, TranslationResult>(new TranslateSiteTextCommand(UserId, Form.Texts[source].ToContent(source), Target));
        Running = false;
        Failed = result.Text is null;
        if (result.Text is { } text)
        {
            Form.Texts[Target] = TextForm.From(text);
            Message = L["Translate_Done"];
            await Changed.InvokeAsync();
        }
        else
        {
            Message = L[$"Translate_Error_{result.Error}"];
        }

        await Refresh();
    }

    protected void Cancel() => Confirming = false;

    private async Task Refresh() => Allowance = await Query<GetTranslationAllowanceQuery, TranslationAllowance>(new GetTranslationAllowanceQuery(UserId));
}
