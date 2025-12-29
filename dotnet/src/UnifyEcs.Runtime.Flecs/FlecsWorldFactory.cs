using System;
using FlecsCore = Flecs.NET.Core;

namespace UnifyECS
{
    /// <summary>
    /// Flecs-specific world factory.
    /// Provides factory methods for creating Flecs worlds.
    /// </summary>
    public static class FlecsWorldFactory
    {
        /// <summary>
        /// Creates a new FlecsSystemRunner with a world.
        /// </summary>
        /// <param name="config">Optional world configuration.</param>
        /// <returns>A new FlecsSystemRunner.</returns>
        public static FlecsSystemRunner Create(WorldConfig? config = null)
        {
            var runner = new FlecsSystemRunner();
            return runner;
        }

        /// <summary>
        /// Creates a new FlecsSystemRunner with a pre-configured world.
        /// </summary>
        /// <param name="world">The Flecs world to use.</param>
        /// <returns>A new FlecsSystemRunner.</returns>
        public static FlecsSystemRunner CreateFromWorld(FlecsCore.World world)
        {
            return new FlecsSystemRunner(world);
        }
    }

    /// <summary>
    /// Flecs-specific world configuration.
    /// Currently a placeholder for future configuration options.
    /// </summary>
    public sealed class FlecsWorldConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether to enable threading.
        /// </summary>
        public bool EnableThreading { get; set; }

        /// <summary>
        /// Gets or sets number of worker threads.
        /// </summary>
        public int WorkerThreads { get; set; }

        /// <summary>
        /// Creates a new FlecsWorldConfig with default values.
        /// </summary>
        public FlecsWorldConfig()
        {
            EnableThreading = false;
            WorkerThreads = 1;
        }
    }
}
