namespace UnifyECS
{
    /// <summary>
    /// Behavior to apply when a required feature is not available on a backend (RFC-0006).
    /// </summary>
    public enum MissingFeatureBehavior
    {
        /// <summary>Emit a compile-time error.</summary>
        Error,

        /// <summary>Allow but emit a warning and a runtime stub.</summary>
        Warn,

        /// <summary>Silently disable the feature (dangerous).</summary>
        NoOp,

        /// <summary>Generate helper code to emulate the feature.</summary>
        Emulate
    }
}
