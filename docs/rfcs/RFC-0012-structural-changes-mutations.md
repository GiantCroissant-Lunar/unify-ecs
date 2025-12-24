# RFC-0012: Structural Changes & Mutation Rules

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0003, RFC-0010, RFC-0011
- **Addresses**: Review Issue #5 (DOTS structural change safety)

## Summary

Define the rules and mechanisms for handling structural changes (adding/removing components, creating/destroying entities) across all backends, with special attention to DOTS thread-safety requirements.

## Motivation

Different ECS backends handle structural changes differently:

| Backend | Structural Change Behavior |
|---------|---------------------------|
| **Arch** | Immediate, blocks iteration |
| **Entitas** | Immediate, may invalidate groups |
| **DOTS** | Deferred via EntityCommandBuffer (ECB), required in jobs |

UnifyECS must:
1. Provide a unified model for structural changes
2. Generate thread-safe code for DOTS
3. Warn users about backend-specific pitfalls
4. Support both immediate and deferred patterns

---

## Structural Change Types

```csharp
/// <summary>
/// Types of operations that modify world structure
/// </summary>
public enum StructuralChangeType
{
    /// <summary>Creating a new entity</summary>
    CreateEntity,
    
    /// <summary>Destroying an existing entity</summary>
    DestroyEntity,
    
    /// <summary>Adding a component to an entity</summary>
    AddComponent,
    
    /// <summary>Removing a component from an entity</summary>
    RemoveComponent,
    
    /// <summary>Changing shared component value (DOTS)</summary>
    SetSharedComponent,
}
```

---

## Deferred Command Buffer

### ICommandBuffer Interface

```csharp
/// <summary>
/// Buffer for deferred structural changes.
/// Commands are executed at a synchronization point.
/// </summary>
public interface ICommandBuffer : IDisposable
{
    /// <summary>Create a new entity (returns temporary handle)</summary>
    Entity CreateEntity();
    
    /// <summary>
    /// Create entity with initial components.
    /// ⚠️ PERFORMANCE NOTE: This overload boxes components and uses runtime 
    /// type lookup. Use for debug tools, editor, or low-volume cases only.
    /// For hot paths, use CreateEntity() + Add&lt;T&gt;() calls instead.
    /// </summary>
    Entity CreateEntity(params object[] components);
    
    /// <summary>Destroy an entity</summary>
    void DestroyEntity(Entity entity);
    
    /// <summary>Add component to entity</summary>
    void Add<T>(Entity entity, T component) where T : struct;
    
    /// <summary>Add tag component</summary>
    void Add<T>(Entity entity) where T : struct;
    
    /// <summary>Remove component from entity</summary>
    void Remove<T>(Entity entity) where T : struct;
    
    /// <summary>Set/replace component value</summary>
    void Set<T>(Entity entity, T component) where T : struct;
    
    /// <summary>Number of pending commands</summary>
    int CommandCount { get; }
    
    /// <summary>Execute all buffered commands</summary>
    void Playback(IWorld world);
    
    /// <summary>Clear all pending commands without executing</summary>
    void Clear();
}
```

### Command Buffer Usage

```csharp
[EcsSystem]
public partial class SpawnSystem
{
    [Inject] protected IWorld World { get; set; }
    [Inject] protected ICommandBuffer Commands { get; set; }
    
    [Query(All = new[] { typeof(Spawner), typeof(Position) })]
    public void Process(Entity entity, ref Spawner spawner, in Position pos)
    {
        spawner.Timer -= DeltaTime;
        
        if (spawner.Timer <= 0)
        {
            // Deferred: safe during iteration
            var spawned = Commands.CreateEntity();
            Commands.Add(spawned, new Position { X = pos.X, Y = pos.Y, Z = pos.Z });
            Commands.Add(spawned, new Velocity { X = 0, Y = 1, Z = 0 });
            Commands.Add(spawned, new Health { Current = 50, Max = 50 });
            
            spawner.Timer = spawner.Interval;
        }
    }
}
```

#### Arch Deferred Structural Changes & World Adapters

On Arch, structural changes can be applied either **immediately** via `IWorld` (e.g., `ArchWorld`) or **deferred** via `ICommandBuffer` and a generated world adapter:

- **Immediate (ArchWorld):**
  - Systems that inject `IWorld` and mark queries with `StructuralChanges(Mode = Immediate, ...)` call directly into `ArchWorld`.
  - `ArchWorld` wraps an `Arch.Core.World` and maintains a shared registry mapping `UnifyECS.Entity` → `Arch.Core.Entity` per inner world instance.
  - This is suitable for single-threaded, non-parallel execution and matches Arch's native immediate structural semantics.

- **Deferred (DefaultCommandBuffer + WorldAdapter):**
  - Systems that declare `StructuralChanges(Mode = Deferred, ...)` and inject `ICommandBuffer` buffer structural commands during query execution.
  - The Arch backend generates a nested `WorldAdapter : IWorld` per such system. At the end of `Execute(World world, float deltaTime)` the generated code:
    - Instantiates `WorldAdapter` over the same `Arch.Core.World`.
    - Calls `Commands.Playback(adapter)` followed by `Commands.Clear()`.
  - The adapter cooperates with `ArchWorld` by calling `ArchWorld.RegisterEntity(world, entity, archEntity)` whenever it creates a new entity, ensuring the shared registry stays consistent.
  - For `CreateEntity(params object[] components)` the adapter creates an empty Arch entity and then calls `Add<T>` for each struct component using a small reflective helper. Non-struct components are rejected.

- **Recommended pattern (Arch):**
  - For performance-sensitive code, prefer:

    ```csharp
    var e = Commands.CreateEntity();
    Commands.Add(e, new Position { X = pos.X, Y = pos.Y });
    Commands.Add(e, new Velocity { X = 1f, Y = 0f });
    ```

    instead of the boxed `CreateEntity(params object[] components)` overload.
  - The `params` overload remains available for debug/editor/low-volume scenarios, consistent with the performance note in the `ICommandBuffer` interface.

- **Analyzer alignment:**
  - The UnifyECS analyzer enforces that, in deferred structural queries, structural writes are performed via the injected `ICommandBuffer` and not mixed with `IWorld` structural calls. This matches the Arch backend's expectation that deferred structural changes flow through the generated `WorldAdapter` playback path.

---

## Structural Change Attributes

### [StructuralChanges] Attribute

```csharp
/// <summary>
/// Indicates that a query method performs structural changes.
/// Generator uses this to select appropriate execution strategy.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StructuralChangesAttribute : Attribute
{
    /// <summary>
    /// When to apply structural changes
    /// </summary>
    public StructuralChangeMode Mode { get; set; } = StructuralChangeMode.Deferred;
    
    /// <summary>
    /// Types of changes this method performs
    /// </summary>
    public StructuralChangeType[] Changes { get; set; } = Array.Empty<StructuralChangeType>();
}

public enum StructuralChangeMode
{
    /// <summary>
    /// Changes are buffered and applied after query completes.
    /// Required for DOTS parallel jobs.
    /// </summary>
    Deferred,
    
    /// <summary>
    /// Changes are applied immediately.
    /// NOT compatible with DOTS parallel execution.
    /// May invalidate iterators on some backends.
    /// </summary>
    Immediate,
    
    /// <summary>
    /// Let generator choose based on backend capabilities.
    /// </summary>
    Auto
}
```

### Usage Examples

```csharp
[EcsSystem]
public partial class CombatSystem
{
    [Inject] protected IWorld World { get; set; }
    [Inject] protected ICommandBuffer Commands { get; set; }
    
    // ═══════════════════════════════════════════════════════════════
    // Deferred: Safe for parallel execution (DOTS-compatible)
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Health) })]
    [StructuralChanges(Mode = StructuralChangeMode.Deferred,
                       Changes = new[] { StructuralChangeType.AddComponent })]
    public void CheckDeath(Entity entity, ref Health health)
    {
        if (health.Current <= 0)
        {
            Commands.Add<Dead>(entity);  // Deferred
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // Immediate: NOT parallel-safe, generates warning for DOTS
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Despawn) })]
    [StructuralChanges(Mode = StructuralChangeMode.Immediate,
                       Changes = new[] { StructuralChangeType.DestroyEntity })]
    public void ProcessDespawn(Entity entity)
    {
        World.DestroyEntity(entity);  // Immediate - may invalidate iterator
    }
    
    // ═══════════════════════════════════════════════════════════════
    // Auto: Generator decides based on backend
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Spawner) })]
    [StructuralChanges(Mode = StructuralChangeMode.Auto,
                       Changes = new[] { StructuralChangeType.CreateEntity })]
    public void ProcessSpawner(ref Spawner spawner)
    {
        // Generator injects appropriate buffer/world access
    }
}
```

---

## Backend-Specific Generation

### Arch Backend (Immediate Safe)

Arch supports immediate structural changes but may reallocate archetypes:

```csharp
// User code
[Query(All = new[] { typeof(Health) })]
[StructuralChanges(Mode = StructuralChangeMode.Immediate)]
public void CheckDeath(Entity entity, ref Health health)
{
    if (health.Current <= 0)
        World.Add<Dead>(entity);
}

// Generated: Arch (deferred for safety)
partial class DeathSystem
{
    private List<Entity> _toAddDead = new();
    
    public void Execute(World world, float deltaTime)
    {
        _toAddDead.Clear();
        
        // Phase 1: Collect changes
        world.Query(in _query, (Entity entity, ref Health health) =>
        {
            if (health.Current <= 0)
                _toAddDead.Add(entity);
        });
        
        // Phase 2: Apply changes
        foreach (var entity in _toAddDead)
        {
            world.Add<Dead>(entity);
        }
    }
}
```

### Entitas Backend (Group-Safe)

Entitas can invalidate group iterators during structural changes:

```csharp
// Generated: Entitas
partial class DeathSystem : IExecuteSystem
{
    private IGroup<GameEntity> _group;
    private List<GameEntity> _buffer = new();
    private List<GameEntity> _toAddDead = new();
    
    public void Execute()
    {
        _toAddDead.Clear();
        _group.GetEntities(_buffer);  // Snapshot before changes
        
        foreach (var entity in _buffer)
        {
            var health = entity.health;
            if (health.Current <= 0 && !entity.isDead)
                _toAddDead.Add(entity);
        }
        
        // Apply after iteration
        foreach (var entity in _toAddDead)
        {
            entity.isDead = true;
        }
    }
}
```

### DOTS Backend (ECB Required)

DOTS requires EntityCommandBuffer for parallel execution:

```csharp
// User code
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, BurstCompile = true, Parallel = true)]
public partial class DeathSystem
{
    [Query(All = new[] { typeof(Health) }, None = new[] { typeof(Dead) })]
    [StructuralChanges(Mode = StructuralChangeMode.Deferred)]
    public void CheckDeath(Entity entity, ref Health health)
    {
        if (health.Current <= 0)
            Commands.Add<Dead>(entity);
    }
}

// Generated: DOTS with ECB
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct DeathSystemDots : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        
        new DeathJob
        {
            Ecb = ecb.AsParallelWriter()
        }.ScheduleParallel();
    }
}

[BurstCompile]
partial struct DeathJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;
    
    void Execute(Entity entity, ref Health health, [ChunkIndexInQuery] int sortKey)
    {
        if (health.Current <= 0)
        {
            Ecb.AddComponent<Dead>(sortKey, entity);
        }
    }
}
```

---

## Thread Safety Rules

### DOTS-Specific Rules

| Scenario | Requirement |
|----------|-------------|
| Main thread system | Can use immediate or deferred |
| Parallel job | **MUST** use ECB.ParallelWriter |
| Schedule dependency | ECB playback ordered by system |
| Cross-system changes | Use ECB from appropriate system |

### Generated Warnings

```csharp
// If user writes:
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, Parallel = true)]
public partial class BadSystem
{
    [Inject] protected IWorld World { get; set; }
    
    [Query(All = new[] { typeof(Health) })]
    [StructuralChanges(Mode = StructuralChangeMode.Immediate)]  // ❌
    public void Process(Entity e, ref Health h)
    {
        World.Add<Dead>(e);  // Immediate in parallel!
    }
}

// Generator emits:
#error UECS012: System 'BadSystem' uses Immediate structural changes with 
       Parallel=true on DOTS backend. This is unsafe. Use Deferred mode or 
       disable parallel execution.
```

### Safe Patterns

```csharp
// ✅ Pattern 1: Deferred mode (always safe)
[StructuralChanges(Mode = StructuralChangeMode.Deferred)]

// ✅ Pattern 2: Disable parallelism for structural systems
[EcsOptimize(EcsBackend.Dots, Parallel = false)]

// ✅ Pattern 3: Split into read and write phases
[EcsSystem(Phase = SystemPhase.Update)]      // Read phase
[EcsSystem(Phase = SystemPhase.LateUpdate)]  // Write phase

// ✅ Pattern 4: Use cleanup system
[EcsSystem(Phase = SystemPhase.Cleanup)]
[StructuralChanges(Mode = StructuralChangeMode.Immediate)]  // Safe in cleanup
```

---

## Command Buffer Injection

### Automatic ECB Selection (DOTS)

```csharp
/// <summary>
/// Specifies which ECB system to use for this system's commands.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandBufferSystemAttribute : Attribute
{
    /// <summary>
    /// ECB system type (DOTS-specific)
    /// </summary>
    public Type SystemType { get; }
    
    public CommandBufferSystemAttribute(Type systemType)
    {
        SystemType = systemType;
    }
}

// Usage
[EcsSystem]
[CommandBufferSystem(typeof(EndSimulationEntityCommandBufferSystem))]
public partial class MySystem
{
    // Commands injected from EndSimulationECBS
}
```

### Default ECB Systems

| Phase | Default ECB System |
|-------|-------------------|
| `EarlyUpdate` | `BeginInitializationEntityCommandBufferSystem` |
| `Update` | `EndSimulationEntityCommandBufferSystem` |
| `LateUpdate` | `EndSimulationEntityCommandBufferSystem` |
| `Cleanup` | Immediate (no ECB) |

---

## Mutation Rules

### Component Mutation

| Operation | Thread-Safe? | Notes |
|-----------|--------------|-------|
| `ref T` (read-write) | ✅ Within entity | Same entity only |
| `in T` (read-only) | ✅ | Safe for parallel |
| Write to other entity | ❌ | Use ECB or single-thread |

### Cross-Entity Access

```csharp
// ❌ UNSAFE: Accessing other entities in parallel
[Query(All = new[] { typeof(Target) })]
public void BadProcess(Entity entity, in Target target)
{
    ref var targetPos = ref World.Get<Position>(target.Entity);  // ❌ Race condition
    targetPos.X += 1;
}

// ✅ SAFE: Deferred modification
[Query(All = new[] { typeof(Target) })]
public void GoodProcess(Entity entity, in Target target)
{
    Commands.Set(target.Entity, new Position { X = 10 });  // ✅ Deferred
}

// ✅ SAFE: Random access lookup (read-only)
[Query(All = new[] { typeof(Target) })]
public void ReadOnlyLookup(Entity entity, in Target target)
{
    if (World.TryGet<Position>(target.Entity, out var pos))  // ✅ Read-only
    {
        // Use pos.X, pos.Y, pos.Z (don't modify)
    }
}
```

### Random Access Pattern

For systems that need to look up arbitrary entities:

```csharp
/// <summary>
/// Marks a system as requiring random entity access.
/// Disables parallel execution on DOTS.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresRandomAccessAttribute : Attribute { }

[EcsSystem]
[RequiresRandomAccess]  // Forces main-thread execution on DOTS
public partial class RelationshipSystem
{
    [Query(All = new[] { typeof(Parent) })]
    public void Process(Entity child, in Parent parent)
    {
        // Safe: system runs on main thread
        ref var parentPos = ref World.Get<Position>(parent.Entity);
        // ...
    }
}
```

---

## Structural Change Playback Order

### Within a Frame

```
┌─────────────────────────────────────────────────────────────────┐
│                        Frame N                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. BeginFrameECB.Playback()                                   │
│                                                                 │
│  2. EarlyUpdate Systems Execute                                 │
│     └─→ EarlyUpdateECB commands accumulated                    │
│                                                                 │
│  3. EarlyUpdateECB.Playback()                                  │
│                                                                 │
│  4. Update Systems Execute                                      │
│     └─→ UpdateECB commands accumulated                         │
│                                                                 │
│  5. UpdateECB.Playback()                                       │
│                                                                 │
│  6. LateUpdate Systems Execute                                  │
│     └─→ LateUpdateECB commands accumulated                     │
│                                                                 │
│  7. LateUpdateECB.Playback()                                   │
│                                                                 │
│  8. Cleanup Systems Execute (immediate changes OK)             │
│                                                                 │
│  9. EndFrameECB.Playback()                                     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Diagnostics

### Compile-Time Warnings

| Code | Message |
|------|---------|
| `UECS012` | Immediate structural changes in parallel system |
| `UECS013` | Missing `[StructuralChanges]` attribute |
| `UECS014` | Cross-entity write without deferred mode |
| `UECS015` | ECB usage without injection |

### Runtime Validation (Debug Mode)

```csharp
#if DEBUG
// Generated validation in debug builds
public void Execute(World world, float deltaTime)
{
    _structuralChangeDetector.BeginIteration();
    
    world.Query(in _query, (Entity e, ref Health h) =>
    {
        Process(e, ref h);
        
        // Detect if user made immediate changes during iteration
        _structuralChangeDetector.ValidateNoChanges(
            "DeathSystem.Process", 
            "Use Commands buffer for deferred changes");
    });
    
    _structuralChangeDetector.EndIteration();
}
#endif
```

---

## Open Questions

1. Should we support transactional command buffers (rollback on error)?
2. How to handle structural changes in reactive callbacks?
3. Should cross-entity reads require explicit attribute?

## References

- RFC-0010: DOTS Backend Constraints
- RFC-0011: Unified API Surface
- [Unity ECB Documentation](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html)
