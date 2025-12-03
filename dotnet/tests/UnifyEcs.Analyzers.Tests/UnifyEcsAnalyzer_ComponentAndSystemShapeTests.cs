using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using UnifyECS;
using UnifyECS.Analyzers;
using Xunit;

namespace UnifyEcs.Analyzers.Tests
{
    public sealed class UnifyEcsAnalyzer_ComponentAndSystemShapeTests
    {
        [Fact]
        public void UECS004_Reported_When_Component_Is_Class()
        {
            const string source = @"using UnifyECS;

[EcsComponent]
public class BadComponent
{
    public int Value;
}
";

            var diagnostics = GetAnalyzerDiagnostics(source);

            var diag = diagnostics.SingleOrDefault(d => d.Id == "UECS004");
            if (diag is null)
            {
                var all = string.Join(" | ", diagnostics.Select(FormatDiagnostic));
                Assert.True(false, $"Expected UECS004 but saw: {all}");
            }

            Assert.Contains("BadComponent", diag!.GetMessage());
        }

        [Fact]
        public void UECS007_Reported_When_System_Is_Not_Partial()
        {
            const string source = @"using UnifyECS;

[EcsSystem]
public sealed class NotPartialSystem
{
    [Query]
    private void Run(ref Entity e) { }
}
";

            var diagnostics = GetAnalyzerDiagnostics(source);

            var diag = diagnostics.SingleOrDefault(d => d.Id == "UECS007");
            if (diag is null)
            {
                var all = string.Join(" | ", diagnostics.Select(FormatDiagnostic));
                Assert.True(false, $"Expected UECS007 but saw: {all}");
            }

            Assert.Contains("NotPartialSystem", diag!.GetMessage());
        }

        [Fact]
        public void UECS016_Reported_When_Query_Returns_NonVoid()
        {
            const string source = @"using UnifyECS;

[EcsComponent]
public partial struct Position { public float X; }

[EcsSystem]
public sealed partial class QueryReturnsIntSystem
{
    [Query(All = new[] { typeof(Position) })]
    private int Run(ref Position pos)
    {
        return 0;
    }
}
";

            var diagnostics = GetAnalyzerDiagnostics(source);

            var diag = diagnostics.SingleOrDefault(d => d.Id == "UECS016");
            if (diag is null)
            {
                var all = string.Join(" | ", diagnostics.Select(FormatDiagnostic));
                Assert.True(false, $"Expected UECS016 but saw: {all}");
            }

            Assert.Contains("Run", diag!.GetMessage());
        }

        private static ImmutableArray<Diagnostic> GetAnalyzerDiagnostics(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Entity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(EcsSystemAttribute).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName: "UnifyEcsAnalyzerComponentAndSystemShapeTests",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analyzer = new UnifyEcsAnalyzer();
            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(analyzer);
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

            var allDiagnostics = compilationWithAnalyzers.GetAllDiagnosticsAsync().GetAwaiter().GetResult();
            return allDiagnostics.Where(d => d.Id.StartsWith("UECS", StringComparison.Ordinal)).ToImmutableArray();
        }

        private static string FormatDiagnostic(Diagnostic d)
        {
            var location = d.Location.GetLineSpan();
            var path = location.Path;
            var line = location.StartLinePosition.Line + 1;
            var column = location.StartLinePosition.Character + 1;
            return $"{d.Id}:{d.GetMessage()} @ {path}({line},{column})";
        }
    }
}
