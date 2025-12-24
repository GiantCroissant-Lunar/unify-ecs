# RFC-0001: Core Architecture

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors

## Summary

UnifyECS is a source-generation-based abstraction layer that enables writing ECS (Entity Component System) game code once and running it on multiple ECS implementations (Arch, Entitas, Unity DOTS, etc.) without runtime overhead.

## Motivation

### Problem Statement

Different ECS frameworks have fundamentally different architectures:

- **Arch ECS**: Archetype-based, synchronous, cache-friendly iteration
- **Entitas**: Context-based with reactive systems and event-driven patterns  
- **Unity DOTS**: Jobs system integration, Burst compiler, specific memory layouts
- **Others**: Sparse sets, component pools, entity-component tables

Switching ECS frameworks currently requires significant rewriting of game code, creating vendor lock-in.

### Goals

1. **Portability for Migration**: Write code once, switch backends with minimal friction
2. **Simultaneous Multi-Backend**: Run the same codebase on multiple ECS implementations for benchmarking, platform targeting, or gradual migration
3. **Zero Runtime Overhead**: Use source generation, not runtime reflection or factories
4. **Explicit Capability Management**: Clear contracts about what features each backend supports

### Non-Goals

- Achieving 100% feature parity across all backends
- Replacing framework-specific optimizations for performance-critical code
- Runtime backend switching

## Design Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Game Code                           │
│         (Backend-agnostic, attribute-decorated)             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   UnifyECS API Layer                        │
│   [EcsComponent], [EcsSystem], [Query], [EcsRequires]       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              Source Generator (Compile-time)                │
│   - Parses attributes and requirements                      │
│   - Checks backend capabilities                             │
│   - Applies missing feature policies                        │
│   - Emits backend-specific implementations                  │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│  Arch Backend   │ │ Entitas Backend │ │  DOTS Backend   │
│   Generated     │ │    Generated    │ │   Generated     │
└─────────────────┘ └─────────────────┘ └─────────────────┘
              │               │               │
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│    Arch ECS     │ │    Entitas      │ │   Unity DOTS    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Core Principles

### 1. Capability-Based Architecture

Instead of forcing a lowest-common-denominator API, systems declare what features they need:

```csharp
[EcsSystem]
[EcsRequires(EcsFeature.Reactive)]
public partial class DamageSystem
{
    [OnChanged(typeof(Health))]
    public void OnHealthChanged(Entity e, ref Health health) { ... }
}
```

The generator then:
- Uses native implementation if backend supports the feature
- Emulates the feature if possible
- Applies configured policy (Error/Warn/NoOp) if unsupported

### 2. Policy-Driven Behavior

Missing feature behavior is configurable via MSBuild:

```xml
<PropertyGroup>
  <UnifyEcsBackends>Arch;Dots</UnifyEcsBackends>
  <UnifyEcsMissingFeaturePolicy>Warn</UnifyEcsMissingFeaturePolicy>
</PropertyGroup>
```

### 3. Explicit Over Implicit

- All capability requirements are declared via attributes
- No silent failures - missing features are caught at compile-time
- Generated code is inspectable and debuggable

### 4. Partial Classes for Extension

User-written code uses `partial class` pattern, allowing generator to fill in backend-specific boilerplate while keeping game logic clean.

## Project Structure

```
UnifyECS/
├── src/
│   ├── UnifyECS.Abstractions/     # Core attributes and interfaces
│   ├── UnifyECS.Generators/       # Source generators
│   ├── UnifyECS.Generators.Arch/  # Arch-specific generator
│   ├── UnifyECS.Generators.Entitas/
│   ├── UnifyECS.Generators.Dots/
│   └── UnifyECS.Analyzers/        # Roslyn analyzers for warnings
├── tests/
│   ├── UnifyECS.Tests.Arch/
│   ├── UnifyECS.Tests.Entitas/
│   └── UnifyECS.Tests.Integration/
└── samples/
    └── UnifyECS.Sample.Movement/  # Basic movement system sample
```

## Success Metrics

1. **Migration Time**: Switching backends should take days, not months
2. **Code Coverage**: 80%+ of typical ECS code should be portable
3. **Performance Overhead**: Generated code should be within 5% of hand-written backend code
4. **Developer Experience**: Clear compile-time errors for unsupported features

## Related RFCs

- RFC-0002: Feature Capability System
- RFC-0003: Attribute API Design
- RFC-0004: Source Generator Pipeline
- RFC-0005: Backend Adapters
- RFC-0006: Missing Feature Policies
- RFC-0007: Multi-Backend Orchestration
- RFC-0008: World Lifecycle & System Execution
- RFC-0009: Component Registry & Type Mapping
- RFC-0010: DOTS Backend Constraints
- RFC-0011: Unified API Surface & Specification
- RFC-0012: Structural Changes & Mutations
- RFC-0013: Diagnostics & Debugging

## Open Questions

1. Should we support hot-reloading of systems during development?
2. How do we handle serialization/deserialization across backends?
3. What's the story for editor tooling integration?

## References

- [Arch ECS](https://github.com/genaray/Arch)
- [Entitas](https://github.com/sschmid/Entitas)
- [Unity DOTS](https://unity.com/dots)
