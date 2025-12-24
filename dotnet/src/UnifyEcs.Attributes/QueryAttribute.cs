using System;

namespace UnifyECS
{
    /// <summary>
    /// Declares a query over entities for a system method (RFC-0011).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class QueryAttribute : Attribute
    {
        public Type[]? All { get; set; }
        public Type[]? Any { get; set; }
        public Type[]? None { get; set; }
        public Type[]? Exclusive { get; set; }

        /// <summary>
        /// If true, generator may cache the query for reuse across frames.
        /// </summary>
        public bool Cached { get; set; } = true;
    }
}
