#if !NET5_0_OR_GREATER
using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Provides support for module initializers when targeting frameworks
    /// that do not ship the attribute in the BCL (e.g., netstandard2.1).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
