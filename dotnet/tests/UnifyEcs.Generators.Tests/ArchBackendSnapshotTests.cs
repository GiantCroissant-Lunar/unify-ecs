using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

        private static GeneratorDriverRunResult RunGenerator(string source)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
            var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Entity).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(EcsComponentAttribute).Assembly.Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName: "GeneratorSnapshotTests",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            IIncrementalGenerator generator = new EcsGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            return driver.GetRunResult();
        }

        private static string GetGeneratedSource(GeneratorDriverRunResult result, string expectedHintName)
        {
            var run = Assert.Single(result.Results);
            var match = run.GeneratedSources.FirstOrDefault(s => string.Equals(s.HintName, expectedHintName, StringComparison.Ordinal));
            Assert.False(match.Equals(default(GeneratedSourceResult)), $"Expected generated source with hint name '{expectedHintName}' but none was found.");
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
    }
}
