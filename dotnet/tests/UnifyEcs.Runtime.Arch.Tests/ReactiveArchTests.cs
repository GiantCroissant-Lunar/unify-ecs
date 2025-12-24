using UnifyECS;
using UnifyEcs.Sample.ArchGame;
using Xunit;

namespace UnifyEcs.Runtime.Arch.Tests
{
    public sealed class ReactiveArchTests
    {
        private static ArchWorld CreateWorld()
        {
            var config = new WorldConfig
            {
                Name = "ReactiveArchTests",
                InitialEntityCapacity = 16,
                DebugMode = false
            };

            return new ArchWorld(config);
        }

        [Fact]
        public void OnAdded_And_OnChanged_FireWithExpectedSemantics()
        {
            using var world = CreateWorld();
            var runner = new ArchSystemRunner(world);
            var system = new HealthReactiveSystem();

            runner.Register(system);
            runner.Initialize();

            // Frame 1: create entity with Health
            var e = world.CreateEntity();
            world.Add<Health>(e, new Health { Value = 10 });

            runner.Update(1f / 60f);

            // First frame after creation: Health appears => OnAdded once
            Assert.Equal(1, system.AddedCount);
            Assert.Equal(0, system.ChangedCount);

            // Frame 2: no value change => no extra Added/Changed
            runner.Update(1f / 60f);
            Assert.Equal(1, system.AddedCount);
            Assert.Equal(0, system.ChangedCount);

            // Frame 3: mutate Health value
            ref var health = ref world.Get<Health>(e);
            health.Value = 42;

            runner.Update(1f / 60f);

            // Value changed => OnChanged once
            Assert.Equal(1, system.AddedCount);
            Assert.Equal(1, system.ChangedCount);
        }

        [Fact]
        public void OnRemoved_FiresWhenComponentOrEntityDisappears()
        {
            using var world = CreateWorld();
            var runner = new ArchSystemRunner(world);
            var system = new HealthReactiveSystem();

            runner.Register(system);
            runner.Initialize();

            var e = world.CreateEntity();
            world.Add<Health>(e, new Health { Value = 10 });

            // First update: component appears => OnAdded
            runner.Update(1f / 60f);
            Assert.Equal(1, system.AddedCount);
            Assert.Equal(0, system.RemovedCount);

            // Destroy entity before next frame
            world.DestroyEntity(e);

            runner.Update(1f / 60f);

            // Health was present in previous snapshot but not now => OnRemoved
            Assert.Equal(1, system.AddedCount);
            Assert.Equal(1, system.RemovedCount);
        }
    }
}
