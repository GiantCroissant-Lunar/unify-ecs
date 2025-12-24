using UnifyECS;

namespace UnifyEcs.Attributes.Tests.Samples
{
    [EcsComponent]
    public partial struct SampleHealth
    {
        public int Current;
        public int Max;
    }

    [EcsComponent(IsTag = true)]
    public partial struct SampleDead
    {
    }

    [EcsComponent]
    public partial struct SampleFireDamage
    {
        public int Damage;
    }

    [EcsComponent]
    public partial struct SampleIceDamage
    {
        public int Damage;
    }

    [EcsSystem]
    public sealed partial class SampleArchQuerySystem
    {
        [Query(
            All = new[] { typeof(SampleHealth) },
            Any = new[] { typeof(SampleFireDamage), typeof(SampleIceDamage) },
            None = new[] { typeof(SampleDead) },
            Cached = false)]
        public void Process(
            Entity entity,
            ref SampleHealth health,
            [Optional] in SampleFireDamage? fire,
            [Optional] in SampleIceDamage? ice)
        {
            // Intentionally left blank; this type exists to drive source generation.
        }
    }
}
