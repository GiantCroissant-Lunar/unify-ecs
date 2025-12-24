# RFC-0003: Attribute API Design

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0002

## Summary

Define the attribute-based API that developers use to declare components, systems, queries, and feature requirements in a backend-agnostic manner.

## Motivation

The attribute API is the primary interface developers interact with. It must be:
- Intuitive and familiar to ECS developers
- Expressive enough to capture common patterns
- Simple enough that generated code is predictable

## Version Scope

This document defines the **v1 attribute-based API** for UnifyECS:

- **In scope**: the shapes and basic semantics of the core attributes (`EcsComponent`, `EcsSystem`, `Query`, `EcsRequires`, reactive callbacks, injection attributes, and `EcsOptimize`) as consumed by the v1 source generators and analyzers.
- **Out of scope**: method-level `[EcsRequires]`, custom attribute-based DSL extensions, and advanced control-flow semantics (such as query methods that return `bool` to control system flow).
- **Compatibility**: later RFCs may extend this API (for example, by adding new attributes or new return-type behaviors), but MUST NOT break code that conforms to the v1 rules.

## Core Attributes

### Component Declaration

```csharp
/// <summary>
/// Marks a struct as an ECS component
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class EcsComponentAttribute : Attribute
{
    /// <summary>
    /// If true, component is treated as a tag (zero-size marker)
    /// </summary>
    public bool IsTag { get; set; }
    
    /// <summary>
    /// If true, component data is shared across entities with same value
    /// Only supported on DOTS backend
    /// </summary>
    public bool IsShared { get; set; }
}

// Usage examples
[EcsComponent]
public struct Position 
{ 
    public float X, Y, Z; 
}

[EcsComponent]
public struct Velocity 
{ 
    public float X, Y, Z; 
}

[EcsComponent(IsTag = true)]
public struct Dead { }

[EcsComponent(IsTag = true)]
public struct PlayerControlled { }

[EcsComponent(IsShared = true)]  // DOTS-specific optimization
public struct TeamId 
{ 
    public int Value; 
}
```

### System Declaration

```csharp
/// <summary>
/// Marks a class as an ECS system
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class EcsSystemAttribute : Attribute
{
    /// <summary>
    /// Execution phase for this system
    /// </summary>
    public SystemPhase Phase { get; set; } = SystemPhase.Update;
    
    /// <summary>
    /// Execution order within the phase (lower = earlier)
    /// </summary>
    public int Order { get; set; } = 0;
    
    /// <summary>
    /// System group this system belongs to
    /// </summary>
    public Type? Group { get; set; }
}

public enum SystemPhase
{
    Initialization,
    EarlyUpdate,
    Update,
    LateUpdate,
    Cleanup
}

// Usage
[EcsSystem(Phase = SystemPhase.Update, Order = 10)]
public partial class MovementSystem
{
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void Process(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
        pos.Y += vel.Y * DeltaTime;
        pos.Z += vel.Z * DeltaTime;
    }
    
    // Injected by generator
    protected float DeltaTime { get; set; }
}
```

### Query Declaration

```csharp
/// <summary>
/// Declares a query for entities matching specific component criteria
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public sealed class QueryAttribute : Attribute
{
    /// <summary>
    /// Entity must have ALL of these components
    /// </summary>
    public Type[] All { get; set; } = Array.Empty<Type>();
    
    /// <summary>
    /// Entity must have at least ONE of these components
    /// </summary>
    public Type[] Any { get; set; } = Array.Empty<Type>();
    
    /// <summary>
    /// Entity must have NONE of these components
    /// </summary>
    public Type[] None { get; set; } = Array.Empty<Type>();
    
    /// <summary>
    /// If true, query results are cached
    /// </summary>
    public bool Cached { get; set; } = true;
}

// Usage examples
[EcsSystem]
public partial class MovementSystem
{
    // Simple query - all entities with Position AND Velocity
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void ProcessMovers(ref Position pos, in Velocity vel) { ... }
    
    // Complex query - entities with Health but not Dead
    [Query(All = new[] { typeof(Health) }, None = new[] { typeof(Dead) })]
    public void ProcessAlive(ref Health health) { ... }
    
    // Any query - entities with either damage type
    [Query(All = new[] { typeof(Health) }, 
           Any = new[] { typeof(FireDamage), typeof(IceDamage) })]
    public void ProcessDamaged(Entity e, ref Health health) { ... }
}
```

### Feature Requirements

```csharp
/// <summary>
/// Declares required ECS features for a system
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class EcsRequiresAttribute : Attribute
{
    public EcsFeature Features { get; }
    
    /// <summary>
    /// Behavior when feature is not supported by backend
    /// </summary>
    public MissingFeatureBehavior IfMissing { get; set; } = MissingFeatureBehavior.UsePolicy;
    
    public EcsRequiresAttribute(EcsFeature features)
    {
        Features = features;
    }
}

public enum MissingFeatureBehavior
{
    /// <summary>Use global policy from MSBuild configuration</summary>
    UsePolicy,
    
    /// <summary>Emit compile error if feature is unsupported</summary>
    Error,
    
    /// <summary>Emit warning and generate stub</summary>
    Warn,
    
    /// <summary>Silently generate no-op (dangerous!)</summary>
    NoOp,
    
    /// <summary>Attempt to emulate the feature</summary>
    Emulate
}

// Usage
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]  // Uses global policy
public partial class DamageSystem { ... }

[EcsSystem]
[EcsRequires(EcsFeature.Jobs, IfMissing = MissingFeatureBehavior.Emulate)]
public partial class PhysicsSystem { ... }

[EcsSystem]
[EcsRequires(EcsFeature.Reactive, IfMissing = MissingFeatureBehavior.NoOp)]  // Debug-only
public partial class DebugHealthLogger { ... }
```

### Reactive Callbacks

```csharp
/// <summary>
/// Called when component is added to an entity
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnAddedAttribute : Attribute
{
    public Type ComponentType { get; }
    
    public OnAddedAttribute(Type componentType)
    {
        ComponentType = componentType;
    }
}

/// <summary>
/// Called when component is removed from an entity
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnRemovedAttribute : Attribute
{
    public Type ComponentType { get; }
    
    public OnRemovedAttribute(Type componentType)
    {
        ComponentType = componentType;
    }
}

/// <summary>
/// Called when component value changes
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnChangedAttribute : Attribute
{
    public Type ComponentType { get; }
    
    public OnChangedAttribute(Type componentType)
    {
        ComponentType = componentType;
    }
}

// Usage
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]
public partial class DamageReactionSystem
{
    [OnAdded(typeof(Health))]
    public void OnHealthAdded(Entity e, ref Health health)
    {
        // Initialize health bar UI
    }
    
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity e, ref Health health)
    {
        if (health.Current <= 0)
        {
            e.Add<Dead>();
        }
    }
    
    [OnRemoved(typeof(Health))]
    public void OnHealthRemoved(Entity e)
    {
        // Cleanup health bar UI
    }
}
```

### Backend-Specific Hints

```csharp
/// <summary>
/// Provides optimization hints for specific backends
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class EcsOptimizeAttribute : Attribute
{
    public EcsBackend Backend { get; }
    
    /// <summary>DOTS: Enable Burst compilation</summary>
    public bool BurstCompile { get; set; }
    
    /// <summary>DOTS: Schedule as parallel job</summary>
    public bool Parallel { get; set; }
    
    /// <summary>Arch: Use inline query (no delegate allocation)</summary>
    public bool InlineQuery { get; set; }
    
    public EcsOptimizeAttribute(EcsBackend backend)
    {
        Backend = backend;
    }
}

// Usage
[EcsSystem]
[EcsOptimize(EcsBackend.Dots, BurstCompile = true, Parallel = true)]
[EcsOptimize(EcsBackend.Arch, InlineQuery = true)]
public partial class PhysicsSystem
{
    [Query(All = new[] { typeof(Position), typeof(Velocity), typeof(RigidBody) })]
    public void Process(ref Position pos, ref Velocity vel, in RigidBody body) { ... }
}
```

### Dependency Injection

```csharp
/// <summary>
/// Marks a property or field for injection by the ECS runtime
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class InjectAttribute : Attribute { }

/// <summary>
/// Injects a reference to another system
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class InjectSystemAttribute : Attribute
{
    public Type? SystemType { get; set; }
}

// Usage
[EcsSystem]
public partial class CombatSystem
{
    [Inject]
    protected IWorldAccess World { get; set; }
    
    [Inject]
    protected float DeltaTime { get; set; }
    
    [InjectSystem]
    protected MovementSystem MovementSystem { get; set; }
    
    [Query(All = new[] { typeof(Attacker), typeof(Position) })]
    public void Process(Entity e, in Attacker attacker, in Position pos)
    {
        // Use injected dependencies
    }
}
```

## System Method Signatures

### Supported Parameter Types

| Parameter | Description | Backend Support |
|-----------|-------------|-----------------|
| `Entity e` | Current entity reference | All |
| `ref T component` | Mutable component access | All |
| `in T component` | Read-only component access | All |
| `float deltaTime` | Frame delta time (injected) | All |
| `IWorldAccess world` | World operations access | All |

### Return Types

- `void`: Standard processing (v1 only)
- `bool`: Reserved for a potential future extension where returning `false` may influence system flow. **Not supported in v1**; analyzers and generators treat non-void return types on query methods as invalid.

## Complete Example

```csharp
using UnifyECS;

[EcsComponent]
public struct Position { public float X, Y, Z; }

[EcsComponent]
public struct Velocity { public float X, Y, Z; }

[EcsComponent]
public struct Health { public int Current, Max; }

[EcsComponent(IsTag = true)]
public struct Dead { }

[EcsComponent(IsTag = true)]
public struct PlayerControlled { }

[EcsSystem(Phase = SystemPhase.Update, Order = 0)]
public partial class MovementSystem
{
    [Inject] protected float DeltaTime { get; set; }
    
    [Query(All = new[] { typeof(Position), typeof(Velocity) }, 
           None = new[] { typeof(Dead) })]
    public void ProcessMovement(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
        pos.Y += vel.Y * DeltaTime;
        pos.Z += vel.Z * DeltaTime;
    }
}

[EcsSystem(Phase = SystemPhase.Update, Order = 100)]
[EcsRequires(EcsFeature.Reactive)]
public partial class DeathSystem
{
    [Inject] protected IWorldAccess World { get; set; }
    
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity e, ref Health health)
    {
        if (health.Current <= 0 && !World.Has<Dead>(e))
        {
            World.Add<Dead>(e);
        }
    }
}
```

## Open Questions

## Non-Goals and Future Extensions

- **Entity parameter position**: v1 does not require `Entity` (when present) to be the first parameter. Entity parameters may appear in any position, and generators/analyzers do not rely on a specific ordering. A stricter convention may be defined in a future RFC.
- **Optional components**: v1 supports the `[Optional]` attribute syntactically, but advanced semantics (for example, tight coupling to `Any` sets or automatic nullable handling) are deliberately left flexible. Future RFCs may define stricter rules and analyzer enforcement for optional components.
- **Method-level `[EcsRequires]`**: v1 only supports declaring feature requirements at the system (class) level. Method-level requirements are considered a future extension and are not part of the v1 contract.

## References

- RFC-0001: Core Architecture
- RFC-0002: Feature Capability System
