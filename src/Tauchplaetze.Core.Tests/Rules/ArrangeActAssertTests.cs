using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tauchplaetze.Core.Tests.Rules;

public class ArrangeActAssertTests
{
    [Fact]
    public void Jede_Testmethode_mit_mehreren_Anweisungen_kennzeichnet_ihre_Pruefung()
    {
        // Act
        var findings = RepositorySources.CSharpFiles()
            .Where(RepositorySources.IsTestFile)
            .SelectMany(f => WithoutAssert(CSharpSyntaxTree.ParseText(File.ReadAllText(f)), Path.GetFileName(f)))
            .ToList();

        // Assert
        Assert.True(findings.Count == 0,
            "Testmethoden ohne «// Assert» (architecture.md, Testing):\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Ein_Test_ohne_Assert_Kommentar_faellt_auf()
    {
        // Arrange
        const string source = """
            public class Probe
            {
                [Fact]
                public void Ohne_Phasen()
                {
                    var x = 1;
                    Assert.Equal(1, x);
                }

                [Fact]
                public void Mit_Phasen()
                {
                    // Arrange
                    var x = 1;

                    // Assert
                    Assert.Equal(1, x);
                }

                [Theory]
                [InlineData(1)]
                public void Mit_Exception(int x)
                {
                    var y = x;

                    // Act & Assert
                    Assert.Throws<Exception>(() => throw new Exception());
                }

                [SqlFact]
                public void Sql_ohne_Phasen()
                {
                    var x = 1;
                    Assert.Equal(1, x);
                }

                [Fact]
                public void Einzeiler() => Assert.True(true);

                private static void Kein_Test()
                {
                    var x = 1;
                    _ = x;
                }
            }
            """;

        // Act
        var findings = WithoutAssert(CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken), "Probe.cs").ToList();

        // Assert
        Assert.Equal(["Probe.cs:4  Ohne_Phasen", "Probe.cs:31  Sql_ohne_Phasen"], findings);
    }

    private static IEnumerable<string> WithoutAssert(SyntaxTree tree, string fileName) =>
        tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(IsTest)
            .Where(m => m.Body is { Statements.Count: > 1 })
            .Where(m => !m.Body!.DescendantTrivia().Any(IsAssertComment))
            .Select(m => $"{fileName}:{m.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1}  {m.Identifier.ValueText}");

    private static bool IsTest(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(a => a.Name.ToString() is "Fact" or "Theory" or "SqlFact" or "SqlTheory");

    private static bool IsAssertComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        && trivia.ToString().Trim() is "// Assert" or "// Act & Assert";
}
