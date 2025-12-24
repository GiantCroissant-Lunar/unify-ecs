using System;

namespace UnifyECS
{
    /// <summary>
    /// Suppress specific UnifyECS diagnostics (RFC-0013).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class SuppressUnifyDiagnosticAttribute : Attribute
    {
        public string[] DiagnosticIds { get; }

        public string? Justification { get; set; }

        public SuppressUnifyDiagnosticAttribute(params string[] diagnosticIds)
        {
            DiagnosticIds = diagnosticIds;
        }
    }

    /// <summary>
    /// Optimization hints for specific backends (e.g., DOTS Burst/parallel, Arch inline queries).
    /// Mirrors RFC-0003 backend-specific hints.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class EcsOptimizeAttribute : Attribute
    {
        public EcsBackend Backend { get; }

        /// <summary>DOTS: Enable Burst compilation.</summary>
        public bool BurstCompile { get; set; }

        /// <summary>DOTS: Schedule as parallel job.</summary>
        public bool Parallel { get; set; }

        /// <summary>Arch: Use inline query (no delegate allocation).</summary>
        public bool InlineQuery { get; set; }

        public EcsOptimizeAttribute(EcsBackend backend)
        {
            Backend = backend;
        }
    }
}
