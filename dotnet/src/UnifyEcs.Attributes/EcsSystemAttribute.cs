using System;

namespace UnifyECS
{
    /// <summary>
    /// Marks a class as an ECS system (RFC-0003, RFC-0011).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EcsSystemAttribute : Attribute
    {
        public SystemPhase Phase { get; set; } = SystemPhase.Update;

        /// <summary>Execution order within the phase (lower runs earlier).</summary>
        public int Order { get; set; }

        /// <summary>Optional system group type.</summary>
        public Type? Group { get; set; }
    }
}
