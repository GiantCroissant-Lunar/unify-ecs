# UnifyECS

A source-generation-based abstraction layer that enables writing ECS game code once and running it on multiple ECS implementations.

## Vision

Write backend-agnostic ECS code using a unified attribute API. Source generators produce optimized, framework-specific implementations at compile-time with zero runtime overhead.

```csharp
[EcsComponent]
public struct Position { public float X, Y, Z; }

[EcsComponent]
public struct Velocity { public float X, Y, Z; }

[EcsSystem(Phase = SystemPhase.Update)]
public partial class MovementSystem
{
    [Query(All = new[] { typeof(Position), typeof(Velocity) })]
    public void Process(ref Position pos, in Velocity vel, float deltaTime)
    {
        pos.X += vel.X * deltaTime;
        pos.Y += vel.Y * deltaTime;
        pos.Z += vel.Z * deltaTime;
    }
}
```

The same code compiles to optimized implementations for:
- **Arch ECS** - Archetype-based, high performance
- **Entitas** - Reactive systems, mature ecosystem
- **Unity DOTS** - Jobs + Burst, enterprise Unity
- And more...

## Key Features

- **Zero Runtime Overhead**: Source generation, not reflection
- **Explicit Capability Management**: Declare what features you need
- **Policy-Driven Compatibility**: Configure how to handle unsupported features
- **Multi-Backend Support**: Migration, benchmarking, cross-platform

## Documentation

See [docs/rfcs/](./docs/rfcs/) for detailed design documents:

| RFC | Title |
|-----|-------|
| [RFC-0001](./docs/rfcs/RFC-0001-core-architecture.md) | Core Architecture |
| [RFC-0002](./docs/rfcs/RFC-0002-feature-capability-system.md) | Feature Capability System |
| [RFC-0003](./docs/rfcs/RFC-0003-attribute-api-design.md) | Attribute API Design |
| [RFC-0004](./docs/rfcs/RFC-0004-source-generator-pipeline.md) | Source Generator Pipeline |
| [RFC-0005](./docs/rfcs/RFC-0005-backend-adapters.md) | Backend Adapters |
| [RFC-0006](./docs/rfcs/RFC-0006-missing-feature-policies.md) | Missing Feature Policies |
| [RFC-0007](./docs/rfcs/RFC-0007-multi-backend-orchestration.md) | Multi-Backend Orchestration |
| [RFC-0008](./docs/rfcs/RFC-0008-world-lifecycle-system-execution.md) | World Lifecycle & System Execution |
| [RFC-0009](./docs/rfcs/RFC-0009-component-registry-type-mapping.md) | Component Registry & Type Mapping |
| [RFC-0010](./docs/rfcs/RFC-0010-dots-backend-constraints.md) | DOTS Backend Constraints |
| [RFC-0011](./docs/rfcs/RFC-0011-unified-api-surface.md) | Unified API Surface & Specification |
| [RFC-0012](./docs/rfcs/RFC-0012-structural-changes-mutations.md) | Structural Changes & Mutations |
| [RFC-0013](./docs/rfcs/RFC-0013-diagnostics-debugging.md) | Diagnostics & Debugging |

## Implementation Plan

See [docs/IMPLEMENTATION-PLAN.md](./docs/IMPLEMENTATION-PLAN.md) for the full implementation roadmap.

### Phases

| Phase | Weeks | Goal |
|-------|-------|------|
| 0: Foundation | 1-2 | Core types, interfaces, attributes |
| 1: Arch Backend | 3-6 | End-to-end working system |
| 2: Analyzers | 7-8 | Compile-time diagnostics |
| 3: Advanced Features | 9-11 | Reactive emulation, feature policies |
| 4: Entitas Backend | 12-14 | Second backend |
| 5: Multi-Backend | 15-16 | Orchestration, benchmarks |
| 6: DOTS Backend | 17-22 | Unity DOTS support |

### RFC Implementation Order

1. RFC-0011 (API Surface) → RFC-0002 (Features) → RFC-0003 (Attributes)
2. RFC-0009 (Registry) → RFC-0008 (Lifecycle) → RFC-0004 (Generator)
3. RFC-0005/Arch → RFC-0012 (Mutations) → RFC-0013 (Diagnostics)
4. RFC-0006 (Policies) → RFC-0005/Entitas → RFC-0007 (Orchestration)
5. RFC-0010 (DOTS) → RFC-0005/DOTS

## Status

🚧 **Design Phase** - RFCs approved for prototype implementation.

## License

TBD
