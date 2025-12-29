using System;
using Friflo.Engine.ECS;

namespace UnifyECS
{
    /// <summary>
    /// Friflo-specific world factory.
    /// Provides factory methods for creating Friflo worlds and system runners.
    /// </summary>
    public static class FrifloWorldFactory
    {
        /// <summary>
        /// Creates a new FrifloSystemRunner with a world.
        /// </summary>
        /// <param name="config">Optional world configuration.</param>
        /// <returns>A new FrifloSystemRunner.</returns>
        public static FrifloSystemRunner Create(WorldConfig? config = null)
        {
            var world = new FrifloWorld(new EntityStore());
            return new FrifloSystemRunner(world);
        }

        /// <summary>
        /// Creates a new FrifloSystemRunner with a pre-configured world.
        /// </summary>
        /// <param name="world">The Friflo world to use.</param>
        /// <returns>A new FrifloSystemRunner.</returns>
        public static FrifloSystemRunner CreateFromWorld(FrifloWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            return new FrifloSystemRunner(world);
        }
    }
}
