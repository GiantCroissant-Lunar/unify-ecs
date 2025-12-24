using System;

namespace UnifyECS
{
    /// <summary>
    /// Marks a property or field for dependency injection (RFC-0011).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class InjectAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a property or field for system injection (other systems).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class InjectSystemAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a parameter as optional in query methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class OptionalAttribute : Attribute
    {
    }
}
