using Friflo.Engine.ECS;

namespace UnifyECS
{
    /// <summary>
    /// Friflo-specific backend system contract. Generated Friflo systems implement
    /// this interface so they can be executed against a Friflo.Engine.ECS.EntityStore world.
    /// </summary>
    public interface IFrifloSystem : IUnifySystem
    {
        /// <summary>
        /// Execute this system against provided Friflo entities for a single frame.
        /// </summary>
        void Execute(EntityStore entities, float deltaTime);
    }
}
