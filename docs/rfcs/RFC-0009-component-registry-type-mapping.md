# RFC-0009: Component Registry & Type Mapping

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0003, RFC-0005
- **Addresses**: Review Issue #6, #7

## Summary

Define how components are registered, assigned stable type IDs, and mapped across backends to enable serialization, snapshots, and cross-backend state synchronization.

## Motivation

The previous RFCs assume components "just work" across backends, but several critical questions remain:

1. How are component types discovered and registered?
2. How do components get stable numeric IDs for serialization?
3. How do we ensure consistent type mapping across backends for snapshots?
4. Are generic components supported?
5. How do backends with generated lookups (Entitas) integrate?

---

## Version Scope

This document defines the **v1 component registry and type mapping model** for UnifyECS:

- **In scope**: the `ComponentTypeId` shape, compile-time ID assignment strategy, structure hashing for version detection, and backend mapping patterns described in this RFC.
- **Out of scope**: component inheritance semantics, aliasing/renaming of component types at the registry level, and full runtime registration/modding flows.
- **Compatibility**: later RFCs may extend the registry (for example, to support aliases or more backends), but MUST NOT change the meaning of existing IDs, hashes, or their mapping rules.

## Component Type Identity

### ComponentTypeId

Each component type gets a stable identifier that persists across:
- Multiple runs
- Serialization/deserialization
- Cross-backend snapshots

```csharp
/// <summary>
/// Stable identifier for a component type.
/// </summary>
public readonly record struct ComponentTypeId
{
    /// <summary>Numeric ID for fast lookup</summary>
    public int Id { get; }
    
    /// <summary>Full type name for serialization stability</summary>
    public string TypeName { get; }
    
    /// <summary>Hash of type structure for version detection</summary>
    public ulong StructureHash { get; }
    
    public ComponentTypeId(int id, string typeName, ulong structureHash)
    {
        Id = id;
        TypeName = typeName;
        StructureHash = structureHash;
    }
}
```

### ID Assignment Strategies

#### Option A: Compile-Time Assignment (Recommended)

The source generator assigns IDs at compile time based on deterministic ordering:

```csharp
// Generated: ComponentTypeRegistry.g.cs
public static partial class ComponentTypeRegistry
{
    // IDs assigned alphabetically by full type name for stability
    public static readonly ComponentTypeId Position = new(
        id: 0,
        typeName: "MyGame.Components.Position",
        structureHash: 0x1A2B3C4D5E6F7890UL);
    
    public static readonly ComponentTypeId Velocity = new(
        id: 1,
        typeName: "MyGame.Components.Velocity",
        structureHash: 0x2B3C4D5E6F7890ABUL);
    
    public static readonly ComponentTypeId Health = new(
        id: 2,
        typeName: "MyGame.Components.Health",
        structureHash: 0x3C4D5E6F7890ABCDUL);
    
    // ... all components
    
    public static readonly int TotalComponentTypes = 42;
    
    private static readonly Dictionary<Type, ComponentTypeId> _byType = new()
    {
        [typeof(Position)] = Position,
        [typeof(Velocity)] = Velocity,
        [typeof(Health)] = Health,
        // ...
    };
    
    private static readonly Dictionary<int, Type> _byId = new()
    {
        [0] = typeof(Position),
        [1] = typeof(Velocity),
        [2] = typeof(Health),
        // ...
    };
    
    private static readonly Dictionary<string, ComponentTypeId> _byName = new()
    {
        ["MyGame.Components.Position"] = Position,
        ["MyGame.Components.Velocity"] = Velocity,
        ["MyGame.Components.Health"] = Health,
        // ...
    };
    
    public static ComponentTypeId GetId<T>() where T : struct
        => _byType[typeof(T)];
    
    public static ComponentTypeId GetId(Type type)
        => _byType[type];
    
    public static Type GetType(int id)
        => _byId[id];
    
    public static Type GetType(string typeName)
        => _byId[_byName[typeName].Id];
    
    public static bool TryGetId(Type type, out ComponentTypeId id)
        => _byType.TryGetValue(type, out id);
}
```

#### Option B: Runtime Registration

For dynamic scenarios or plugin systems:

```csharp
public sealed class RuntimeComponentRegistry
{
    private readonly Dictionary<Type, ComponentTypeId> _types = new();
    private readonly List<Type> _orderedTypes = new();
    private int _nextId = 0;
    
    public ComponentTypeId Register<T>() where T : struct
    {
        return Register(typeof(T));
    }
    
    public ComponentTypeId Register(Type type)
    {
        if (_types.TryGetValue(type, out var existing))
            return existing;
        
        var id = new ComponentTypeId(
            id: _nextId++,
            typeName: type.FullName!,
            structureHash: ComputeStructureHash(type));
        
        _types[type] = id;
        _orderedTypes.Add(type);
        
        return id;
    }
    
    private static ulong ComputeStructureHash(Type type)
    {
        // Hash based on field names, types, and order
        ulong hash = 0;
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            hash = hash * 31 + (ulong)field.Name.GetHashCode();
            hash = hash * 31 + (ulong)field.FieldType.FullName!.GetHashCode();
        }
        return hash;
    }
}
```

---

## Structure Hash for Version Detection

The structure hash detects when a component's layout changes:

```csharp
/// <summary>
/// Computes a hash of component structure for version detection.
/// Changes when fields are added, removed, renamed, or reordered.
/// </summary>
public static class StructureHasher
{
    public static ulong ComputeHash<T>() where T : struct
    {
        return ComputeHash(typeof(T));
    }
    
    public static ulong ComputeHash(Type type)
    {
        var fields = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(f => f.MetadataToken) // Consistent ordering
            .ToArray();
        
        ulong hash = 14695981039346656037UL; // FNV offset basis
        
        foreach (var field in fields)
        {
            // Hash field name
            foreach (char c in field.Name)
            {
                hash ^= c;
                hash *= 1099511628211UL; // FNV prime
            }
            
            // Hash field type
            foreach (char c in field.FieldType.FullName ?? field.FieldType.Name)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            
            // Hash field offset (for layout changes)
            var offset = (ulong)Marshal.OffsetOf(type, field.Name).ToInt64();
            hash ^= offset;
            hash *= 1099511628211UL;
        }
        
        return hash;
    }
}
```

### Version Mismatch Handling

```csharp
public enum VersionMismatchPolicy
{
    /// <summary>Throw exception on mismatch</summary>
    Error,
    
    /// <summary>Log warning, attempt best-effort deserialization</summary>
    Warn,
    
    /// <summary>Silently skip mismatched components</summary>
    Skip,
    
    /// <summary>Use registered migration handler</summary>
    Migrate
}

public interface IComponentMigration
{
    ulong FromHash { get; }
    ulong ToHash { get; }
    Type ComponentType { get; }
    
    object Migrate(object oldValue);
}

// Example migration
public class HealthMigrationV1ToV2 : IComponentMigration
{
    public ulong FromHash => 0x123456789ABCDEF0UL;
    public ulong ToHash => 0x0FEDCBA987654321UL;
    public Type ComponentType => typeof(Health);
    
    public object Migrate(object oldValue)
    {
        // Old: struct Health { int Value; }
        // New: struct Health { int Current; int Max; }
        var oldHealth = (HealthV1)oldValue;
        return new Health { Current = oldHealth.Value, Max = oldHealth.Value };
    }
}
```

---

## Backend-Specific Mapping

### Arch ECS

Arch uses types directly - no special mapping needed:

```csharp
// Generated: ComponentMapping.Arch.g.cs
public static class ArchComponentMapping
{
    // Arch uses typeof(T) directly in queries
    // No additional mapping required
    
    public static void EnsureRegistered(Arch.Core.World world)
    {
        // Arch auto-registers components on first use
        // Nothing to do
    }
}
```

### Entitas

Entitas requires component indices to be generated:

```csharp
// Generated: ComponentMapping.Entitas.g.cs
public static class EntitasComponentMapping
{
    // Maps UnifyECS ComponentTypeId to Entitas component index
    private static readonly Dictionary<int, int> _unifyToEntitas = new()
    {
        [ComponentTypeRegistry.Position.Id] = GameComponentsLookup.Position,
        [ComponentTypeRegistry.Velocity.Id] = GameComponentsLookup.Velocity,
        [ComponentTypeRegistry.Health.Id] = GameComponentsLookup.Health,
    };
    
    private static readonly Dictionary<int, int> _entitasToUnify = new()
    {
        [GameComponentsLookup.Position] = ComponentTypeRegistry.Position.Id,
        [GameComponentsLookup.Velocity] = ComponentTypeRegistry.Velocity.Id,
        [GameComponentsLookup.Health] = ComponentTypeRegistry.Health.Id,
    };
    
    public static int ToEntitasIndex(ComponentTypeId unifyId)
        => _unifyToEntitas[unifyId.Id];
    
    public static ComponentTypeId ToUnifyId(int entitasIndex)
        => ComponentTypeRegistry.GetId(_entitasToUnify[entitasIndex]);
}

// Integration with Entitas Contexts
public static class EntitasContextSetup
{
    public static void Initialize(Contexts contexts)
    {
        // Entitas contexts are pre-generated
        // Validate that our mapping matches
        ValidateMapping(contexts);
    }
    
    private static void ValidateMapping(Contexts contexts)
    {
        // Ensure Entitas component indices match our expectations
        if (GameComponentsLookup.Position != _expectedPositionIndex)
            throw new InvalidOperationException(
                "Entitas component indices don't match UnifyECS mapping. Regenerate Entitas code.");
    }
}
```

### DOTS

DOTS uses TypeIndex internally:

```csharp
// Generated: ComponentMapping.Dots.g.cs
public static class DotsComponentMapping
{
    // DOTS assigns TypeIndex at runtime
    private static Dictionary<int, TypeIndex>? _unifyToDots;
    private static Dictionary<TypeIndex, int>? _dotsToUnify;
    
    public static void Initialize()
    {
        _unifyToDots = new Dictionary<int, TypeIndex>
        {
            [ComponentTypeRegistry.Position.Id] = TypeManager.GetTypeIndex<Position>(),
            [ComponentTypeRegistry.Velocity.Id] = TypeManager.GetTypeIndex<Velocity>(),
            [ComponentTypeRegistry.Health.Id] = TypeManager.GetTypeIndex<Health>(),
        };
        
        _dotsToUnify = _unifyToDots.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    }
    
    public static TypeIndex ToDotsTypeIndex(ComponentTypeId unifyId)
        => _unifyToDots![unifyId.Id];
    
    public static ComponentTypeId ToUnifyId(TypeIndex dotsIndex)
        => ComponentTypeRegistry.GetId(_dotsToUnify![dotsIndex]);
}
```

---

## Generic Components

### Limitation: Generic Components Are Not Supported

**Decision**: UnifyECS does not support generic component types.

Rationale:
1. Backend compatibility issues:
   - Entitas requires concrete types for code generation
   - DOTS requires concrete IComponentData types
   - Serialization becomes complex with open generics

2. Source generation complexity:
   - Cannot enumerate all possible generic instantiations
   - Type IDs would be unstable

3. Workarounds exist:
   - Use concrete types: `HealthInt`, `HealthFloat`
   - Use composition: `Health<T>` → `Health { object Value; }`

```csharp
// ❌ NOT SUPPORTED
[EcsComponent]
public struct Reference<T> where T : struct
{
    public Entity Target;
}

// ✅ SUPPORTED - Use concrete types
[EcsComponent]
public struct EntityReference
{
    public Entity Target;
}

[EcsComponent]
public struct TargetReference
{
    public Entity Target;
    public TargetType Type;
}
```

---

## Component Serialization

### Binary Serialization

```csharp
/// <summary>
/// Serializes components to binary format for snapshots
/// </summary>
public interface IComponentSerializer
{
    void Serialize(BinaryWriter writer, ComponentTypeId typeId, object component);
    object Deserialize(BinaryReader reader, ComponentTypeId typeId);
}

// Generated: ComponentSerializer.g.cs
public sealed class GeneratedComponentSerializer : IComponentSerializer
{
    public void Serialize(BinaryWriter writer, ComponentTypeId typeId, object component)
    {
        switch (typeId.Id)
        {
            case 0: // Position
                var pos = (Position)component;
                writer.Write(pos.X);
                writer.Write(pos.Y);
                writer.Write(pos.Z);
                break;
                
            case 1: // Velocity
                var vel = (Velocity)component;
                writer.Write(vel.X);
                writer.Write(vel.Y);
                writer.Write(vel.Z);
                break;
                
            case 2: // Health
                var health = (Health)component;
                writer.Write(health.Current);
                writer.Write(health.Max);
                break;
                
            // ... all components
            
            default:
                throw new ArgumentException($"Unknown component type: {typeId}");
        }
    }
    
    public object Deserialize(BinaryReader reader, ComponentTypeId typeId)
    {
        return typeId.Id switch
        {
            0 => new Position
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            },
            
            1 => new Velocity
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            },
            
            2 => new Health
            {
                Current = reader.ReadInt32(),
                Max = reader.ReadInt32()
            },
            
            // ... all components
            
            _ => throw new ArgumentException($"Unknown component type: {typeId}")
        };
    }
}
```

### Snapshot Format

For cross-backend snapshots (RFC-0007):

```csharp
/// <summary>
/// Logical snapshot format - backend-independent
/// </summary>
public sealed class LogicalSnapshot
{
    public int Version { get; init; } = 1;
    public long Timestamp { get; init; }
    public string SourceBackend { get; init; } = "";
    
    /// <summary>
    /// Component type registry at snapshot time.
    /// Maps TypeId → (TypeName, StructureHash)
    /// </summary>
    public Dictionary<int, (string Name, ulong Hash)> TypeRegistry { get; init; } = new();
    
    /// <summary>
    /// Entities indexed by logical ID (not backend-specific ID)
    /// </summary>
    public Dictionary<int, EntitySnapshot> Entities { get; init; } = new();
}

public sealed class EntitySnapshot
{
    /// <summary>Logical entity ID for cross-backend reference</summary>
    public int LogicalId { get; init; }
    
    /// <summary>Components indexed by UnifyECS ComponentTypeId</summary>
    public Dictionary<int, byte[]> Components { get; init; } = new();
}
```

### Entity ID Mapping

Since backends assign different entity IDs, snapshots use logical IDs:

```csharp
public sealed class EntityIdMapper
{
    // Logical ID → Backend-specific ID
    private readonly Dictionary<int, Entity> _logicalToBackend = new();
    
    // Backend-specific ID → Logical ID  
    private readonly Dictionary<int, int> _backendToLogical = new();
    
    private int _nextLogicalId = 0;
    
    /// <summary>
    /// Register a backend entity and get its logical ID
    /// </summary>
    public int GetOrCreateLogicalId(Entity backendEntity)
    {
        if (_backendToLogical.TryGetValue(backendEntity.Id, out var logicalId))
            return logicalId;
        
        logicalId = _nextLogicalId++;
        _backendToLogical[backendEntity.Id] = logicalId;
        _logicalToBackend[logicalId] = backendEntity;
        return logicalId;
    }
    
    /// <summary>
    /// Get backend entity from logical ID
    /// </summary>
    public Entity GetBackendEntity(int logicalId)
        => _logicalToBackend[logicalId];
    
    /// <summary>
    /// Clear mappings (call when world is reset)
    /// </summary>
    public void Clear()
    {
        _logicalToBackend.Clear();
        _backendToLogical.Clear();
        _nextLogicalId = 0;
    }
}
```

---

## Cross-Assembly Components

When components are defined in different assemblies:

```csharp
// Assembly: MyGame.Core
[EcsComponent]
public struct Position { public float X, Y, Z; }

// Assembly: MyGame.Combat
[EcsComponent]
public struct Damage { public int Value; }
```

The source generator handles this via:

1. **Per-assembly partial registries**:

```csharp
// Generated in MyGame.Core: ComponentTypeRegistry.Core.g.cs
public static partial class ComponentTypeRegistry
{
    public static readonly ComponentTypeId Position = new(0, "MyGame.Core.Position", ...);
}

// Generated in MyGame.Combat: ComponentTypeRegistry.Combat.g.cs
public static partial class ComponentTypeRegistry
{
    public static readonly ComponentTypeId Damage = new(100, "MyGame.Combat.Damage", ...);
}
```

2. **ID namespacing**: Each assembly gets an ID range to avoid conflicts:

```xml
<!-- MyGame.Core.csproj -->
<PropertyGroup>
  <UnifyEcsComponentIdBase>0</UnifyEcsComponentIdBase>
</PropertyGroup>

<!-- MyGame.Combat.csproj -->
<PropertyGroup>
  <UnifyEcsComponentIdBase>100</UnifyEcsComponentIdBase>
</PropertyGroup>
```

3. **Merged registry at runtime**:

```csharp
// Generated in entry assembly: ComponentTypeRegistry.Merged.g.cs
public static partial class ComponentTypeRegistry
{
    static ComponentTypeRegistry()
    {
        // Merge all component registrations
        MergeFrom(MyGame.Core.ComponentTypeRegistry.GetAll());
        MergeFrom(MyGame.Combat.ComponentTypeRegistry.GetAll());
        
        ValidateNoIdConflicts();
    }
}
```

---

## Open Questions

## Non-Goals and Future Extensions

- **Component inheritance**: v1 assumes components are flat value types without inheritance in the registry model. If component inheritance is introduced in a future RFC, it MUST remain compatible with the existing ID and hash scheme.
- **Aliases and renaming**: v1 does not provide registry-level aliases for refactoring component names or namespaces. Refactor support and aliasing are explicitly deferred to a future extension built on top of `ComponentTypeId`.
- **Runtime registration and modding**: v1 focuses on compile-time registration; runtime registration patterns for modding or plugins are treated as an optional extension (e.g., via a separate `RuntimeComponentRegistry`) and are not required by the core registry.
- **StructureHash contents**: v1 treats `StructureHash` as based on structural layout (fields, order, types). Attribute metadata is not included. Future RFCs may extend the hashing scheme but MUST preserve the meaning of existing hashes.

## References

- RFC-0001: Core Architecture
- RFC-0003: Attribute API Design
- RFC-0005: Backend Adapters
- RFC-0007: Multi-Backend Orchestration
