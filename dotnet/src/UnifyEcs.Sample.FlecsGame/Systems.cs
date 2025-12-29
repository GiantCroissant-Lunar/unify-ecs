using UnifyECS;

namespace UnifyEcs.Sample.FlecsGame;

/// <summary>
/// Movement system - updates positions based on velocity.
/// </summary>
[EcsSystem(Phase = SystemPhase.Update, Order = 0)]
public partial class MovementSystem
{
    [Query]
    private void UpdatePosition(in Velocity velocity, ref Position position)
    {
        position.X += velocity.X;
        position.Y += velocity.Y;
    }
}

/// <summary>
/// Health decay system - reduces health over time.
/// </summary>
[EcsSystem(Phase = SystemPhase.Update, Order = 1)]
public partial class HealthDecaySystem
{
    [Query]
    private void DecayHealth(ref Health health)
    {
        if (health.Value > 0)
        {
            health.Value--;
        }
    }
}

/// <summary>
/// Cleanup system - removes entities with zero health.
/// Note: Flecs backend doesn't support structural changes yet.
/// </summary>
[EcsSystem(Phase = SystemPhase.LateUpdate, Order = 0)]
public partial class CleanupSystem
{
    private readonly IWorld _world;

    public CleanupSystem(IWorld world)
    {
        _world = world;
    }

    [Query]
    private void RemoveDeadEntities(ref Health health, UnifyECS.Entity entity)
    {
        if (health.Value <= 0)
        {
            _world.DestroyEntity(entity);
        }
    }
}
