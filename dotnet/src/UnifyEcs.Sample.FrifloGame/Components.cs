using UnifyECS;
using Friflo.Engine.ECS;

namespace UnifyEcs.Sample.FrifloGame
{
    [EcsComponent]
    public struct SamplePosition : IComponent
    {
        public float X;
        public float Y;
    }

    [EcsComponent]
    public struct Velocity : IComponent
    {
        public float X;
        public float Y;
    }

    [EcsComponent]
    public partial struct Health : IComponent
    {
        public int Value;
    }

    [EcsComponent]
    public struct Perception : IComponent
    {
        public int VisibleCount;
    }
}
