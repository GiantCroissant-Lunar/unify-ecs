namespace UnifyECS
{
    /// <summary>
    /// Interface for Flecs backend systems.
    /// Generated systems implement this interface and are registered
    /// with the FlecsSystemRunner.
    /// </summary>
    public interface IFlecsSystem : IUnifySystem
    {
        /// <summary>
        /// Execute the system against the given Flecs world.
        /// </summary>
        /// <param name="world">The Flecs world wrapper.</param>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        void Execute(FlecsWorld world, float deltaTime);
    }
}
