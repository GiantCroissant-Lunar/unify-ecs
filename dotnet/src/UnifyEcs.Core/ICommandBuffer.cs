using System;

namespace UnifyECS
{
    /// <summary>
    /// Buffer for deferred structural changes (RFC-0012).
    /// Commands are executed at a synchronization point.
    /// </summary>
    public interface ICommandBuffer : IDisposable
    {
        /// <summary>Create a new entity (returns temporary handle).</summary>
        Entity CreateEntity();

        /// <summary>
        /// Create entity with initial components.
        /// ⚠ PERFORMANCE NOTE: This overload boxes components and uses runtime
        /// type lookup. Use for debug tools, editor, or low-volume cases only.
        /// For hot paths, use CreateEntity() + Add&lt;T&gt;() calls instead.
        /// </summary>
        Entity CreateEntity(params object[] components);

        /// <summary>Destroy an entity.</summary>
        void DestroyEntity(Entity entity);

        /// <summary>Add component to entity.</summary>
        void Add<T>(Entity entity, T component) where T : struct;

        /// <summary>Add tag component.</summary>
        void Add<T>(Entity entity) where T : struct;

        /// <summary>Remove component from entity.</summary>
        void Remove<T>(Entity entity) where T : struct;

        /// <summary>Set/replace component value.</summary>
        void Set<T>(Entity entity, T component) where T : struct;

        /// <summary>Number of pending commands.</summary>
        int CommandCount { get; }

        /// <summary>Execute all buffered commands against the given world.</summary>
        void Playback(IWorld world);

        /// <summary>Clear all pending commands without executing them.</summary>
        void Clear();
    }
}
