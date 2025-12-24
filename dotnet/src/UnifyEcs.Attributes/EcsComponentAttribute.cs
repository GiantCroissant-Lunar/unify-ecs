using System;

namespace UnifyECS
{
    /// <summary>
    /// Marks a struct as an ECS component (RFC-0003, RFC-0011).
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EcsComponentAttribute : Attribute
    {
        /// <summary>
        /// If true, component is treated as a tag (zero-size marker).
        /// </summary>
        public bool IsTag { get; set; }
    }

    /// <summary>
    /// Marks a component as managed (not DOTS-compatible). See RFC-0010.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class ManagedComponentAttribute : Attribute
    {
    }
}
