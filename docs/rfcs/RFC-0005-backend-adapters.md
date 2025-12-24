# RFC-0005: Backend Adapters

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0004

## Summary

Define the backend adapter architecture for each supported ECS framework, including component registration, query translation, and system execution patterns.

> 📌 **Design Note**: Generated system implementations are free to bypass `IWorld` and use backend-native APIs internally for performance, as long as the externally visible semantics defined in RFC-0011 are preserved. Users write portable code against the unified API; the generator emits optimized backend-specific code.

## Supported Backends

### Phase 1 (Initial)
- **Arch ECS** - Primary development target
- **Entitas** - Popular alternative with reactive systems

### Phase 2
- **Unity DOTS** - Enterprise Unity development
- **DefaultEcs** - Lightweight alternative
- **Friflo** - High-performance option

### Phase 3
- **LeoECS** - Russian community favorite
- **Svelto.ECS** - Unique hybrid approach
- Custom backends via plugin API

## Backend Interface

```csharp
/// <summary>
/// Interface that all backend emitters must implement
/// </summary>
public interface IBackendEmitter
{
    /// <summary>Backend identifier</summary>
    EcsBackend Backend { get; }
    
    /// <summary>Native features supported</summary>
    EcsFeature NativeFeatures { get; }
    
    /// <summary>Features that can be emulated</summary>
    EcsFeature EmulatableFeatures { get; }
    
    /// <summary>Generate component wrapper/registration code</summary>
    string EmitComponent(ComponentModel component);
    
    /// <summary>Generate system implementation</summary>
    string EmitSystem(SystemModel system, FeatureSupportLevel level);
    
    /// <summary>Generate world extension methods</summary>
    string EmitWorldExtensions(IEnumerable<ComponentModel> components);
    
    /// <summary>Generate reactive system emulation helpers</summary>
    string? EmitReactiveEmulation(SystemModel system);
    
    /// <summary>Generate bootstrap/registration code</summary>
    string EmitBootstrap(IEnumerable<SystemModel> systems);
}
```

---

## Arch ECS Backend

### Overview

Arch is an archetype-based ECS with excellent cache performance and a clean API.

**Native Features:**
- `Basic` - Entity/Component/Query/System
- `AdvancedFiltering` - Any/None queries
- `TagComponents` - Zero-size components
- `Events` - Component events via subscribers
- `SystemGroups` - System organization

**Emulatable Features:**
- `Reactive` - Via change tracking helpers
- `Relationships` - Via component references

### Component Generation

```csharp
// Input
[EcsComponent]
public struct Position { public float X, Y, Z; }

// Generated: Nothing needed! Arch uses structs directly.
// But we generate helper extensions:

// Position.Arch.g.cs
public static class PositionArchExtensions
{
    public static ref Position GetPosition(this World world, Entity entity)
        => ref world.Get<Position>(entity);
    
    public static bool HasPosition(this World world, Entity entity)
        => world.Has<Position>(entity);
    
    public static void AddPosition(this World world, Entity entity, Position component)
        => world.Add(entity, component);
    
    public static void RemovePosition(this World world, Entity entity)
        => world.Remove<Position>(entity);
}
```

### System Generation

```csharp
// Input
[EcsSystem]
public partial class MovementSystem
{
    [Inject] protected float DeltaTime { get; set; }
    
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void Process(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
    }
}

// Generated: MovementSystem.Arch.g.cs
partial class MovementSystem : IArchSystem
{
    private static readonly QueryDescription _query_Process = new QueryDescription()
        .WithAll<Position, Velocity>();
    
    public void Execute(World world, float deltaTime)
    {
        this.DeltaTime = deltaTime;
        
        world.Query(in _query_Process, (ref Position pos, in Velocity vel) =>
        {
            Process(ref pos, in vel);
        });
    }
}
```

### Arch World Adapter & Entity Registry

For systems that perform **deferred** structural changes (see RFC-0012), the Arch backend generates a small `WorldAdapter` helper that implements the unified `IWorld` API on top of an `Arch.Core.World`:

- **ArchWorld** implements `IWorld` and wraps an `Arch.Core.World` instance.
  - It maintains a **shared registry** mapping `UnifyECS.Entity` → `Arch.Core.Entity` per `Arch.Core.World`.
  - All entities created via `ArchWorld` register this mapping.
- For systems with `[StructuralChanges(Mode = Deferred, ...)]` and an injected `ICommandBuffer`, the Arch emitter generates:
  - A nested `WorldAdapter : IWorld` type inside the Arch system.
  - `Execute(World world, float deltaTime)` that:
    - Runs the query over `world`.
    - If the injected command buffer has pending commands, constructs a `WorldAdapter` over the same `Arch.Core.World` and calls `Commands.Playback(adapter)`.
- The generated `WorldAdapter`:
  - Creates new Arch entities via `World.Create(...)`.
  - Creates matching `UnifyECS.Entity` handles and registers them **both** in a local `_entityMap` and via `ArchWorld.RegisterEntity(world, entity, archEntity)` so that `ArchWorld` and the adapter see a consistent view of entity IDs.
  - Implements `Add<T>`, `Remove<T>`, `Get<T>`, etc. by converting `UnifyECS.Entity` to `Arch.Core.Entity` using its local map.
- For `ICommandBuffer.CreateEntity(params object[] components)` the generated adapter:
  - Creates an empty Arch entity, then uses a reflective `AddBoxed<T>` helper to call `Add<T>` for each **struct** component in the array.
  - Rejects non-value-type components with a clear exception.

**Usage guidance (Arch):**

- Prefer the strongly-typed pattern

  ```csharp
  var e = Commands.CreateEntity();
  Commands.Add(e, new Position { X = pos.X, Y = pos.Y });
  Commands.Add(e, new Velocity { X = 1f, Y = 0f });
  ```

  for hot paths.
- Reserve `CreateEntity(params object[] components)` for **debug tools, editor utilities, or low-volume cases**, as it boxes components and uses reflection in the Arch backend.
- When using deferred structural changes (`Mode = Deferred`), all structural writes in that query body should go through the injected `ICommandBuffer`; the Arch backend assumes this pattern when generating the `WorldAdapter` playback code.

### Reactive Emulation

```csharp
// Input
[EcsSystem]
[EcsRequires(EcsFeature.Reactive, IfMissing = MissingFeatureBehavior.Emulate)]
public partial class HealthReactiveSystem
{
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity e, ref Health health)
    {
        if (health.Current <= 0) { /* ... */ }
    }
}

// Generated: HealthReactiveSystem.Arch.g.cs
partial class HealthReactiveSystem : IArchSystem
{
    // Change tracking state
    private Dictionary<Entity, Health> _previousHealth = new();
    private List<(Entity Entity, Health Current, Health Previous)> _changedBuffer = new();
    
    private static readonly QueryDescription _healthQuery = new QueryDescription()
        .WithAll<Health>();
    
    public void Execute(World world, float deltaTime)
    {
        _changedBuffer.Clear();
        
        // Detect changes
        world.Query(in _healthQuery, (Entity entity, ref Health health) =>
        {
            if (_previousHealth.TryGetValue(entity, out var prev))
            {
                if (!health.Equals(prev))
                {
                    _changedBuffer.Add((entity, health, prev));
                }
            }
            else
            {
                // New entity - treat as added (initial state)
                _changedBuffer.Add((entity, health, default));
            }
            _previousHealth[entity] = health;
        });
        
        // Process changes
        foreach (var (entity, current, _) in _changedBuffer)
        {
            var health = current;
            OnHealthChanged(entity, ref health);
            
            // Write back if modified
            if (!health.Equals(current))
            {
                world.Set(entity, health);
            }
        }
        
        // Cleanup destroyed entities (optional, can be deferred)
    }
}
```

---

## Entitas Backend

> ⚠️ **Compatibility Note**: Entitas has its own code generation tooling. UnifyECS-generated code must integrate with, not replace, Entitas' native generators. Users may need to configure Entitas contexts separately or use a compatibility layer.

### Overview

Entitas is a mature ECS with first-class reactive system support and code generation.

**Native Features:**
- `Basic` - Entity/Component/Query/System
- `AdvancedFiltering` - Matchers with complex logic
- `TagComponents` - Flag components
- `Events` - Entity events
- `Reactive` - First-class OnAdded/OnRemoved/OnChanged
- `SystemGroups` - Feature groups

**Emulatable Features:**
- `WorldEvents` - Via singleton entity

### Component Generation

```csharp
// Input
[EcsComponent]
public struct Position { public float X, Y, Z; }

// Generated: PositionComponent.Entitas.g.cs
// Entitas requires class-based components with specific interface

[Game]  // Context attribute
public sealed class PositionComponent : IComponent
{
    public float X;
    public float Y;
    public float Z;
}

// Generated: GameComponentsLookup.Entitas.g.cs (partial)
public static partial class GameComponentsLookup
{
    public const int Position = /* index */;
    public static readonly string[] componentNames = { ..., "Position", ... };
    public static readonly Type[] componentTypes = { ..., typeof(PositionComponent), ... };
}

// Generated: GameEntity.Entitas.g.cs (partial extensions)
public partial class GameEntity
{
    public PositionComponent position => (PositionComponent)GetComponent(GameComponentsLookup.Position);
    public bool hasPosition => HasComponent(GameComponentsLookup.Position);
    
    public void AddPosition(float x, float y, float z)
    {
        var component = CreateComponent<PositionComponent>(GameComponentsLookup.Position);
        component.X = x;
        component.Y = y;
        component.Z = z;
        AddComponent(GameComponentsLookup.Position, component);
    }
    
    public void ReplacePosition(float x, float y, float z)
    {
        var component = CreateComponent<PositionComponent>(GameComponentsLookup.Position);
        component.X = x;
        component.Y = y;
        component.Z = z;
        ReplaceComponent(GameComponentsLookup.Position, component);
    }
    
    public void RemovePosition()
    {
        RemoveComponent(GameComponentsLookup.Position);
    }
}
```

### System Generation

```csharp
// Input
[EcsSystem]
public partial class MovementSystem
{
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void Process(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
    }
}

// Generated: MovementSystem.Entitas.g.cs
partial class MovementSystem : IExecuteSystem, IInitializeSystem
{
    private GameContext _context;
    private IGroup<GameEntity> _group_Process;
    private List<GameEntity> _buffer = new();
    
    public void Initialize(Contexts contexts)
    {
        _context = contexts.game;
        _group_Process = _context.GetGroup(
            GameMatcher.AllOf(GameMatcher.Position, GameMatcher.Velocity));
    }
    
    public void Execute()
    {
        _group_Process.GetEntities(_buffer);
        
        foreach (var entity in _buffer)
        {
            // Convert to ref struct for method call
            var pos = new Position { X = entity.position.X, Y = entity.position.Y, Z = entity.position.Z };
            var vel = new Velocity { X = entity.velocity.X, Y = entity.velocity.Y, Z = entity.velocity.Z };
            
            Process(ref pos, in vel);
            
            // Write back
            entity.ReplacePosition(pos.X, pos.Y, pos.Z);
        }
    }
}
```

### Native Reactive System

```csharp
// Input
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]
public partial class HealthReactiveSystem
{
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity e, ref Health health)
    {
        if (health.Current <= 0) { /* ... */ }
    }
}

// Generated: HealthReactiveSystem.Entitas.g.cs
partial class HealthReactiveSystem : ReactiveSystem<GameEntity>
{
    public HealthReactiveSystem(Contexts contexts) : base(contexts.game) { }
    
    protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
    {
        return context.CreateCollector(GameMatcher.Health.Added());
    }
    
    protected override bool Filter(GameEntity entity)
    {
        return entity.hasHealth;
    }
    
    protected override void Execute(List<GameEntity> entities)
    {
        foreach (var entity in entities)
        {
            var health = new Health { Current = entity.health.Current, Max = entity.health.Max };
            
            OnHealthChanged(entity.ToUnifyEntity(), ref health);
            
            if (health.Current != entity.health.Current || health.Max != entity.health.Max)
            {
                entity.ReplaceHealth(health.Current, health.Max);
            }
        }
    }
}
```

---

## Unity DOTS Backend

> ⚠️ **Important**: DOTS has significant constraints. See **RFC-0010: DOTS Backend Constraints** for complete details on unmanaged requirements, Burst compatibility, and system generation strategies.

### Overview

Unity DOTS is the most complex backend due to Jobs/Burst requirements.

**Native Features:**
- `Basic`, `AdvancedFiltering`, `TagComponents`
- `Jobs` - Parallel job execution
- `BurstCompile` - LLVM compilation
- `SharedComponents` - Chunk grouping
- `ChunkIteration` - Low-level access
- `Relationships` - Entity references
- `SystemGroups` - Update groups

**Emulatable Features:**
- `Events` - Via polling or ECB
- `Reactive` - Via ICleanupComponentData (see RFC-0010)

### Component Generation

```csharp
// Input
[EcsComponent]
public struct Position { public float X, Y, Z; }

// Generated: Position.Dots.g.cs
// Add IComponentData marker
public partial struct Position : IComponentData { }

// For tag components:
// Input
[EcsComponent(IsTag = true)]
public struct Dead { }

// Generated: Dead.Dots.g.cs
public partial struct Dead : IComponentData, IEnableableComponent { }
```

### System Generation

```csharp
// Input
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, BurstCompile = true, Parallel = true)]
public partial class MovementSystem
{
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void Process(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
    }
}

// Generated: MovementSystem.Dots.g.cs
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
partial struct MovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        new MovementJob
        {
            DeltaTime = deltaTime
        }.ScheduleParallel();
    }
    
    [BurstCompile]
    partial struct MovementJob : IJobEntity
    {
        public float DeltaTime;
        
        void Execute(ref Position pos, in Velocity vel)
        {
            pos.X += vel.X * DeltaTime;
            pos.Y += vel.Y * DeltaTime;
            pos.Z += vel.Z * DeltaTime;
        }
    }
}
```

### Non-Burstable Fallback

```csharp
// When BurstCompile is false or not applicable
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial class MovementSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        this.DeltaTime = deltaTime;
        
        Entities
            .WithAll<Position, Velocity>()
            .ForEach((ref Position pos, in Velocity vel) =>
            {
                Process(ref pos, in vel);
            })
            .Schedule();
    }
}
```

---

## Backend Registration

```csharp
/// <summary>
/// Registers a custom backend emitter
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class RegisterBackendEmitterAttribute : Attribute
{
    public Type EmitterType { get; }
    
    public RegisterBackendEmitterAttribute(Type emitterType)
    {
        EmitterType = emitterType;
    }
}

// Usage in custom backend assembly:
[assembly: RegisterBackendEmitter(typeof(MyCustomBackendEmitter))]

public class MyCustomBackendEmitter : IBackendEmitter
{
    public EcsBackend Backend => EcsBackend.Custom;
    // ... implementation
}
```

## World Abstraction

```csharp
/// <summary>
/// Backend-agnostic world access interface
/// </summary>
public interface IWorldAccess
{
    Entity CreateEntity();
    void DestroyEntity(Entity entity);
    bool Exists(Entity entity);
    
    void Add<T>(Entity entity, T component) where T : struct;
    void Remove<T>(Entity entity) where T : struct;
    ref T Get<T>(Entity entity) where T : struct;
    bool Has<T>(Entity entity) where T : struct;
    
    void Set<T>(Entity entity, T component) where T : struct;
}

// Each backend generates its own implementation:
// ArchWorldAccess, EntitasWorldAccess, DotsWorldAccess, etc.
```

## Open Questions

1. How to handle DOTS managed components (class-based)?
2. Should we generate Entitas contexts or expect existing ones?
3. How to handle backend-specific initialization (Unity's World vs Arch's World)?

## References

- [Arch ECS GitHub](https://github.com/genaray/Arch)
- [Entitas GitHub](https://github.com/sschmid/Entitas)
- [Unity DOTS Documentation](https://docs.unity3d.com/Packages/com.unity.entities@latest)
- RFC-0008: World Lifecycle & System Execution
- RFC-0009: Component Registry & Type Mapping
- RFC-0010: DOTS Backend Constraints (required reading for DOTS)
