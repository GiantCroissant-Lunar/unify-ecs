using System;

namespace UnifyECS
{
    /// <summary>
    /// Invoked when a component of the given type is added to an entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnAddedAttribute : Attribute
    {
        public Type ComponentType { get; }

        public OnAddedAttribute(Type componentType)
        {
            ComponentType = componentType;
        }
    }

    /// <summary>
    /// Invoked when a component of the given type is removed from an entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnRemovedAttribute : Attribute
    {
        public Type ComponentType { get; }

        public OnRemovedAttribute(Type componentType)
        {
            ComponentType = componentType;
        }
    }

    /// <summary>
    /// Invoked when a component of the given type changes value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnChangedAttribute : Attribute
    {
        public Type ComponentType { get; }

        public OnChangedAttribute(Type componentType)
        {
            ComponentType = componentType;
        }
    }
}
