# RFC-0014: Multi-World Support and Rich Query Model

## 1. Summary

This RFC proposes two related enhancements to **UnifyECS**:

- **Multi-world orchestration**: an optional layer on top of the existing `IWorld`/`ISystemRunner` model that supports multiple ECS worlds in a single process (e.g., `Game`, `UI`, `Particles`, `Persistent`).
- **Richer query model per system**: clarifying and extending the attribute+generator model so that a single system can declare **multiple queries** (e.g., cache positions, cache blockers, update agents) in a structured way.

These changes are motivated by downstream projects (e.g., `mung-bean`, `sim-world`) that:

- Use **multiple ECS worlds** as a core architectural primitive.
- Have systems that naturally decompose into several query passes over the same world.

The intent is to evolve UnifyECS, not to fundamentally change its design:

- Keep the current **single-world core** (`IWorld`, `ISystemRunner`, `ArchWorld`, `ArchSystemRunner`).
- Add a **multi-world orchestration layer** in a separate namespace/assembly.
- Ensure the **source generator** cleanly supports multiple `[Query]` methods per system class.

---

## 2. Motivation

### 2.1 Multi-world is a first-class concept in some domains

Several target domains (e.g., roguelikes, simulation-heavy games, and sandbox worlds) naturally organize simulation data into multiple worlds:

- `Game` – main gameplay entities (actors, tiles, projectiles, etc.).
- `UI` – UI-specific entities (HUD elements, menus, tooltips).
- `Particles` – transient visual effects with different lifetime/rules.
- `Persistent` – long-lived entities that span sessions or scenes.

Projects like `mung-bean` already have an explicit `WorldManager` + `MultiWorldSystemRunner` over `Arch.Core.World`. For these users, a **single `IWorld` per `ISystemRunner`** is not enough – they need a convenient, reusable way to orchestrate multiple worlds while still taking advantage of UnifyECS abstractions.

### 2.2 Systems often need multiple queries

Non-trivial systems (e.g., perception, AI, combat) rarely consist of a single scan over the world. Typical patterns include:

- **Pre-pass queries** to cache data (e.g., all positions, blockers, sound sources).
- **Main queries** to update each agent or entity based on that cached data.
- **Post-pass queries** for clean-up or secondary effects.

With the current UnifyECS attribute model, it is natural to express these as **multiple `[Query]` methods** within one system class. The source generator should:

- Discover all `[Query]` methods in a system.
- Emit one `world.Query(...)` pass for each, in a deterministic order.
- Allow systems to express complex logic while minimizing manual boilerplate.

---

## 3. Goals & Non-Goals

### 3.1 Goals

- **G1: Multi-world layer**
  - Introduce a reusable multi-world orchestration abstraction on top of `IWorld` and `ISystemRunner`.
  - Provide at least one backend implementation (Arch) that downstream projects can use.

- **G2: Multi-query systems**
  - Clearly support multiple `[Query]` methods per system in the attribute+generator model.
  - Ensure the generator emits predictable, debuggable code for these methods.

- **G3: Backwards compatibility**
  - Do not break existing single-world use cases.
  - Make the multi-world layer **opt-in** – users who only need one world can ignore it.

### 3.2 Non-Goals

- NG1: We do **not** try to design a universal multi-world orchestration model that covers all possible use cases. We provide a reasonable default that works well for common patterns (e.g., game + UI + particles).
- NG2: We do **not** attempt to redesign the entire attribute model in this RFC. We only clarify that multiple `[Query]` methods are supported and outline how they are handled.
- NG3: We do **not** introduce new backends in this RFC; changes are scoped to the Arch backend and backend-agnostic interfaces.

---

## 4. Proposed Design: Multi-World Layer

### 4.1 New abstractions

We introduce a new namespace (and likely a separate assembly) `UnifyECS.MultiWorld` with the following interfaces:

```csharp
namespace UnifyECS.MultiWorld;

/// <summary>
/// Represents a set of named worlds managed together.
/// Implementations may be backend-specific but expose IWorld handles.
/// </summary>
public interface IWorldSet
{
    IWorld GetOrCreate(string id);
    IWorld? Get(string id);
    IEnumerable<string> Ids { get; }
}

/// <summary>
/// A system that operates across multiple worlds.
/// Typically used for cross-world coordination or diagnostics.
/// </summary>
public interface IMultiWorldSystem
{
    void Update(IWorldSet worlds, float deltaTime);
}

/// <summary>
/// Orchestrates systems for multiple worlds.
/// Wraps one or more single-world runners and provides a multi-world view.
/// </summary>
public interface IMultiWorldRunner : IDisposable
{
    IWorldSet Worlds { get; }

    /// <summary>
    /// Register a system that operates on a specific world.
    /// </summary>
    void Register(string worldId, IUnifySystem system);

    /// <summary>
    /// Register a system that can see all worlds.
    /// </summary>
    void Register(IMultiWorldSystem system);

    /// <summary>
    /// Perform initialization (e.g., dependency injection) for all worlds.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Update all worlds and systems for a single frame.
    /// </summary>
    void Update(float deltaTime);
}
```

These interfaces are **backend-agnostic** and only reference `IWorld` and `IUnifySystem`.

### 4.2 Arch backend implementation

In `UnifyEcs.Runtime.Arch` we provide Arch-specific implementations:

```csharp
namespace UnifyECS;

/// <summary>
/// Arch-backed multi-world set. Each world is an ArchWorld, but exposed as IWorld.
/// </summary>
public sealed class ArchWorldSet : IWorldSet
{
    private readonly Dictionary<string, ArchWorld> _worlds = new();

    public IWorld GetOrCreate(string id)
    {
        if (_worlds.TryGetValue(id, out var existing))
            return existing;

        var world = (ArchWorld)WorldFactory.Create(EcsBackend.Arch, new WorldConfig
        {
            Name = id
        });

        _worlds[id] = world;
        return world;
    }

    public IWorld? Get(string id) => _worlds.TryGetValue(id, out var world) ? world : null;

    public IEnumerable<string> Ids => _worlds.Keys;

    // Optional: expose Arch.Core.World for advanced scenarios via a helper.
}

/// <summary>
/// Arch-specific implementation of IMultiWorldRunner.
/// Binds one ArchSystemRunner per world id and coordinates updates.
/// </summary>
public sealed class ArchMultiWorldRunner : IMultiWorldRunner
{
    private readonly ArchWorldSet _worlds = new();
    private readonly Dictionary<string, ArchSystemRunner> _runners = new();
    private readonly List<IMultiWorldSystem> _multiWorldSystems = new();
    private bool _initialized;
    private bool _disposed;

    public IWorldSet Worlds => _worlds;

    public void Register(string worldId, IUnifySystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        ThrowIfDisposed();

        var world = (ArchWorld)_worlds.GetOrCreate(worldId);

        if (!_runners.TryGetValue(worldId, out var runner))
        {
            runner = new ArchSystemRunner(world);
            _runners[worldId] = runner;
        }

        runner.Register(system); // expects an IArchSystem implementation
    }

    public void Register(IMultiWorldSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        ThrowIfDisposed();
        _multiWorldSystems.Add(system);
    }

    public void Initialize()
    {
        ThrowIfDisposed();
        if (_initialized) throw new InvalidOperationException("Already initialized.");
        _initialized = true;

        foreach (var runner in _runners.Values)
        {
            runner.Initialize();
        }
    }

    public void Update(float deltaTime)
    {
        ThrowIfDisposed();
        if (!_initialized) throw new InvalidOperationException("Call Initialize() before Update().");

        // Per-world systems
        foreach (var runner in _runners.Values)
        {
            runner.Update(deltaTime);
        }

        // Cross-world systems
        foreach (var system in _multiWorldSystems)
        {
            system.Update(_worlds, deltaTime);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var runner in _runners.Values)
        {
            runner.Dispose();
        }

        _runners.Clear();
        _multiWorldSystems.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ArchMultiWorldRunner));
    }
}
```

### 4.3 Adapting existing single-world systems

Downstream projects that already have Arch-based systems (e.g., `ISystem.Update(World, dt)`) can adapt them to `IArchSystem` via a thin wrapper:

```csharp
public sealed class ArchSystemAdapter : IArchSystem
{
    private readonly Action<Arch.Core.World, float> _update;

    public ArchSystemAdapter(Action<Arch.Core.World, float> update)
    {
        _update = update ?? throw new ArgumentNullException(nameof(update));
    }

    public void Execute(Arch.Core.World world, float deltaTime)
        => _update(world, deltaTime);
}
```

Or wrapping existing ECS abstractions in a project-specific adapter (`MungBeanArchSystemAdapter`, `SimWorldArchSystemAdapter`, etc.).

This allows multi-world adoption **without** having to rewrite all systems to attribute-based UnifyECS systems immediately.

---

## 5. Proposed Design: Multi-Query Systems

### 5.1 Current attribute model

Previous RFCs (0003, 0004, 0011, 0012) define the attribute-based system model roughly as:

- `[EcsSystem]` marking a partial class as a system.
- `[Query]` marking methods that represent query-driven passes over a world.
- `[Inject]`, `[StructuralChanges]`, etc., providing additional metadata.

Today, a typical system is shown with a **single `[Query]` method** in examples. However, in more complex scenarios, a system will often contain multiple passes.

### 5.2 Clarifying multi-query support

We standardize the following:

- A single system class **may contain multiple `[Query]` methods**.
- The generator will:
  - Discover all `[Query]`-marked methods in the system class.
  - Generate one `world.Query(...)` (or equivalent backend-specific iteration) per method.
  - Execute them in a deterministic order (e.g., declaration order or a defined ordering rule documented in the RFCs).

Illustrative example:

```csharp
[EcsSystem(Phase = SystemPhase.Update)]
public partial class VisualPerceptionSystem
{
    private readonly List<(Entity, Position)> _positionBuffer = new();
    private readonly HashSet<(int X, int Y)> _blockedTiles = new();

    [Query]
    private void CachePositions(Entity entity, ref Position pos)
    {
        _positionBuffer.Add((entity, pos));
    }

    [Query]
    private void CacheBlocking(ref Position pos, ref Blocking blocking)
    {
        if (!blocking.BlocksSight)
            return;
        _blockedTiles.Add(((int)MathF.Round(pos.X), (int)MathF.Round(pos.Y)));
    }

    [Query]
    private void UpdatePerception(Entity entity, ref PerceptionComponent perception, ref Position position)
    {
        // Uses _positionBuffer and _blockedTiles computed in previous queries
        // to update perception data.
    }
}
```

The generated backend-specific code for Arch would be conceptually similar to:

```csharp
public void Execute(World world, float deltaTime)
{
    // Clear buffers
    _positionBuffer.Clear();
    _blockedTiles.Clear();

    // Pass 1: CachePositions
    world.Query(_cachePositionsQueryDescription,
        (Entity e, ref Position pos) => CachePositions(e, ref pos));

    // Pass 2: CacheBlocking
    world.Query(_cacheBlockingQueryDescription,
        (ref Position pos, ref Blocking blocking) => CacheBlocking(ref pos, ref blocking));

    // Pass 3: UpdatePerception
    world.Query(_updatePerceptionQueryDescription,
        (Entity e, ref PerceptionComponent p, ref Position pos) =>
            UpdatePerception(e, ref p, ref pos));
}
```

The exact emission details (query descriptions, filtering, etc.) are governed by RFC-0011 and RFC-0012. This RFC simply **codifies that multiple `[Query]` methods are supported and expected**.

### 5.3 Future extensions (out of scope for this RFC)

We may later introduce additional attributes to express

- **buffering semantics** (e.g., `[Query(Buffer = nameof(_positionBuffer))]`)
- **structural change hints** at the method level
- **ordering/grouping** metadata between queries within a system

These are left for future RFCs.

---

## 6. Impact on Existing Code

### 6.1 Backwards compatibility

- Single-world users remain unaffected; they can ignore `UnifyECS.MultiWorld`.
- Existing systems that only have one `[Query]` method continue to work as before.
- Projects that already use multiple `[Query]` methods may see more predictable and documented behavior once the generator officially supports them as per this RFC.

### 6.2 Downstream projects (e.g., mung-bean, sim-world)

- Can adopt `ArchMultiWorldRunner` and `ArchWorldSet` as a replacement (or complement) to their own multi-world orchestration, while still using Arch as the backend.
- Can gradually transition systems to attribute-based UnifyECS systems, starting with the simplest cases.
- Can mix:
  - Generated `IArchSystem` implementations.
  - Adapter-based systems wrapping existing ECS contracts.

This allows large projects to adopt UnifyECS incrementally.

---

## 7. Open Questions

1. **Ordering of `[Query]` methods**
   - Should we rely on source declaration order, or introduce an explicit ordering attribute?
2. **World naming conventions**
   - Should `ArchWorldSet` provide a set of conventional IDs (e.g., `Game`, `Ui`, `Particles`), or leave all naming entirely to the user?
3. **Diagnostics for multi-world**
   - Should we add specific diagnostics when a multi-world runner is misconfigured (e.g., no systems registered for any world)?

These can be addressed iteratively once the initial implementation lands.

---

## 8. Implementation Plan

1. **Core interfaces**
   - Add `UnifyECS.MultiWorld` interfaces (`IWorldSet`, `IMultiWorldSystem`, `IMultiWorldRunner`).

2. **Arch backend**
   - Implement `ArchWorldSet` and `ArchMultiWorldRunner` in `UnifyEcs.Runtime.Arch`.

3. **Generator updates**
   - Ensure the source generator:
     - Discovers all `[Query]` methods per system.
     - Emits one query pass per method in a well-defined order.
   - Add tests and documentation covering multi-query systems.

4. **Documentation & samples**
   - Add a sample showing:
     - Creation of multiple worlds via `ArchWorldSet`.
     - Registration of systems per world with `ArchMultiWorldRunner`.
     - Usage of multiple `[Query]` methods within a single system.
