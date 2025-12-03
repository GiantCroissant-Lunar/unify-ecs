# UnifyECS Implementation Plan

## Overview

This document outlines the implementation phases, milestones, and dependencies for building UnifyECS from the RFC specifications (RFC-0001 to RFC-0013).

## Project Structure

```
unify-ecs/
├── docs/
│   └── rfcs/                    # RFC documents
├── src/
│   ├── UnifyEcs.Core/           # Core types, interfaces, abstractions
│   ├── UnifyEcs.Attributes/     # [EcsComponent], [EcsSystem], [Query], etc.
│   ├── UnifyEcs.Generators/     # Roslyn source generators
│   ├── UnifyEcs.Analyzers/      # Roslyn analyzers (UECS diagnostics)
│   ├── UnifyEcs.Runtime/        # Runtime helpers (validation, logging, profiling)
│   ├── UnifyEcs.Backends.Arch/  # Arch ECS backend emitter
│   ├── UnifyEcs.Backends.Entitas/  # Entitas backend emitter
│   └── UnifyEcs.Backends.Dots/  # Unity DOTS backend emitter
├── tests/
│   ├── UnifyEcs.Core.Tests/
│   ├── UnifyEcs.Generators.Tests/
│   └── UnifyEcs.Integration.Tests/
├── samples/
│   ├── Sample.Arch/             # Sample game with Arch backend
│   ├── Sample.Entitas/          # Sample game with Entitas backend
│   └── Sample.Dots/             # Unity project with DOTS backend
└── benchmarks/
    └── UnifyEcs.Benchmarks/     # Performance benchmarks
```

## Current Repository Layout (v1)

The current .NET implementation in this repository lives under `dotnet/` and corresponds to a subset of the original plan:

```
unify-ecs/
├── docs/
│   └── rfcs/
├── dotnet/
│   ├── src/
│   │   ├── UnifyEcs.Core/
│   │   ├── UnifyEcs.Attributes/
│   │   ├── UnifyEcs.Generators/
│   │   ├── UnifyEcs.Analyzers/
│   │   ├── UnifyEcs.Runtime.Arch/
│   │   └── UnifyEcs.Sample.ArchGame/
│   └── tests/
│       ├── UnifyEcs.Core.Tests/
│       ├── UnifyEcs.Attributes.Tests/
│       ├── UnifyEcs.Runtime.Arch.Tests/
│       └── UnifyEcs.Analyzers.Tests/
```

---

## Implementation Phases

### Phase 0: Foundation (Weeks 1-2)

**Goal**: Set up project infrastructure and core types.

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Create solution structure | - | P0 | 2h |
| Set up CI/CD (GitHub Actions) | - | P0 | 2h |
| Implement `Entity` struct | RFC-0011 | P0 | 2h |
| Implement core enums (`EcsBackend`, `EcsFeature`, `SystemPhase`) | RFC-0002 | P0 | 2h |
| Implement attribute types | RFC-0003, RFC-0011 | P0 | 4h |
| Implement `IWorld` interface | RFC-0008, RFC-0011 | P0 | 2h |
| Implement `ISystemRunner` interface | RFC-0008 | P0 | 2h |
| Implement `ICommandBuffer` interface | RFC-0012 | P0 | 2h |
| Implement `ComponentTypeId` and registry stubs | RFC-0009 | P0 | 4h |
| Unit tests for core types | - | P0 | 4h |

**Deliverables**:
- `UnifyEcs.Core` NuGet package (v0.1.0-alpha)
- `UnifyEcs.Attributes` NuGet package (v0.1.0-alpha)

**Dependencies**: None

---

### Phase 1: Arch Backend + Basic Generator (Weeks 3-6)

**Goal**: End-to-end working system with Arch ECS.

#### 1A: Roslyn Generator Infrastructure (Week 3)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Set up incremental source generator project | RFC-0004 | P0 | 4h |
| Implement syntax discovery (`[EcsComponent]`, `[EcsSystem]`) | RFC-0004 | P0 | 8h |
| Implement `ComponentModel` parser | RFC-0004 | P0 | 4h |
| Implement `SystemModel` parser | RFC-0004 | P0 | 8h |
| Implement `QueryModel` parser (All/Any/None/Cached) | RFC-0004, RFC-0011 | P0 | 8h |
| Unit tests for parsers | - | P0 | 8h |

#### 1B: Arch Backend Emitter (Week 4)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `ArchBackendEmitter` | RFC-0005 | P0 | 4h |
| Emit component registration code | RFC-0005, RFC-0009 | P0 | 4h |
| Emit system wrapper code | RFC-0005 | P0 | 8h |
| Emit query construction code | RFC-0005 | P0 | 8h |
| Emit injection code (`[Inject]`, `[InjectSystem]`) | RFC-0008, RFC-0011 | P0 | 4h |
| Emit command buffer integration | RFC-0012 | P1 | 4h |

#### 1C: Arch Runtime Integration (Week 5)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `ArchWorld : IWorld` | RFC-0008 | P0 | 8h |
| Implement `ArchSystemRunner : ISystemRunner` | RFC-0008 | P0 | 8h |
| Implement `ArchCommandBuffer : ICommandBuffer` | RFC-0012 | P0 | 4h |
| Implement `WorldFactory` with Arch support | RFC-0008 | P0 | 2h |
| Integration tests (create world, spawn entities, run systems) | - | P0 | 8h |

#### 1D: First Sample (Week 6)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Create `Sample.Arch` console app | - | P0 | 4h |
| Implement simple game loop (Position, Velocity, Movement) | RFC-0011 | P0 | 4h |
| Document getting started guide | - | P1 | 4h |
| Performance baseline benchmarks | - | P1 | 4h |

**Deliverables**:
- `UnifyEcs.Generators` NuGet package (v0.1.0-alpha)
- `UnifyEcs.Backends.Arch` NuGet package (v0.1.0-alpha)
- Working sample application

**Dependencies**: Phase 0

---

### Phase 2: Analyzers + Diagnostics (Weeks 7-8)

**Goal**: Compile-time and runtime error handling.

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `UnifyEcsAnalyzer` base | RFC-0013 | P0 | 4h |
| Implement UECS001-010 (component/system validation) | RFC-0013 | P0 | 12h |
| Implement UECS011-015 (structural change validation) | RFC-0012, RFC-0013 | P0 | 8h |
| Implement UECS100+ warnings | RFC-0013 | P1 | 8h |
| Implement `UnifyEcsLogger` | RFC-0013 | P0 | 4h |
| Implement runtime validation helpers | RFC-0013 | P0 | 4h |
| Implement `EntityInspector` | RFC-0013 | P1 | 4h |
| Implement `SystemProfiler` | RFC-0013 | P1 | 4h |
| Unit tests for analyzers | - | P0 | 8h |

**Deliverables**:
- `UnifyEcs.Analyzers` NuGet package (v0.1.0-alpha)
- `UnifyEcs.Runtime` NuGet package (v0.1.0-alpha)

**Dependencies**: Phase 1

---

### Phase 3: Feature Policies + Advanced Features (Weeks 9-11)

**Goal**: Missing feature handling and reactive support.

#### 3A: Missing Feature Policies (Week 9)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `MissingFeaturePolicy` configuration | RFC-0006 | P0 | 4h |
| Implement Error policy (compile-time errors) | RFC-0006 | P0 | 4h |
| Implement Warn policy (runtime warnings) | RFC-0006 | P0 | 4h |
| Implement NoOp policy (stub generation) | RFC-0006 | P0 | 4h |
| Implement Emulate policy (helper code gen) | RFC-0006 | P1 | 8h |
| MSBuild property integration | RFC-0006 | P1 | 4h |

#### 3B: Reactive System Emulation (Week 10)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement change tracking for Arch | RFC-0006 | P1 | 8h |
| Emit `[OnAdded]` emulation code | RFC-0006, RFC-0011 | P1 | 8h |
| Emit `[OnRemoved]` emulation code | RFC-0006, RFC-0011 | P1 | 4h |
| Emit `[OnChanged]` emulation code | RFC-0006, RFC-0011 | P1 | 8h |
| Integration tests for reactive | - | P0 | 8h |

#### 3C: Component Registry (Week 11)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement compile-time ID assignment | RFC-0009 | P0 | 8h |
| Implement structure hash generation | RFC-0009 | P0 | 4h |
| Implement `ComponentTypeRegistry` | RFC-0009 | P0 | 4h |
| Cross-assembly component discovery | RFC-0009 | P1 | 8h |
| Serialization helpers | RFC-0009 | P2 | 8h |

**Deliverables**:
- Feature policy system working
- Reactive emulation for Arch

**Dependencies**: Phase 2

---

### Phase 4: Entitas Backend (Weeks 12-14)

**Goal**: Second backend with native reactive support.

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `EntitasBackendEmitter` | RFC-0005 | P0 | 4h |
| Emit component code (IComponent interface) | RFC-0005 | P0 | 8h |
| Emit system code (IExecuteSystem, etc.) | RFC-0005 | P0 | 12h |
| Emit reactive system code (IReactiveSystem) | RFC-0005 | P0 | 8h |
| Implement `EntitasWorld : IWorld` | RFC-0008 | P0 | 8h |
| Implement `EntitasSystemRunner : ISystemRunner` | RFC-0008 | P0 | 8h |
| Implement `EntitasCommandBuffer : ICommandBuffer` | RFC-0012 | P0 | 4h |
| Entitas-specific analyzers | RFC-0013 | P1 | 4h |
| Create `Sample.Entitas` | - | P0 | 8h |
| Cross-backend test suite | - | P0 | 8h |

**Deliverables**:
- `UnifyEcs.Backends.Entitas` NuGet package (v0.1.0-alpha)
- Working Entitas sample

**Dependencies**: Phase 3

---

### Phase 5: Multi-Backend Orchestration (Weeks 15-16)

**Goal**: Run multiple backends simultaneously.

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `IBackendRunner` interface | RFC-0007 | P1 | 4h |
| Implement `BackendOrchestrator` | RFC-0007 | P1 | 8h |
| Implement `LogicalSnapshot` capture | RFC-0007, RFC-0009 | P1 | 8h |
| Implement `EntityIdMapper` | RFC-0009 | P1 | 4h |
| Implement snapshot validation | RFC-0007 | P1 | 8h |
| A/B execution mode | RFC-0007 | P2 | 8h |
| Benchmark integration | RFC-0007 | P2 | 8h |
| Multi-backend integration tests | - | P1 | 8h |

**Deliverables**:
- Multi-backend orchestration working
- Benchmarking infrastructure

**Dependencies**: Phase 4

---

### Phase 6: Unity DOTS Backend (Weeks 17-22)

**Goal**: Production-quality DOTS support.

#### 6A: DOTS Emitter (Weeks 17-18)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `DotsBackendEmitter` | RFC-0005, RFC-0010 | P0 | 4h |
| Emit unmanaged component code | RFC-0010 | P0 | 8h |
| Emit `ISystem` code (Burst-compatible) | RFC-0010 | P0 | 16h |
| Emit `SystemBase` code (fallback) | RFC-0010 | P0 | 8h |
| Emit ECB integration | RFC-0010, RFC-0012 | P0 | 12h |
| Emit system group registration | RFC-0010 | P0 | 8h |

#### 6B: DOTS Runtime (Weeks 19-20)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `DotsWorld : IWorld` | RFC-0008, RFC-0010 | P0 | 12h |
| Implement `DotsSystemRunner : ISystemRunner` | RFC-0008, RFC-0010 | P0 | 12h |
| Implement ECB injection | RFC-0010, RFC-0012 | P0 | 8h |
| Player loop integration | RFC-0010 | P0 | 8h |
| Managed component support | RFC-0010 | P1 | 8h |

#### 6C: DOTS Reactive Emulation (Week 21)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Implement `ICleanupComponentData` tracking | RFC-0010 | P1 | 12h |
| Emit reactive emulation systems | RFC-0010 | P1 | 12h |
| Integration tests | - | P0 | 8h |

#### 6D: Unity Sample (Week 22)

| Task | RFCs | Priority | Est. |
|------|------|----------|------|
| Create Unity project with DOTS | - | P0 | 4h |
| Create `Sample.Dots` scene | - | P0 | 8h |
| Unity Editor integration (Entity debugger) | RFC-0013 | P2 | 16h |
| Performance benchmarks vs native DOTS | - | P1 | 8h |

**Deliverables**:
- `UnifyEcs.Backends.Dots` Unity package
- Working Unity sample
- Editor tooling (basic)

**Dependencies**: Phase 5

---

## Implementation Order by RFC

| Order | RFC | Phase | Rationale |
|-------|-----|-------|-----------|
| 1 | RFC-0011 | 0 | Core types, Entity, attribute definitions |
| 2 | RFC-0002 | 0 | Feature enums needed by everything |
| 3 | RFC-0003 | 0 | Attribute types for generator discovery |
| 4 | RFC-0009 | 0-1 | ComponentTypeId needed for registry |
| 5 | RFC-0008 | 1 | IWorld/ISystemRunner needed for backends |
| 6 | RFC-0004 | 1 | Generator pipeline architecture |
| 7 | RFC-0005 (Arch) | 1 | First backend emitter |
| 8 | RFC-0012 | 1-2 | Structural changes, command buffers |
| 9 | RFC-0013 | 2 | Analyzers and diagnostics |
| 10 | RFC-0006 | 3 | Missing feature policies |
| 11 | RFC-0005 (Entitas) | 4 | Second backend |
| 12 | RFC-0007 | 5 | Multi-backend orchestration |
| 13 | RFC-0010 | 6 | DOTS-specific constraints |
| 14 | RFC-0005 (DOTS) | 6 | DOTS backend emitter |
| 15 | RFC-0001 | - | Vision doc, no implementation |

---

## Milestone Summary

| Milestone | Target | Key Deliverable |
|-----------|--------|-----------------|
| **M1: Core Alpha** | Week 2 | Core types, attributes published |
| **M2: Arch Working** | Week 6 | End-to-end Arch sample |
| **M3: Analyzers** | Week 8 | Compile-time diagnostics |
| **M4: Feature Complete** | Week 11 | Reactive, policies, registry |
| **M5: Entitas Working** | Week 14 | Second backend verified |
| **M6: Multi-Backend** | Week 16 | Orchestration, benchmarks |
| **M7: DOTS Alpha** | Week 20 | Basic DOTS support |
| **M8: DOTS Complete** | Week 22 | Full DOTS + Unity Editor |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| DOTS complexity | Start simple (ISystem only), add SystemBase later |
| Generator performance | Use incremental generators, cache models |
| Cross-backend inconsistencies | Extensive integration test suite |
| Entitas API changes | Pin to stable Entitas version |
| Unity version compatibility | Target LTS versions (2022.3+) |

---

## Success Criteria

### Alpha (M2)
- [ ] Can define components and systems with attributes
- [ ] Generator produces working Arch code
- [ ] Sample runs without errors
- [ ] Basic unit test coverage (>60%)

### Beta (M5)
- [ ] Two backends (Arch, Entitas) working
- [ ] Feature policies implemented
- [ ] Analyzers catch common errors
- [ ] Documentation complete

### Release Candidate (M8)
- [ ] Three backends (Arch, Entitas, DOTS) working
- [ ] Multi-backend orchestration working
- [ ] Performance competitive with native
- [ ] Unity Editor integration
- [ ] Test coverage >80%

---

## Next Steps

1. **Create GitHub repository** with solution structure
2. **Implement Phase 0** (core types and attributes)
3. **Set up CI/CD** with automated tests
4. **Begin Phase 1A** (generator infrastructure)

Ready to start? Let me know which phase to begin implementing.
