using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>Legende und Alternativtext pro Sprache: je höchstens 300 Zeichen, leere fallen weg.</summary>
internal static class MediaTexts
{
    public const int Length = 300;

    private static readonly HashSet<string> Cultures = ["de", "en", "fr", "it"];

    public static bool AreValid(IReadOnlyList<MediaText> texts) =>
        texts.All(t => Cultures.Contains(t.Culture) && !(t.Caption?.Trim().Length > Length) && !(t.AltText?.Trim().Length > Length))
        && texts.DistinctBy(t => t.Culture).Count() == texts.Count;

    public static List<MediaItemTranslation> ToEntities(Guid mediaId, IReadOnlyList<MediaText> texts) =>
        [.. texts
            .Where(t => !string.IsNullOrWhiteSpace(t.Caption) || !string.IsNullOrWhiteSpace(t.AltText))
            .Select(t => new MediaItemTranslation { MediaItemId = mediaId, Culture = t.Culture, Caption = Blank(t.Caption), AltText = Blank(t.AltText) })];

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
