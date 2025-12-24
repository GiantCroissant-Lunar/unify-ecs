# RFC-0002: Feature Capability System

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001

## Summary

Define a tiered feature capability system that maps ECS features to backend support levels, enabling explicit declaration of system requirements and intelligent code generation.

## Motivation

Different ECS frameworks support different feature sets. Rather than limiting the API to the intersection of all frameworks, we define a superset and let systems declare their requirements explicitly.

## Design

## Version Scope

This document defines the **v1 feature capability system** for UnifyECS:

- **In scope**: the built-in `EcsFeature` flags, the `BackendCapabilities` matrix for the initial backends, and how systems declare requirements via `[EcsRequires]` and `MissingFeatureBehavior`.
- **Out of scope**: custom feature definitions, backend-specific feature flags outside `EcsFeature`, and inheritance/merging rules across base/derived systems.
- **Compatibility**: later RFCs may add new features or backends, but MUST NOT change the semantics of existing flags or requirements declared using this version.

### Feature Flags

```csharp
[Flags]
public enum EcsFeature
{
    None = 0,
    
    // ═══════════════════════════════════════════════════════════════
    // Tier 1: Universal (all backends MUST support)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Basic entity lifecycle: create, destroy, exists checks
    /// </summary>
    EntityLifecycle = 1 << 0,
    
    /// <summary>
    /// Component operations: add, remove, get, set, has
    /// </summary>
    ComponentOperations = 1 << 1,
    
    /// <summary>
    /// Basic queries: iterate entities with specific components
    /// </summary>
    BasicQueries = 1 << 2,
    
    /// <summary>
    /// System execution: ordered system updates
    /// </summary>
    SystemExecution = 1 << 3,
    
    /// <summary>
    /// Combination of all Tier 1 features
    /// </summary>
    Basic = EntityLifecycle | ComponentOperations | BasicQueries | SystemExecution,
    
    // ═══════════════════════════════════════════════════════════════
    // Tier 2: Common (most backends support, may require emulation)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Component change events: callbacks when components are modified
    /// </summary>
    Events = 1 << 4,
    
    /// <summary>
    /// Complex query filters: Any, None, Optional component matchers
    /// </summary>
    AdvancedFiltering = 1 << 5,
    
    /// <summary>
    /// Tag components: zero-size marker components
    /// </summary>
    TagComponents = 1 << 6,
    
    /// <summary>
    /// System groups: organize systems into execution groups
    /// </summary>
    SystemGroups = 1 << 7,
    
    // ═══════════════════════════════════════════════════════════════
    // Tier 3: Advanced (some backends support natively)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Reactive systems: OnAdded, OnRemoved, OnChanged callbacks
    /// </summary>
    Reactive = 1 << 8,
    
    /// <summary>
    /// Entity relationships: parent/child, references between entities
    /// </summary>
    Relationships = 1 << 9,
    
    /// <summary>
    /// Parallel job execution: multi-threaded system processing
    /// </summary>
    Jobs = 1 << 10,
    
    /// <summary>
    /// World events: system-wide event bus
    /// </summary>
    WorldEvents = 1 << 11,
    
    // ═══════════════════════════════════════════════════════════════
    // Tier 4: Specialized (framework-specific, rarely portable)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// DOTS Burst compilation support
    /// </summary>
    BurstCompile = 1 << 12,
    
    /// <summary>
    /// Shared components (entities with same shared component grouped)
    /// </summary>
    SharedComponents = 1 << 13,
    
    /// <summary>
    /// Chunk iteration: access to underlying storage chunks
    /// </summary>
    ChunkIteration = 1 << 14,
    
    /// <summary>
    /// Component pools: custom component storage strategies
    /// </summary>
    ComponentPools = 1 << 15,
}
```

### Backend Capability Registry

```csharp
public static class BackendCapabilities
{
    /// <summary>
    /// Get features natively supported by a backend
    /// </summary>
    public static EcsFeature GetNativeFeatures(EcsBackend backend) => backend switch
    {
        EcsBackend.Arch => 
            EcsFeature.Basic | 
            EcsFeature.AdvancedFiltering |
            EcsFeature.TagComponents |
            EcsFeature.Events |
            EcsFeature.SystemGroups,
            
        EcsBackend.Entitas => 
            EcsFeature.Basic | 
            EcsFeature.AdvancedFiltering |
            EcsFeature.TagComponents |
            EcsFeature.Events |
            EcsFeature.Reactive |
            EcsFeature.SystemGroups,
            
        EcsBackend.Dots => 
            EcsFeature.Basic | 
            EcsFeature.AdvancedFiltering |
            EcsFeature.TagComponents |
            EcsFeature.Jobs |
            EcsFeature.BurstCompile |
            EcsFeature.SharedComponents |
            EcsFeature.ChunkIteration |
            EcsFeature.SystemGroups |
            EcsFeature.Relationships,
            
        EcsBackend.DefaultEcs =>
            EcsFeature.Basic |
            EcsFeature.AdvancedFiltering |
            EcsFeature.TagComponents |
            EcsFeature.Events |
            EcsFeature.Relationships |
            EcsFeature.WorldEvents,
            
        EcsBackend.Friflo =>
            EcsFeature.Basic |
            EcsFeature.AdvancedFiltering |
            EcsFeature.TagComponents |
            EcsFeature.Events |
            EcsFeature.Relationships |
            EcsFeature.Jobs,
            
        _ => EcsFeature.Basic
    };
    
    /// <summary>
    /// Get features that can be emulated on a backend (with performance cost)
    /// </summary>
    public static EcsFeature GetEmulatableFeatures(EcsBackend backend) => backend switch
    {
        EcsBackend.Arch => 
            EcsFeature.Reactive |       // Track previous state, detect changes
            EcsFeature.Relationships,   // Via component references + queries
            
        EcsBackend.Entitas => 
            EcsFeature.WorldEvents,     // Via singleton entity with event list
            
        EcsBackend.Dots => 
            EcsFeature.Events |         // Poll for changes
            EcsFeature.Reactive,        // Via ISystemStateComponent + cleanup systems
            
        _ => EcsFeature.None
    };
    
    /// <summary>
    /// Check if a backend supports a feature (natively or via emulation)
    /// </summary>
    public static bool Supports(EcsBackend backend, EcsFeature feature, bool allowEmulation = true)
    {
        var native = GetNativeFeatures(backend);
        if ((native & feature) == feature) return true;
        
        if (allowEmulation)
        {
            var emulatable = GetEmulatableFeatures(backend);
            return (emulatable & feature) == feature;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get support level for a feature
    /// </summary>
    public static FeatureSupportLevel GetSupportLevel(EcsBackend backend, EcsFeature feature)
    {
        var native = GetNativeFeatures(backend);
        if ((native & feature) == feature) 
            return FeatureSupportLevel.Native;
        
        var emulatable = GetEmulatableFeatures(backend);
        if ((emulatable & feature) == feature) 
            return FeatureSupportLevel.Emulated;
        
        return FeatureSupportLevel.Unsupported;
    }
}

public enum FeatureSupportLevel
{
    /// <summary>Feature is natively supported with full performance</summary>
    Native,
    
    /// <summary>Feature can be emulated but may have performance overhead</summary>
    Emulated,
    
    /// <summary>Feature cannot be supported on this backend</summary>
    Unsupported
}
```

### Feature Compatibility Matrix

| Feature | Arch | Entitas | DOTS | DefaultEcs | Friflo |
|---------|------|---------|------|------------|--------|
| Basic | ✅ Native | ✅ Native | ✅ Native | ✅ Native | ✅ Native |
| AdvancedFiltering | ✅ Native | ✅ Native | ✅ Native | ✅ Native | ✅ Native |
| TagComponents | ✅ Native | ✅ Native | ✅ Native | ✅ Native | ✅ Native |
| Events | ✅ Native | ✅ Native | 🔶 Emulated | ✅ Native | ✅ Native |
| Reactive | 🔶 Emulated | ✅ Native | 🔶 Emulated | ❌ None | ❌ None |
| Relationships | 🔶 Emulated | ❌ None | ✅ Native | ✅ Native | ✅ Native |
| Jobs | ❌ None | ❌ None | ✅ Native | ❌ None | ✅ Native |
| BurstCompile | ❌ None | ❌ None | ✅ Native | ❌ None | ❌ None |
| SharedComponents | ❌ None | ❌ None | ✅ Native | ❌ None | ❌ None |

Legend:
- ✅ Native: Full support with optimal performance
- 🔶 Emulated: Supported via generated code, may have overhead
- ❌ None: Not supported

### Usage in System Declarations

```csharp
// Explicit requirement - error if not supported
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]
public partial class DamageReactiveSystem { ... }

// Optional feature with fallback behavior
[EcsSystem]
[EcsRequires(EcsFeature.Jobs, IfMissing = MissingFeatureBehavior.Emulate)]
public partial class PhysicsSystem { ... }

// Multiple requirements
[EcsSystem]
[EcsRequires(EcsFeature.Reactive | EcsFeature.Relationships)]
public partial class HierarchyReactiveSystem { ... }
```

### Compile-Time Validation

The source generator validates requirements at compile-time:

```csharp
// Generator pseudo-code
foreach (var system in systemsToGenerate)
{
    var required = system.GetRequiredFeatures();
    
    foreach (var backend in configuredBackends)
    {
        var supported = BackendCapabilities.GetNativeFeatures(backend) 
                      | BackendCapabilities.GetEmulatableFeatures(backend);
        
        var missing = required & ~supported;
        
        if (missing != EcsFeature.None)
        {
            var behavior = system.GetMissingBehavior() ?? globalPolicy;
            
            switch (behavior)
            {
                case MissingFeatureBehavior.Error:
                    EmitError($"System {system.Name} requires {missing} but {backend} doesn't support it");
                    break;
                    
                case MissingFeatureBehavior.Warn:
                    EmitWarning($"System {system.Name} will be stubbed for {backend} (missing {missing})");
                    GenerateStub(system, backend);
                    break;
                    
                case MissingFeatureBehavior.NoOp:
                    GenerateNoOp(system, backend);
                    break;
            }
        }
    }
}
```

## Non-Goals and Future Extensions

- **Inheritance of feature requirements**: In v1, `[EcsRequires]` is evaluated per concrete system type. Requirements are not inherited or merged from base classes. Future RFCs may introduce inheritance rules, but they MUST remain source-compatible with v1.
- **Granularity of feature flags**: v1 uses coarse-grained flags as defined in the `EcsFeature` enum (for example, a combined `Reactive` flag). Finer-grained flags (such as separating `OnAdded`/`OnRemoved`/`OnChanged`) are explicitly deferred to future RFCs.
- **Custom feature definitions**: v1 only supports the built-in `EcsFeature` enum for capability checks. Third-party or user-defined features are not part of this RFC and may be added later via an extension mechanism without breaking v1.

## References

- RFC-0001: Core Architecture
- RFC-0006: Missing Feature Policies
