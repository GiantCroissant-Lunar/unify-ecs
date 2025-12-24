using System;

namespace UnifyECS
{
    /// <summary>
    /// Tiny legacy sample that wires ArchWorld, ArchSystemRunner, and a single Arch system.
    ///
    /// This helper is kept only for internal smoke-testing. For real examples,
    /// prefer the dedicated UnifyEcs.Sample.ArchGame project which uses
    /// [EcsSystem]-generated Arch systems end-to-end.
    /// </summary>
    [Obsolete("Use the UnifyEcs.Sample.ArchGame project instead.")]
    internal static class ArchSampleGame
    {
        public static void RunOneFrame()
        {
            // 1. Create an Arch-backed world via the unified WorldFactory.
            var world = (ArchWorld)WorldFactory.Create(
                EcsBackend.Arch,
                new WorldConfig
                {
                    Name = "ArchSampleWorld",
                    InitialEntityCapacity = 128,
                    DebugMode = false
                });

            // 2. Create an ArchSystemRunner bound to this world.
            var runner = new ArchSystemRunner(world);

            // 3. Register a single sample system.
            runner.Register(new SampleCounterSystem());

            // 4. Initialize and run one frame.
            runner.Initialize();
            runner.Update(1f / 60f);

            // 5. Cleanup.
            runner.Dispose();
            world.Dispose();
        }
    }
}
