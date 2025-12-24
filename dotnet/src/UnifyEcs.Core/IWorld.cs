using System;

namespace UnifyECS
{
    /// <summary>
    /// Backend-agnostic world API as seen by systems and game code.
    /// This is the portable abstraction described in RFC-0008 and RFC-0011.
    /// </summary>
    public interface IWorld : IDisposable
    {
        /// <summary>Create a new empty entity.</summary>
        Entity CreateEntity();

        /// <summary>
        /// Create a new entity with initial components.
        /// For hot paths prefer CreateEntity() + Add&lt;T&gt;() calls.
        /// </summary>
        Entity CreateEntity(params object[] components);

        /// <summary>Returns true if the entity exists in this world.</summary>
        bool Exists(Entity entity);

        /// <summary>Destroy the entity and all its components.</summary>
        void DestroyEntity(Entity entity);

        /// <summary>Add or replace a component on an entity.</summary>
        ref T Add<T>(Entity entity, T component) where T : struct;

        /// <summary>Add a default-initialized component to an entity.</summary>
        ref T Add<T>(Entity entity) where T : struct;

        /// <summary>Returns true if the entity has the component.</summary>
        bool Has<T>(Entity entity) where T : struct;

        /// <summary>Get a reference to a component. Throws if missing.</summary>
        ref T Get<T>(Entity entity) where T : struct;

        /// <summary>Try get a copy of a component value.</summary>
        bool TryGet<T>(Entity entity, out T component) where T : struct;

        /// <summary>Remove a component from an entity.</summary>
        void Remove<T>(Entity entity) where T : struct;

        /// <summary>Number of live entities in the world.</summary>
        int EntityCount { get; }
    }
}
