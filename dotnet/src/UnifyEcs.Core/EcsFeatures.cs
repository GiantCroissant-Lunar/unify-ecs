using System;

namespace UnifyECS
{
    /// <summary>
    /// Supported ECS backends.
    /// </summary>
    public enum EcsBackend
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

    /// <summary>
    /// Feature flags describing backend capabilities.
    /// Mirrors RFC-0002.
    /// </summary>
    [Flags]
    public enum EcsFeature
    {
        None = 0,

        // Tier 1: Universal
        EntityLifecycle = 1 << 0,
        ComponentOperations = 1 << 1,
        BasicQueries = 1 << 2,
        SystemExecution = 1 << 3,

        /// <summary>
        /// Combination of all Tier 1 features.
        /// </summary>
        Basic = EntityLifecycle | ComponentOperations | BasicQueries | SystemExecution,

        // Tier 2: Common
        Events = 1 << 4,
        AdvancedFiltering = 1 << 5,
        TagComponents = 1 << 6,
        SystemGroups = 1 << 7,

        // Tier 3: Advanced
        Reactive = 1 << 8,
        Relationships = 1 << 9,
        Jobs = 1 << 10,
        WorldEvents = 1 << 11,

        // Tier 4: Specialized
        BurstCompile = 1 << 12,
        SharedComponents = 1 << 13,
        ChunkIteration = 1 << 14,
        ComponentPools = 1 << 15,
    }

    public enum FeatureSupportLevel
    {
        /// <summary>Feature is natively supported with full performance.</summary>
        Native,

        /// <summary>Feature can be emulated but may have performance overhead.</summary>
        Emulated,

        /// <summary>Feature cannot be supported on this backend.</summary>
        Unsupported
    }

    /// <summary>
    /// Backend capability registry (RFC-0002).
    /// </summary>
    public static class BackendCapabilities
    {
        public static EcsFeature GetNativeFeatures(EcsBackend backend) => backend switch
        {
            EcsBackend.Arch =>
                EcsFeature.Basic |
                EcsFeature.AdvancedFiltering |
                EcsFeature.TagComponents |
                EcsFeature.Events |
                EcsFeature.SystemGroups,

            EcsBackend.Entitas =>
                EcsFeature.Basic |
                EcsFeature.AdvancedFiltering |
                EcsFeature.TagComponents |
                EcsFeature.Events |
                EcsFeature.Reactive |
                EcsFeature.SystemGroups,

            EcsBackend.Flecs =>
                EcsFeature.Basic |
                EcsFeature.AdvancedFiltering |
                EcsFeature.TagComponents |
                EcsFeature.Events |
                EcsFeature.Reactive |
                EcsFeature.Relationships |
                EcsFeature.Jobs |
                EcsFeature.WorldEvents |
                EcsFeature.SystemGroups,

            EcsBackend.Dots =>
                EcsFeature.Basic |
                EcsFeature.AdvancedFiltering |
                EcsFeature.TagComponents |
                EcsFeature.Jobs |
                EcsFeature.BurstCompile |
                EcsFeature.SharedComponents |
                EcsFeature.ChunkIteration |
                EcsFeature.SystemGroups |
                EcsFeature.Relationships,

            EcsBackend.DefaultEcs =>
                EcsFeature.Basic |
                EcsFeature.AdvancedFiltering |
                EcsFeature.TagComponents |
                EcsFeature.Events |
                EcsFeature.Relationships |
                EcsFeature.WorldEvents,

            EcsBackend.Friflo =>
                EcsFeature.Basic |
                EcsFeature.AdvancedFiltering |
                EcsFeature.TagComponents |
                EcsFeature.Events |
                EcsFeature.Relationships |
                EcsFeature.Jobs,

            _ => EcsFeature.Basic
        };

        public static EcsFeature GetEmulatableFeatures(EcsBackend backend) => backend switch
        {
            EcsBackend.Arch =>
                EcsFeature.Reactive |
                EcsFeature.Relationships,

            EcsBackend.Entitas =>
                EcsFeature.WorldEvents,

            EcsBackend.Flecs =>
                EcsFeature.None,

            EcsBackend.Friflo =>
                EcsFeature.Reactive,

            EcsBackend.Dots =>
                EcsFeature.Events |
                EcsFeature.Reactive,

            _ => EcsFeature.None
        };

        public static bool Supports(EcsBackend backend, EcsFeature feature, bool allowEmulation = true)
        {
            var native = GetNativeFeatures(backend);
            if ((native & feature) == feature) return true;

            if (allowEmulation)
            {
                var emulatable = GetEmulatableFeatures(backend);
                return (emulatable & feature) == feature;
            }

            return false;
        }

        public static FeatureSupportLevel GetSupportLevel(EcsBackend backend, EcsFeature feature)
        {
            var native = GetNativeFeatures(backend);
            if ((native & feature) == feature)
                return FeatureSupportLevel.Native;

            var emulatable = GetEmulatableFeatures(backend);
            if ((emulatable & feature) == feature)
                return FeatureSupportLevel.Emulated;

            return FeatureSupportLevel.Unsupported;
        }
    }
}
