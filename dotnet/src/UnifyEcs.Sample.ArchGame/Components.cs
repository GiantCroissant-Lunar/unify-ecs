using UnifyECS;

namespace UnifyEcs.Sample.ArchGame
{
    [EcsComponent]
    public struct Position
    {
        public float X;
        public float Y;
    }

    [EcsComponent]
    public struct Velocity
    {
        public float X;
        public float Y;
    }

    [EcsComponent]
    public partial struct Health
    {
        public int Value;
    }

    [EcsComponent]
    public struct Perception
    {
        public int VisibleCount;
    }
}
