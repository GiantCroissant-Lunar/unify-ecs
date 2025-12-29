using System;
using System.Collections.Generic;
using FlecsCore = Flecs.NET.Core;

namespace UnifyECS
{
    /// <summary>
    /// Flecs-specific world implementation.
    /// Wraps a Flecs World and provides entity registration/tracking.
    /// </summary>
    public sealed class FlecsWorld : IWorld
    {
        private readonly FlecsCore.World _world;
        private readonly Dictionary<int, FlecsCore.Entity> _unifyToFlecs;
        private readonly Dictionary<ulong, int> _flecsToUnifyId;
        private int _nextEntityId;

        /// <summary>
        /// Gets the underlying Flecs World.
        /// </summary>
        public FlecsCore.World World => _world;

        /// <summary>
        /// Gets the entity count.
        /// </summary>
        public int EntityCount => _unifyToFlecs.Count;

        /// <summary>
        /// Creates a new FlecsWorld.
        /// </summary>
        public FlecsWorld()
        {
            _world = new FlecsCore.World();
            _unifyToFlecs = new Dictionary<int, FlecsCore.Entity>();
            _flecsToUnifyId = new Dictionary<ulong, int>();
            _nextEntityId = 1;
        }

        /// <summary>
        /// Creates a new FlecsWorld wrapping an existing world.
        /// </summary>
        /// <param name="world">Existing Flecs World.</param>
        public FlecsWorld(FlecsCore.World world)
        {
            _world = world;
            _unifyToFlecs = new Dictionary<int, FlecsCore.Entity>();
            _flecsToUnifyId = new Dictionary<ulong, int>();
            _nextEntityId = 1;
        }

        /// <summary>
        /// Creates a new FlecsWorld with configuration.
        /// </summary>
        /// <param name="config">World configuration.</param>
        public FlecsWorld(FlecsWorldConfig config)
        {
            _world = new FlecsCore.World();
            // TODO: Apply config
            _unifyToFlecs = new Dictionary<int, FlecsCore.Entity>();
            _flecsToUnifyId = new Dictionary<ulong, int>();
            _nextEntityId = 1;
        }

        public UnifyECS.Entity CreateEntity()
        {
            var flecsEntity = _world.Entity();
            
            var entityId = _nextEntityId++;
            var entity = new UnifyECS.Entity(entityId, 0);

            _unifyToFlecs[entityId] = flecsEntity;
            _flecsToUnifyId[flecsEntity.Id] = entityId;

            return entity;
        }

        public UnifyECS.Entity CreateEntity(params object[] components)
        {
            if (components is null || components.Length == 0)
            {
                return CreateEntity();
            }

            var flecsEntity = _world.Entity();
            
            var entityId = _nextEntityId++;
            var entity = new UnifyECS.Entity(entityId, 0);

            _unifyToFlecs[entityId] = flecsEntity;
            _flecsToUnifyId[flecsEntity.Id] = entityId;

            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null) continue;
                
                SetComponentDynamic(flecsEntity, component);
            }

            return entity;
        }

        private void SetComponentDynamic(FlecsCore.Entity flecsEntity, object component)
        {
            var type = component.GetType();
            // Invoke flecsEntity.Set<T>(component) via reflection
            var method = typeof(FlecsCore.Entity).GetMethod("Set", new[] { type });
            if (method != null)
            {
                method.Invoke(flecsEntity, new[] { component });
            }
        }

        public bool Exists(UnifyECS.Entity entity)
        {
            if (_unifyToFlecs.TryGetValue(entity.Id, out var flecsEntity))
            {
                return flecsEntity.IsAlive();
            }
            return false;
        }

        public void DestroyEntity(UnifyECS.Entity entity)
        {
            if (_unifyToFlecs.TryGetValue(entity.Id, out var flecsEntity))
            {
                flecsEntity.Destruct();
                _unifyToFlecs.Remove(entity.Id);
                _flecsToUnifyId.Remove(flecsEntity.Id);
            }
        }

        public ref T Add<T>(UnifyECS.Entity entity, T component) where T : struct
        {
            var flecsEntity = ToFlecs(entity);
            flecsEntity.Set(component);
            return ref flecsEntity.GetMut<T>();
        }

        public ref T Add<T>(UnifyECS.Entity entity) where T : struct
        {
            var flecsEntity = ToFlecs(entity);
            flecsEntity.Set<T>(default);
            return ref flecsEntity.GetMut<T>();
        }

        public bool Has<T>(UnifyECS.Entity entity) where T : struct
        {
            var flecsEntity = ToFlecs(entity);
            return flecsEntity.Has<T>();
        }

        public ref T Get<T>(UnifyECS.Entity entity) where T : struct
        {
            var flecsEntity = ToFlecs(entity);
            return ref flecsEntity.GetMut<T>();
        }

        public bool TryGet<T>(UnifyECS.Entity entity, out T component) where T : struct
        {
            var flecsEntity = ToFlecs(entity);
            if (flecsEntity.Has<T>())
            {
                component = flecsEntity.Get<T>();
                return true;
            }
            
            component = default;
            return false;
        }

        public void Remove<T>(UnifyECS.Entity entity) where T : struct
        {
            var flecsEntity = ToFlecs(entity);
            flecsEntity.Remove<T>();
        }

        public void Dispose()
        {
            _world.Dispose();
            _unifyToFlecs.Clear();
            _flecsToUnifyId.Clear();
        }

        private FlecsCore.Entity ToFlecs(UnifyECS.Entity entity)
        {
            if (_unifyToFlecs.TryGetValue(entity.Id, out var flecsEntity))
            {
                return flecsEntity;
            }

            throw new InvalidOperationException($"Entity {entity.Id} does not belong to this world or has been destroyed.");
        }
        
        public static UnifyECS.Entity MapEntity(FlecsWorld world, FlecsCore.Entity flecsEntity)
        {
             return world.GetUnifyEntity(flecsEntity);
        }

        public UnifyECS.Entity GetUnifyEntity(FlecsCore.Entity flecsEntity)
        {
            if (_flecsToUnifyId.TryGetValue(flecsEntity.Id, out var id))
            {
                return new UnifyECS.Entity(id, 0);
            }
            return default;
        }
    }
}
