# RFC-0007: Multi-Backend Orchestration

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0005

## Summary

Define the architecture for running the same game code on multiple ECS backends simultaneously, enabling benchmarking, gradual migration, and cross-platform deployment.

## Motivation

### Use Cases

1. **Benchmarking**: Compare framework performance on identical workloads
2. **Gradual Migration**: Run both old and new backends side-by-side during transition
3. **Cross-Platform**: Use DOTS on Unity, Arch on standalone, same game code
4. **Framework Evaluation**: Test new ECS libraries without full commitment
5. **A/B Testing**: Compare behavior/performance in production

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Game Loop                                │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐  │
│   │               UnifyEcsOrchestrator                       │  │
│   │                                                          │  │
│   │   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐       │  │
│   │   │ArchRunner   │ │EntitasRunner│ │ DotsRunner  │       │  │
│   │   │             │ │             │ │             │       │  │
│   │   │ ┌─────────┐ │ │ ┌─────────┐ │ │ ┌─────────┐ │       │  │
│   │   │ │Movement │ │ │ │Movement │ │ │ │Movement │ │       │  │
│   │   │ │System   │ │ │ │System   │ │ │ │System   │ │       │  │
│   │   │ └─────────┘ │ │ └─────────┘ │ │ └─────────┘ │       │  │
│   │   │ ┌─────────┐ │ │ ┌─────────┐ │ │ ┌─────────┐ │       │  │
│   │   │ │Combat   │ │ │ │Combat   │ │ │ │Combat   │ │       │  │
│   │   │ │System   │ │ │ │System   │ │ │ │System   │ │       │  │
│   │   │ └─────────┘ │ │ └─────────┘ │ │ └─────────┘ │       │  │
│   │   └─────────────┘ └─────────────┘ └─────────────┘       │  │
│   │          │               │               │               │  │
│   │          ▼               ▼               ▼               │  │
│   │   ┌─────────────┐ ┌─────────────┐ ┌─────────────┐       │  │
│   │   │ Arch World  │ │Entitas Ctxs │ │ DOTS World  │       │  │
│   │   └─────────────┘ └─────────────┘ └─────────────┘       │  │
│   └─────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Orchestrator API

```csharp
/// <summary>
/// Coordinates execution across multiple ECS backends
/// </summary>
public sealed class UnifyEcsOrchestrator : IDisposable
{
    private readonly Dictionary<EcsBackend, IBackendRunner> _runners = new();
    private readonly OrchestratorConfig _config;
    
    public UnifyEcsOrchestrator(OrchestratorConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Register a backend runner
    /// </summary>
    public void RegisterBackend(IBackendRunner runner)
    {
        _runners[runner.Backend] = runner;
    }
    
    /// <summary>
    /// Execute one frame across all backends
    /// </summary>
    public void Update(float deltaTime)
    {
        switch (_config.ExecutionMode)
        {
            case ExecutionMode.Sequential:
                foreach (var runner in _runners.Values)
                    runner.Execute(deltaTime);
                break;
                
            case ExecutionMode.Parallel:
                Parallel.ForEach(_runners.Values, runner =>
                    runner.Execute(deltaTime));
                break;
                
            case ExecutionMode.Primary:
                _runners[_config.PrimaryBackend].Execute(deltaTime);
                break;
        }
    }
    
    /// <summary>
    /// Synchronize state from primary to secondary backends
    /// </summary>
    public void SyncFromPrimary()
    {
        if (!_runners.TryGetValue(_config.PrimaryBackend, out var primary))
            return;
        
        var snapshot = primary.CaptureSnapshot();
        
        foreach (var (backend, runner) in _runners)
        {
            if (backend != _config.PrimaryBackend)
                runner.ApplySnapshot(snapshot);
        }
    }
    
    /// <summary>
    /// Compare state across backends for validation
    /// </summary>
    public ValidationResult ValidateConsistency()
    {
        var snapshots = _runners.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.CaptureSnapshot());
        
        return StateValidator.Compare(snapshots);
    }
    
    public void Dispose()
    {
        foreach (var runner in _runners.Values)
            runner.Dispose();
    }
}

public enum ExecutionMode
{
    /// <summary>Execute backends one after another</summary>
    Sequential,
    
    /// <summary>Execute backends in parallel (for benchmarking)</summary>
    Parallel,
    
    /// <summary>Execute only the primary backend (production mode)</summary>
    Primary
}

public record OrchestratorConfig
{
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Primary;
    public EcsBackend PrimaryBackend { get; init; } = EcsBackend.Arch;
    public bool EnableValidation { get; init; } = false;
    public TimeSpan SyncInterval { get; init; } = TimeSpan.Zero;
}
```

## Backend Runner Interface

> 📌 **Note**: Snapshots use **logical entity IDs** and **ComponentTypeId** from RFC-0009 to ensure cross-backend compatibility. Backend-specific entity IDs are mapped via `EntityIdMapper`.

```csharp
/// <summary>
/// Runner for a specific ECS backend
/// </summary>
public interface IBackendRunner : IDisposable
{
    EcsBackend Backend { get; }
    
    /// <summary>Execute one frame</summary>
    void Execute(float deltaTime);
    
    /// <summary>Capture current world state as logical snapshot</summary>
    LogicalSnapshot CaptureSnapshot();
    
    /// <summary>Apply state from another backend</summary>
    void ApplySnapshot(LogicalSnapshot snapshot);
    
    /// <summary>Get system execution statistics</summary>
    SystemStats GetStats();
    
    /// <summary>Entity ID mapper for this backend</summary>
    EntityIdMapper EntityMapper { get; }
}

/// <summary>
/// Logical snapshot - backend-independent state representation.
/// See RFC-0009 for full specification.
/// </summary>
public record LogicalSnapshot
{
    public int Version { get; init; } = 1;
    public long Timestamp { get; init; }
    public string SourceBackend { get; init; } = "";
    
    /// <summary>Type registry for deserialization</summary>
    public Dictionary<int, (string Name, ulong Hash)> TypeRegistry { get; init; } = new();
    
    /// <summary>Entities indexed by logical ID (not backend-specific)</summary>
    public Dictionary<int, EntitySnapshot> Entities { get; init; } = new();
}

public record EntitySnapshot
{
    /// <summary>Logical entity ID (mapped from backend-specific ID)</summary>
    public int LogicalId { get; init; }
    
    /// <summary>Components indexed by ComponentTypeId</summary>
    public Dictionary<int, byte[]> Components { get; init; } = new();
}
```

## Generated Runner Implementations

### Arch Runner

```csharp
// Generated: ArchRunner.g.cs
public sealed class ArchRunner : IBackendRunner
{
    public EcsBackend Backend => EcsBackend.Arch;
    
    private readonly World _world;
    private readonly IArchSystem[] _systems;
    
    public ArchRunner()
    {
        _world = World.Create();
        
        // Systems registered by generator
        _systems = new IArchSystem[]
        {
            new MovementSystem(),
            new CombatSystem(),
            new HealthReactiveSystem(),
            // ... all generated systems
        };
    }
    
    public void Execute(float deltaTime)
    {
        foreach (var system in _systems)
        {
            system.Execute(_world, deltaTime);
        }
    }
    
    public WorldSnapshot CaptureSnapshot()
    {
        var entities = ImmutableDictionary.CreateBuilder<int, EntitySnapshot>();
        
        _world.Query(new QueryDescription(), (Entity entity) =>
        {
            var components = new Dictionary<Type, object>();
            
            // Capture each registered component type
            if (_world.Has<Position>(entity))
                components[typeof(Position)] = _world.Get<Position>(entity);
            if (_world.Has<Velocity>(entity))
                components[typeof(Velocity)] = _world.Get<Velocity>(entity);
            // ... all component types
            
            entities[entity.Id] = new EntitySnapshot
            {
                Id = entity.Id,
                Components = components.ToImmutableDictionary()
            };
        });
        
        return new WorldSnapshot
        {
            EntityCount = _world.Size,
            Entities = entities.ToImmutable(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    public void ApplySnapshot(WorldSnapshot snapshot)
    {
        // Clear existing entities
        _world.Clear();
        
        // Recreate from snapshot
        foreach (var (id, entitySnapshot) in snapshot.Entities)
        {
            var entity = _world.Create();
            
            foreach (var (type, value) in entitySnapshot.Components)
            {
                // Use reflection or generated switch for component types
                ApplyComponent(entity, type, value);
            }
        }
    }
    
    public void Dispose()
    {
        World.Destroy(_world);
    }
}
```

#### Arch Runner & World Registry Notes

The example above uses a plain `Arch.Core.World` for clarity. In real projects that also exercise the **unified `IWorld` API** (e.g., via `ArchWorld`):

- `ArchWorld` wraps an `Arch.Core.World` instance and maintains a **shared registry** mapping `UnifyECS.Entity` to `Arch.Core.Entity`.
- Generated Arch systems that perform **deferred** structural changes instantiate a nested `WorldAdapter : IWorld` over the same `Arch.Core.World` and call `ArchWorld.RegisterEntity(world, entity, archEntity)` whenever new entities are created.
- When integrating with the orchestrator and logical snapshots (see RFC-0009), backends can choose to:
  - Capture state directly from the underlying `Arch.Core.World`, or
  - Use `ArchWorld` as the primary view, relying on the registry to translate between logical and backend-specific entity IDs.

This keeps the Arch backend's immediate and deferred structural semantics consistent across both single-backend and multi-backend/orchestrated configurations.

### Entitas Runner

```csharp
// Generated: EntitasRunner.g.cs
public sealed class EntitasRunner : IBackendRunner
{
    public EcsBackend Backend => EcsBackend.Entitas;
    
    private readonly Contexts _contexts;
    private readonly Systems _systems;
    
    public EntitasRunner()
    {
        _contexts = new Contexts();
        
        _systems = new Feature("UnifyECS Systems")
            .Add(new MovementSystem(_contexts))
            .Add(new CombatSystem(_contexts))
            .Add(new HealthReactiveSystem(_contexts));
        
        _systems.Initialize();
    }
    
    public void Execute(float deltaTime)
    {
        // Entitas typically uses a different time injection mechanism
        _contexts.game.ReplaceDeltaTime(deltaTime);
        _systems.Execute();
        _systems.Cleanup();
    }
    
    public WorldSnapshot CaptureSnapshot()
    {
        var entities = ImmutableDictionary.CreateBuilder<int, EntitySnapshot>();
        
        foreach (var entity in _contexts.game.GetEntities())
        {
            var components = new Dictionary<Type, object>();
            
            if (entity.hasPosition)
                components[typeof(Position)] = new Position 
                { 
                    X = entity.position.X, 
                    Y = entity.position.Y, 
                    Z = entity.position.Z 
                };
            // ... all component types
            
            entities[entity.creationIndex] = new EntitySnapshot
            {
                Id = entity.creationIndex,
                Components = components.ToImmutableDictionary()
            };
        }
        
        return new WorldSnapshot
        {
            EntityCount = _contexts.game.count,
            Entities = entities.ToImmutable(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
    
    public void Dispose()
    {
        _systems.TearDown();
    }
}
```

## Benchmarking Mode

```csharp
public sealed class BenchmarkOrchestrator
{
    private readonly UnifyEcsOrchestrator _orchestrator;
    private readonly Dictionary<EcsBackend, List<FrameMetrics>> _metrics = new();
    
    public BenchmarkOrchestrator(params EcsBackend[] backends)
    {
        _orchestrator = new UnifyEcsOrchestrator(new OrchestratorConfig
        {
            ExecutionMode = ExecutionMode.Sequential
        });
        
        foreach (var backend in backends)
        {
            _orchestrator.RegisterBackend(CreateRunner(backend));
            _metrics[backend] = new List<FrameMetrics>();
        }
    }
    
    public void RunBenchmark(int frameCount, float deltaTime, Action<int, WorldSnapshot> setupFrame)
    {
        for (int frame = 0; frame < frameCount; frame++)
        {
            // Setup identical initial state
            var snapshot = _orchestrator.CaptureSnapshot(EcsBackend.Arch);
            setupFrame(frame, snapshot);
            
            foreach (var backend in _metrics.Keys)
            {
                // Apply same initial state
                _orchestrator.GetRunner(backend).ApplySnapshot(snapshot);
                
                // Measure execution
                var sw = Stopwatch.StartNew();
                _orchestrator.GetRunner(backend).Execute(deltaTime);
                sw.Stop();
                
                _metrics[backend].Add(new FrameMetrics
                {
                    Frame = frame,
                    ExecutionTimeMs = sw.Elapsed.TotalMilliseconds,
                    EntityCount = _orchestrator.GetRunner(backend).CaptureSnapshot().EntityCount
                });
            }
            
            // Validate results match
            var validation = _orchestrator.ValidateConsistency();
            if (!validation.IsConsistent)
            {
                Console.WriteLine($"Frame {frame}: State divergence detected!");
                Console.WriteLine(validation.ToString());
            }
        }
    }
    
    public BenchmarkReport GenerateReport()
    {
        return new BenchmarkReport
        {
            Backends = _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => new BackendMetrics
                {
                    AverageFrameTimeMs = kvp.Value.Average(m => m.ExecutionTimeMs),
                    MinFrameTimeMs = kvp.Value.Min(m => m.ExecutionTimeMs),
                    MaxFrameTimeMs = kvp.Value.Max(m => m.ExecutionTimeMs),
                    P99FrameTimeMs = Percentile(kvp.Value.Select(m => m.ExecutionTimeMs), 99)
                })
        };
    }
}
```

## State Validation

```csharp
public static class StateValidator
{
    public static ValidationResult Compare(
        IReadOnlyDictionary<EcsBackend, WorldSnapshot> snapshots)
    {
        if (snapshots.Count < 2)
            return ValidationResult.Valid();
        
        var reference = snapshots.First();
        var differences = new List<StateDifference>();
        
        foreach (var (backend, snapshot) in snapshots.Skip(1))
        {
            // Compare entity counts
            if (snapshot.EntityCount != reference.Value.EntityCount)
            {
                differences.Add(new StateDifference
                {
                    Type = DifferenceType.EntityCount,
                    Backend = backend,
                    Expected = reference.Value.EntityCount,
                    Actual = snapshot.EntityCount
                });
            }
            
            // Compare entity states
            foreach (var (entityId, refEntity) in reference.Value.Entities)
            {
                if (!snapshot.Entities.TryGetValue(entityId, out var otherEntity))
                {
                    differences.Add(new StateDifference
                    {
                        Type = DifferenceType.MissingEntity,
                        Backend = backend,
                        EntityId = entityId
                    });
                    continue;
                }
                
                // Compare components
                foreach (var (type, refValue) in refEntity.Components)
                {
                    if (!otherEntity.Components.TryGetValue(type, out var otherValue))
                    {
                        differences.Add(new StateDifference
                        {
                            Type = DifferenceType.MissingComponent,
                            Backend = backend,
                            EntityId = entityId,
                            ComponentType = type
                        });
                        continue;
                    }
                    
                    if (!ComponentEquals(refValue, otherValue))
                    {
                        differences.Add(new StateDifference
                        {
                            Type = DifferenceType.ComponentMismatch,
                            Backend = backend,
                            EntityId = entityId,
                            ComponentType = type,
                            Expected = refValue,
                            Actual = otherValue
                        });
                    }
                }
            }
        }
        
        return new ValidationResult(differences.Count == 0, differences);
    }
    
    private static bool ComponentEquals(object a, object b)
    {
        // Use floating-point tolerance for numeric comparisons
        if (a is Position posA && b is Position posB)
        {
            return Math.Abs(posA.X - posB.X) < 0.0001f
                && Math.Abs(posA.Y - posB.Y) < 0.0001f
                && Math.Abs(posA.Z - posB.Z) < 0.0001f;
        }
        
        return a.Equals(b);
    }
}
```

## Usage Examples

### Single Backend (Production)

```csharp
// Simple case - one backend
var orchestrator = new UnifyEcsOrchestrator(new OrchestratorConfig
{
    ExecutionMode = ExecutionMode.Primary,
    PrimaryBackend = EcsBackend.Arch
});

orchestrator.RegisterBackend(new ArchRunner());

// Game loop
while (running)
{
    orchestrator.Update(deltaTime);
}
```

### Migration Mode

```csharp
// Run both backends, compare results
var orchestrator = new UnifyEcsOrchestrator(new OrchestratorConfig
{
    ExecutionMode = ExecutionMode.Sequential,
    PrimaryBackend = EcsBackend.Arch,
    EnableValidation = true
});

orchestrator.RegisterBackend(new ArchRunner());
orchestrator.RegisterBackend(new EntitasRunner());

// Initial sync
orchestrator.SyncFromPrimary();

while (running)
{
    orchestrator.Update(deltaTime);
    
    // Periodic validation
    if (frameCount % 60 == 0)
    {
        var validation = orchestrator.ValidateConsistency();
        if (!validation.IsConsistent)
            LogDivergence(validation);
    }
}
```

### Benchmarking Mode

```csharp
var benchmark = new BenchmarkOrchestrator(
    EcsBackend.Arch,
    EcsBackend.Entitas,
    EcsBackend.Dots);

// Create 10,000 entities with Position + Velocity
benchmark.Setup(world =>
{
    for (int i = 0; i < 10_000; i++)
    {
        world.CreateEntity()
            .Add(new Position { X = i, Y = 0, Z = 0 })
            .Add(new Velocity { X = 1, Y = 0, Z = 0 });
    }
});

// Run 1000 frames
benchmark.RunBenchmark(frameCount: 1000, deltaTime: 0.016f);

// Generate report
var report = benchmark.GenerateReport();
Console.WriteLine(report.ToMarkdown());

// Output:
// | Backend  | Avg (ms) | Min (ms) | Max (ms) | P99 (ms) |
// |----------|----------|----------|----------|----------|
// | Arch     | 0.42     | 0.38     | 0.87     | 0.65     |
// | Entitas  | 0.89     | 0.82     | 1.45     | 1.21     |
// | DOTS     | 0.15     | 0.12     | 0.34     | 0.28     |
```

## Build Configuration

```xml
<!-- Multi-backend build -->
<PropertyGroup>
  <UnifyEcsBackends>Arch;Entitas;Dots</UnifyEcsBackends>
  <UnifyEcsGenerateOrchestrator>true</UnifyEcsGenerateOrchestrator>
  <UnifyEcsGenerateBenchmark>true</UnifyEcsGenerateBenchmark>
</PropertyGroup>

<!-- Single-backend production build -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <UnifyEcsBackends>Arch</UnifyEcsBackends>
  <UnifyEcsGenerateOrchestrator>false</UnifyEcsGenerateOrchestrator>
</PropertyGroup>
```

## Open Questions

1. How to handle backend-specific entity IDs during sync?
2. Should orchestrator support async execution?
3. How to handle event timing differences between backends?
4. Performance impact of snapshot capture - can we use incremental diffs?

## References

- RFC-0001: Core Architecture
- RFC-0005: Backend Adapters
- RFC-0008: World Lifecycle & System Execution
- RFC-0009: Component Registry & Type Mapping (logical snapshots, entity ID mapping)
- RFC-0010: DOTS Backend Constraints (snapshot limitations for DOTS)
