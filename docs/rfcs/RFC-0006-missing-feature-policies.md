# RFC-0006: Missing Feature Policies

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0002

## Summary

Define the policy system for handling situations where a system requires ECS features not supported by a configured backend.

## Motivation

Not all ECS frameworks support all features. Rather than silently failing or forcing lowest-common-denominator code, we need explicit, configurable policies that:

1. Prevent silent bugs
2. Enable gradual adoption
3. Allow different strictness levels for different scenarios

## Policy Types

```csharp
public enum MissingFeatureBehavior
{
    /// <summary>
    /// Use the global policy defined in MSBuild configuration.
    /// This is the default for [EcsRequires] attributes.
    /// </summary>
    UsePolicy,
    
    /// <summary>
    /// Emit a compile-time error if feature is unsupported.
    /// Use for critical gameplay systems that MUST work.
    /// </summary>
    Error,
    
    /// <summary>
    /// Emit a compile-time warning and generate a stub.
    /// Stub logs a warning at runtime and returns early.
    /// Use during development/migration.
    /// </summary>
    Warn,
    
    /// <summary>
    /// Silently generate a no-op stub.
    /// DANGEROUS - use only for debug/optional systems.
    /// </summary>
    NoOp,
    
    /// <summary>
    /// Attempt to emulate the feature with generated helper code.
    /// May have performance overhead.
    /// </summary>
    Emulate
}
```

## Configuration

### MSBuild Properties

```xml
<PropertyGroup>
  <!-- Backends to generate code for -->
  <UnifyEcsBackends>Arch;Entitas</UnifyEcsBackends>
  
  <!-- Global policy for missing features -->
  <UnifyEcsMissingFeaturePolicy>Error</UnifyEcsMissingFeaturePolicy>
  
  <!-- Per-feature policy overrides -->
  <UnifyEcsPolicy_Reactive>Emulate</UnifyEcsPolicy_Reactive>
  <UnifyEcsPolicy_Jobs>Warn</UnifyEcsPolicy_Jobs>
  <UnifyEcsPolicy_BurstCompile>NoOp</UnifyEcsPolicy_BurstCompile>
</PropertyGroup>
```

### Policy Resolution Order

1. **System-level attribute**: `[EcsRequires(Feature, IfMissing = ...)]`
2. **Per-feature MSBuild property**: `<UnifyEcsPolicy_Reactive>...</UnifyEcsPolicy_Reactive>`
3. **Global MSBuild property**: `<UnifyEcsMissingFeaturePolicy>...</UnifyEcsMissingFeaturePolicy>`
4. **Default**: `Error`

```csharp
public static MissingFeatureBehavior ResolvePolicy(
    EcsFeature feature,
    MissingFeatureBehavior systemLevel,
    GeneratorConfig config)
{
    // 1. System-level override
    if (systemLevel != MissingFeatureBehavior.UsePolicy)
        return systemLevel;
    
    // 2. Per-feature MSBuild override
    if (config.FeaturePolicies.TryGetValue(feature, out var featurePolicy))
        return featurePolicy;
    
    // 3. Global MSBuild policy
    if (config.GlobalPolicy != MissingFeatureBehavior.UsePolicy)
        return config.GlobalPolicy;
    
    // 4. Default
    return MissingFeatureBehavior.Error;
}
```

## Policy Behaviors

### Error Policy

**When to use:** Critical gameplay systems that must work on all configured backends.

```csharp
[EcsSystem]
[EcsRequires(EcsFeature.Basic)]  // Will error if any backend doesn't support
public partial class MovementSystem { ... }
```

**Generator output:**
```
error UECS001: System 'MovementSystem' requires feature 'Reactive' which is not 
supported by backend 'Arch'. Consider using [EcsRequires(..., IfMissing = Emulate)] 
or changing your backend configuration.
```

### Warn Policy

**When to use:** Development phase, gradual migration, optional enhanced features.

```csharp
[EcsSystem]
[EcsRequires(EcsFeature.Reactive, IfMissing = MissingFeatureBehavior.Warn)]
public partial class DamageReactiveSystem { ... }
```

**Generator output (warning):**
```
warning UECS002: System 'DamageReactiveSystem' will be stubbed for backend 'Arch' 
(missing 'Reactive'). System will log warning and return early at runtime.
```

**Generated stub:**
```csharp
// DamageReactiveSystem.Arch.g.cs
partial class DamageReactiveSystem : IArchSystem
{
    private static bool _warningLogged = false;
    
    public void Execute(World world, float deltaTime)
    {
        if (!_warningLogged)
        {
            UnifyEcs.Logger.Warn(
                "DamageReactiveSystem requires 'Reactive' feature which is not " +
                "supported by Arch backend. System is disabled.");
            _warningLogged = true;
        }
        // Return early - system is disabled
    }
}
```

### NoOp Policy

**When to use:** Debug/diagnostic systems, optional analytics, non-critical features.

⚠️ **WARNING:** This policy is dangerous. Use only when you're absolutely certain the system is optional.

```csharp
[EcsSystem]
[EcsRequires(EcsFeature.Reactive, IfMissing = MissingFeatureBehavior.NoOp)]
public partial class DebugHealthChangeLogger { ... }
```

**Generated stub:**
```csharp
// DebugHealthChangeLogger.Arch.g.cs
partial class DebugHealthChangeLogger : IArchSystem
{
    // No-op implementation - feature not supported on this backend
    public void Execute(World world, float deltaTime)
    {
        // Intentionally empty
    }
}
```

### Emulate Policy

**When to use:** Features that can be approximated with acceptable overhead.

```csharp
[EcsSystem]
[EcsRequires(EcsFeature.Reactive, IfMissing = MissingFeatureBehavior.Emulate)]
public partial class HealthReactiveSystem
{
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity e, ref Health health) { ... }
}
```

**Emulation strategy for Reactive on Arch:**

```csharp
// HealthReactiveSystem.Arch.g.cs
partial class HealthReactiveSystem : IArchSystem
{
    // ═══════════════════════════════════════════════════════════════
    // EMULATION: Reactive feature emulated via change tracking
    // Performance note: O(n) comparison per frame where n = entity count
    // ═══════════════════════════════════════════════════════════════
    
    private readonly Dictionary<Entity, Health> _previousState = new();
    private readonly List<Entity> _removedEntities = new();
    
    private static readonly QueryDescription _trackQuery = new QueryDescription()
        .WithAll<Health>();
    
    public void Execute(World world, float deltaTime)
    {
        // Phase 1: Detect changes
        var changes = new List<(Entity, Health)>();
        
        world.Query(in _trackQuery, (Entity entity, ref Health health) =>
        {
            if (_previousState.TryGetValue(entity, out var prev))
            {
                if (!HealthEquals(prev, health))
                {
                    changes.Add((entity, health));
                }
            }
            else
            {
                // New entity with Health - also counts as change
                changes.Add((entity, health));
            }
            _previousState[entity] = health;
        });
        
        // Phase 2: Invoke callbacks
        foreach (var (entity, health) in changes)
        {
            var h = health;
            OnHealthChanged(entity, ref h);
            
            // Write back if modified
            if (!HealthEquals(h, health))
            {
                world.Set(entity, h);
                _previousState[entity] = h;
            }
        }
        
        // Phase 3: Cleanup destroyed entities (periodic)
        // ... omitted for brevity
    }
    
    private static bool HealthEquals(Health a, Health b)
        => a.Current == b.Current && a.Max == b.Max;
}
```

## Emulation Strategies

### Reactive Feature Emulation

| Callback | Emulation Strategy | Overhead |
|----------|-------------------|----------|
| `OnAdded` | Track entity set, detect new entries | Low |
| `OnRemoved` | Track entity set, detect missing entries | Medium |
| `OnChanged` | Store previous values, compare each frame | High |

### Relationships Feature Emulation

| Operation | Emulation Strategy | Overhead |
|-----------|-------------------|----------|
| `GetParent` | Store parent Entity as component field | Low |
| `GetChildren` | Query for entities with matching parent | Medium |
| `Hierarchy traversal` | Recursive queries | High |

### Jobs Feature Emulation

| Scenario | Emulation Strategy | Overhead |
|----------|-------------------|----------|
| Parallel iteration | Sequential loop | None (just slower) |
| Job scheduling | Immediate execution | None |

## Analyzer Integration

Generate Roslyn analyzers to provide IDE feedback:

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MissingFeatureAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSymbolAction(AnalyzeSystem, SymbolKind.NamedType);
    }
    
    private void AnalyzeSystem(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        
        if (!HasEcsSystemAttribute(type))
            return;
        
        var required = GetRequiredFeatures(type);
        var backends = GetConfiguredBackends(context);
        
        foreach (var backend in backends)
        {
            var supported = BackendCapabilities.GetNativeFeatures(backend)
                          | BackendCapabilities.GetEmulatableFeatures(backend);
            
            var missing = required & ~supported;
            
            if (missing != EcsFeature.None)
            {
                var behavior = ResolvePolicyForAnalyzer(type, missing, context);
                
                if (behavior == MissingFeatureBehavior.Error)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedFeature,
                        type.Locations.First(),
                        type.Name, backend, missing));
                }
                else if (behavior == MissingFeatureBehavior.Warn)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedFeatureWarning,
                        type.Locations.First(),
                        type.Name, backend, missing));
                }
            }
        }
    }
}
```

## Configuration Presets

### Strict (Production)

```xml
<PropertyGroup>
  <UnifyEcsMissingFeaturePolicy>Error</UnifyEcsMissingFeaturePolicy>
</PropertyGroup>
```

All unsupported features cause build failures.

### Permissive (Development)

```xml
<PropertyGroup>
  <UnifyEcsMissingFeaturePolicy>Warn</UnifyEcsMissingFeaturePolicy>
</PropertyGroup>
```

Unsupported features generate warnings and stubs.

### Migration

```xml
<PropertyGroup>
  <UnifyEcsMissingFeaturePolicy>Warn</UnifyEcsMissingFeaturePolicy>
  <UnifyEcsPolicy_Reactive>Emulate</UnifyEcsPolicy_Reactive>
  <UnifyEcsPolicy_Jobs>NoOp</UnifyEcsPolicy_Jobs>
</PropertyGroup>
```

Reactive features are emulated, Jobs are silently disabled.

## Best Practices

### DO

- ✅ Use `Error` for core gameplay systems
- ✅ Use `Emulate` when performance overhead is acceptable
- ✅ Use `Warn` during development/migration
- ✅ Use `NoOp` only for debug/diagnostic systems
- ✅ Document why a system uses a permissive policy

### DON'T

- ❌ Use `NoOp` for gameplay-critical systems
- ❌ Rely on emulation for performance-critical systems
- ❌ Ignore warnings in production builds

## Open Questions

1. Should we support runtime policy changes?
2. How to handle cascading feature dependencies (e.g., Reactive requires Events)?
3. Should emulation overhead be measurable/reported?

## References

- RFC-0001: Core Architecture
- RFC-0002: Feature Capability System
