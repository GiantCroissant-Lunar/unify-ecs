# RFC-0008: World Lifecycle & System Execution

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0003, RFC-0005
- **Addresses**: Review Issue #1, #8

## Implementation Status (v1)

- **Implemented**:
  - `IWorld` in `UnifyEcs.Core` with core entity/component operations and `EntityCount`.
  - `WorldConfig`, `WorldFactory`, and `IWorldFactory` used by the Arch runtime.
  - `IUnifySystem`, `ISystemRunner`, and `SystemRunner` orchestration, plus `ArchSystemRunner` and `ArchWorld` in `UnifyEcs.Runtime.Arch`.
  - A working Arch sample (`UnifyEcs.Sample.ArchGame`) that uses `WorldFactory` + `SystemRunner`.
- **Not yet implemented / differs from this RFC**:
  - `WorldId`, `Backend`, `IsDisposed`, and `IQueryBuilder` members on `IWorld` are not part of the current v1 interface; the runtime uses a leaner `IWorld` focused on entity/component APIs.
  - Interface-based system phase contracts (`IExecuteSystem`, `IInitializeSystem`, etc.) are not publicly exposed; phase and order are driven by `[EcsSystem(Phase, Order)]` and `SystemRunner`.
  - Backend-agnostic groups (`SystemGroup` and `SystemGroupAttribute`) and multi-backend runners are still design-only.

This RFC should be read as the long-term lifecycle design; the shipped v1 focuses on the subset above for the Arch backend.

## Summary

Define the unified world lifecycle API, system registration, dependency injection, and execution model that abstracts backend-specific initialization patterns.

## Motivation

The previous RFCs define components, systems, and queries but leave critical questions unanswered:

1. How does a user create a world?

## Design Principles

1. **Explicit over implicit**: Users must explicitly create worlds and register systems
2. **Backend-agnostic entry point**: One API regardless of backend
3. **Testable**: Worlds and systems can be instantiated in isolation
4. **No global state**: Multiple worlds can coexist

---

## World Abstraction

### IWorld Interface

```csharp
/// <summary>
/// Backend-agnostic world interface.
/// Each backend provides its own implementation.
/// </summary>
public interface IWorld : IDisposable
{
    /// <summary>Unique identifier for this world</summary>
    WorldId Id { get; }
    
    /// <summary>Backend this world uses</summary>
    EcsBackend Backend { get; }
    
    /// <summary>Whether the world has been disposed</summary>
    bool IsDisposed { get; }
    
    // ═══════════════════════════════════════════════════════════════
    // Entity Operations
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Create a new entity</summary>
    Entity CreateEntity();
    
    /// <summary>Create entity with initial components</summary>
    Entity CreateEntity(params object[] components);
    
    /// <summary>Destroy an entity</summary>
    void DestroyEntity(Entity entity);
    
    /// <summary>Check if entity exists</summary>
    bool Exists(Entity entity);
    
    /// <summary>Get entity count</summary>
    int EntityCount { get; }
    
    // ═══════════════════════════════════════════════════════════════
    // Component Operations
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Add component to entity</summary>
    void Add<T>(Entity entity, T component) where T : struct;
    
    /// <summary>Remove component from entity</summary>
    void Remove<T>(Entity entity) where T : struct;
    
    /// <summary>Get component reference</summary>
    ref T Get<T>(Entity entity) where T : struct;
    
    /// <summary>Try get component</summary>
    bool TryGet<T>(Entity entity, out T component) where T : struct;
    
    /// <summary>Check if entity has component</summary>
    bool Has<T>(Entity entity) where T : struct;
    
    /// <summary>Set/replace component value</summary>
    void Set<T>(Entity entity, T component) where T : struct;
    
    // ═══════════════════════════════════════════════════════════════
    // Query Operations
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>Create a query builder</summary>
    IQueryBuilder Query();
    
    /// <summary>Iterate entities matching component signature</summary>
    void Query<T1>(QueryAction<T1> action) where T1 : struct;
    void Query<T1, T2>(QueryAction<T1, T2> action) where T1 : struct where T2 : struct;
    // ... up to reasonable arity (8-16 components)
}

public delegate void QueryAction<T1>(Entity entity, ref T1 c1);
public delegate void QueryAction<T1, T2>(Entity entity, ref T1 c1, ref T2 c2);
// ... etc

public readonly record struct WorldId(Guid Value)
{
    public static WorldId New() => new(Guid.NewGuid());
}
```

### World Factory

```csharp
/// <summary>
/// Factory for creating worlds with specific backends
/// </summary>
public static class WorldFactory
{
    private static readonly Dictionary<EcsBackend, IWorldFactory> _factories = new();
    
    /// <summary>
    /// Register a backend world factory.
    /// Called by generated code or manual setup.
    /// </summary>
    public static void Register(EcsBackend backend, IWorldFactory factory)
    {
        _factories[backend] = factory;
    }
    
    /// <summary>
    /// Create a world using the specified backend
    /// </summary>
    public static IWorld Create(EcsBackend backend, WorldConfig? config = null)
    {
        if (!_factories.TryGetValue(backend, out var factory))
            throw new InvalidOperationException(
                $"Backend '{backend}' not registered. Ensure the backend assembly is referenced.");
        
        return factory.Create(config ?? WorldConfig.Default);
    }
    
    /// <summary>
    /// Create a world using the default backend (first registered)
    /// </summary>
    public static IWorld Create(WorldConfig? config = null)
    {
        if (_factories.Count == 0)
            throw new InvalidOperationException("No backends registered.");
        
        var defaultBackend = _factories.Keys.First();
        return Create(defaultBackend, config);
    }
}

public interface IWorldFactory
{
    EcsBackend Backend { get; }
    IWorld Create(WorldConfig config);
}

public record WorldConfig
{
    /// <summary>Initial entity capacity hint</summary>
    public int InitialEntityCapacity { get; init; } = 1024;
    
    /// <summary>World name for debugging</summary>
    public string? Name { get; init; }
    
    /// <summary>Enable debug mode (slower but more validation)</summary>
    public bool DebugMode { get; init; } = false;
    
    public static WorldConfig Default { get; } = new();
}
```

---

## System Lifecycle

### System Instance Model

**Decision**: Systems are **per-world instances**, not global singletons.

Rationale:
- Enables multiple worlds with independent system state
- Simplifies testing (inject mock dependencies per instance)
- Matches DOTS model (systems belong to a World)
- Avoids hidden global state

```csharp
/// <summary>
/// Base interface for all unified systems.
/// Backend-specific interfaces extend this.
/// </summary>
public interface IUnifySystem
{
    /// <summary>System execution phase</summary>
    SystemPhase Phase { get; }
    
    /// <summary>Order within phase (lower = earlier)</summary>
    int Order { get; }
    
    /// <summary>Whether this system is enabled</summary>
    bool Enabled { get; set; }
}

/// <summary>
/// System that executes each frame
/// </summary>
public interface IExecuteSystem : IUnifySystem
{
    void Execute(float deltaTime);
}

/// <summary>
/// System that initializes once when added to world
/// </summary>
public interface IInitializeSystem : IUnifySystem
{
    void Initialize();
}

/// <summary>
/// System that cleans up when removed from world
/// </summary>
public interface ITeardownSystem : IUnifySystem
{
    void Teardown();
}

/// <summary>
/// System that runs cleanup logic after execute systems
/// </summary>
public interface ICleanupSystem : IUnifySystem
{
    void Cleanup();
}
```

### System Registration

```csharp
/// <summary>
/// Manages system registration, ordering, and execution for a world
/// </summary>
public interface ISystemRunner : IDisposable
{
    /// <summary>The world this runner manages</summary>
    IWorld World { get; }
    
    /// <summary>Register a system instance</summary>
    void Register(IUnifySystem system);
    
    /// <summary>Register multiple systems</summary>
    void Register(params IUnifySystem[] systems);
    
    /// <summary>Unregister a system</summary>
    void Unregister(IUnifySystem system);
    
    /// <summary>Get a registered system by type</summary>
    T? GetSystem<T>() where T : class, IUnifySystem;
    
    /// <summary>Execute all systems for one frame</summary>
    void Update(float deltaTime);
    
    /// <summary>Initialize all registered systems (call once after registration)</summary>
    void Initialize();
    
    /// <summary>Teardown all systems</summary>
    void Teardown();
}
```

### SystemRunner Implementation

```csharp
public class SystemRunner : ISystemRunner
{
    public IWorld World { get; }
    
    private readonly List<IUnifySystem> _systems = new();
    private readonly Dictionary<Type, IUnifySystem> _systemsByType = new();
    
    private IUnifySystem[]? _initializeSystems;
    private IUnifySystem[]? _executeSystems;
    private IUnifySystem[]? _cleanupSystems;
    private bool _initialized = false;
    private bool _dirty = true;
    
    public SystemRunner(IWorld world)
    {
        World = world;
    }
    
    public void Register(IUnifySystem system)
    {
        _systems.Add(system);
        _systemsByType[system.GetType()] = system;
        _dirty = true;
        
        // Inject dependencies
        InjectDependencies(system);
    }
    
    public void Initialize()
    {
        if (_initialized)
            throw new InvalidOperationException("SystemRunner already initialized");
        
        RebuildExecutionOrder();
        
        foreach (var system in _initializeSystems!)
        {
            if (system is IInitializeSystem init)
                init.Initialize();
        }
        
        _initialized = true;
    }
    
    public void Update(float deltaTime)
    {
        if (!_initialized)
            throw new InvalidOperationException("Call Initialize() before Update()");
        
        if (_dirty)
            RebuildExecutionOrder();
        
        // Execute phase
        foreach (var system in _executeSystems!)
        {
            if (system.Enabled && system is IExecuteSystem exec)
                exec.Execute(deltaTime);
        }
        
        // Cleanup phase
        foreach (var system in _cleanupSystems!)
        {
            if (system.Enabled && system is ICleanupSystem cleanup)
                cleanup.Cleanup();
        }
    }
    
    public T? GetSystem<T>() where T : class, IUnifySystem
    {
        return _systemsByType.TryGetValue(typeof(T), out var system) 
            ? system as T 
            : null;
    }
    
    private void RebuildExecutionOrder()
    {
        var ordered = _systems
            .OrderBy(s => s.Phase)
            .ThenBy(s => s.Order)
            .ToArray();
        
        _initializeSystems = ordered;
        _executeSystems = ordered.Where(s => s is IExecuteSystem).ToArray();
        _cleanupSystems = ordered.Where(s => s is ICleanupSystem).ToArray();
        _dirty = false;
    }
    
    private void InjectDependencies(IUnifySystem system)
    {
        // See Dependency Injection section below
    }
    
    public void Dispose()
    {
        Teardown();
        _systems.Clear();
    }
}
```

---

## Dependency Injection

### Injection Points

Injection occurs **during system registration**, before `Initialize()` is called.

```csharp
/// <summary>
/// Context provided to systems for dependency resolution
/// </summary>
public interface ISystemContext
{
    /// <summary>The world this system operates on</summary>
    IWorld World { get; }
    
    /// <summary>The system runner (for accessing other systems)</summary>
    ISystemRunner Runner { get; }
    
    /// <summary>Current frame delta time (updated each frame)</summary>
    float DeltaTime { get; }
    
    /// <summary>Total elapsed time</summary>
    float ElapsedTime { get; }
    
    /// <summary>Current frame number</summary>
    long FrameCount { get; }
}

/// <summary>
/// Injectable dependencies
/// </summary>
public enum InjectableType
{
    World,          // IWorld
    DeltaTime,      // float
    ElapsedTime,    // float
    FrameCount,     // long
    System,         // Other IUnifySystem
    Context,        // ISystemContext
}
```

### Injection Implementation

```csharp
public static class SystemInjector
{
    public static void Inject(IUnifySystem system, ISystemContext context)
    {
        var type = system.GetType();
        
        // Process [Inject] properties
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            var injectAttr = prop.GetCustomAttribute<InjectAttribute>();
            if (injectAttr == null) continue;
            
            var value = ResolveInjectable(prop.PropertyType, context);
            if (value != null && prop.CanWrite)
            {
                prop.SetValue(system, value);
            }
        }
        
        // Process [InjectSystem] properties
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            var injectAttr = prop.GetCustomAttribute<InjectSystemAttribute>();
            if (injectAttr == null) continue;
            
            var targetType = injectAttr.SystemType ?? prop.PropertyType;
            
            // Note: System may not be registered yet - use lazy resolution
            var lazySystem = new LazySystemReference(context.Runner, targetType);
            // Store for later resolution, or use proxy pattern
        }
    }
    
    private static object? ResolveInjectable(Type type, ISystemContext context)
    {
        if (type == typeof(IWorld))
            return context.World;
        if (type == typeof(float) || type == typeof(Single))
            return context.DeltaTime; // Will be updated each frame
        if (type == typeof(ISystemContext))
            return context;
        
        return null;
    }
}
```

### Generated Injection (Source Generator Approach)

Instead of runtime reflection, the source generator produces injection code:

```csharp
// User code
[EcsSystem]
public partial class MovementSystem
{
    [Inject] protected IWorld World { get; set; }
    [Inject] protected float DeltaTime { get; set; }
    [InjectSystem] protected PhysicsSystem Physics { get; set; }
}

// Generated: MovementSystem.Inject.g.cs
partial class MovementSystem : IInjectableSystem
{
    private ISystemContext _context;
    private PhysicsSystem? _physics;
    
    void IInjectableSystem.Inject(ISystemContext context)
    {
        _context = context;
        World = context.World;
    }
    
    void IInjectableSystem.LateInject(ISystemRunner runner)
    {
        _physics = runner.GetSystem<PhysicsSystem>();
        if (_physics == null)
            throw new InvalidOperationException(
                "MovementSystem requires PhysicsSystem but it is not registered");
    }
    
    // DeltaTime is updated each frame via context
    protected new float DeltaTime => _context.DeltaTime;
}

public interface IInjectableSystem
{
    void Inject(ISystemContext context);
    void LateInject(ISystemRunner runner);
}
```

---

## System Groups

```csharp
/// <summary>
/// Groups systems for ordered execution
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SystemGroupAttribute : Attribute
{
    public Type? Group { get; }
    public int OrderInGroup { get; set; } = 0;
    
    public SystemGroupAttribute(Type? group = null)
    {
        Group = group;
    }
}

/// <summary>
/// Marker for system groups
/// </summary>
public abstract class SystemGroup
{
    public virtual SystemPhase Phase => SystemPhase.Update;
    public virtual int Order => 0;
}

// Built-in groups
public class InitializationSystemGroup : SystemGroup
{
    public override SystemPhase Phase => SystemPhase.Initialization;
}

public class SimulationSystemGroup : SystemGroup
{
    public override SystemPhase Phase => SystemPhase.Update;
    public override int Order => 0;
}

public class PresentationSystemGroup : SystemGroup
{
    public override SystemPhase Phase => SystemPhase.LateUpdate;
    public override int Order => 100;
}

// Usage
[EcsSystem]
[SystemGroup(typeof(SimulationSystemGroup), OrderInGroup = 10)]
public partial class MovementSystem { ... }

[EcsSystem]
[SystemGroup(typeof(SimulationSystemGroup), OrderInGroup = 20)]
public partial class PhysicsSystem { ... }  // Runs after MovementSystem
```

---

## Complete Initialization Example

```csharp
// ═══════════════════════════════════════════════════════════════
// Application Startup
// ═══════════════════════════════════════════════════════════════

public class Game
{
    private IWorld _world;
    private ISystemRunner _systems;
    
    public void Initialize()
    {
        // 1. Create world
        _world = WorldFactory.Create(EcsBackend.Arch, new WorldConfig
        {
            Name = "MainWorld",
            InitialEntityCapacity = 10_000
        });
        
        // 2. Create system runner
        _systems = new SystemRunner(_world);
        
        // 3. Register systems (order matters for [InjectSystem])
        _systems.Register(
            new InputSystem(),
            new MovementSystem(),
            new PhysicsSystem(),
            new CollisionSystem(),
            new HealthSystem(),
            new RenderSystem()
        );
        
        // 4. Initialize all systems (calls IInitializeSystem.Initialize)
        _systems.Initialize();
        
        // 5. Create initial entities
        SpawnPlayer();
        SpawnEnemies(100);
    }
    
    public void Update(float deltaTime)
    {
        // Execute all systems
        _systems.Update(deltaTime);
    }
    
    public void Shutdown()
    {
        _systems.Dispose();
        _world.Dispose();
    }
    
    private void SpawnPlayer()
    {
        var player = _world.CreateEntity();
        _world.Add(player, new Position { X = 0, Y = 0 });
        _world.Add(player, new Velocity());
        _world.Add(player, new Health { Current = 100, Max = 100 });
        _world.Add<PlayerControlled>(player);
    }
}
```

---

## Backend-Specific Initialization

### Arch Backend

```csharp
// Generated: ArchWorldFactory.g.cs
public sealed class ArchWorldFactory : IWorldFactory
{
    public EcsBackend Backend => EcsBackend.Arch;
    
    public IWorld Create(WorldConfig config)
    {
        return new ArchWorld(config);
    }
}

public sealed class ArchWorld : IWorld
{
    private readonly Arch.Core.World _world;
    
    public ArchWorld(WorldConfig config)
    {
        _world = Arch.Core.World.Create();
        // Arch doesn't need explicit component registration
    }
    
    public Entity CreateEntity()
    {
        var archEntity = _world.Create();
        return new Entity(archEntity.Id);
    }
    
    // ... implement all IWorld methods
}

// Auto-registration via module initializer
[ModuleInitializer]
internal static class ArchBackendRegistration
{
    public static void Initialize()
    {
        WorldFactory.Register(EcsBackend.Arch, new ArchWorldFactory());
    }
}
```

### Entitas Backend

```csharp
// Generated: EntitasWorldFactory.g.cs
public sealed class EntitasWorldFactory : IWorldFactory
{
    public EcsBackend Backend => EcsBackend.Entitas;
    
    public IWorld Create(WorldConfig config)
    {
        // Entitas requires pre-generated Contexts
        var contexts = new Contexts();
        return new EntitasWorld(contexts, config);
    }
}

public sealed class EntitasWorld : IWorld
{
    private readonly Contexts _contexts;
    private readonly GameContext _game;
    
    public EntitasWorld(Contexts contexts, WorldConfig config)
    {
        _contexts = contexts;
        _game = contexts.game;
    }
    
    // Map UnifyECS Entity to Entitas GameEntity
    private readonly Dictionary<int, GameEntity> _entityMap = new();
    
    public Entity CreateEntity()
    {
        var entitasEntity = _game.CreateEntity();
        var unifyEntity = new Entity(entitasEntity.creationIndex);
        _entityMap[unifyEntity.Id] = entitasEntity;
        return unifyEntity;
    }
    
    // ... implement all IWorld methods with Entitas translation
}
```

### DOTS Backend (see RFC-0010 for constraints)

```csharp
// Generated: DotsWorldFactory.g.cs  
// NOTE: DOTS world creation is complex - see RFC-0010

public sealed class DotsWorldFactory : IWorldFactory
{
    public EcsBackend Backend => EcsBackend.Dots;
    
    public IWorld Create(WorldConfig config)
    {
        // DOTS worlds have special initialization requirements
        var worldName = config.Name ?? "UnifyEcsWorld";
        
        // Create unmanaged world
        var world = new Unity.Entities.World(worldName, WorldFlags.Game);
        
        // Add required system groups
        var initGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
        var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
        var presGroup = world.GetOrCreateSystemManaged<PresentationSystemGroup>();
        
        return new DotsWorld(world, config);
    }
}
```

---

## Open Questions

## Non-Goals and Future Extensions

- **World identification and metadata**: v1 `IWorld` focuses on core entity/component operations (as implemented in `UnifyEcs.Core` and described in RFC-0011). Additional properties such as world IDs, backend metadata, or richer query-builder APIs are considered future extensions.
- **Interface-based scheduling**: v1 treats `IUnifySystem` as a marker interface. Execution order is driven by attributes (`[EcsSystem(Phase, Order)]`) and generated/backend-specific runners rather than `IUnifySystem` properties or sub-interfaces like `IExecuteSystem`. Those interfaces are reserved for potential future use.
- **Dynamic system management**: support for hot-reloading systems at runtime, resolving dependency cycles between systems, and async initialization/teardown is explicitly out of scope for v1. These topics may be addressed by later RFCs building on the existing lifecycle.
- **World access from non-system code**: providing global or service-locator style access to worlds (e.g., from UI callbacks) is not defined by v1 and is left to application architecture or future guidance.

## References

- RFC-0001: Core Architecture
- RFC-0003: Attribute API Design
- RFC-0005: Backend Adapters
- RFC-0010: DOTS Backend Constraints
