using Arch.Core;

namespace UnifyECS
{
    /// <summary>
    /// Arch-specific backend system contract. Generated Arch systems implement
    /// this interface so they can be executed against an Arch.Core.World.
    /// </summary>
    public interface IArchSystem : IUnifySystem
    {
        /// <summary>
        /// Execute this system against the provided Arch world for a single frame.
        /// </summary>
        void Execute(World world, float deltaTime);
    }
}
