using UnifyECS;

namespace UnifyEcs.Attributes.Tests.Samples
{
    [EcsSystem]
    public sealed partial class SampleArchNoFilterSystem
    {
        // No All/Any/None/Exclusive filters; exercises the no-filter Arch path.
        [Query(Cached = false)]
        public void ProcessAll(Entity entity)
        {
            // Intentionally empty; exists only to drive Arch generation.
        }
    }
}
