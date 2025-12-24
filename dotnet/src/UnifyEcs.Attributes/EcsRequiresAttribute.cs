using System;

namespace UnifyECS
{
    /// <summary>
    /// Declares feature requirements for a system (RFC-0002, RFC-0006).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EcsRequiresAttribute : Attribute
    {
        public EcsFeature Features { get; }

        /// <summary>
        /// Behavior when a required feature is missing on the selected backend.
        /// </summary>
        public MissingFeatureBehavior IfMissing { get; set; } = MissingFeatureBehavior.Error;

        public EcsRequiresAttribute(EcsFeature features)
        {
            Features = features;
        }
    }
}
