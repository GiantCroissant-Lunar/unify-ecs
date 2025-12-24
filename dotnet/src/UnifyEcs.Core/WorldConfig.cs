using System;

namespace UnifyECS
{
    public sealed class WorldConfig
    {
        public int InitialEntityCapacity { get; init; } = 1024;

        public string? Name { get; init; }

        public bool DebugMode { get; init; } = false;

        public static WorldConfig Default { get; } = new WorldConfig();
    }
}
