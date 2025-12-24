# UnifyECS RFCs

This directory contains Request for Comments (RFCs) for the UnifyECS project.

## RFC Status

| RFC | Title | Status |
|-----|-------|--------|
| [RFC-0001](./RFC-0001-core-architecture.md) | Core Architecture | Draft |
| [RFC-0002](./RFC-0002-feature-capability-system.md) | Feature Capability System | Draft |
| [RFC-0003](./RFC-0003-attribute-api-design.md) | Attribute API Design | Draft |
| [RFC-0004](./RFC-0004-source-generator-pipeline.md) | Source Generator Pipeline | Draft |
| [RFC-0005](./RFC-0005-backend-adapters.md) | Backend Adapters | Draft |
| [RFC-0006](./RFC-0006-missing-feature-policies.md) | Missing Feature Policies | Draft |
| [RFC-0007](./RFC-0007-multi-backend-orchestration.md) | Multi-Backend Orchestration | Draft |
| [RFC-0008](./RFC-0008-world-lifecycle-system-execution.md) | World Lifecycle & System Execution | Draft |
| [RFC-0009](./RFC-0009-component-registry-type-mapping.md) | Component Registry & Type Mapping | Draft |
| [RFC-0010](./RFC-0010-dots-backend-constraints.md) | DOTS Backend Constraints | Draft |
| [RFC-0011](./RFC-0011-unified-api-surface.md) | Unified API Surface & Specification | Draft |
| [RFC-0012](./RFC-0012-structural-changes-mutations.md) | Structural Changes & Mutations | Draft |
| [RFC-0013](./RFC-0013-diagnostics-debugging.md) | Diagnostics & Debugging | Draft |

## Reading Order

For newcomers, we recommend reading in this order:

1. **RFC-0001: Core Architecture** - Overall vision and goals
2. **RFC-0011: Unified API Surface** - What code developers write (canonical reference)
3. **RFC-0002: Feature Capability System** - How features are categorized
4. **RFC-0003: Attribute API Design** - The developer-facing API details
5. **RFC-0006: Missing Feature Policies** - How unsupported features are handled
6. **RFC-0008: World Lifecycle & System Execution** - World creation, system registration, DI
7. **RFC-0009: Component Registry & Type Mapping** - Type IDs, serialization, cross-backend mapping
8. **RFC-0012: Structural Changes & Mutations** - Thread safety, ECB, mutation rules
9. **RFC-0004: Source Generator Pipeline** - How code is generated
10. **RFC-0005: Backend Adapters** - Implementation details per backend
11. **RFC-0010: DOTS Backend Constraints** - Unity DOTS-specific requirements
12. **RFC-0007: Multi-Backend Orchestration** - Running multiple backends
13. **RFC-0013: Diagnostics & Debugging** - Logging, validation, tooling

## Implementation

All RFCs (0001-0013) are now **approved for prototype implementation**.

See [../IMPLEMENTATION-PLAN.md](../IMPLEMENTATION-PLAN.md) for:
- Project structure and package layout
- 6-phase implementation roadmap (22 weeks)
- RFC implementation order and dependencies
- Milestone definitions and success criteria

## RFC Process

### Statuses

- **Draft**: Initial proposal, open for major changes
- **Review**: Ready for community review
- **Accepted**: Approved for implementation
- **Implemented**: Fully implemented
- **Superseded**: Replaced by another RFC

### Creating a New RFC

1. Copy `_template.md` (TBD)
2. Assign next available RFC number
3. Fill in all sections
4. Submit PR for review

## Key Concepts

### Feature Tiers

| Tier | Features | Support |
|------|----------|---------|
| 1 - Universal | Entity, Component, Query, System | All backends |
| 2 - Common | Events, Filtering, Tags, Groups | Most backends |
| 3 - Advanced | Reactive, Relationships, Jobs | Some backends |
| 4 - Specialized | Burst, Shared Components, Chunks | Few backends |

### Missing Feature Policies

| Policy | Behavior |
|--------|----------|
| Error | Compile-time error (safest) |
| Warn | Warning + runtime stub |
| NoOp | Silent stub (dangerous) |
| Emulate | Generate helper code |

### Supported Backends

| Phase | Backends |
|-------|----------|
| 1 | Arch ECS, Entitas |
| 2 | Unity DOTS, DefaultEcs, Friflo |
| 3 | LeoECS, Svelto.ECS, Custom |

## Review Issues Addressed

### First Review (RFC-0001 to RFC-0007)

| Issue | Description | Addressed In |
|-------|-------------|--------------|
| #1 | Missing world initialization & lifecycle API | RFC-0008 |
| #2 | DOTS backend oversimplified | RFC-0010 |
| #6 | No component type registration spec | RFC-0009 |
| #7 | Snapshot system cannot work as written | RFC-0009 (logical snapshots) |
| #8 | No lifecycle for system injection | RFC-0008 |

### Second Review (RFC-0001 to RFC-0010)

| Issue | Description | Addressed In |
|-------|-------------|--------------|
| Missing unified API surface | RFC-0001 didn't specify exact user code | RFC-0011 |
| Attribute DSL grammar incomplete | No formal grammar for Query DSL | RFC-0011 |
| DOTS structural change safety | Missing ECB/thread safety rules | RFC-0012 |
| No debugging/logging spec | Missing diagnostics tooling | RFC-0013 |
| IWorld vs backend-native inconsistency | Mixed approaches in RFCs | RFC-0011 (design decision) |

### Remaining Issues (Minor)

| Issue | Description | Status |
|-------|-------------|--------|
| #3 | Entitas compatibility with native generators | Noted in RFC-0005 |
| #4 | Generator partial class inheritance | Wrapper pattern in RFC-0010/0011 |
| #5 | Reactive emulation performance | Warning system in RFC-0013 |
| Multi-assembly ID allocation | Formal protocol | Noted in RFC-0009 |
