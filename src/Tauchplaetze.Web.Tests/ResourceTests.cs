using System.Xml.Linq;

namespace Tauchplaetze.Web.Tests;

public class ResourceTests
{
    private static readonly string[] Translations = ["en", "fr", "it"];

    /// <remarks>Marke und Sprachnamen sind in allen Sprachen gleich (`i18n.md`).</remarks>
    private static readonly string[] SameInAllLanguages = ["Brand", "Language_de", "Language_en", "Language_fr", "Language_it", "Wind_Value", "Tile_Wind", "Profile_Value", "Start_Distance", "Place_lugano", "Place_bern", "Place_basel", "Place_stgallen", "Place_lausanne", "Place_chur", "Place_zuerich", "Place_luzern", "Directory_Filter", "Version", "MyData_Account", "Nav_Account", "Meta_NoIndex", "No", "Profile_Title"];

    public static TheoryData<string> ResourceSets() => new(
        Directory.EnumerateFiles(LocalizationFolder(), "*.resx")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(f => f.Count(c => c == '.') == 1)
            .Select(f => Path.GetFileNameWithoutExtension(f)));

    [Theory]
    [MemberData(nameof(ResourceSets))]
    public void Alle_vier_Sprachen_haben_dieselben_Schluessel(string resourceSet)
    {
        // Arrange
        var german = Entries(resourceSet, null);

        // Act
        var differences = Translations
            .Select(language => (language, keys: Entries(resourceSet, language).Keys.ToHashSet()))
            .Where(t => !t.keys.SetEquals(german.Keys))
            .Select(t => $"{resourceSet}.{t.language}.resx: fehlt {string.Join(", ", german.Keys.Except(t.keys))}, zu viel {string.Join(", ", t.keys.Except(german.Keys))}")
            .ToList();

        // Assert
        Assert.True(differences.Count == 0, string.Join("\n", differences));
    }

    [Theory]
    [MemberData(nameof(ResourceSets))]
    public void Jede_Uebersetzung_ist_echt_und_nicht_der_deutsche_Text(string resourceSet)
    {
        // Arrange
        var german = Entries(resourceSet, null);

        // Act
        var findings = Translations
            .SelectMany(language => Entries(resourceSet, language)
                .Where(e => !SameInAllLanguages.Contains(e.Key))
                .Where(e => string.IsNullOrWhiteSpace(e.Value) || (german.TryGetValue(e.Key, out var de) && de == e.Value))
                .Select(e => $"{resourceSet}.{language}.resx: {e.Key}"))
            .ToList();

        // Assert
        Assert.True(findings.Count == 0, "Leer oder deutsch geblieben (i18n.md):\n  " + string.Join("\n  ", findings));
    }

    private static Dictionary<string, string> Entries(string resourceSet, string? language)
    {
        var file = Path.Combine(LocalizationFolder(), language is null ? $"{resourceSet}.resx" : $"{resourceSet}.{language}.resx");
        return XDocument.Load(file).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
    }

    private static string LocalizationFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "src", "Tauchplaetze.Web", "Localization");
    }
}
