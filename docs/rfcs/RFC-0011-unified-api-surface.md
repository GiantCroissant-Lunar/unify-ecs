# RFC-0011: Unified API Surface & Specification

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0003, RFC-0008
- **Addresses**: Review Issues #1 (unified API), #2 (attribute DSL grammar)

## Implementation Status (v1)

- **Implemented in `dotnet/src`**:
  - `Entity` struct exactly as specified here (Id, Generation, `IsValid`, `Null`, equality, `ToString`).
  - Component and system attributes (`EcsComponent`, `EcsSystem`, `Query`, `EcsRequires`, reactive attributes, `StructuralChanges`, `Inject`, `EcsOptimize`) in `UnifyEcs.Attributes`.
  - Shape rules for components and systems enforced by `UnifyEcs.Analyzers` (UECS004–UECS009, UECS011, UECS016–UECS018, UECS101).
  - Query DSL (All/Any/None/Cached) and parameter conventions as consumed by `EcsGenerator` and emitted for the Arch backend.
- **Partially implemented / not yet enforced**:
  - Some higher-level conventions (e.g. full system group semantics, advanced injection patterns like `ISystemContext`) are present in RFCs but not exposed in the current public API.
  - DOTS-specific portability and parallelism rules are only enforced at the analyzer level; there is no DOTS backend yet.
  - Multi-backend orchestration and cross-backend behavioral guarantees described in related RFCs are not implemented.

This document accurately describes the code developers write today for the Arch backend; sections that rely on multi-backend or DOTS behavior should be treated as forward-looking until those backends are implemented.

## Summary

Define the exact user-facing API surface, DSL grammar, and code conventions that developers use when writing UnifyECS game code. This RFC serves as the canonical reference for what developers write and what the source generator accepts.

## Motivation

Previous RFCs describe generation rules and backend adapters but lack a formal specification of:
- What code developers actually write
- The exact grammar for attributes and queries
- Restrictions on components and systems
- The meaning of "Entity" in the unified model

This RFC provides that canonical specification.

## Version Scope

This document defines the **v1 unified API surface** for UnifyECS:

- **In scope**: components, systems, queries, injection, reactive methods, and world usage as written by end users.
- **Out of scope**: backend-specific optimizations, exact structural-change ordering, and multi-backend orchestration semantics (see RFC-0007 and RFC-0012).
- **Compatibility**: future RFCs may extend this surface (for example, richer parallel semantics or async patterns) but MUST NOT break code that conforms to this version.

---

## Core Types

### Entity

```csharp
/// <summary>
/// Opaque handle to an entity. Backend-agnostic.
/// </summary>
/// <remarks>
/// - Entity is a VALUE TYPE (struct) for performance
/// - Entity.Id is the only guaranteed stable identifier
/// - Entity comparison uses Id only
/// - Entity does NOT store component data
/// - Entity is backend-specific internally but unified externally
/// </remarks>
public readonly struct Entity : IEquatable<Entity>
{
    /// <summary>
    /// Unique identifier within a world.
    /// Valid range: 0 to int.MaxValue
    /// Value of -1 indicates Null/Invalid entity.
    /// </summary>
    public int Id { get; }
    
    /// <summary>
    /// Generation/version counter to detect stale references.
    /// </summary>
    public int Generation { get; }
    
    /// <summary>
    /// Returns true if this is a valid entity reference.
    /// </summary>
    public bool IsValid => Id >= 0;
    
    /// <summary>
    /// Null/invalid entity constant.
    /// </summary>
    public static readonly Entity Null = new(-1, 0);
    
    public Entity(int id, int generation = 0)
    {
        Id = id;
        Generation = generation;
    }
    
    public bool Equals(Entity other) => Id == other.Id && Generation == other.Generation;
    public override bool Equals(object? obj) => obj is Entity e && Equals(e);
    public override int GetHashCode() => HashCode.Combine(Id, Generation);
    public static bool operator ==(Entity a, Entity b) => a.Equals(b);
    public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
    
    public override string ToString() => IsValid ? $"Entity({Id}:{Generation})" : "Entity.Null";
}
```

### Component Definition Rules

| Rule | Requirement | Rationale |
|------|-------------|-----------|
| **Type** | Must be `struct` | Performance, DOTS compatibility |
| **Attribute** | Must have `[EcsComponent]` | Discovery by generator |
| **Fields** | Public or internal fields allowed | Serialization, backend mapping |
| **Properties** | Allowed but not recommended | May confuse serialization |
| **Methods** | Allowed (utility methods) | Convenience |
| **Inheritance** | Not allowed (structs) | N/A |
| **Interfaces** | Allowed | Backend markers added by generator |
| **Generics** | **NOT ALLOWED** | Backend compatibility (see RFC-0009) |
| **Nested Types** | **NOT ALLOWED** | Generator complexity |
| **Partial** | Allowed and recommended | Generator extends with backend interfaces |
| **Readonly Fields** | Allowed | Immutable data patterns |
| **Reference Fields** | **NOT ALLOWED** for portable | DOTS unmanaged requirement |

#### Valid Component Examples

```csharp
// ✅ VALID: Simple data component
[EcsComponent]
public partial struct Position
{
    public float X;
    public float Y;
    public float Z;
}

// ✅ VALID: With utility method
[EcsComponent]
public partial struct Velocity
{
    public float X, Y, Z;
    
    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);
}

// ✅ VALID: Tag component (zero-size)
[EcsComponent(IsTag = true)]
public partial struct Dead { }

// ✅ VALID: With readonly fields
[EcsComponent]
public partial struct TeamId
{
    public readonly int Value;
    public TeamId(int value) => Value = value;
}

// ✅ VALID: Fixed-size buffer (for DOTS compatibility)
[EcsComponent]
public partial struct FixedName
{
    public unsafe fixed char Buffer[32];
}
```

#### Invalid Component Examples

```csharp
// ❌ INVALID: Class-based (must be struct)
[EcsComponent]
public class BadComponent { }

// ❌ INVALID: Generic component
[EcsComponent]
public partial struct Container<T> { public T Value; }

// ❌ INVALID: Nested type
public class Outer
{
    [EcsComponent]
    public partial struct Inner { }  // ❌ Not allowed
}

// ❌ INVALID: Reference type field (not portable)
[EcsComponent]
public partial struct Named
{
    public string Name;  // ❌ Reference type
}

// ⚠️ ALLOWED but DOTS-incompatible (generates warning)
[EcsComponent]
[ManagedComponent]  // Explicit opt-in required
public partial struct ManagedData
{
    public string Name;  // Only works on non-DOTS backends
}
```

---

## System Definition Rules

### System Class Requirements

| Rule | Requirement | Rationale |
|------|-------------|-----------|
| **Type** | Must be `class` | Instance state, DI |
| **Attribute** | Must have `[EcsSystem]` | Discovery |
| **Partial** | **REQUIRED** | Generator extends class |
| **Sealed** | Allowed but not required | Preference |
| **Abstract** | **NOT ALLOWED** | Must be instantiable |
| **Generic** | **NOT ALLOWED** | Backend compatibility |
| **Nested** | **NOT ALLOWED** | Generator complexity |
| **Inheritance** | Single base class allowed | Backend may add interface |
| **Multiple Query Methods** | Allowed | Each generates separate query |

### System Attribute

```csharp
[EcsSystem(Phase = SystemPhase.Update, Order = 10)]
[EcsRequires(EcsFeature.Basic)]
public partial class MovementSystem
{
    // ...
}
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Phase` | `SystemPhase` | `Update` | Execution phase |
| `Order` | `int` | `0` | Order within phase (lower = earlier) |
| `Group` | `Type?` | `null` | System group class |

### System Phases

```csharp
public enum SystemPhase
{
    /// <summary>Run once at world startup</summary>
    Initialization = 0,
    
    /// <summary>Run before main update (input processing)</summary>
    EarlyUpdate = 100,
    
    /// <summary>Main game logic</summary>
    Update = 200,
    
    /// <summary>Run after main update (reactions, constraints)</summary>
    LateUpdate = 300,
    
    /// <summary>Cleanup, prepare for next frame</summary>
    Cleanup = 400,
}
```

---

## Query DSL Grammar

### Formal Grammar (EBNF-like)

```ebnf
(* Query Attribute *)
QueryAttribute ::= '[Query' QueryParameters? ']'

QueryParameters ::= '(' ParameterList ')'
ParameterList   ::= Parameter (',' Parameter)*
Parameter       ::= AllParam | AnyParam | NoneParam | CachedParam

AllParam    ::= 'All' '=' TypeArray
AnyParam    ::= 'Any' '=' TypeArray  
NoneParam   ::= 'None' '=' TypeArray
CachedParam ::= 'Cached' '=' BoolLiteral

TypeArray   ::= 'new[]' '{' TypeList '}'
              | 'new' 'Type[]' '{' TypeList '}'
TypeList    ::= TypeOf (',' TypeOf)*
TypeOf      ::= 'typeof' '(' TypeName ')'

BoolLiteral ::= 'true' | 'false'
TypeName    ::= Identifier ('.' Identifier)*

(* Query Method *)
QueryMethod ::= QueryAttribute MethodSignature

MethodSignature ::= AccessModifier? 'void' Identifier '(' ParameterDeclList ')'

ParameterDeclList ::= ParameterDecl (',' ParameterDecl)*
ParameterDecl     ::= RefKind? TypeName Identifier

RefKind ::= 'ref' | 'in' | 'out'

AccessModifier ::= 'public' | 'protected' | 'private' | 'internal'
```

### Query Attribute Rules

| Rule | Description |
|------|-------------|
| `All` components must be present on entity | Conjunction (AND) |
| `Any` at least one must be present | Disjunction (OR) |
| `None` must NOT be present | Exclusion |
| Components in `All` SHOULD appear as method parameters | Generator warning if mismatch |
| Components in `Any` MAY appear as `[Optional]` parameters | May be null |
| `None` components NEVER appear as parameters | Filtered out |

### Query Method Parameter Rules

| Parameter Type | Syntax | Meaning |
|----------------|--------|---------|
| Entity access | `Entity e` | Current entity handle |
| Read-write component | `ref T component` | Mutable access |
| Read-only component | `in T component` | Immutable access |
| Optional component | `[Optional] ref T? component` | May be null if not present |
| Write-only (rare) | `out T component` | Initialize new value |

### Parameter Matching Rules

```
Method parameters MUST match Query.All components:

[Query(All = new[] { typeof(A), typeof(B) })]
void Process(ref A a, in B b)  // ✅ Matches All

[Query(All = new[] { typeof(A), typeof(B), typeof(C) })]
void Process(ref A a, in B b)  // ❌ ERROR: Missing C

[Query(All = new[] { typeof(A) })]
void Process(ref A a, ref B b)  // ❌ ERROR: B not in All
```

### Complete Query Examples

```csharp
[EcsSystem]
public partial class ExampleSystem
{
    // ═══════════════════════════════════════════════════════════════
    // Basic Query: All entities with Position AND Velocity
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void ProcessMovement(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
        pos.Y += vel.Y * DeltaTime;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // With Entity access
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Health) })]
    public void ProcessHealth(Entity entity, ref Health health)
    {
        if (health.Current <= 0)
        {
            World.Add<Dead>(entity);  // Add tag via world
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // Exclusion: Has Health but NOT Dead
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Health) }, None = new[] { typeof(Dead) })]
    public void ProcessAlive(ref Health health)
    {
        health.Current = Math.Min(health.Current + 1, health.Max);  // Regen
    }
    
    // ═══════════════════════════════════════════════════════════════
    // Any: Has at least one damage type
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Health) }, 
           Any = new[] { typeof(FireDamage), typeof(IceDamage) })]
    public void ProcessDamaged(
        Entity entity,
        ref Health health,
        [Optional] in FireDamage? fire,
        [Optional] in IceDamage? ice)
    {
        if (fire.HasValue)
            health.Current -= fire.Value.Damage;
        if (ice.HasValue)
            health.Current -= ice.Value.Damage;
    }
    
    // ═══════════════════════════════════════════════════════════════
    // Non-cached query (re-evaluated each call)
    // ═══════════════════════════════════════════════════════════════
    [Query(All = new[] { typeof(Spawner) }, Cached = false)]
    public void ProcessSpawners(Entity entity, ref Spawner spawner)
    {
        // Query rebuilt each frame (useful if archetypes change frequently)
    }
}
```

---

## Injection DSL

### Injectable Types

| Attribute | Target Type | Description |
|-----------|-------------|-------------|
| `[Inject]` | `IWorld` | World reference |
| `[Inject]` | `float` (as DeltaTime) | Frame delta time |
| `[Inject]` | `ISystemContext` | Full context object |
| `[Inject]` | `ICommandBuffer` | Deferred structural changes (see RFC-0012) |
| `[InjectSystem]` | `T : IUnifySystem` | Another system reference |

### Injection Rules

```csharp
[EcsSystem]
public partial class CombatSystem
{
    // ═══════════════════════════════════════════════════════════════
    // World injection (always available)
    // ═══════════════════════════════════════════════════════════════
    [Inject] 
    protected IWorld World { get; set; }
    
    // ═══════════════════════════════════════════════════════════════
    // DeltaTime injection (updated each frame)
    // ═══════════════════════════════════════════════════════════════
    [Inject] 
    protected float DeltaTime { get; set; }
    
    // ═══════════════════════════════════════════════════════════════
    // System injection (resolved after all systems registered)
    // ═══════════════════════════════════════════════════════════════
    [InjectSystem] 
    protected MovementSystem Movement { get; set; }
    
    // ═══════════════════════════════════════════════════════════════
    // Full context (includes World, DeltaTime, FrameCount, etc.)
    // ═══════════════════════════════════════════════════════════════
    [Inject]
    protected ISystemContext Context { get; set; }
}
```

### Injection Timing

| Phase | What Happens |
|-------|--------------|
| `Register()` | System instance created |
| `Inject()` | `[Inject]` properties populated |
| `LateInject()` | `[InjectSystem]` properties resolved |
| `Initialize()` | `IInitializeSystem.Initialize()` called |
| `Update()` | `DeltaTime` updated, `Execute()` called |

---

## Reactive DSL

### Reactive Attributes

| Attribute | Trigger | Parameters |
|-----------|---------|------------|
| `[OnAdded(typeof(T))]` | Component T added to entity | `Entity, ref T` |
| `[OnRemoved(typeof(T))]` | Component T removed from entity | `Entity` (no component) |
| `[OnChanged(typeof(T))]` | Component T value changed | `Entity, ref T` |

### Reactive Method Signatures

```csharp
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]
public partial class ReactiveExampleSystem
{
    // ═══════════════════════════════════════════════════════════════
    // OnAdded: Called when component is first added
    // ═══════════════════════════════════════════════════════════════
    [OnAdded(typeof(Health))]
    public void OnHealthAdded(Entity entity, ref Health health)
    {
        // Initialize health bar UI
        health.Max = health.Current;  // Can modify
    }
    
    // ═══════════════════════════════════════════════════════════════
    // OnRemoved: Called when component is removed
    // NOTE: Component value is NOT available (already removed)
    // ═══════════════════════════════════════════════════════════════
    [OnRemoved(typeof(Health))]
    public void OnHealthRemoved(Entity entity)
    {
        // Cleanup health bar UI
    }
    
    // ═══════════════════════════════════════════════════════════════
    // OnChanged: Called when component value differs from previous
    // ═══════════════════════════════════════════════════════════════
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity entity, ref Health health)
    {
        if (health.Current <= 0)
        {
            World.Add<Dead>(entity);
        }
    }
}
```

### Reactive Ordering

| Order | Behavior |
|-------|----------|
| `OnAdded` | Called in entity creation order within frame |
| `OnRemoved` | Called in removal order within frame |
| `OnChanged` | Called for all changes after system execution |

---

## World API (User-Facing)

### Creating and Using Worlds

```csharp
// ═══════════════════════════════════════════════════════════════
// 1. Create world with specific backend
// ═══════════════════════════════════════════════════════════════
IWorld world = WorldFactory.Create(EcsBackend.Arch);

// ═══════════════════════════════════════════════════════════════
// 2. Create world with default backend
// ═══════════════════════════════════════════════════════════════
IWorld world = WorldFactory.Create();

// ═══════════════════════════════════════════════════════════════
// 3. Entity operations
// ═══════════════════════════════════════════════════════════════
Entity player = world.CreateEntity();
world.Add(player, new Position { X = 0, Y = 0 });
world.Add(player, new Health { Current = 100, Max = 100 });
world.Add<PlayerControlled>(player);  // Tag component

// ═══════════════════════════════════════════════════════════════
// 4. Component access
// ═══════════════════════════════════════════════════════════════
ref Position pos = ref world.Get<Position>(player);
pos.X += 10;

if (world.Has<Health>(player))
{
    ref Health health = ref world.Get<Health>(player);
    health.Current -= 10;
}

world.Remove<PlayerControlled>(player);

// ═══════════════════════════════════════════════════════════════
// 5. Destruction
// ═══════════════════════════════════════════════════════════════
world.DestroyEntity(player);
```

### IWorld vs Backend-Native API

**Design Decision**: Systems use **injected `IWorld`** for portable code, generators may emit **backend-native code** for performance.

```csharp
// User writes (portable):
[EcsSystem]
public partial class MySystem
{
    [Inject] protected IWorld World { get; set; }
    
    [Query(All = new[] { typeof(Health) })]
    public void Process(Entity e, ref Health h)
    {
        if (h.Current <= 0)
            World.Add<Dead>(e);  // Uses IWorld
    }
}

// Generator MAY emit (backend-optimized):
partial class MySystem
{
    public void Execute(Arch.Core.World archWorld, float dt)
    {
        archWorld.Query(in _query, (Entity e, ref Health h) =>
        {
            if (h.Current <= 0)
                archWorld.Add<Dead>(e);  // Direct Arch API
        });
    }
}
```

---

## Complete Example: Game Code

```csharp
using UnifyECS;

namespace MyGame.Components
{
    [EcsComponent]
    public partial struct Position { public float X, Y, Z; }
    
    [EcsComponent]
    public partial struct Velocity { public float X, Y, Z; }
    
    [EcsComponent]
    public partial struct Health { public int Current, Max; }
    
    [EcsComponent(IsTag = true)]
    public partial struct Dead { }
    
    [EcsComponent(IsTag = true)]
    public partial struct PlayerControlled { }
}

namespace MyGame.Systems
{
    using MyGame.Components;
    
    [EcsSystem(Phase = SystemPhase.Update, Order = 0)]
    public partial class MovementSystem
    {
        [Inject] protected float DeltaTime { get; set; }
        
        [Query(All = new[] { typeof(Position), typeof(Velocity) },
               None = new[] { typeof(Dead) })]
        public void Process(ref Position pos, in Velocity vel)
        {
            pos.X += vel.X * DeltaTime;
            pos.Y += vel.Y * DeltaTime;
            pos.Z += vel.Z * DeltaTime;
        }
    }
    
    [EcsSystem(Phase = SystemPhase.Update, Order = 100)]
    [EcsRequires(EcsFeature.Reactive, IfMissing = MissingFeatureBehavior.Emulate)]
    public partial class DeathSystem
    {
        [Inject] protected IWorld World { get; set; }
        
        [OnChanged(typeof(Health))]
        public void OnHealthChanged(Entity entity, ref Health health)
        {
            if (health.Current <= 0 && !World.Has<Dead>(entity))
            {
                World.Add<Dead>(entity);
            }
        }
    }
}

namespace MyGame
{
    using MyGame.Systems;
    
    public class Game
    {
        private IWorld _world;
        private ISystemRunner _runner;
        
        public void Start()
        {
            _world = WorldFactory.Create(EcsBackend.Arch);
            _runner = new SystemRunner(_world);
            
            _runner.Register(
                new MovementSystem(),
                new DeathSystem()
            );
            
            _runner.Initialize();
            
            // Spawn player
            var player = _world.CreateEntity();
            _world.Add(player, new Position());
            _world.Add(player, new Velocity { X = 1, Y = 0, Z = 0 });
            _world.Add(player, new Health { Current = 100, Max = 100 });
            _world.Add<PlayerControlled>(player);
        }
        
        public void Update(float deltaTime)
        {
            _runner.Update(deltaTime);
        }
        
        public void Shutdown()
        {
            _runner.Dispose();
            _world.Dispose();
        }
    }
}
```

---
 
## Non-Goals and Future Extensions

- **Async query methods**: Not supported in v1. Query methods MUST be synchronous instance methods returning `void`. A future RFC may introduce an async execution model, but it MUST preserve source compatibility for existing synchronous systems.
- **Return values from query methods**: Not supported in v1. Query methods MUST communicate via side effects only; early-termination or aggregation semantics are backend-specific concerns and are intentionally outside this RFC.
- **Fine-grained parallel conflict rules**: In v1, parallel execution is treated as an implementation detail and optimization. Systems MUST be semantically correct when executed single-threaded. Backends and related RFCs (e.g., RFC-0003 optimization hints and RFC-0012 structural changes) MAY define stricter rules and analyzer behavior for when parallelization is allowed.

## References

- RFC-0003: Attribute API Design
- RFC-0008: World Lifecycle & System Execution
- RFC-0009: Component Registry & Type Mapping
