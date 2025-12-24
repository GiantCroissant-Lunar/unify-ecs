namespace UnifyECS
{
    /// <summary>
    /// High-level system phases, backend-agnostic.
    /// Mirrors RFC-0011.
    /// </summary>
    public enum SystemPhase
    {
        /// <summary>Run once at world startup.</summary>
        Initialization = 0,

        /// <summary>Run before main update (input processing).</summary>
        EarlyUpdate = 100,

        /// <summary>Main game logic.</summary>
        Update = 200,

        /// <summary>Run after main update (reactions, constraints).</summary>
        LateUpdate = 300,

        /// <summary>Cleanup, prepare for next frame.</summary>
        Cleanup = 400,
    }
}
