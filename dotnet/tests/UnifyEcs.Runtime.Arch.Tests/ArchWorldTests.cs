using System;
using System.Reflection;
using Arch.Core;
using Arch.Core.Utils;
using UnifyECS;
using Xunit;

namespace UnifyEcs.Runtime.Arch.Tests
{
    public class ArchWorldTests
    {
        private static ArchWorld CreateWorld()
        {
            var config = new WorldConfig
            {
                Name = "TestArchWorld",
                InitialEntityCapacity = 16,
                DebugMode = false
            };
            return new ArchWorld(config);
        }

        [Fact]
        public void CreateAndDestroyEntity_UpdatesExistsAndEntityCount()
        {
            using var world = CreateWorld();

            Assert.Equal(0, world.EntityCount);

            var entity = world.CreateEntity();
            Assert.True(world.Exists(entity));
            Assert.Equal(1, world.EntityCount);

            world.DestroyEntity(entity);
            Assert.False(world.Exists(entity));
            Assert.Equal(0, world.EntityCount);
        }

        [Fact]
        public void RegisterEntity_AllowsExternalEntitiesToBeManagedViaIWorld()
        {
            using var world = CreateWorld();

            // Get underlying Arch.Core.World via reflection (internal InnerWorld property)
            var innerWorldProperty = typeof(ArchWorld).GetProperty(
                "InnerWorld",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.NotNull(innerWorldProperty);

            var innerWorld = (World)innerWorldProperty!.GetValue(world)!;
            Assert.NotNull(innerWorld);

            // Create an Arch.Core entity directly on the inner world
            var archEntity = innerWorld!.Create(Array.Empty<ComponentType>());

            // Create a matching UnifyECS.Entity handle
            var entity = new UnifyECS.Entity(archEntity.Id, archEntity.Version);

            ArchWorld.RegisterEntity(innerWorld, entity, archEntity);

            // The ArchWorld IWorld implementation should now be able to see and manage it
            Assert.True(world.Exists(entity));
            Assert.True(innerWorld.IsAlive(archEntity));

            world.DestroyEntity(entity);

            Assert.False(world.Exists(entity));
            Assert.False(innerWorld.IsAlive(archEntity));
        }

        [Fact]
        public void ArchWorldFactory_RegistersWithWorldFactory()
        {
            var factory = new ArchWorldFactory();
            WorldFactory.Register(EcsBackend.Arch, factory);

            using var world = (ArchWorld)WorldFactory.Create(EcsBackend.Arch, new WorldConfig
            {
                Name = "FactoryWorld",
                InitialEntityCapacity = 8,
                DebugMode = false
            });

            Assert.IsType<ArchWorld>(world);
        }
    }
}
