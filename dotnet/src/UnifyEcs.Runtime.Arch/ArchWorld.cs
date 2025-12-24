using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Utils;

namespace UnifyECS
{
    /// <summary>
    /// Arch-backed implementation of IWorld that adapts UnifyECS.Entity handles
    /// to Arch.Core.Entity and delegates operations to an underlying Arch.Core.World.
    /// </summary>
    public sealed class ArchWorld : IWorld
    {
        private readonly World _world;

        private static readonly ConditionalWeakTable<World, ConcurrentDictionary<Entity, Arch.Core.Entity>> _entityMaps = new();

        public ArchWorld(WorldConfig config)
        {
            // Use InitialEntityCapacity as an entityCapacity hint for Arch.
            _world = World.Create(entityCapacity: config.InitialEntityCapacity);
        }

        public static void RegisterEntity(World world, Entity entity, Arch.Core.Entity archEntity)
        {
            if (world is null) throw new ArgumentNullException(nameof(world));

            var map = _entityMaps.GetValue(world, static _ => new ConcurrentDictionary<Entity, Arch.Core.Entity>());
            map[entity] = archEntity;
        }

        public static bool TryGetArchEntity(World world, Entity entity, out Arch.Core.Entity archEntity)
        {
            if (world is null) throw new ArgumentNullException(nameof(world));

            if (!entity.IsValid)
            {
                archEntity = default;
                return false;
            }

            if (!_entityMaps.TryGetValue(world, out var map))
            {
                archEntity = default;
                return false;
            }

            return map.TryGetValue(entity, out archEntity);
        }

        private static void RemoveEntityMapping(World world, Entity entity)
        {
            if (!_entityMaps.TryGetValue(world, out var map))
            {
                return;
            }

            map.TryRemove(entity, out _);
        }

        private Arch.Core.Entity GetArchEntity(Entity entity)
        {
            if (!entity.IsValid)
            {
                throw new InvalidOperationException($"Entity {entity} does not belong to this ArchWorld or has been destroyed.");
            }

            if (!TryGetArchEntity(_world, entity, out var archEntity) || !_world.IsAlive(archEntity))
            {
                throw new InvalidOperationException($"Entity {entity} does not belong to this ArchWorld or has been destroyed.");
            }

            return archEntity;
        }

        public Entity CreateEntity()
        {
            // Create an entity with no components (empty signature).
            var archEntity = _world.Create(Array.Empty<ComponentType>());

            // Apply initial component values.

            var entity = new Entity(archEntity.Id, archEntity.Version);
            RegisterEntity(_world, entity, archEntity);
            return entity;
        }

        public Entity CreateEntity(params object[] components)
        {
            if (components == null || components.Length == 0)
            {
                return CreateEntity();
            }

            var types = new ComponentType[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    throw new ArgumentNullException(nameof(components), "Components cannot contain null values.");
                }

                types[i] = (ComponentType)component.GetType();
            }

            // Create entity with the desired component signature.
            var archEntity = _world.Create(types);

            // Apply initial component values.
            _world.SetRange(archEntity, components.AsSpan());

            var entity = new Entity(archEntity.Id, archEntity.Version);
            RegisterEntity(_world, entity, archEntity);
            return entity;
        }

        public bool Exists(Entity entity)
        {
            if (!entity.IsValid)
            {
                return false;
            }

            return TryGetArchEntity(_world, entity, out var archEntity) && _world.IsAlive(archEntity);
        }

        public void DestroyEntity(Entity entity)
        {
            if (!entity.IsValid)
            {
                return;
            }

            if (!TryGetArchEntity(_world, entity, out var archEntity))
            {
                return;
            }

            if (!_world.IsAlive(archEntity))
            {
                return;
            }

            _world.Destroy(archEntity);
            RemoveEntityMapping(_world, entity);
        }

        public ref T Add<T>(Entity entity, T component) where T : struct
        {
            var archEntity = GetArchEntity(entity);
            _world.Add(archEntity, in component);
            return ref _world.Get<T>(archEntity);
        }

        public ref T Add<T>(Entity entity) where T : struct
        {
            var archEntity = GetArchEntity(entity);
            _world.Add<T>(archEntity);
            return ref _world.Get<T>(archEntity);
        }

        public bool Has<T>(Entity entity) where T : struct
        {
            var archEntity = GetArchEntity(entity);
            return _world.Has<T>(archEntity);
        }

        public ref T Get<T>(Entity entity) where T : struct
        {
            var archEntity = GetArchEntity(entity);
            return ref _world.Get<T>(archEntity);
        }

        public bool TryGet<T>(Entity entity, out T component) where T : struct
        {
            var archEntity = GetArchEntity(entity);
            if (_world.TryGet<T>(archEntity, out var value))
            {
                component = value!;
                return true;
            }

            component = default;
            return false;
        }

        public void Remove<T>(Entity entity) where T : struct
        {
            var archEntity = GetArchEntity(entity);
            _world.Remove<T>(archEntity);
        }

        public int EntityCount => _world.Size;

        /// <summary>
        /// Exposes the underlying Arch.Core.World for backend-specific runners
        /// and adapters.
        /// </summary>
        public World InnerWorld => _world;

        public void Dispose()
        {
            _entityMaps.Remove(_world);
            _world.Dispose();
        }
    }
}
