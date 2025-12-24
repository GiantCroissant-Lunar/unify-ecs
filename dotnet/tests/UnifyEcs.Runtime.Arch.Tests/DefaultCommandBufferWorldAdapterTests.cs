using System;
using UnifyECS;
using Xunit;
using global::Arch.Core;
using global::Arch.Core.Extensions;
using global::Arch.Core.Utils;

namespace UnifyEcs.Runtime.Arch.Tests
{
    public struct TestPosition
    {
        public float X;
    }

    internal sealed class TestWorldAdapter : IWorld
    {
        private readonly World _world;

        public TestWorldAdapter(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public UnifyECS.Entity CreateEntity()
        {
            var archEntity = _world.Create(Array.Empty<ComponentType>());
            var entity = new UnifyECS.Entity(archEntity.Id, archEntity.Version);
            ArchWorld.RegisterEntity(_world, entity, archEntity);
            return entity;
        }

        public UnifyECS.Entity CreateEntity(params object[] components)
        {
            if (components is null || components.Length == 0)
            {
                return CreateEntity();
            }

            var archEntity = _world.Create(Array.Empty<ComponentType>());
            var entity = new UnifyECS.Entity(archEntity.Id, archEntity.Version);
            ArchWorld.RegisterEntity(_world, entity, archEntity);

            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i] ?? throw new ArgumentNullException(nameof(components));
                AddComponent(entity, component);
            }

            return entity;
        }

        public bool Exists(UnifyECS.Entity entity)
        {
            if (!entity.IsValid)
            {
                return false;
            }

            return ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) && _world.IsAlive(archEntity);
        }

        public void DestroyEntity(UnifyECS.Entity entity)
        {
            if (!entity.IsValid)
            {
                return;
            }

            if (!ArchWorld.TryGetArchEntity(_world, entity, out var archEntity))
            {
                return;
            }

            if (!_world.IsAlive(archEntity))
            {
                return;
            }

            _world.Destroy(archEntity);
        }

        public ref T Add<T>(UnifyECS.Entity entity, T component) where T : struct
        {
            if (!ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) || !_world.IsAlive(archEntity))
            {
                throw new InvalidOperationException($"Entity {entity} does not belong to this TestWorldAdapter.");
            }

            _world.Add(archEntity, in component);
            return ref _world.Get<T>(archEntity);
        }

        public ref T Add<T>(UnifyECS.Entity entity) where T : struct
        {
            if (!ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) || !_world.IsAlive(archEntity))
            {
                throw new InvalidOperationException($"Entity {entity} does not belong to this TestWorldAdapter.");
            }

            _world.Add<T>(archEntity);
            return ref _world.Get<T>(archEntity);
        }

        public bool Has<T>(UnifyECS.Entity entity) where T : struct
        {
            if (!entity.IsValid)
            {
                return false;
            }

            return ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) && _world.IsAlive(archEntity) && _world.Has<T>(archEntity);
        }

        public ref T Get<T>(UnifyECS.Entity entity) where T : struct
        {
            if (!ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) || !_world.IsAlive(archEntity))
            {
                throw new InvalidOperationException($"Entity {entity} does not belong to this TestWorldAdapter.");
            }

            return ref _world.Get<T>(archEntity);
        }

        public bool TryGet<T>(UnifyECS.Entity entity, out T component) where T : struct
        {
            if (!entity.IsValid)
            {
                component = default;
                return false;
            }

            if (!ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) || !_world.IsAlive(archEntity))
            {
                component = default;
                return false;
            }

            if (_world.TryGet<T>(archEntity, out var value))
            {
                component = value!;
                return true;
            }

            component = default;
            return false;
        }

        public void Remove<T>(UnifyECS.Entity entity) where T : struct
        {
            if (!entity.IsValid)
            {
                return;
            }

            if (!ArchWorld.TryGetArchEntity(_world, entity, out var archEntity) || !_world.IsAlive(archEntity))
            {
                return;
            }

            _world.Remove<T>(archEntity);
        }

        public int EntityCount => _world.Size;

        public void Dispose()
        {
            // Adapter does not own the underlying world.
        }

        private void AddComponent(UnifyECS.Entity entity, object component)
        {
            if (component is null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var type = component.GetType();
            if (!type.IsValueType)
            {
                throw new InvalidOperationException("Arch backend only supports struct components in CreateEntity(params object[]).");
            }

            var method = typeof(TestWorldAdapter).GetMethod("AddBoxed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var generic = method!.MakeGenericMethod(type);
            generic.Invoke(this, new object[] { entity, component });
        }

        private void AddBoxed<T>(UnifyECS.Entity entity, object component) where T : struct
        {
            Add(entity, (T)component);
        }
    }

    public class DefaultCommandBufferWorldAdapterTests
    {
        private static ArchWorld CreateWorld()
        {
            var config = new WorldConfig
            {
                Name = "TestArchWorld_Buffer",
                InitialEntityCapacity = 16,
                DebugMode = false
            };
            return new ArchWorld(config);
        }

        [Fact]
        public void Playback_CreateEntity_AddsEntitiesVisibleToArchWorld()
        {
            using var archWorld = CreateWorld();

            // Get underlying Arch.Core.World via reflection to share between ArchWorld and adapter
            var innerWorldProperty = typeof(ArchWorld).GetProperty(
                "InnerWorld",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            Assert.NotNull(innerWorldProperty);

            var innerWorld = (World)innerWorldProperty!.GetValue(archWorld)!;
            Assert.NotNull(innerWorld);

            using var adapter = new TestWorldAdapter(innerWorld);
            using var buffer = new DefaultCommandBuffer();

            // Record a create-entity command with an initial TestPosition component
            buffer.CreateEntity(new TestPosition { X = 42f });

            Assert.Equal(1, buffer.CommandCount);
            Assert.Equal(0, archWorld.EntityCount);

            buffer.Playback(adapter);

            // After playback, ArchWorld should see the new entity/entities
            Assert.Equal(1, archWorld.EntityCount);

            var query = new QueryDescription().WithAll<TestPosition>();
            var count = 0;
            TestPosition? seen = null;

            innerWorld.Query(in query, (global::Arch.Core.Entity entity, ref TestPosition pos) =>
            {
                // The ArchWorld IWorld adapter should be able to see this entity
                var unify = new UnifyECS.Entity(entity.Id, entity.Version);
                Assert.True(archWorld.Exists(unify));

                ref var viaWorld = ref archWorld.Get<TestPosition>(unify);
                seen = viaWorld;
                count++;
            });

            Assert.Equal(1, count);
            Assert.True(seen.HasValue);
            Assert.Equal(42f, seen.Value.X);
        }

        [Fact]
        public void Playback_DestroyEntity_RemovesEntitiesFromArchWorld()
        {
            using var archWorld = CreateWorld();

            // Create an entity via ArchWorld so it is registered in the shared registry
            var entity = archWorld.CreateEntity();
            archWorld.Add<TestPosition>(entity, new TestPosition { X = 1f });

            Assert.True(archWorld.Exists(entity));
            Assert.Equal(1, archWorld.EntityCount);

            // Share the underlying Arch.Core.World between ArchWorld and the adapter
            var innerWorldProperty = typeof(ArchWorld).GetProperty(
                "InnerWorld",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            Assert.NotNull(innerWorldProperty);

            var innerWorld = (World)innerWorldProperty!.GetValue(archWorld)!;
            Assert.NotNull(innerWorld);

            using var adapter = new TestWorldAdapter(innerWorld);
            using var buffer = new DefaultCommandBuffer();

            // Record a deferred destroy of the existing entity
            buffer.DestroyEntity(entity);

            Assert.Equal(1, buffer.CommandCount);

            buffer.Playback(adapter);

            // After playback, the entity should be gone from both ArchWorld and the inner Arch world
            Assert.False(archWorld.Exists(entity));
            Assert.Equal(0, archWorld.EntityCount);

            var query = new QueryDescription().WithAll<TestPosition>();
            var count = 0;

            innerWorld.Query(in query, (global::Arch.Core.Entity e, ref TestPosition pos) =>
            {
                count++;
            });

            Assert.Equal(0, count);
        }
    }
}
