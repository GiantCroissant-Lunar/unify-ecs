using UnifyECS;
using UnifyEcs.Sample.ArchGame;
using Xunit;
using global::Arch.Core;
using global::Arch.Core.Extensions;
using global::Arch.Core.Utils;

namespace UnifyEcs.Runtime.Arch.Tests
{
    public class GeneratedArchSystemsTests
    {
        private static ArchWorld CreateWorld()
        {
            var config = new WorldConfig
            {
                Name = "GeneratedArchSystemsTests",
                InitialEntityCapacity = 128,
                DebugMode = false
            };

            return new ArchWorld(config);
        }

        [Fact]
        public void SpawnSystem_DeferredPlayback_CreatesEntitiesVisibleToArchWorld()
        {
            using var world = CreateWorld();
            var runner = new ArchSystemRunner(world);

            var entity = world.CreateEntity();
            world.Add<Position>(entity, new Position { X = 0f, Y = 0f });

            using var commands = new DefaultCommandBuffer();

            var spawn = new SpawnSystem
            {
                Commands = commands
            };

            runner.Register(spawn);
            runner.Initialize();

            Assert.Equal(1, world.EntityCount);

            runner.Update(1f / 60f);

            Assert.Equal(0, commands.CommandCount);
            Assert.True(world.EntityCount >= 2);
        }

        [Fact]
        public void SpawnWithVelocitySystem_DeferredPlayback_AddsPositionAndVelocity()
        {
            using var world = CreateWorld();
            var runner = new ArchSystemRunner(world);

            var entity = world.CreateEntity();
            world.Add<Position>(entity, new Position { X = 0f, Y = 0f });

            using var commands = new DefaultCommandBuffer();

            var spawn = new SpawnWithVelocitySystem
            {
                Commands = commands
            };

            runner.Register(spawn);
            runner.Initialize();

            Assert.Equal(1, world.EntityCount);

            runner.Update(1f / 60f);

            Assert.Equal(0, commands.CommandCount);
            Assert.True(world.EntityCount >= 2);

            // Verify that at least one entity has both Position and Velocity components in the underlying Arch world.
            var innerWorldProperty = typeof(ArchWorld).GetProperty(
                "InnerWorld",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            Assert.NotNull(innerWorldProperty);

            var innerWorld = (World)innerWorldProperty!.GetValue(world)!;
            Assert.NotNull(innerWorld);

            var query = new QueryDescription().WithAll<Position, Velocity>();
            var count = 0;

            innerWorld.Query(in query, (global::Arch.Core.Entity e, ref Position pos, ref Velocity vel) =>
            {
                count++;
            });

            Assert.True(count >= 1);
        }

        [Fact]
        public void WorldAdapter_CreateEntity_Params_AddsStructComponents()
        {
            using var world = CreateWorld();

            // Get underlying Arch.Core.World
            var innerWorldProperty = typeof(ArchWorld).GetProperty(
                "InnerWorld",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            Assert.NotNull(innerWorldProperty);

            var innerWorld = (World)innerWorldProperty!.GetValue(world)!;
            Assert.NotNull(innerWorld);

            // Use the generated nested WorldAdapter type from SpawnWithVelocitySystem
            var adapterType = typeof(SpawnWithVelocitySystem).GetNestedType(
                "WorldAdapter",
                System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(adapterType);

            var adapter = (IWorld)System.Activator.CreateInstance(adapterType!, innerWorld)!;

            using var buffer = new DefaultCommandBuffer();

            buffer.CreateEntity(
                new Position { X = 10f, Y = 20f },
                new Velocity { X = 1f, Y = 0f });

            Assert.Equal(1, buffer.CommandCount);

            buffer.Playback(adapter);

            // After playback, the inner Arch world should have at least one entity with both Position and Velocity.
            var query = new QueryDescription().WithAll<Position, Velocity>();
            var count = 0;

            innerWorld.Query(in query, (global::Arch.Core.Entity e, ref Position pos, ref Velocity vel) =>
            {
                count++;
            });

            Assert.Equal(1, count);
        }

        [Fact]
        public void DeferredAndImmediateSystems_InteractCorrectly()
        {
            using var world = CreateWorld();
            var runner = new ArchSystemRunner(world);

            var entity = world.CreateEntity();
            world.Add<Position>(entity, new Position { X = 0f, Y = 0f });

            using var commands = new DefaultCommandBuffer();

            var move = new MoveRightSystem();
            var spawn = new SpawnSystem { Commands = commands };
            var spawnWithVelocity = new SpawnWithVelocitySystem { Commands = commands };
            var kill = new KillIfTooFarSystem { World = world };

            runner.Register(move);
            runner.Register(spawn);
            runner.Register(spawnWithVelocity);
            runner.Register(kill);
            runner.Initialize();

            // Access underlying Arch world once for the Velocity check.
            var innerWorldProperty = typeof(ArchWorld).GetProperty(
                "InnerWorld",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

            Assert.NotNull(innerWorldProperty);

            var innerWorld = (World)innerWorldProperty!.GetValue(world)!;
            Assert.NotNull(innerWorld);

            var sawVelocityEntity = false;
            var iterations = 0;
            const int maxIterations = 500;

            while (world.EntityCount > 0 && iterations < maxIterations)
            {
                runner.Update(1f / 60f);

                if (!sawVelocityEntity)
                {
                    var query = new QueryDescription().WithAll<Position, Velocity>();
                    var count = 0;

                    innerWorld.Query(in query, (global::Arch.Core.Entity e, ref Position pos, ref Velocity vel) =>
                    {
                        count++;
                    });

                    if (count > 0)
                    {
                        sawVelocityEntity = true;
                    }
                }

                iterations++;
            }

            Assert.True(iterations > 0);
            Assert.True(sawVelocityEntity);
            Assert.Equal(0, world.EntityCount);
        }

        [Fact]
        public void MoveRightAndKillIfTooFar_ImmediateDestroy_RemovesEntity()
        {
            using var world = CreateWorld();
            var runner = new ArchSystemRunner(world);

            var entity = world.CreateEntity();
            world.Add<Position>(entity, new Position { X = 0f, Y = 0f });

            var move = new MoveRightSystem();
            var kill = new KillIfTooFarSystem
            {
                World = world
            };

            runner.Register(move);
            runner.Register(kill);
            runner.Initialize();

            var iterations = 0;
            const int maxIterations = 200;

            while (world.EntityCount > 0 && iterations < maxIterations)
            {
                runner.Update(1f / 60f);
                iterations++;
            }

            Assert.True(iterations > 0);
            Assert.Equal(0, world.EntityCount);
        }
    }
}
