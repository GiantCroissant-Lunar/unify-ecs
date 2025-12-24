using UnifyECS;

namespace UnifyEcs.Sample.ArchGame
{
    [EcsSystem(Phase = SystemPhase.Update)]
    [EcsOptimize(EcsBackend.Arch, InlineQuery = true)]
    public partial class MoveRightSystem
    {
        [Query]
        private void Move(ref Position pos)
        {
            pos.X += 1f;
        }
    }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class SpawnWithVelocitySystem
    {
        [Inject]
        public ICommandBuffer Commands { get; set; } = null!;

        [Query]
        [StructuralChanges(Mode = StructuralChangeMode.Deferred,
                           Changes = new[] { StructuralChangeType.CreateEntity, StructuralChangeType.AddComponent })]
        private void Spawn(ref Position pos)
        {
            if (pos.X < 5f)
            {
                var e = Commands.CreateEntity();
                Commands.Add(e, new Position { X = pos.X + 20f, Y = pos.Y });
                Commands.Add(e, new Velocity { X = 1f, Y = 0f });
            }
        }
    }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class SpawnSystem
    {
        [Inject]
        public ICommandBuffer Commands { get; set; } = null!;

        [Query]
        [StructuralChanges(Mode = StructuralChangeMode.Deferred,
                           Changes = new[] { StructuralChangeType.CreateEntity, StructuralChangeType.AddComponent })]
        private void Spawn(ref Position pos)
        {
            if (pos.X < 10f)
            {
                Commands.CreateEntity(new Position { X = pos.X + 10f, Y = pos.Y });
            }
        }
    }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class KillIfTooFarSystem
    {
        [Inject]
        public IWorld World { get; set; } = null!;

        [Query]
        [StructuralChanges(Mode = StructuralChangeMode.Immediate,
                           Changes = new[] { StructuralChangeType.DestroyEntity })]
        private void Kill(Entity entity, ref Position pos)
        {
            if (pos.X > 100f)
            {
                System.Console.WriteLine($"KillIfTooFarSystem: destroying entity at X={pos.X}");
                World.DestroyEntity(entity);
            }
        }
    }

    [EcsSystem(Phase = SystemPhase.Update)]
    [EcsRequires(EcsFeature.Reactive)]
    public partial class HealthReactiveSystem
    {
        public int AddedCount { get; private set; }
        public int ChangedCount { get; private set; }
        public int RemovedCount { get; private set; }

        [Query(All = new[] { typeof(Health) })]
        private void Tick(ref Health health)
        {
            // The reactive helpers operate on snapshots; Tick can remain empty.
        }

        [OnAdded(typeof(Health))]
        private void OnHealthAdded(Entity entity, in Health health)
        {
            AddedCount++;
        }

        [OnChanged(typeof(Health))]
        private void OnHealthChanged(Entity entity, in Health health)
        {
            ChangedCount++;
        }

        [OnRemoved(typeof(Health))]
        private void OnHealthRemoved(Entity entity, in Health health)
        {
            RemovedCount++;
        }
    }

    [EcsSystem(Phase = SystemPhase.Update)]
    public partial class VisualPerceptionSystem
    {
        [Query]
        private void Cache(ref Position pos, ref Perception perception)
        {
            perception.VisibleCount = (int)pos.X;
        }

        [Query]
        private void Finalize(ref Perception perception)
        {
            perception.VisibleCount += 1;
        }
    }
}
