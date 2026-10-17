using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tauchplaetze.Core.Tests.Rules;

public class CommentsInMethodBodiesTests
{
    private static readonly string[] ToolDirectives = ["language=", "lang=", "ReSharper disable", "ReSharper restore"];

    [Fact]
    public void Im_Produktivcode_stehen_keine_Kommentare_in_Method_Bodies()
    {
        // Act
        var findings = RepositorySources.CSharpFiles()
            .Where(f => !RepositorySources.IsTestFile(f) && !f.EndsWith(".g.cs", StringComparison.Ordinal))
            .SelectMany(file => Find(CSharpSyntaxTree.ParseText(File.ReadAllText(file)), Path.GetFileName(file)))
            .ToList();

        // Assert
        Assert.True(findings.Count == 0,
            "Kommentar in einem Method Body (architecture.md, Code-Dokumentation: Extract Method, "
            + "<remarks> oder löschen):\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Ein_Kommentar_im_Body_faellt_auf_einer_zwischen_den_Members_nicht()
    {
        // Arrange
        const string source = """
            public sealed class Probe
            {
                // Zwischen den Members: erlaubt.
                private static readonly int[] Steps =
                [
                    1, // im Initializer
                ];

                public int Run(string text)
                {
                    // language=regex
                    const string pattern = "^a+$";

                    // im Block
                    var x = 1; /* am Zeilenende */
                    return Steps.Sum(s => s /* im Lambda */ + x);
                }

                public int Count => 1 // im Expression Body
                    + 1;
            }
            """;

        // Act
        var findings = Find(CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken), "Probe.cs").ToList();

        // Assert
        Assert.Equal(
            [
                "Probe.cs:6  // im Initializer",
                "Probe.cs:14  // im Block",
                "Probe.cs:15  /* am Zeilenende */",
                "Probe.cs:16  /* im Lambda */",
                "Probe.cs:19  // im Expression Body",
            ],
            findings);
    }

    private static IEnumerable<string> Find(SyntaxTree tree, string fileName) =>
        tree.GetRoot()
            .DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .Where(t => !IsToolDirective(t) && IsInBody(t.Token))
            .Select(t => $"{fileName}:{t.GetLocation().GetLineSpan().StartLinePosition.Line + 1}  {t.ToString().Trim()}");

    private static bool IsToolDirective(SyntaxTrivia comment)
    {
        var text = comment.ToString().TrimStart('/', '*', ' ');
        return ToolDirectives.Any(d => text.StartsWith(d, StringComparison.Ordinal));
    }

    private static bool IsInBody(SyntaxToken token) =>
        token.Parent?.AncestorsAndSelf().Any(node => node
            is BlockSyntax
            or ArrowExpressionClauseSyntax
            or GlobalStatementSyntax
            or EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax or PropertyDeclarationSyntax }) == true;
}
