# RFC-0010: DOTS Backend Constraints & Limitations

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0005, RFC-0006
- **Addresses**: Review Issue #2

## Summary

Define the specific constraints, limitations, and architectural requirements for the Unity DOTS (Entities 1.0+) backend, which has the most restrictive requirements among supported backends.

## Motivation

Unity DOTS is fundamentally different from other ECS frameworks:

1. **Unmanaged-first architecture**: Most APIs expect unmanaged types
2. **Burst compilation**: High-performance path requires specific patterns
3. **Jobs system**: Parallel execution with strict thread safety
4. **World management**: Complex world initialization and system groups
5. **Memory layout**: Chunk-based storage with specific alignment requirements

These constraints make DOTS the "constraint leader" - if something works on DOTS, it likely works everywhere.

---

## DOTS Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Unity Player Loop                        │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Default World Bootstrap                      │
│     (DefaultWorldInitialization.Initialize)                     │
└─────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ Initialization  │ │   Simulation    │ │  Presentation   │
│  SystemGroup    │ │  SystemGroup    │ │  SystemGroup    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
              │               │               │
              ▼               ▼               ▼
         [Systems]       [Systems]       [Systems]
              │               │               │
              └───────────────┼───────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Entity Manager                              │
│                   (Chunk Storage)                               │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Constraints

### Unmanaged Components Only

DOTS requires components to implement `IComponentData` and be **unmanaged** (no reference types):

```csharp
// ✅ VALID - unmanaged struct
[EcsComponent]
public struct Position : IComponentData
{
    public float X, Y, Z;
}

// ❌ INVALID - contains reference type
[EcsComponent]
public struct Named : IComponentData
{
    public string Name;  // ❌ Reference type not allowed
}

// ❌ INVALID - class-based component
[EcsComponent]
public class PlayerData : IComponentData  // ❌ Must be struct
{
    public int Score;
}
```

### Generated Component Validation

```csharp
// Generated: Position.Dots.g.cs
// Validation that component meets DOTS requirements

#if UNITY_DOTS
// Add IComponentData marker
public partial struct Position : IComponentData { }

// Compile-time validation
static class PositionDotsValidation
{
    // This will fail to compile if Position contains managed types
    static void ValidateUnmanaged()
    {
        _ = new Unity.Collections.NativeArray<Position>(1, Unity.Collections.Allocator.Temp);
    }
}
#endif
```

### Managed Components (Limited Support)

DOTS supports managed components but with severe restrictions:

```csharp
/// <summary>
/// Marks a component as requiring managed storage in DOTS.
/// These components cannot be used in Burst-compiled code or jobs.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ManagedComponentAttribute : Attribute { }

// Usage
[EcsComponent]
[ManagedComponent]  // Explicit opt-in
public class ManagedHealthBar : IComponentData
{
    public UnityEngine.UI.Image HealthBarImage;
}

// Generated: ManagedHealthBar.Dots.g.cs
#if UNITY_DOTS
// Managed components use class, not struct
public partial class ManagedHealthBar : IComponentData { }

// Warning in generated code
#warning "ManagedHealthBar is a managed component - cannot use in Burst/Jobs"
#endif
```

---

## System Constraints

### ISystem vs SystemBase

DOTS has two system types with different capabilities:

| Feature | ISystem (struct) | SystemBase (class) |
|---------|------------------|-------------------|
| Burst compatible | ✅ Yes | ❌ No |
| Can store managed state | ❌ No | ✅ Yes |
| Parallel job scheduling | ✅ Yes | ✅ Yes |
| Reactive emulation | ❌ No | ⚠️ Limited |

### System Generation Strategy

```csharp
// User code
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, BurstCompile = true)]
public partial class MovementSystem
{
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void Process(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
    }
}
```

**Decision Tree for DOTS System Generation:**

```
Is BurstCompile = true?
├── Yes: Can all queries be Burst-compiled?
│   ├── Yes: Generate ISystem + IJobEntity
│   └── No: Generate SystemBase with partial Burst
└── No: Generate SystemBase (managed)

Does system use reactive features ([OnChanged], etc.)?
├── Yes: Is emulation acceptable?
│   ├── Yes: Generate SystemBase with ICleanupComponent tracking
│   └── No: Error - Reactive not natively supported in DOTS
└── No: Use standard generation
```

### Burst-Compatible ISystem

```csharp
// Generated: MovementSystem.Dots.g.cs
#if UNITY_DOTS
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MovementSystemDots : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Position>();
        state.RequireForUpdate<Velocity>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // Option 1: IJobEntity (recommended for parallelism)
        new MovementJob { DeltaTime = deltaTime }.ScheduleParallel();
        
        // Option 2: SystemAPI.Query (simpler, main-thread)
        // foreach (var (pos, vel) in SystemAPI.Query<RefRW<Position>, RefRO<Velocity>>())
        // {
        //     pos.ValueRW.X += vel.ValueRO.X * deltaTime;
        // }
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
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
#endif
```

### Non-Burst SystemBase (Managed State)

When systems require managed state (e.g., for reactive emulation):

```csharp
// Generated: HealthReactiveSystem.Dots.g.cs
#if UNITY_DOTS
using Unity.Entities;
using Unity.Collections;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class HealthReactiveSystemDots : SystemBase
{
    // ⚠️ WARNING: This system uses managed emulation for reactive features
    // Performance will be lower than native DOTS patterns
    
    private EntityQuery _healthQuery;
    
    // Managed state for change tracking (NOT Burst-compatible)
    private NativeHashMap<Entity, Health> _previousHealth;
    
    protected override void OnCreate()
    {
        _healthQuery = GetEntityQuery(ComponentType.ReadOnly<Health>());
        _previousHealth = new NativeHashMap<Entity, Health>(1024, Allocator.Persistent);
    }
    
    protected override void OnDestroy()
    {
        _previousHealth.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // Change detection (main thread only - not parallelizable)
        var entities = _healthQuery.ToEntityArray(Allocator.Temp);
        var healths = _healthQuery.ToComponentDataArray<Health>(Allocator.Temp);
        
        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            var health = healths[i];
            
            if (_previousHealth.TryGetValue(entity, out var prev))
            {
                if (!health.Equals(prev))
                {
                    // Invoke user callback
                    OnHealthChangedCallback(entity, ref health);
                }
            }
            
            _previousHealth[entity] = health;
        }
        
        entities.Dispose();
        healths.Dispose();
    }
    
    private void OnHealthChangedCallback(Entity entity, ref Health health)
    {
        // User logic from OnHealthChanged
        if (health.Current <= 0)
        {
            EntityManager.AddComponent<Dead>(entity);
        }
    }
}
#endif
```

---

## Injection Constraints

### What CAN'T Be Injected in ISystem

```csharp
// ❌ INVALID: ISystem is unmanaged struct - cannot have injected managed properties
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, BurstCompile = true)]
public partial class InvalidSystem
{
    [Inject] protected IWorld World { get; set; }  // ❌ Managed reference
    [InjectSystem] protected OtherSystem Other { get; set; }  // ❌ Managed reference
}
```

### Alternative: SystemAPI

DOTS systems use `SystemAPI` instead of injected dependencies:

```csharp
// Generated for DOTS
[BurstCompile]
public partial struct MovementSystemDots : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Use SystemAPI instead of injected DeltaTime
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // Use SystemAPI.GetSingleton instead of injected data
        var config = SystemAPI.GetSingleton<GameConfig>();
        
        // Query via SystemAPI
        foreach (var (pos, vel) in SystemAPI.Query<RefRW<Position>, RefRO<Velocity>>())
        {
            // ...
        }
    }
}
```

### Wrapper Pattern for Compatibility

To maintain UnifyECS injection semantics:

```csharp
// User code (portable)
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

// Generated: MovementSystem.Dots.g.cs
// Create a SystemBase wrapper that injects into the portable class
#if UNITY_DOTS
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class MovementSystemDotsWrapper : SystemBase
{
    private MovementSystem _inner = new MovementSystem();
    
    protected override void OnUpdate()
    {
        // Inject compatible values
        _inner.DeltaTime = SystemAPI.Time.DeltaTime;
        
        // Execute queries
        Entities
            .ForEach((ref Position pos, in Velocity vel) =>
            {
                _inner.Process(ref pos, in vel);
            })
            .Schedule();
    }
}
#endif
```

---

## World Initialization

### Standard DOTS Bootstrap

```csharp
// For standard Unity projects using DefaultWorldInitialization
public static class UnifyEcsDotsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        // Get or create the default world
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            world = DefaultWorldInitialization.Initialize("UnifyECS World");
        }
        
        // Register UnifyECS systems
        RegisterUnifySystems(world);
    }
    
    private static void RegisterUnifySystems(World world)
    {
        // Systems are registered via [WorldSystemFilter] attributes
        // or manually added to system groups
        
        var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        
        // Add generated systems
        simGroup.AddSystemToUpdateList(world.CreateSystem<MovementSystemDots>());
        simGroup.AddSystemToUpdateList(world.CreateSystem<PhysicsSystemDots>());
        // ... etc
        
        simGroup.SortSystems();
    }
}
```

### Custom World (Non-Default)

```csharp
// For custom world scenarios (e.g., server simulation)
public sealed class DotsWorldFactory : IWorldFactory
{
    public EcsBackend Backend => EcsBackend.Dots;
    
    public IWorld Create(WorldConfig config)
    {
        // Create a custom DOTS world (not the default)
        var flags = config.DebugMode 
            ? WorldFlags.Editor 
            : WorldFlags.Game;
        
        var dotsWorld = new World(config.Name ?? "UnifyECS", flags);
        
        // Manually create system groups (not using DefaultWorldInitialization)
        CreateSystemGroups(dotsWorld);
        
        // Register UnifyECS systems
        RegisterSystems(dotsWorld);
        
        return new DotsWorldAdapter(dotsWorld);
    }
    
    private void CreateSystemGroups(World world)
    {
        // Create standard groups
        var init = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
        var sim = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        var pres = world.GetOrCreateSystemManaged<PresentationSystemGroup>();
        
        // Wire up to player loop (if needed)
        // For headless/server, manually call Update()
    }
}
```

---

## Feature Support Matrix (DOTS)

| Feature | Support Level | Notes |
|---------|--------------|-------|
| Basic Entity/Component | ✅ Native | Full support |
| Basic Queries | ✅ Native | Via EntityQuery or SystemAPI.Query |
| AdvancedFiltering | ✅ Native | WithAll/WithAny/WithNone |
| TagComponents | ✅ Native | IComponentData + IEnableableComponent |
| Jobs | ✅ Native | IJobEntity, IJobChunk |
| BurstCompile | ✅ Native | [BurstCompile] attribute |
| SystemGroups | ✅ Native | UpdateInGroup, UpdateBefore/After |
| Relationships | ✅ Native | IBufferElementData, LinkedEntityGroup |
| SharedComponents | ✅ Native | ISharedComponentData |
| ChunkIteration | ✅ Native | IJobChunk, ArchetypeChunk |
| **Events** | 🔶 Emulated | Polling-based, not reactive |
| **Reactive** | 🔶 Emulated | ICleanupComponent + tracking systems |
| **DeltaTime Injection** | 🔶 Adapted | Via SystemAPI.Time |
| **System Injection** | ❌ Limited | Only in SystemBase, not ISystem |

---

## Reactive Emulation Strategy

### ICleanupComponent Pattern

DOTS provides `ICleanupComponentData` for tracking entity lifecycle:

```csharp
// Track when Health is added/removed
public struct HealthCleanup : ICleanupComponentData
{
    public Health LastValue;
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(HealthReactiveSystemDots))]
public partial struct HealthTrackingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Add cleanup component when Health is added
        foreach (var (health, entity) in 
            SystemAPI.Query<RefRO<Health>>()
                .WithNone<HealthCleanup>()
                .WithEntityAccess())
        {
            state.EntityManager.AddComponentData(entity, new HealthCleanup 
            { 
                LastValue = health.ValueRO 
            });
            
            // Fire OnAdded callback
            OnHealthAdded(entity, health.ValueRO);
        }
        
        // Detect removed Health (cleanup component exists but Health doesn't)
        foreach (var (cleanup, entity) in 
            SystemAPI.Query<RefRO<HealthCleanup>>()
                .WithNone<Health>()
                .WithEntityAccess())
        {
            // Fire OnRemoved callback
            OnHealthRemoved(entity, cleanup.ValueRO.LastValue);
            
            // Remove cleanup component
            state.EntityManager.RemoveComponent<HealthCleanup>(entity);
        }
    }
}
```

### Performance Warning

```csharp
// Generated code includes performance warnings
#if UNITY_DOTS
// ⚠️ PERFORMANCE WARNING
// 
// This system emulates reactive behavior (OnChanged) which is not native to DOTS.
// Emulation requires:
// - Additional ICleanupComponentData storage
// - Per-entity comparison each frame
// - Main-thread execution (not Burst-compatible)
// 
// For performance-critical code, consider:
// - Using native DOTS patterns (enableable components, version numbers)
// - Restructuring logic to avoid reactive patterns
// - Using [EcsOptimize(EcsBackend.Dots)] with custom implementation
//
// Estimated overhead: O(n) where n = entities with Health component
#warning "HealthReactiveSystem uses emulated reactive features - see generated code for details"
#endif
```

---

## What Cannot Be Supported on DOTS

| Feature | Reason | Workaround |
|---------|--------|------------|
| Class-based components (normal) | DOTS requires unmanaged IComponentData | Use structs or [ManagedComponent] |
| String fields in components | Managed type | Use FixedString or BlobAsset |
| Arbitrary object references | Managed type | Use Entity references or BlobAsset |
| Synchronous reactive callbacks | Jobs are async | Use cleanup components |
| Dynamic component types | Type safety | Pre-register all types |
| Hot-reload of systems | Burst compilation | Disable Burst in editor |

---

## Best Practices for DOTS Backend

### DO

```csharp
// ✅ Use unmanaged types
[EcsComponent]
public struct Position { public float3 Value; }

// ✅ Use FixedString for text
[EcsComponent]
public struct Named { public FixedString64Bytes Name; }

// ✅ Use Entity for references
[EcsComponent]
public struct Target { public Entity Value; }

// ✅ Enable Burst for performance-critical systems
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, BurstCompile = true, Parallel = true)]
public partial class MovementSystem { ... }

// ✅ Batch structural changes
[EcsSystem]
public partial class SpawnSystem
{
    [Query]
    public void Process(Entity e, in SpawnRequest request, ref EntityCommandBuffer ecb)
    {
        ecb.CreateEntity();  // Batched, not immediate
    }
}
```

### DON'T

```csharp
// ❌ Don't use managed types in components
[EcsComponent]
public struct Bad { public string Name; }

// ❌ Don't expect injection in ISystem
[EcsOptimize(EcsBackend.Dots, BurstCompile = true)]
public partial class BadSystem
{
    [Inject] IWorld World { get; set; }  // Won't work
}

// ❌ Don't rely on reactive for critical gameplay
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]  // Will be slow on DOTS
public partial class CriticalSystem { ... }

// ❌ Don't make structural changes in Burst jobs
[BurstCompile]
void Execute(ref Position pos, EntityCommandBuffer.ParallelWriter ecb)
{
    // ECB.ParallelWriter required for parallel jobs
}
```

---

## Open Questions

1. Should we support DOTS Netcode integration?
2. How to handle BlobAsset components in UnifyECS?
3. Should we generate separate assemblies for Burst-compiled code?
4. How to support DOTS subscenes and entity serialization?

## References

- [Unity Entities 1.0 Documentation](https://docs.unity3d.com/Packages/com.unity.entities@1.0)
- [Burst Compiler Documentation](https://docs.unity3d.com/Packages/com.unity.burst@1.8)
- RFC-0001: Core Architecture
- RFC-0005: Backend Adapters
- RFC-0006: Missing Feature Policies
