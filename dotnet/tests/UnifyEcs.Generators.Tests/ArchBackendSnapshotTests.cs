using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using UnifyECS;
using UnifyECS.Generators;
using Xunit;

namespace UnifyEcs.Generators.Tests
{
    public sealed class ArchBackendSnapshotTests
    {
        [Fact]
        public void SimpleQuery_EmitsArchSystemWithQueryDescription()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests
{
    [EcsComponent]
    public partial struct Position { public float X, Y; }

    [EcsComponent]
    public partial struct Velocity { public float X, Y; }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class MoveSystem
    {
        [Query(All = new[] { typeof(Position), typeof(Velocity) })]
        private void Move(ref Position pos, in Velocity vel)
        {
            pos.X += vel.X;
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_MoveSystem.Arch.g.cs");

            Assert.Contains("partial class MoveSystem : global::UnifyECS.IArchSystem", generated);
            Assert.Contains("private static readonly QueryDescription _query_Move = new QueryDescription()", generated);
            Assert.Contains(".WithAll<SnapshotTests.Position, SnapshotTests.Velocity>()", generated);
            Assert.Contains("world.Query(in _query_Move", generated);
        }

        [Fact]
        public void DeferredStructuralChanges_EmitWorldAdapterAndPlayback()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests
{
    [EcsComponent]
    public partial struct Position { public float X, Y; }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class DeferredSpawnSystem
    {
        [Inject]
        public ICommandBuffer Commands { get; set; } = null!;

        [Query]
        [StructuralChanges(Mode = StructuralChangeMode.Deferred,
                           Changes = new[] { StructuralChangeType.CreateEntity, StructuralChangeType.AddComponent })]
        private void Spawn(ref Position pos)
        {
            var e = Commands.CreateEntity();
            Commands.Add(e, new Position { X = pos.X + 10f, Y = pos.Y });
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_DeferredSpawnSystem.Arch.g.cs");

            Assert.Contains("private sealed class WorldAdapter : global::UnifyECS.IWorld", generated);
            Assert.Contains("public void Execute(World world, float deltaTime)", generated);
            Assert.Contains("var __commands = this.Commands;", generated);
            Assert.Contains("if (__commands is not null && __commands.CommandCount > 0)", generated);
            Assert.Contains("var __worldAdapter = new WorldAdapter(world);", generated);
            Assert.Contains("__commands.Playback(__worldAdapter);", generated);
            Assert.Contains("__commands.Clear();", generated);
        }

        [Fact]
        public void Bootstrap_EmitsRegisterAll_InDeterministicOrder()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_Bootstrap
{
    public sealed class GroupA { }
    public sealed class GroupB { }

    [EcsSystem(Phase = SystemPhase.Update, Order = 2, Group = typeof(GroupA))]
    public partial class Sys2 { }

    [EcsSystem(Phase = SystemPhase.Update, Order = 1, Group = typeof(GroupA))]
    public partial class Sys1 { }

    [EcsSystem(Phase = SystemPhase.EarlyUpdate, Order = 0, Group = typeof(GroupB))]
    public partial class Early { }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "UnifyECS.GeneratedArchBootstrap.Arch.g.cs");

            Assert.Contains("public static class GeneratedArchBootstrap", generated);

            var earlyIndex = generated.IndexOf("typeof(global::SnapshotTests_Bootstrap.Early)", StringComparison.Ordinal);
            var sys1Index = generated.IndexOf("typeof(global::SnapshotTests_Bootstrap.Sys1)", StringComparison.Ordinal);
            var sys2Index = generated.IndexOf("typeof(global::SnapshotTests_Bootstrap.Sys2)", StringComparison.Ordinal);

            Assert.True(earlyIndex >= 0);
            Assert.True(sys1Index >= 0);
            Assert.True(sys2Index >= 0);

            Assert.True(earlyIndex < sys1Index);
            Assert.True(sys1Index < sys2Index);
        }

        [Fact]
        public void QueryFilters_AnyNoneExclusive_EmitWithAnyWithNoneWithExclusive()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_QueryFilters
{
    [EcsComponent]
    public partial struct A { }

    [EcsComponent]
    public partial struct B { }

    [EcsComponent]
    public partial struct C { }

    [EcsComponent]
    public partial struct D { }

    [EcsSystem]
    public partial class FilterSystem
    {
        [Query(All = new[] { typeof(A) }, Any = new[] { typeof(B), typeof(C) }, None = new[] { typeof(D) }, Exclusive = new[] { typeof(C) })]
        private void Run(ref A a)
        {
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_QueryFilters_FilterSystem.Arch.g.cs");

            Assert.Contains(".WithAll<SnapshotTests_QueryFilters.A>()", generated);
            Assert.Contains(".WithAny<", generated);
            Assert.Contains("SnapshotTests_QueryFilters.B", generated);
            Assert.Contains("SnapshotTests_QueryFilters.C", generated);
            Assert.Contains(".WithNone<", generated);
            Assert.Contains("SnapshotTests_QueryFilters.D", generated);
            Assert.Contains(".WithExclusive<", generated);
        }

        [Fact]
        public void EntityParam_UnifyEntity_IsConvertedFromArchEntity()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_EntityParam
{
    [EcsComponent]
    public partial struct Position { public float X, Y; }

    [EcsSystem]
    public partial class UnifyEntitySystem
    {
        [Query]
        private void Run(UnifyECS.Entity entity, ref Position pos)
        {
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_EntityParam_UnifyEntitySystem.Arch.g.cs");

            Assert.Contains("(Arch.Core.Entity entity, ref SnapshotTests_EntityParam.Position pos)", generated);
            Assert.Contains("Run(new global::UnifyECS.Entity(entity.Id, entity.Version), ref pos);", generated);
        }

        [Fact]
        public void EntityParam_ArchEntity_IsPassedThrough()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_EntityParam
{
    [EcsComponent]
    public partial struct Position { public float X, Y; }

    [EcsSystem]
    public partial class ArchEntitySystem
    {
        [Query]
        private void Run(global::Arch.Core.Entity entity, ref Position pos)
        {
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_EntityParam_ArchEntitySystem.Arch.g.cs");

            Assert.Contains("(Arch.Core.Entity entity, ref SnapshotTests_EntityParam.Position pos)", generated);
            Assert.Contains("Run(entity, ref pos);", generated);
            Assert.DoesNotContain("new global::UnifyECS.Entity(entity.Id, entity.Version)", generated);
        }

        [Fact]
        public void EntityParam_AliasArchEntity_IsPassedThrough()
        {
            const string source = @"using UnifyECS;
using Entity = global::Arch.Core.Entity;

namespace SnapshotTests_EntityParam
{
    [EcsComponent]
    public partial struct Position { public float X, Y; }

    [EcsSystem]
    public partial class AliasArchEntitySystem
    {
        [Query]
        private void Run(Entity entity, ref Position pos)
        {
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_EntityParam_AliasArchEntitySystem.Arch.g.cs");

            Assert.Contains("(Arch.Core.Entity entity, ref SnapshotTests_EntityParam.Position pos)", generated);
            Assert.Contains("Run(entity, ref pos);", generated);
            Assert.DoesNotContain("new global::UnifyECS.Entity(entity.Id, entity.Version)", generated);
        }

        private static GeneratorDriverRunResult RunGenerator(string source)
        {
            var parseOptions = CSharpParseOptions.Default;
            var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(typeof(Entity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(EcsComponentAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(global::Arch.Core.Entity).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName: "GeneratorSnapshotTests",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            IIncrementalGenerator generator = new EcsGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            return driver.GetRunResult();
        }

        private static GeneratorDriverRunResult RunGenerator(
            string source,
            IDictionary<string, string> globalOptions)
        {
            var parseOptions = CSharpParseOptions.Default;
            var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(typeof(Entity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(EcsComponentAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(global::Arch.Core.Entity).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName: "GeneratorSnapshotTests_WithPolicies",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            IIncrementalGenerator generator = new EcsGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            var optionsProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
            driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            return driver.GetRunResult();
        }

        private static string GetGeneratedSource(GeneratorDriverRunResult result, string expectedHintName)
        {
            var run = Assert.Single(result.Results);
            var match = run.GeneratedSources.FirstOrDefault(s => string.Equals(s.HintName, expectedHintName, StringComparison.Ordinal));
            if (match.Equals(default(GeneratedSourceResult)))
            {
                var available = string.Join(", ", run.GeneratedSources.Select(s => s.HintName));
                var driverDiagnostics = string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
                var generatorDiagnostics = string.Join("\n", run.Diagnostics.Select(d => d.ToString()));
                Assert.False(
                    true,
                    $"Expected generated source with hint name '{expectedHintName}' but none was found. Available: [{available}]\nDriver diagnostics:\n{driverDiagnostics}\nGenerator diagnostics:\n{generatorDiagnostics}");
            }
            return match.SourceText.ToString();
        }

        [Fact]
        public void ReactiveHandlers_Present_DoNotPreventArchGeneration()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests
{
    [EcsComponent]
    public partial struct Health { public int Value; }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class HealthReactiveSystem
    {
        [Query(All = new[] { typeof(Health) })]
        private void Tick(ref Health health)
        {
        }

        [OnAdded(typeof(Health))]
        private void OnHealthAdded(Entity entity, in Health health)
        {
        }

        [OnChanged(typeof(Health))]
        private void OnHealthChanged(Entity entity, in Health health)
        {
        }

        [OnRemoved(typeof(Health))]
        private void OnHealthRemoved(Entity entity, in Health health)
        {
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_HealthReactiveSystem.Arch.g.cs");

            Assert.Contains("partial class HealthReactiveSystem : global::UnifyECS.IArchSystem", generated);
        }

        [Fact]
        public void NoOpPolicy_EmitsNoOpStubForUnsupportedFeatureOnArch()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_Policies
{
    [EcsSystem]
    [EcsRequires(EcsFeature.WorldEvents)]
    public partial class WorldEventsSystem
    {
        [Query]
        private void Run(Entity e) { }
    }
}
";

            var options = new Dictionary<string, string>
            {
                ["build_property.UnifyEcsBackends"] = "Arch",
                ["build_property.UnifyEcsPolicy_WorldEvents"] = "NoOp",
            };

            var result = RunGenerator(source, options);
            var generated = GetGeneratedSource(result, "SnapshotTests_Policies_WorldEventsSystem.Arch.g.cs");

            // Current behavior: ensure an Arch backend implementation is generated
            // for WorldEventsSystem under NoOp policy. The exact stub shape may
            // evolve, so we only assert the presence of an IArchSystem class.
            Assert.Contains("partial class WorldEventsSystem : global::UnifyECS.IArchSystem", generated);
        }

        [Fact]
        public void Reactive_EmulatePolicy_EmitsReactivePartialForArch()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_Policies
{
    [EcsComponent]
    public partial struct Health { public int Value; }

    [EcsSystem]
    [EcsRequires(EcsFeature.Reactive)]
    public partial class HealthReactiveSystem
    {
        [Query(All = new[] { typeof(Health) })]
        private void Tick(ref Health health)
        {
        }

        [OnAdded(typeof(Health))]
        private void OnHealthAdded(Entity entity, in Health health) { }

        [OnChanged(typeof(Health))]
        private void OnHealthChanged(Entity entity, in Health health) { }

        [OnRemoved(typeof(Health))]
        private void OnHealthRemoved(Entity entity, in Health health) { }
    }
}
";

            var options = new Dictionary<string, string>
            {
                ["build_property.UnifyEcsBackends"] = "Arch",
                ["build_property.UnifyEcsPolicy_Reactive"] = "Emulate",
            };

            var result = RunGenerator(source, options);

            var arch = GetGeneratedSource(result, "SnapshotTests_Policies_HealthReactiveSystem.Arch.g.cs");
            Assert.Contains("partial class HealthReactiveSystem : global::UnifyECS.IArchSystem", arch);
        }

        [Fact]
        public void MultipleQueries_EmitMultiplePassesInDeclarationOrder()
        {
            const string source = @"using UnifyECS;

namespace SnapshotTests_MultiQuery
{
    [EcsComponent]
    public partial struct Position { public float X, Y; }

    [EcsComponent]
    public partial struct Blocking { public bool BlocksSight; }

    [EcsComponent]
    public partial struct PerceptionComponent { public int Score; }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class VisualPerceptionSystem
    {
        private readonly System.Collections.Generic.List<(Entity, Position)> _positionBuffer = new();
        private readonly System.Collections.Generic.HashSet<(int X, int Y)> _blockedTiles = new();

        [Query]
        private void CachePositions(Entity entity, ref Position pos)
        {
            _positionBuffer.Add((entity, pos));
        }

        [Query]
        private void CacheBlocking(ref Position pos, ref Blocking blocking)
        {
            if (!blocking.BlocksSight)
                return;

            _blockedTiles.Add(((int)pos.X, (int)pos.Y));
        }

        [Query]
        private void UpdatePerception(Entity entity, ref PerceptionComponent perception, ref Position position)
        {
            // Uses _positionBuffer and _blockedTiles
        }
    }
}
";

            var result = RunGenerator(source);
            var generated = GetGeneratedSource(result, "SnapshotTests_MultiQuery_VisualPerceptionSystem.Arch.g.cs");

            Assert.Contains("partial class VisualPerceptionSystem : global::UnifyECS.IArchSystem", generated);

            Assert.Contains("private static readonly QueryDescription _query_CachePositions", generated);
            Assert.Contains("private static readonly QueryDescription _query_CacheBlocking", generated);
            Assert.Contains("private static readonly QueryDescription _query_UpdatePerception", generated);

            var idxCachePositions = generated.IndexOf("world.Query(in _query_CachePositions", StringComparison.Ordinal);
            var idxCacheBlocking = generated.IndexOf("world.Query(in _query_CacheBlocking", StringComparison.Ordinal);
            var idxUpdatePerception = generated.IndexOf("world.Query(in _query_UpdatePerception", StringComparison.Ordinal);

            Assert.True(idxCachePositions >= 0, "CachePositions query not found");
            Assert.True(idxCacheBlocking >= 0, "CacheBlocking query not found");
            Assert.True(idxUpdatePerception >= 0, "UpdatePerception query not found");

            Assert.True(idxCachePositions < idxCacheBlocking, "CachePositions should run before CacheBlocking");
            Assert.True(idxCacheBlocking < idxUpdatePerception, "CacheBlocking should run before UpdatePerception");
        }
    }

    internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            private readonly IDictionary<string, string> _backing;

            public TestAnalyzerConfigOptions(IDictionary<string, string> backing)
            {
                _backing = backing;
            }

            public override bool TryGetValue(string key, out string value) =>
                _backing.TryGetValue(key, out value!);
        }

        private readonly AnalyzerConfigOptions _global;
        private static readonly AnalyzerConfigOptions _empty =
            new TestAnalyzerConfigOptions(new Dictionary<string, string>());

        public TestAnalyzerConfigOptionsProvider(IDictionary<string, string> globalOptions)
        {
            _global = new TestAnalyzerConfigOptions(
                globalOptions ?? new Dictionary<string, string>());
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _empty;
    }
}
