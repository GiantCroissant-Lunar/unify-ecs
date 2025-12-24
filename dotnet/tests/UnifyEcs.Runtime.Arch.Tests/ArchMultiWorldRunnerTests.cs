using System;
using System.Collections.Generic;
using Arch.Core;
using UnifyECS;
using UnifyECS.MultiWorld;
using Xunit;

namespace UnifyEcs.Runtime.Arch.Tests
{
    public sealed class ArchMultiWorldRunnerTests
    {
        private sealed class CountingSystem : IArchSystem
        {
            public readonly List<World> Worlds = new List<World>();

            public void Execute(World world, float deltaTime)
            {
                if (world == null) throw new ArgumentNullException(nameof(world));
                Worlds.Add(world);
            }
        }

        private sealed class MultiWorldCounter : IMultiWorldSystem
        {
            public readonly List<string> SeenWorlds = new List<string>();

            public void Update(IWorldSet worlds, float deltaTime)
            {
                foreach (var id in worlds.Ids)
                {
                    SeenWorlds.Add(id);
                }
            }
        }

        [Fact]
        public void Worlds_GetOrCreate_ReturnsSameInstancePerId()
        {
            var runner = new ArchMultiWorldRunner();
            var set = runner.Worlds;

            var w1 = set.GetOrCreate("Game");
            var w2 = set.GetOrCreate("Game");

            Assert.Same(w1, w2);
        }

        [Fact]
        public void Register_PerWorldSystems_ExecutesAgainstCorrectWorlds()
        {
            var runner = new ArchMultiWorldRunner();

            var systemGame = new CountingSystem();
            var systemUi = new CountingSystem();

            runner.Register("Game", systemGame);
            runner.Register("UI", systemUi);

            runner.Initialize();
            runner.Update(0.016f);

            Assert.Single(systemGame.Worlds);
            Assert.Single(systemUi.Worlds);
            Assert.NotSame(systemGame.Worlds[0], systemUi.Worlds[0]);
        }

        [Fact]
        public void Register_MultiWorldSystem_SeesAllWorldIds()
        {
            var runner = new ArchMultiWorldRunner();

            var systemGame = new CountingSystem();
            var systemUi = new CountingSystem();
            var multi = new MultiWorldCounter();

            runner.Register("Game", systemGame);
            runner.Register("UI", systemUi);
            runner.Register(multi);

            runner.Initialize();
            runner.Update(0.016f);

            Assert.Contains("Game", multi.SeenWorlds);
            Assert.Contains("UI", multi.SeenWorlds);
        }
    }
}
