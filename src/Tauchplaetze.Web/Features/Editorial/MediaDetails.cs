using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>Bearbeitbare Angaben eines Fotos: Fotograf, Tiefe, Legende und Alternativtext pro Sprache. Öffentlich, weil das Formular sie als Parameter bekommt.</summary>
public class MediaDetails
{
    public Guid Id { get; set; }

    public string Photographer { get; set; } = "";

    public string? PhotographerUrl { get; set; }

    public int? Depth { get; set; }

    public Dictionary<string, (string? Caption, string? AltText)> Texts { get; } = Languages.All.ToDictionary(l => l, _ => ((string?)null, (string?)null));

    public List<string> Errors { get; set; } = [];

    public static MediaDetails From(SiteMediaItem item)
    {
        var details = new MediaDetails { Id = item.Id, Photographer = item.Photographer, PhotographerUrl = item.PhotographerUrl, Depth = item.Depth };
        foreach (var text in item.Texts.Where(t => details.Texts.ContainsKey(t.Culture)))
        {
            details.Texts[text.Culture] = (text.Caption, text.AltText);
        }

        return details;
    }

    public IReadOnlyList<MediaText> ToTexts() => [.. Texts.Select(t => new MediaText(t.Key, t.Value.Caption, t.Value.AltText))];
}
