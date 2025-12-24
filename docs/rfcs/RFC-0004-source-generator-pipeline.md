# RFC-0004: Source Generator Pipeline

- **Status**: Draft
- **Created**: 2024-12-02
- **Authors**: UnifyECS Contributors
- **Depends On**: RFC-0001, RFC-0002, RFC-0003

## Summary

Define the source generator architecture that transforms UnifyECS attributes into backend-specific implementations at compile-time.

## Motivation

Source generation provides:
- Zero runtime overhead (no reflection)
- Compile-time validation of feature requirements
- Debuggable generated code
- Integration with IDE tooling

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Roslyn Compilation                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                 UnifyECS Incremental Generator                  │
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │   Parser    │──│  Analyzer   │──│    Backend Emitters     │ │
│  │             │  │             │  │                         │ │
│  │ • Symbols   │  │ • Features  │  │ • ArchEmitter           │ │
│  │ • Attribs   │  │ • Policies  │  │ • EntitasEmitter        │ │
│  │ • Types     │  │ • Compat    │  │ • DotsEmitter           │ │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
     *.Arch.g.cs      *.Entitas.g.cs    *.Dots.g.cs
```

## Generator Stages

### Stage 1: Discovery & Parsing

```csharp
[Generator]
public class UnifyEcsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Discover components
        var components = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "UnifyECS.EcsComponentAttribute",
                predicate: (node, _) => node is TypeDeclarationSyntax,
                transform: ParseComponent);
        
        // 2. Discover systems
        var systems = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "UnifyECS.EcsSystemAttribute",
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: ParseSystem);
        
        // 3. Load configuration
        var config = context.AnalyzerConfigOptionsProvider
            .Select(ParseMSBuildConfig);
        
        // 4. Combine and generate
        var combined = components.Collect()
            .Combine(systems.Collect())
            .Combine(config);
        
        context.RegisterSourceOutput(combined, Generate);
    }
}
```

### Stage 2: Model Building

```csharp
/// <summary>
/// Parsed component model
/// </summary>
public record ComponentModel
{
    public string Name { get; init; }
    public string Namespace { get; init; }
    public string FullName { get; init; }
    public bool IsTag { get; init; }
    public bool IsShared { get; init; }
    public ImmutableArray<FieldModel> Fields { get; init; }
}

/// <summary>
/// Parsed system model
/// </summary>
public record SystemModel
{
    public string Name { get; init; }
    public string Namespace { get; init; }
    public string FullName { get; init; }
    public SystemPhase Phase { get; init; }
    public int Order { get; init; }
    public EcsFeature RequiredFeatures { get; init; }
    public MissingFeatureBehavior MissingBehavior { get; init; }
    public ImmutableArray<QueryMethodModel> QueryMethods { get; init; }
    public ImmutableArray<ReactiveMethodModel> ReactiveMethods { get; init; }
    public ImmutableArray<InjectionModel> Injections { get; init; }
    public ImmutableDictionary<EcsBackend, OptimizationHints> Optimizations { get; init; }
}

/// <summary>
/// Parsed query method model
/// </summary>
public record QueryMethodModel
{
    public string MethodName { get; init; }
    public ImmutableArray<Type> AllComponents { get; init; }
    public ImmutableArray<Type> AnyComponents { get; init; }
    public ImmutableArray<Type> NoneComponents { get; init; }
    public ImmutableArray<ParameterModel> Parameters { get; init; }
    public bool IsCached { get; init; }
}
```

### Stage 3: Configuration Resolution

```csharp
/// <summary>
/// Generator configuration from MSBuild
/// </summary>
public record GeneratorConfig
{
    /// <summary>Backends to generate for</summary>
    public ImmutableArray<EcsBackend> Backends { get; init; }
    
    /// <summary>Global missing feature policy</summary>
    public MissingFeatureBehavior GlobalPolicy { get; init; }
    
    /// <summary>Output directory for generated files</summary>
    public string OutputPath { get; init; }
    
    /// <summary>Enable debug comments in generated code</summary>
    public bool DebugComments { get; init; }
}

// MSBuild configuration:
// <PropertyGroup>
//   <UnifyEcsBackends>Arch;Dots</UnifyEcsBackends>
//   <UnifyEcsMissingFeaturePolicy>Warn</UnifyEcsMissingFeaturePolicy>
//   <UnifyEcsDebugComments>true</UnifyEcsDebugComments>
// </PropertyGroup>

private static GeneratorConfig ParseMSBuildConfig(AnalyzerConfigOptionsProvider options)
{
    var globalOptions = options.GlobalOptions;
    
    globalOptions.TryGetValue("build_property.UnifyEcsBackends", out var backendsStr);
    globalOptions.TryGetValue("build_property.UnifyEcsMissingFeaturePolicy", out var policyStr);
    globalOptions.TryGetValue("build_property.UnifyEcsDebugComments", out var debugStr);
    
    var backends = (backendsStr ?? "Arch")
        .Split(';')
        .Select(s => Enum.Parse<EcsBackend>(s.Trim()))
        .ToImmutableArray();
    
    var policy = Enum.TryParse<MissingFeatureBehavior>(policyStr, out var p) 
        ? p 
        : MissingFeatureBehavior.Error;
    
    return new GeneratorConfig
    {
        Backends = backends,
        GlobalPolicy = policy,
        DebugComments = bool.TryParse(debugStr, out var d) && d
    };
}
```

### Stage 4: Feature Validation

```csharp
public static class FeatureValidator
{
    public record ValidationResult(
        bool IsValid,
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableDictionary<EcsBackend, FeatureSupportLevel> SupportLevels);
    
    public static ValidationResult Validate(
        SystemModel system,
        GeneratorConfig config)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var supportLevels = ImmutableDictionary.CreateBuilder<EcsBackend, FeatureSupportLevel>();
        var isValid = true;
        
        foreach (var backend in config.Backends)
        {
            var required = system.RequiredFeatures;
            var native = BackendCapabilities.GetNativeFeatures(backend);
            var emulatable = BackendCapabilities.GetEmulatableFeatures(backend);
            
            // Check native support
            if ((native & required) == required)
            {
                supportLevels[backend] = FeatureSupportLevel.Native;
                continue;
            }
            
            // Check emulation support
            var canEmulate = (native | emulatable) & required;
            if (canEmulate == required)
            {
                supportLevels[backend] = FeatureSupportLevel.Emulated;
                continue;
            }
            
            // Feature not supported - apply policy
            var missing = required & ~(native | emulatable);
            var behavior = system.MissingBehavior == MissingFeatureBehavior.UsePolicy
                ? config.GlobalPolicy
                : system.MissingBehavior;
            
            switch (behavior)
            {
                case MissingFeatureBehavior.Error:
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.UnsupportedFeature,
                        Location.None,
                        system.Name, backend, missing));
                    isValid = false;
                    break;
                    
                case MissingFeatureBehavior.Warn:
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.UnsupportedFeatureWarning,
                        Location.None,
                        system.Name, backend, missing));
                    supportLevels[backend] = FeatureSupportLevel.Unsupported;
                    break;
                    
                case MissingFeatureBehavior.NoOp:
                    supportLevels[backend] = FeatureSupportLevel.Unsupported;
                    break;
            }
        }
        
        return new ValidationResult(isValid, diagnostics.ToImmutable(), supportLevels.ToImmutable());
    }
}
```

### Stage 5: Code Emission

```csharp
public interface IBackendEmitter
{
    EcsBackend Backend { get; }
    
    string EmitComponent(ComponentModel component);
    string EmitSystem(SystemModel system, FeatureSupportLevel supportLevel);
    string EmitWorldExtensions(IEnumerable<ComponentModel> components);
}

public class ArchEmitter : IBackendEmitter
{
    public EcsBackend Backend => EcsBackend.Arch;
    
    public string EmitSystem(SystemModel system, FeatureSupportLevel supportLevel)
    {
        if (supportLevel == FeatureSupportLevel.Unsupported)
            return EmitStub(system);
        
        var sb = new StringBuilder();
        
        sb.AppendLine($"// <auto-generated/>");
        sb.AppendLine($"// UnifyECS Generated for Arch Backend");
        sb.AppendLine();
        sb.AppendLine($"namespace {system.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {system.Name}");
        sb.AppendLine("    {");
        
        // Emit query descriptions
        foreach (var query in system.QueryMethods)
        {
            EmitQueryDescription(sb, query);
        }
        
        // Emit execute method
        EmitExecuteMethod(sb, system);
        
        // Emit reactive emulation if needed
        if (supportLevel == FeatureSupportLevel.Emulated && 
            system.RequiredFeatures.HasFlag(EcsFeature.Reactive))
        {
            EmitReactiveEmulation(sb, system);
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private void EmitExecuteMethod(StringBuilder sb, SystemModel system)
    {
        sb.AppendLine();
        sb.AppendLine("        public void Execute(World world, float deltaTime)");
        sb.AppendLine("        {");
        sb.AppendLine("            this.DeltaTime = deltaTime;");
        
        foreach (var query in system.QueryMethods)
        {
            var queryName = $"_query_{query.MethodName}";
            var parameters = BuildParameterList(query);
            
            sb.AppendLine($"            world.Query(in {queryName}, ({parameters}) =>");
            sb.AppendLine("            {");
            sb.AppendLine($"                {query.MethodName}({BuildArgumentList(query)});");
            sb.AppendLine("            });");
        }
        
        sb.AppendLine("        }");
    }
}
```

## Generated Code Examples

### Input: Movement System

```csharp
[EcsSystem(Phase = SystemPhase.Update)]
public partial class MovementSystem
{
    [Inject] protected float DeltaTime { get; set; }
    
    [Query(All = new[] { typeof(Position), typeof(Velocity) },
           None = new[] { typeof(Dead) })]
    public void Process(ref Position pos, in Velocity vel)
    {
        pos.X += vel.X * DeltaTime;
        pos.Y += vel.Y * DeltaTime;
        pos.Z += vel.Z * DeltaTime;
    }
}
```

### Output: Arch Backend

```csharp
// <auto-generated/>
// UnifyECS Generated for Arch Backend
// Feature Support: Native

namespace MyGame.Systems
{
    partial class MovementSystem
    {
        private static readonly QueryDescription _query_Process = new QueryDescription()
            .WithAll<Position, Velocity>()
            .WithNone<Dead>();
        
        public void Execute(World world, float deltaTime)
        {
            this.DeltaTime = deltaTime;
            
            world.Query(in _query_Process, (ref Position pos, in Velocity vel) =>
            {
                Process(ref pos, in vel);
            });
        }
    }
}
```

### Output: Entitas Backend

```csharp
// <auto-generated/>
// UnifyECS Generated for Entitas Backend
// Feature Support: Native

namespace MyGame.Systems
{
    partial class MovementSystem : IExecuteSystem, IInitializeSystem
    {
        private GameContext _context;
        private IGroup<GameEntity> _group_Process;
        private List<GameEntity> _buffer_Process = new();
        
        public void Initialize(GameContext context)
        {
            _context = context;
            _group_Process = context.GetGroup(
                GameMatcher.AllOf(GameMatcher.Position, GameMatcher.Velocity)
                           .NoneOf(GameMatcher.Dead));
        }
        
        public void Execute()
        {
            _group_Process.GetEntities(_buffer_Process);
            
            foreach (var entity in _buffer_Process)
            {
                var pos = entity.position;
                var vel = entity.velocity;
                
                Process(ref pos, in vel);
                
                entity.ReplacePosition(pos.X, pos.Y, pos.Z);
            }
        }
    }
}
```

### Output: DOTS Backend

```csharp
// <auto-generated/>
// UnifyECS Generated for DOTS Backend
// Feature Support: Native

namespace MyGame.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    partial class MovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            this.DeltaTime = deltaTime;
            
            Entities
                .WithAll<Position, Velocity>()
                .WithNone<Dead>()
                .ForEach((ref Position pos, in Velocity vel) =>
                {
                    Process(ref pos, in vel);
                })
                .Schedule();
        }
    }
}
```

## Incremental Generation

The generator uses Roslyn's incremental generation for performance:

```csharp
// Only regenerate affected files when source changes
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    // Track individual components
    var components = context.SyntaxProvider
        .ForAttributeWithMetadataName(...)
        .WithTrackingName("Components");
    
    // Track individual systems
    var systems = context.SyntaxProvider
        .ForAttributeWithMetadataName(...)
        .WithTrackingName("Systems");
    
    // Generate per-system (incremental)
    context.RegisterSourceOutput(
        systems.Combine(config), 
        (ctx, data) => GenerateSystem(ctx, data.Left, data.Right));
    
    // Generate world extensions (batch)
    context.RegisterSourceOutput(
        components.Collect().Combine(config),
        (ctx, data) => GenerateWorldExtensions(ctx, data.Left, data.Right));
}
```

## Diagnostics

```csharp
public static class Diagnostics
{
    public static readonly DiagnosticDescriptor UnsupportedFeature = new(
        id: "UECS001",
        title: "Unsupported ECS Feature",
        messageFormat: "System '{0}' requires feature '{2}' which is not supported by backend '{1}'",
        category: "UnifyECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor UnsupportedFeatureWarning = new(
        id: "UECS002",
        title: "Unsupported ECS Feature (Stubbed)",
        messageFormat: "System '{0}' will be stubbed for backend '{1}' (missing '{2}')",
        category: "UnifyECS",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor InvalidQueryParameter = new(
        id: "UECS003",
        title: "Invalid Query Parameter",
        messageFormat: "Query method '{0}' has parameter '{1}' of type '{2}' which is not a registered component",
        category: "UnifyECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

## Open Questions

1. How to handle cross-assembly component references?
2. Should we support analyzers that run alongside the generator?
3. How to provide intellisense for generated members before generation completes?

## References

- [Roslyn Source Generators](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- RFC-0001: Core Architecture
- RFC-0003: Attribute API Design
