using System.Text.RegularExpressions;

namespace Tauchplaetze.Web.Tests;

public partial class StyleSheetTests
{
    [Fact]
    public void Jedes_Stylesheet_schliesst_jede_Regel_sonst_verwirft_der_Browser_alles_danach()
    {
        // Arrange
        var files = Directory.EnumerateFiles(WebFolder(), "*.css", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}") && !f.EndsWith(".min.css"));

        // Act
        var broken = files.Where(f => !IsBalanced(File.ReadAllText(f))).Select(Path.GetFileName).ToList();

        // Assert
        Assert.Empty(broken);
    }

    private static bool IsBalanced(string css)
    {
        var depth = 0;
        foreach (var character in Comments().Replace(css, ""))
        {
            depth += character switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private static string WebFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Path.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException(), "src", "Tauchplaetze.Web");
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();
}
