# UnifyECS

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-blue.svg)](https://dotnet.microsoft.com/download)

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

## Project Structure

```
unify-ecs/
├── dotnet/
│   ├── src/
│   │   ├── UnifyEcs.Core/         # Core types, interfaces, attributes
│   │   ├── UnifyEcs.Generators/   # Source generator pipeline
│   │   ├── UnifyEcs.Analyzers/    # Roslyn analyzers
│   │   ├── UnifyEcs.Attributes/   # Public attribute API
│   │   ├── UnifyEcs.Runtime.*/    # Backend-specific runtimes
│   │   └── UnifyEcs.Sample.*/     # Sample games
│   └── UnifyGrid.sln              # Main solution file
├── docs/
│   ├── rfcs/                      # Design documents
│   ├── specs/                     # Feature specifications
│   ├── handovers/                 # Session handovers
│   └── reviews/                   # Code reviews
└── build/                         # Build scripts
```

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

## Supported Backends

| Backend | Status | Version |
|---------|--------|---------|
| Arch | ✅ Implemented | 2.1.0 |
| Flecs | ✅ Implemented | 4.0.4 |
| Friflo | ✅ Implemented | 1.3.2 |
| Entitas | 🚧 Planned | - |
| Unity DOTS | 🚧 Planned | - |

## Building

```bash
cd dotnet
dotnet build UnifyGrid.sln
dotnet test UnifyGrid.sln
```

## Requirements

- .NET 8.0 or later (.NET 9.0 for Flecs backend)
- C# 11 or later

## Status

🚧 **Early Development** - Core architecture and initial backend implementations in progress.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Third-Party Notices

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for licenses of backend dependencies and referenced works.
