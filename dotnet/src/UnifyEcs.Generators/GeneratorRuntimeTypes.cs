using System;

namespace UnifyECS.Generators;

internal enum EcsBackend
{
    Arch,
    Entitas,
    Flecs,
    Dots,
    DefaultEcs,
    Friflo,
    LeoEcs,
    SveltoEcs,
    Custom
}

[Flags]
internal enum EcsFeature
{
    None = 0,
    EntityLifecycle = 1 << 0,
    ComponentOperations = 1 << 1,
    BasicQueries = 1 << 2,
    SystemExecution = 1 << 3,
    Basic = EntityLifecycle | ComponentOperations | BasicQueries | SystemExecution,
    Events = 1 << 4,
    AdvancedFiltering = 1 << 5,
    TagComponents = 1 << 6,
    SystemGroups = 1 << 7,
    Reactive = 1 << 8,
    Relationships = 1 << 9,
    Jobs = 1 << 10,
    WorldEvents = 1 << 11,
    BurstCompile = 1 << 12,
    SharedComponents = 1 << 13,
    ChunkIteration = 1 << 14,
    ComponentPools = 1 << 15,
}

internal enum FeatureSupportLevel
{
    Native,
    Emulated,
    Unsupported
}

internal enum MissingFeatureBehavior
{
    Error,
    Warn,
    NoOp,
    Emulate
}

internal enum SystemPhase
{
    Initialization = 0,
    EarlyUpdate = 100,
    Update = 200,
    LateUpdate = 300,
    Cleanup = 400,
}

internal enum StructuralChangeType
{
    CreateEntity,
    DestroyEntity,
    AddComponent,
    RemoveComponent,
    SetSharedComponent,
}

internal enum StructuralChangeMode
{
    Deferred,
    Immediate,
    Auto
}

internal static class BackendCapabilities
{
    public static EcsFeature GetNativeFeatures(EcsBackend backend)
    {
        switch (backend)
        {
            case EcsBackend.Arch:
                return EcsFeature.Basic |
                       EcsFeature.AdvancedFiltering |
                       EcsFeature.TagComponents |
                       EcsFeature.Events |
                       EcsFeature.SystemGroups;
            case EcsBackend.Entitas:
                return EcsFeature.Basic |
                       EcsFeature.AdvancedFiltering |
                       EcsFeature.TagComponents |
                       EcsFeature.Events |
                       EcsFeature.Reactive |
                       EcsFeature.SystemGroups;
            case EcsBackend.Flecs:
                return EcsFeature.Basic |
                       EcsFeature.AdvancedFiltering |
                       EcsFeature.TagComponents |
                       EcsFeature.Events |
                       EcsFeature.Reactive |
                       EcsFeature.Relationships |
                       EcsFeature.Jobs |
                       EcsFeature.WorldEvents |
                       EcsFeature.SystemGroups;
            case EcsBackend.Dots:
                return EcsFeature.Basic |
                       EcsFeature.AdvancedFiltering |
                       EcsFeature.TagComponents |
                       EcsFeature.Jobs |
                       EcsFeature.BurstCompile |
                       EcsFeature.SharedComponents |
                       EcsFeature.ChunkIteration |
                       EcsFeature.SystemGroups |
                       EcsFeature.Relationships;
            case EcsBackend.DefaultEcs:
                return EcsFeature.Basic |
                       EcsFeature.AdvancedFiltering |
                       EcsFeature.TagComponents |
                       EcsFeature.Events |
                       EcsFeature.Relationships |
                       EcsFeature.WorldEvents;
            case EcsBackend.Friflo:
                return EcsFeature.Basic |
                       EcsFeature.AdvancedFiltering |
                       EcsFeature.TagComponents |
                       EcsFeature.Events |
                       EcsFeature.Relationships |
                       EcsFeature.Jobs;
            default:
                return EcsFeature.Basic;
        }
    }

    public static EcsFeature GetEmulatableFeatures(EcsBackend backend)
    {
        switch (backend)
        {
            case EcsBackend.Arch:
                return EcsFeature.Reactive | EcsFeature.Relationships;
            case EcsBackend.Entitas:
                return EcsFeature.WorldEvents;
            case EcsBackend.Flecs:
                return EcsFeature.None;
            case EcsBackend.Friflo:
                return EcsFeature.Reactive;
            case EcsBackend.Dots:
                return EcsFeature.Events | EcsFeature.Reactive;
            default:
                return EcsFeature.None;
        }
    }

    public static FeatureSupportLevel GetSupportLevel(EcsBackend backend, EcsFeature feature)
    {
        var native = GetNativeFeatures(backend);
        if ((native & feature) == feature)
        {
            return FeatureSupportLevel.Native;
        }

        var emulatable = GetEmulatableFeatures(backend);
        return (emulatable & feature) == feature
            ? FeatureSupportLevel.Emulated
            : FeatureSupportLevel.Unsupported;
    }
}
