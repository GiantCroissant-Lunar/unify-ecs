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
    public class UnifyEcsAnalyzer_Uecs019Tests
    {
        [Fact]
        public void UECS019_Reported_When_DeferredQuery_Mixes_World_And_CommandBuffer()
        {
            const string source = @"using UnifyECS;

[EcsComponent]
public partial struct Position
{
    public float X;
}

[EcsSystem]
public partial class MixedSystem
{
    [Inject] public IWorld World { get; set; } = null!;
    [Inject] public ICommandBuffer Commands { get; set; } = null!;

    [Query]
    [StructuralChanges(Mode = StructuralChangeMode.Deferred,
                       Changes = new[] { StructuralChangeType.DestroyEntity })]
    private void Run(Entity e, ref Position pos)
    {
        World.DestroyEntity(e);
        Commands.DestroyEntity(e);
    }
}
";

            var diagnostics = GetAnalyzerDiagnostics(source);

            var diag = diagnostics.SingleOrDefault(d => d.Id == "UECS019");
            if (diag is null)
            {
                var all = string.Join(" | ", diagnostics.Select(d =>
                {
                    var location = d.Location.GetLineSpan();
                    var path = location.Path;
                    var line = location.StartLinePosition.Line + 1;
                    var column = location.StartLinePosition.Character + 1;
                    return $"{d.Id}:{d.GetMessage()} @ {path}({line},{column})";
                }));
                Assert.True(false, $"Expected UECS019 but saw: {all}");
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
                assemblyName: "UnifyEcsAnalyzerTests",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Sanity check: ensure core UnifyECS types are resolvable in this compilation
            var worldType = compilation.GetTypeByMetadataName("UnifyECS.IWorld");
            var commandBufferType = compilation.GetTypeByMetadataName("UnifyECS.ICommandBuffer");
            Assert.NotNull(worldType);
            Assert.NotNull(commandBufferType);

            var analyzer = new UnifyEcsAnalyzer();
            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(analyzer);
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

            var allDiagnostics = compilationWithAnalyzers.GetAllDiagnosticsAsync().GetAwaiter().GetResult();
            return allDiagnostics.Where(d => d.Id.StartsWith("UECS", StringComparison.Ordinal)).ToImmutableArray();
        }
    }
}
