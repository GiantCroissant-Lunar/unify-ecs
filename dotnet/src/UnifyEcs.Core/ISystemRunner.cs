using System;

namespace UnifyECS
{
    /// <summary>
    /// Orchestrates system execution for a single world (RFC-0008).
    /// </summary>
    public interface ISystemRunner : IDisposable
    {
        /// <summary>
        /// The world this runner is associated with.
        /// All registered systems are expected to operate on this world.
        /// </summary>
        IWorld World { get; }

        /// <summary>
        /// Register a single system instance with this runner.
        /// </summary>
        void Register(IUnifySystem system);

        /// <summary>
        /// Register one or more systems with this runner.
        /// Systems are ordered by phase/group/order as defined in RFC-0011.
        /// </summary>
        void Register(params IUnifySystem[] systems);

        /// <summary>
        /// Get a registered system by its concrete type, or null if it is not present.
        /// </summary>
        T? GetSystem<T>() where T : class, IUnifySystem;

        /// <summary>Perform dependency injection and one-time initialization.</summary>
        void Initialize();

        /// <summary>Execute one frame of simulation.</summary>
        void Update(float deltaTime);
    }
}
