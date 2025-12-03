# RFC-0013: Diagnostics & Debugging Specification

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0006, RFC-0008
- **Addresses**: Review Issue #7 (debugging/logging/error handling)

## Implementation Status (v1)

- **Implemented today**:
  - A single Roslyn analyzer (`UnifyEcsAnalyzer`) in `UnifyEcs.Analyzers` with descriptors defined in `Diagnostics.cs`.
  - Enforced rules for component and system shape, query correctness, and structural change safety (diagnostics UECS003, UECS004–UECS009, UECS010, UECS011, UECS012–UECS015, UECS018, UECS019, UECS101).
  - Tests in `dotnet/tests/UnifyEcs.Analyzers.Tests` that cover at least a subset of these diagnostics (for example UECS019) and are being expanded.
- **Not yet implemented**:
  - Diagnostics UECS001/UECS002 and UECS100+ / UECS200+ listed in this RFC are design-only and not emitted by the current analyzer.
  - Runtime logging, validation, entity inspection, and profiling types (`UnifyEcsLogger`, `UnifyEcsDebug`, `RuntimeValidation`, `EntityInspector`, `SystemProfiler`, `IEntityDebugger`, etc.) are not present in the current `dotnet/` implementation.
  - Console/Editor tooling and MSBuild configuration knobs for diagnostics/profiling are not yet wired up.

As of v1, diagnostics are focused on compile-time validation; runtime debugging and tooling APIs in this RFC describe the intended direction rather than the current implementation.

## Summary

Define the diagnostics, logging, debugging tools, and error handling mechanisms for UnifyECS, including compile-time analyzers, runtime validation, and development tooling.

## Motivation

A production-quality ECS framework needs:
1. Clear compile-time errors for invalid configurations
2. Runtime validation in debug builds
3. Logging for stubbed/emulated features
4. Entity debugger views
5. Performance profiling hooks
6. Error recovery strategies

---

## Compile-Time Diagnostics

### Roslyn Analyzers

UnifyECS ships with Roslyn analyzers that provide IDE feedback:

```csharp
// UnifyECS.Analyzers package
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnifyEcsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(
            Diagnostics.UECS001_UnsupportedFeature,
            Diagnostics.UECS002_UnsupportedFeatureWarning,
            Diagnostics.UECS003_InvalidQueryParameter,
            // ... all diagnostics
        );
}
```

### Diagnostic Codes

#### Errors (UECS001-099)

| Code | Severity | Message |
|------|----------|---------|
| `UECS001` | Error | System '{0}' requires feature '{1}' not supported by backend '{2}' |
| `UECS002` | Warning | System '{0}' will be stubbed for backend '{1}' (missing '{2}') |
| `UECS003` | Error | Query method '{0}' parameter '{1}' type '{2}' is not a registered component |
| `UECS004` | Error | Component '{0}' must be a struct |
| `UECS005` | Error | Component '{0}' cannot be generic |
| `UECS006` | Error | Component '{0}' cannot be a nested type |
| `UECS007` | Error | System '{0}' must be a partial class |
| `UECS008` | Error | System '{0}' cannot be abstract |
| `UECS009` | Error | System '{0}' cannot be generic |
| `UECS010` | Error | Query method '{0}' parameters do not match All components |
| `UECS011` | Error | Component '{0}' contains reference type field '{1}' (not DOTS-compatible) |
| `UECS012` | Error | Immediate structural changes in parallel system (DOTS) |
| `UECS013` | Warning | Method performs structural changes but missing [StructuralChanges] |
| `UECS014` | Error | Cross-entity write without deferred mode |
| `UECS015` | Error | ICommandBuffer used but not injected |

#### Warnings (UECS100-199)

| Code | Severity | Message |
|------|----------|---------|
| `UECS100` | Warning | Reactive feature emulated on backend '{0}' - performance may be degraded |
| `UECS101` | Warning | Component '{0}' has reference field - requires [ManagedComponent] for DOTS |
| `UECS102` | Warning | System '{0}' has no queries - consider using IInitializeSystem |
| `UECS103` | Warning | Query caching disabled - performance impact |
| `UECS104` | Warning | Large component '{0}' ({1} bytes) - consider splitting |
| `UECS105` | Info | System '{0}' stubbed for backend '{1}' (NoOp policy) |

#### Info (UECS200-299)

| Code | Severity | Message |
|------|----------|---------|
| `UECS200` | Info | Generated {0} systems for backend '{1}' |
| `UECS201` | Info | Component '{0}' assigned TypeId {1} |
| `UECS202` | Info | Reactive emulation using change tracking for '{0}' |

### Diagnostic Attributes

```csharp
/// <summary>
/// Suppress specific UnifyECS diagnostics
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SuppressUnifyDiagnosticAttribute : Attribute
{
    public string[] DiagnosticIds { get; }
    public string? Justification { get; set; }
    
    public SuppressUnifyDiagnosticAttribute(params string[] diagnosticIds)
    {
        DiagnosticIds = diagnosticIds;
    }
}

// Usage
[EcsSystem]
[SuppressUnifyDiagnostic("UECS100", Justification = "Emulation acceptable for debug system")]
[EcsRequires(EcsFeature.Reactive)]
public partial class DebugHealthLogger { }
```

---

## Runtime Validation

### Debug Mode

```csharp
/// <summary>
/// Runtime validation settings
/// </summary>
public static class UnifyEcsDebug
{
    /// <summary>Enable/disable all runtime checks</summary>
    public static bool Enabled { get; set; } = 
#if DEBUG
        true;
#else
        false;
#endif
    
    /// <summary>Throw on validation failure vs log warning</summary>
    public static bool ThrowOnError { get; set; } = true;
    
    /// <summary>Log level for diagnostics</summary>
    public static LogLevel MinLogLevel { get; set; } = LogLevel.Warning;
}
```

### Validation Checks

```csharp
public static class RuntimeValidation
{
    /// <summary>
    /// Validate entity exists before component access
    /// </summary>
    [Conditional("DEBUG")]
    public static void ValidateEntityExists(IWorld world, Entity entity, string operation)
    {
        if (!world.Exists(entity))
        {
            var message = $"Entity {entity} does not exist during {operation}";
            if (UnifyEcsDebug.ThrowOnError)
                throw new InvalidEntityException(message);
            else
                UnifyEcsLogger.Error(message);
        }
    }
    
    /// <summary>
    /// Validate component exists before Get
    /// </summary>
    [Conditional("DEBUG")]
    public static void ValidateHasComponent<T>(IWorld world, Entity entity) where T : struct
    {
        if (!world.Has<T>(entity))
        {
            var message = $"Entity {entity} does not have component {typeof(T).Name}";
            if (UnifyEcsDebug.ThrowOnError)
                throw new MissingComponentException(message);
            else
                UnifyEcsLogger.Error(message);
        }
    }
    
    /// <summary>
    /// Validate no structural changes during iteration
    /// </summary>
    [Conditional("DEBUG")]
    public static void ValidateNoStructuralChanges(string systemName, string context)
    {
        if (StructuralChangeTracker.IsIterating)
        {
            var message = $"Structural change detected in {systemName} during iteration. {context}";
            if (UnifyEcsDebug.ThrowOnError)
                throw new StructuralChangeException(message);
            else
                UnifyEcsLogger.Error(message);
        }
    }
}
```

### Generated Validation Code

```csharp
// Generated in DEBUG builds
partial class MovementSystem
{
    public void Execute(World world, float deltaTime)
    {
#if DEBUG
        UnifyEcsLogger.Trace($"MovementSystem.Execute begin (dt={deltaTime})");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        StructuralChangeTracker.BeginIteration("MovementSystem");
#endif
        
        world.Query(in _query, (Entity entity, ref Position pos, in Velocity vel) =>
        {
#if DEBUG
            RuntimeValidation.ValidateEntityExists(world, entity, "MovementSystem.Process");
#endif
            Process(ref pos, in vel);
        });
        
#if DEBUG
        StructuralChangeTracker.EndIteration();
        sw.Stop();
        UnifyEcsLogger.Trace($"MovementSystem.Execute end ({sw.ElapsedMilliseconds}ms)");
        _profiler.RecordFrame("MovementSystem", sw.Elapsed);
#endif
    }
}
```

---

## Logging System

> ⚠️ **Thread Safety**: Logging, profiling, and entity inspection APIs are designed for **main-thread usage only**. In DOTS/Jobs contexts:
> - Minimize logging from parallel jobs (use Burst's `Debug.Log` sparingly)
> - `EntityInspector` and `SystemProfiler` should only be called from main thread
> - For job debugging, prefer deferred logging via collections or native containers

### IUnifyLogger Interface

```csharp
/// <summary>
/// Logging interface for UnifyECS diagnostics
/// </summary>
public interface IUnifyLogger
{
    void Log(LogLevel level, string message);
    void Log(LogLevel level, string message, Exception? exception);
    bool IsEnabled(LogLevel level);
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}
```

### Default Logger

```csharp
/// <summary>
/// Global logger for UnifyECS
/// </summary>
public static class UnifyEcsLogger
{
    private static IUnifyLogger _logger = new ConsoleLogger();
    
    public static void SetLogger(IUnifyLogger logger) => _logger = logger;
    
    public static void Trace(string message) => Log(LogLevel.Trace, message);
    public static void Debug(string message) => Log(LogLevel.Debug, message);
    public static void Info(string message) => Log(LogLevel.Info, message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message) => Log(LogLevel.Error, message);
    public static void Error(string message, Exception ex) => _logger.Log(LogLevel.Error, message, ex);
    
    private static void Log(LogLevel level, string message)
    {
        if (_logger.IsEnabled(level))
            _logger.Log(level, message);
    }
}

/// <summary>
/// Console-based logger (default)
/// </summary>
public class ConsoleLogger : IUnifyLogger
{
    public void Log(LogLevel level, string message)
    {
        var prefix = level switch
        {
            LogLevel.Trace => "[TRACE]",
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Info => "[INFO]",
            LogLevel.Warning => "[WARN]",
            LogLevel.Error => "[ERROR]",
            LogLevel.Critical => "[CRIT]",
            _ => "[???]"
        };
        
        Console.WriteLine($"{prefix} UnifyECS: {message}");
    }
    
    public void Log(LogLevel level, string message, Exception? ex)
    {
        Log(level, $"{message}\n{ex}");
    }
    
    public bool IsEnabled(LogLevel level) => level >= UnifyEcsDebug.MinLogLevel;
}
```

### Unity Integration

```csharp
#if UNITY_5_3_OR_NEWER
/// <summary>
/// Unity Debug.Log integration
/// </summary>
public class UnityLogger : IUnifyLogger
{
    public void Log(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Info:
                UnityEngine.Debug.Log($"[UnifyECS] {message}");
                break;
            case LogLevel.Warning:
                UnityEngine.Debug.LogWarning($"[UnifyECS] {message}");
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                UnityEngine.Debug.LogError($"[UnifyECS] {message}");
                break;
        }
    }
    
    public void Log(LogLevel level, string message, Exception? ex)
    {
        if (ex != null)
            UnityEngine.Debug.LogException(ex);
        else
            Log(level, message);
    }
    
    public bool IsEnabled(LogLevel level) => level >= UnifyEcsDebug.MinLogLevel;
}
#endif
```

### Stubbed System Logging

When systems are stubbed due to missing features:

```csharp
// Generated for stubbed system (Warn policy)
partial class ReactiveHealthSystem
{
    private static bool _warningLogged = false;
    
    public void Execute(World world, float deltaTime)
    {
        if (!_warningLogged)
        {
            UnifyEcsLogger.Warning(
                "ReactiveHealthSystem requires 'Reactive' feature which is not supported " +
                "by Arch backend. System is disabled. " +
                "Consider using [EcsRequires(EcsFeature.Reactive, IfMissing = Emulate)]");
            _warningLogged = true;
        }
        // No-op: system disabled
    }
}
```

---

## Entity Debugger

### DebuggerDisplay Attributes

```csharp
// Generated for Entity
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly partial struct Entity
{
    private string DebuggerDisplay => IsValid 
        ? $"Entity({Id}:{Generation})" 
        : "Entity.Null";
}

// Generated for components
[DebuggerDisplay("Position({X}, {Y}, {Z})")]
public partial struct Position { }

[DebuggerDisplay("Health({Current}/{Max})")]
public partial struct Health { }
```

### Entity Inspector

```csharp
/// <summary>
/// Debug inspection utilities for entities
/// </summary>
public static class EntityInspector
{
    /// <summary>
    /// Get all components on an entity as a dictionary
    /// </summary>
    public static Dictionary<Type, object> GetAllComponents(IWorld world, Entity entity)
    {
        var result = new Dictionary<Type, object>();
        
        // Generated code iterates all registered components
        if (world.Has<Position>(entity))
            result[typeof(Position)] = world.Get<Position>(entity);
        if (world.Has<Velocity>(entity))
            result[typeof(Velocity)] = world.Get<Velocity>(entity);
        if (world.Has<Health>(entity))
            result[typeof(Health)] = world.Get<Health>(entity);
        // ... all components
        
        return result;
    }
    
    /// <summary>
    /// Get human-readable entity description
    /// </summary>
    public static string Describe(IWorld world, Entity entity)
    {
        if (!world.Exists(entity))
            return $"Entity {entity.Id} (INVALID)";
        
        var components = GetAllComponents(world, entity);
        var sb = new StringBuilder();
        sb.AppendLine($"Entity {entity.Id}:{entity.Generation}");
        sb.AppendLine($"Components ({components.Count}):");
        
        foreach (var (type, value) in components)
        {
            sb.AppendLine($"  - {type.Name}: {value}");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Dump all entities to log
    /// </summary>
    public static void DumpWorld(IWorld world)
    {
        UnifyEcsLogger.Debug($"=== World Dump ({world.EntityCount} entities) ===");
        
        world.Query((Entity entity) =>
        {
            UnifyEcsLogger.Debug(Describe(world, entity));
        });
        
        UnifyEcsLogger.Debug("=== End World Dump ===");
    }
}
```

### Visual Debugger Interface

```csharp
/// <summary>
/// Interface for visual debugging tools (Unity Inspector, ImGui, etc.)
/// </summary>
public interface IEntityDebugger
{
    /// <summary>Get all entities matching a filter</summary>
    IEnumerable<Entity> GetEntities(Func<Entity, bool>? filter = null);
    
    /// <summary>Get component types present on entity</summary>
    IEnumerable<Type> GetComponentTypes(Entity entity);
    
    /// <summary>Get component value as boxed object</summary>
    object? GetComponent(Entity entity, Type componentType);
    
    /// <summary>Set component value from boxed object</summary>
    void SetComponent(Entity entity, Type componentType, object value);
    
    /// <summary>Get system execution statistics</summary>
    IEnumerable<SystemStats> GetSystemStats();
}

public record SystemStats
{
    public string SystemName { get; init; } = "";
    public TimeSpan LastExecutionTime { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public int EntitiesProcessed { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsStubbed { get; init; }
}
```

---

## Performance Profiling

### Built-in Profiler

```csharp
/// <summary>
/// Performance profiling for UnifyECS systems
/// </summary>
public sealed class SystemProfiler
{
    private readonly Dictionary<string, ProfilerData> _data = new();
    private readonly int _historySize;
    
    public SystemProfiler(int historySize = 60)
    {
        _historySize = historySize;
    }
    
    public void RecordFrame(string systemName, TimeSpan duration)
    {
        if (!_data.TryGetValue(systemName, out var data))
        {
            data = new ProfilerData(_historySize);
            _data[systemName] = data;
        }
        
        data.Record(duration);
    }
    
    public ProfilerReport GetReport()
    {
        return new ProfilerReport
        {
            Systems = _data.ToDictionary(
                kvp => kvp.Key,
                kvp => new SystemProfileReport
                {
                    LastFrame = kvp.Value.Last,
                    Average = kvp.Value.Average,
                    Min = kvp.Value.Min,
                    Max = kvp.Value.Max,
                    P95 = kvp.Value.Percentile(95),
                    P99 = kvp.Value.Percentile(99)
                })
        };
    }
}

public record ProfilerReport
{
    public Dictionary<string, SystemProfileReport> Systems { get; init; } = new();
    
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| System | Last | Avg | Min | Max | P95 | P99 |");
        sb.AppendLine("|--------|------|-----|-----|-----|-----|-----|");
        
        foreach (var (name, stats) in Systems.OrderByDescending(s => s.Value.Average))
        {
            sb.AppendLine($"| {name} | {stats.LastFrame.TotalMilliseconds:F2}ms | " +
                         $"{stats.Average.TotalMilliseconds:F2}ms | " +
                         $"{stats.Min.TotalMilliseconds:F2}ms | " +
                         $"{stats.Max.TotalMilliseconds:F2}ms | " +
                         $"{stats.P95.TotalMilliseconds:F2}ms | " +
                         $"{stats.P99.TotalMilliseconds:F2}ms |");
        }
        
        return sb.ToString();
    }
}
```

### Unity Profiler Integration

```csharp
#if UNITY_5_3_OR_NEWER
using UnityEngine.Profiling;

// Generated system wrapper with profiling
partial class MovementSystem
{
    private static readonly ProfilerMarker _profilerMarker = 
        new ProfilerMarker("UnifyECS.MovementSystem");
    
    public void Execute(World world, float deltaTime)
    {
        using (_profilerMarker.Auto())
        {
            // System execution
        }
    }
}
#endif
```

---

## Exception Types

```csharp
/// <summary>
/// Base exception for UnifyECS errors
/// </summary>
public class UnifyEcsException : Exception
{
    public UnifyEcsException(string message) : base(message) { }
    public UnifyEcsException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Entity does not exist or is invalid
/// </summary>
public class InvalidEntityException : UnifyEcsException
{
    public Entity Entity { get; }
    
    public InvalidEntityException(Entity entity, string message) 
        : base(message)
    {
        Entity = entity;
    }
}

/// <summary>
/// Component not present on entity
/// </summary>
public class MissingComponentException : UnifyEcsException
{
    public Entity Entity { get; }
    public Type ComponentType { get; }
    
    public MissingComponentException(Entity entity, Type componentType) 
        : base($"Entity {entity} does not have component {componentType.Name}")
    {
        Entity = entity;
        ComponentType = componentType;
    }
}

/// <summary>
/// Structural change during iteration
/// </summary>
public class StructuralChangeException : UnifyEcsException
{
    public string SystemName { get; }
    
    public StructuralChangeException(string systemName, string message) 
        : base(message)
    {
        SystemName = systemName;
    }
}

/// <summary>
/// Feature not supported by backend
/// </summary>
public class UnsupportedFeatureException : UnifyEcsException
{
    public EcsFeature Feature { get; }
    public EcsBackend Backend { get; }
    
    public UnsupportedFeatureException(EcsFeature feature, EcsBackend backend)
        : base($"Feature '{feature}' is not supported by backend '{backend}'")
    {
        Feature = feature;
        Backend = backend;
    }
}

/// <summary>
/// System dependency not satisfied
/// </summary>
public class SystemDependencyException : UnifyEcsException
{
    public Type SystemType { get; }
    public Type DependencyType { get; }
    
    public SystemDependencyException(Type systemType, Type dependencyType)
        : base($"System '{systemType.Name}' requires '{dependencyType.Name}' but it is not registered")
    {
        SystemType = systemType;
        DependencyType = dependencyType;
    }
}
```

---

## Debug Commands

### Console Commands (Development)

```csharp
/// <summary>
/// Debug commands for development console
/// </summary>
public static class UnifyEcsCommands
{
    /// <summary>
    /// Register debug commands with a console system
    /// </summary>
    public static void RegisterCommands(IDebugConsole console)
    {
        console.Register("ecs.dump", "Dump all entities", args =>
        {
            EntityInspector.DumpWorld(CurrentWorld);
        });
        
        console.Register("ecs.stats", "Show system performance stats", args =>
        {
            var report = Profiler.GetReport();
            Console.WriteLine(report.ToMarkdown());
        });
        
        console.Register("ecs.entity", "Inspect entity by ID", args =>
        {
            if (int.TryParse(args[0], out var id))
            {
                var entity = new Entity(id);
                Console.WriteLine(EntityInspector.Describe(CurrentWorld, entity));
            }
        });
        
        console.Register("ecs.spawn", "Spawn test entity", args =>
        {
            var entity = CurrentWorld.CreateEntity();
            CurrentWorld.Add(entity, new Position());
            Console.WriteLine($"Spawned {entity}");
        });
        
        console.Register("ecs.systems", "List registered systems", args =>
        {
            foreach (var system in SystemRunner.GetAllSystems())
            {
                var status = system.Enabled ? "enabled" : "disabled";
                Console.WriteLine($"  {system.GetType().Name} [{status}]");
            }
        });
        
        console.Register("ecs.toggle", "Enable/disable system", args =>
        {
            var systemName = args[0];
            var system = SystemRunner.GetSystem(systemName);
            if (system != null)
            {
                system.Enabled = !system.Enabled;
                Console.WriteLine($"{systemName} is now {(system.Enabled ? "enabled" : "disabled")}");
            }
        });
    }
}
```

---

## Configuration

### MSBuild Properties

```xml
<PropertyGroup>
  <!-- Enable/disable debug validation in Release builds -->
  <UnifyEcsDebugValidation>false</UnifyEcsDebugValidation>
  
  <!-- Enable profiling markers -->
  <UnifyEcsProfiling>true</UnifyEcsProfiling>
  
  <!-- Minimum log level -->
  <UnifyEcsLogLevel>Warning</UnifyEcsLogLevel>
  
  <!-- Generate debugger display attributes -->
  <UnifyEcsDebuggerDisplay>true</UnifyEcsDebuggerDisplay>
</PropertyGroup>
```

---

## Open Questions

1. Should we integrate with OpenTelemetry for distributed tracing?
2. How to handle async exception propagation?
3. Should entity debugger support live editing in play mode?

## References

- RFC-0006: Missing Feature Policies
- RFC-0008: World Lifecycle & System Execution
- [Roslyn Analyzers](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
