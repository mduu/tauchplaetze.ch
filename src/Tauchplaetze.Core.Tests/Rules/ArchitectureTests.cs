using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Tauchplaetze.Core.CrossCutting.UseCases;

namespace Tauchplaetze.Core.Tests.Rules;

/// <remarks>
/// Drei Schichten (architecture.md, Testing): Der Compiler hält `internal` fest. Mono.Cecil liest die
/// Assembly-Referenzen und die Methodenrümpfe, auch die vom Compiler erzeugten Typen eines `async`-Handlers.
/// Roslyn prüft die Dateien. Jede Regel hat ein Gegenbeispiel, das anschlagen muss.
/// </remarks>
public class ArchitectureTests
{
    private static readonly string[] InfrastructureAssemblies =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Data.SqlClient",
        "Azure.",
        "NetTopologySuite",
    ];

    private static readonly string[] WritingMethods = ["SaveChanges", "SaveChangesAsync", "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

    private static Assembly CoreAssembly => typeof(CoreRegistration).Assembly;

    [Fact]
    public void Die_Fassade_Web_referenziert_keine_Infrastruktur()
    {
        // Arrange
        using var web = ModuleDefinition.ReadModule(typeof(Program).Assembly.Location);

        // Act
        var findings = InfrastructureReferences(web).ToList();

        // Assert
        Assert.True(findings.Count == 0,
            "Web greift auf Infrastruktur zu. Nur der Core darf das (architecture.md, Prinzipien):\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Der_Core_referenziert_Infrastruktur_und_die_Regel_sieht_das()
    {
        // Arrange
        using var core = ModuleDefinition.ReadModule(CoreAssembly.Location);

        // Act
        var findings = InfrastructureReferences(core).ToList();

        // Assert
        Assert.Contains(findings, f => f.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Die_Daten_des_Core_sind_nicht_oeffentlich()
    {
        // Act
        var findings = CoreAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Tauchplaetze.Core.Data", StringComparison.Ordinal) == true)
            .Where(t => t.IsPublic || t.IsNestedPublic)
            .Where(t => !IsMigration(t))
            .Select(t => t.FullName)
            .ToList();

        // Assert
        Assert.True(findings.Count == 0, "Öffentliche Typen in Tauchplaetze.Core.Data:\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Handler_sind_internal_und_liegen_in_Commands_oder_Queries()
    {
        // Act
        var findings = Handlers(CoreAssembly).SelectMany(HandlerViolations).ToList();

        // Assert
        Assert.True(findings.Count == 0, "Regeln für Handler verletzt (architecture.md, Ein Use Case):\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Ein_oeffentlicher_Handler_ausserhalb_von_Queries_faellt_auf()
    {
        // Act
        var findings = HandlerViolations(typeof(Probes.PublicHandlerInWrongPlace)).ToList();

        // Assert
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Query_Handler_schreiben_nichts()
    {
        // Arrange
        using var core = ModuleDefinition.ReadModule(CoreAssembly.Location);

        // Act
        var findings = WritingQueryHandlers(core).ToList();

        // Assert
        Assert.True(findings.Count == 0, "Query-Handler mit Seiteneffekt (ADR-002, CQS):\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Ein_Query_Handler_mit_SaveChanges_im_async_Body_faellt_auf()
    {
        // Arrange
        using var tests = ModuleDefinition.ReadModule(typeof(ArchitectureTests).Assembly.Location);

        // Act
        var findings = WritingQueryHandlers(tests).ToList();

        // Assert
        Assert.Equal([$"{typeof(Probes.WritingQueryHandler).FullName} ruft SaveChangesAsync"], findings);
    }

    [Fact]
    public void Jede_Datei_in_Commands_und_Queries_enthaelt_genau_einen_passenden_Handler()
    {
        // Arrange
        var features = Path.Combine(RepositorySources.Root(), "src", "Tauchplaetze.Core", "Features");

        // Act
        var findings = Directory.EnumerateFiles(features, "*.cs", SearchOption.AllDirectories)
            .Where(f => FolderOf(f) is "Commands" or "Queries")
            .Select(f => HandlerFileViolation(f, File.ReadAllText(f)))
            .OfType<string>()
            .ToList();

        // Assert
        Assert.True(findings.Count == 0, "Use-Case-Dateien verletzt (architecture.md, Ein Use Case):\n  " + string.Join("\n  ", findings));
    }

    [Fact]
    public void Eine_Query_Datei_mit_zwei_Handlern_faellt_auf()
    {
        // Arrange
        const string source = """
            internal class FirstHandler : IQueryHandler<A, int> { }
            internal class SecondHandler : IQueryHandler<B, int> { }
            """;

        // Act
        var finding = HandlerFileViolation(Path.Combine("Features", "X", "Queries", "Two.cs"), source);

        // Assert
        Assert.NotNull(finding);
    }

    private static IEnumerable<string> InfrastructureReferences(ModuleDefinition module) =>
        module.AssemblyReferences
            .Select(r => r.Name)
            .Where(name => InfrastructureAssemblies.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));

    private static bool IsMigration(Type type) =>
        type.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) == true;

    private static IEnumerable<Type> Handlers(Assembly assembly) =>
        assembly.GetTypes().Where(t => t.GetInterfaces().Any(HandlerRegistration.IsHandlerContract));

    private static IEnumerable<string> HandlerViolations(Type handler)
    {
        if (handler.IsPublic || handler.IsNestedPublic)
        {
            yield return $"{handler.FullName} ist öffentlich";
        }

        var expected = IsQueryHandler(handler) ? ".Queries" : ".Commands";
        if (handler.Namespace?.EndsWith(expected, StringComparison.Ordinal) != true)
        {
            yield return $"{handler.FullName} liegt nicht in {expected}";
        }
    }

    private static bool IsQueryHandler(Type handler) =>
        handler.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

    private static IEnumerable<string> WritingQueryHandlers(ModuleDefinition module) =>
        module.Types
            .Where(t => t.Interfaces.Any(i => i.InterfaceType.Name.StartsWith("IQueryHandler", StringComparison.Ordinal)))
            .SelectMany(handler => WritingCalls(handler).Distinct().Select(call => $"{handler.FullName.Replace('/', '+')} ruft {call}"));

    private static IEnumerable<string> WritingCalls(TypeDefinition type) =>
        type.Methods
            .Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions)
            .Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
            .Select(i => ((MethodReference)i.Operand).Name)
            .Where(WritingMethods.Contains)
            .Concat(type.NestedTypes.SelectMany(WritingCalls));

    private static string FolderOf(string file) => Path.GetFileName(Path.GetDirectoryName(file)) ?? "";

    private static string? HandlerFileViolation(string file, string source)
    {
        var contract = FolderOf(file) == "Commands" ? "ICommandHandler" : "IQueryHandler";
        var handlers = CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Count(c => c.BaseList?.Types.Any(t => t.ToString().StartsWith(contract + "<", StringComparison.Ordinal)) == true);

        return handlers == 1 ? null : $"{Path.GetFileName(file)}: {handlers} × {contract}";
    }
}
