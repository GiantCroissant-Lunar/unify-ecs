using System.Collections.Generic;

namespace UnifyECS.Generators.Backends
{
    /// <summary>
    /// Entitas backend emitter skeleton (RFC-0005).
    /// Phase 1: this is a placeholder that reports capabilities
    /// and provides empty implementations for all emission methods.
    /// Actual Entitas generation will be implemented in later phases.
    /// </summary>
    internal sealed class EntitasBackendEmitter : IBackendEmitter
    {
        public EcsBackend Backend => EcsBackend.Entitas;

        public EcsFeature NativeFeatures => BackendCapabilities.GetNativeFeatures(EcsBackend.Entitas);

        public EcsFeature EmulatableFeatures => BackendCapabilities.GetEmulatableFeatures(EcsBackend.Entitas);

        public string EmitComponent(ComponentModel component)
        {
            // Phase 1: no Entitas component wrappers yet.
            return string.Empty;
        }

        public string EmitSystem(SystemModel system, FeatureSupportLevel level)
        {
            // Phase 1: Entitas system emission is not implemented yet.
            // This stub keeps the interface complete without affecting compilation.
            return string.Empty;
        }

        public string EmitWorldExtensions(IReadOnlyList<ComponentModel> components)
        {
            // Phase 1: no world extensions for Entitas.
            return string.Empty;
        }

        public string? EmitReactiveEmulation(SystemModel system)
        {
            // Phase 1: reactive emulation for Entitas will be added later.
            return null;
        }

        public string EmitBootstrap(IReadOnlyList<SystemModel> systems)
        {
            // Phase 1: bootstrap/runner generation for Entitas is not implemented.
            return string.Empty;
        }
    }
}
