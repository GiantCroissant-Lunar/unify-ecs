using System;

namespace UnifyECS
{
    /// <summary>
    /// Types of operations that modify world structure (RFC-0012).
    /// </summary>
    public enum StructuralChangeType
    {
        CreateEntity,
        DestroyEntity,
        AddComponent,
        RemoveComponent,
        SetSharedComponent,
    }

    /// <summary>
    /// Mode for applying structural changes.
    /// </summary>
    public enum StructuralChangeMode
    {
        /// <summary>Changes are buffered and applied after query completes.</summary>
        Deferred,

        /// <summary>Changes are applied immediately (not safe for parallel DOTS).</summary>
        Immediate,

        /// <summary>Let generator choose based on backend capabilities.</summary>
        Auto
    }

    /// <summary>
    /// Indicates that a query method performs structural changes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class StructuralChangesAttribute : Attribute
    {
        public StructuralChangeMode Mode { get; set; } = StructuralChangeMode.Deferred;

        public StructuralChangeType[] Changes { get; set; } = Array.Empty<StructuralChangeType>();
    }

    /// <summary>
    /// Marks a system as requiring random entity access (disables parallel DOTS execution).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class RequiresRandomAccessAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies which DOTS ECB system to use for this system's commands.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CommandBufferSystemAttribute : Attribute
    {
        public Type SystemType { get; }

        public CommandBufferSystemAttribute(Type systemType)
        {
            SystemType = systemType;
        }
    }
}
