using Arch.Core;
using Arch.Core.Extensions;
using UnifyECS;
using Xunit;

namespace UnifyEcs.Runtime.Arch.Tests;

/// <summary>
/// Regression harness for the pattern of calling Has/TryGet inside generated
/// Arch backend queries. This is currently marked Skip because it is known
/// to crash when executed against certain Arch versions; see consumer
/// projects (e.g. mung-bean) and RFC notes for details.
/// </summary>
public sealed class HasInsideQueryRegressionTests
{
    [EcsComponent]
    public partial struct Position
    {
        public float X;
        public float Y;
    }

    [EcsComponent]
    public partial struct PlayerTag
    {
    }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class HazardousClassificationSystem : IArchSystem
    {
        private static readonly QueryDescription Query = new QueryDescription().WithAll<Position>();

        [Query]
        private void Classify(global::Arch.Core.Entity entity, ref Position pos)
        {
            // This pattern (Has/TryGet inside a generated Arch query) has been
            // observed to cause native AccessViolation crashes in real
            // consumer projects when executed. The test below is intentionally
            // skipped until the underlying issue is fully understood and
            // addressed in either Arch or the UnifyECS backend.
            if (entity.Has<PlayerTag>())
            {
                // no-op; we just exercise the Has path
            }
        }

        public void Execute(World world, float deltaTime)
        {
            world.Query(in Query, (global::Arch.Core.Entity entity, ref Position pos) =>
            {
                Classify(entity, ref pos);
            });
        }
    }

    [Fact(Skip = "Known Arch backend issue: Has/TryGet inside generated queries can cause native crash on some versions. See RFC-0014 and consumer regressions.")]
    public void HasInsideQuery_OnArchBackend_IsCurrentlyUnsafe()
    {
        var runner = new ArchMultiWorldRunner();
        var world = runner.Worlds.GetOrCreate("Game");

        var entity = world.CreateEntity();
        world.Add(entity, new Position { X = 0f, Y = 0f });
        world.Add(entity, new PlayerTag());

        var system = new HazardousClassificationSystem();
        runner.Register("Game", system);

        runner.Initialize();

        // When the underlying issue is fixed, this call should execute
        // without crashing and the Skip can be removed.
        runner.Update(1f);
    }
}
