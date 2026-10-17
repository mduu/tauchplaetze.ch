namespace Tauchplaetze.Core.Tests.Rules;

internal static class RepositorySources
{
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Wurzel des Repositorys nicht gefunden.");
    }

    /// <summary>Alle C#-Dateien unter `src`, ohne Spikes, Build-Ausgaben und Migrationen.</summary>
    public static IEnumerable<string> CSharpFiles()
    {
        var separator = Path.DirectorySeparatorChar;

        return Directory
            .EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}")
                        && !f.Contains($"{separator}bin{separator}")
                        && !f.Contains($"{separator}spikes{separator}")
                        && !f.Contains($"{separator}Migrations{separator}"));
    }

    public static bool IsTestFile(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}Tauchplaetze.Core.Tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}Tauchplaetze.Web.Tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
