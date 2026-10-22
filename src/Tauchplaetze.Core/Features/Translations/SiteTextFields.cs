using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Translations;

/// <summary>Die Felder der Texte eines Platzes, wie sie zur Übersetzung gehen. Nur diese Felder, nur mit diesen Längen: kein freier Text.</summary>
internal static class SiteTextFields
{
    public static readonly IReadOnlyList<string> Cultures = ["de", "en", "fr", "it"];

    private static readonly (string Name, int MaxLength, Func<DiveSiteContentTranslation, string?> Get)[] Fields =
    [
        ("underwater", 8000, t => t.UnderwaterDescription),
        ("hazards", 2000, t => t.HazardsInfo),
        ("approach", 2000, t => t.ApproachInfo),
        ("surroundings", 2000, t => t.SurroundingsInfo),
        ("facilities", 1000, t => t.FacilitiesInfo),
        ("divingBan", 500, t => t.DivingBanNote),
    ];

    /// <summary>Die ausgefüllten Felder, getrimmt.</summary>
    public static IReadOnlyDictionary<string, string> Of(DiveSiteContentTranslation text) =>
        Fields.Where(f => !string.IsNullOrWhiteSpace(f.Get(text))).ToDictionary(f => f.Name, f => f.Get(text)!.Trim());

    public static bool AreValid(IReadOnlyDictionary<string, string> fields) =>
        fields.Count > 0 && Fields.All(f => !fields.TryGetValue(f.Name, out var value) || value.Length <= f.MaxLength);

    public static DiveSiteContentTranslation ToText(string culture, IReadOnlyDictionary<string, string> fields) => new(
        culture,
        fields.GetValueOrDefault("underwater"),
        fields.GetValueOrDefault("surroundings"),
        fields.GetValueOrDefault("approach"),
        fields.GetValueOrDefault("hazards"),
        fields.GetValueOrDefault("facilities"),
        fields.GetValueOrDefault("divingBan"),
        true);

    /// <summary>Hash der Felder mit Sprache. Gleich für gleichen Text, unabhängig von Leerraum am Rand.</summary>
    public static string HashOf(string culture, IReadOnlyDictionary<string, string> fields, string extra = "") =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { culture, extra, fields = new SortedDictionary<string, string>(fields.ToDictionary()) }))));

    public static string HashOf(DiveSiteContentTranslation text) => HashOf(text.Culture, Of(text));
}
