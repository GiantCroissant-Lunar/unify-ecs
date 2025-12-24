using System;
using UnifyECS;
using Xunit;

namespace UnifyEcs.Attributes.Tests
{
    public class AttributeSmokeTests
    {
        [Fact]
        public void EcsComponentAttribute_CanBeAppliedToStruct()
        {
            var attr = (EcsComponentAttribute?)Attribute.GetCustomAttribute(
                typeof(SampleComponent), typeof(EcsComponentAttribute));

            Assert.NotNull(attr);
            Assert.True(attr!.IsTag);
        }

        [Fact]
        public void EcsSystemAttribute_DefaultsToUpdatePhase()
        {
            var attr = (EcsSystemAttribute?)Attribute.GetCustomAttribute(
                typeof(SampleSystem), typeof(EcsSystemAttribute));

            Assert.NotNull(attr);
            Assert.Equal(SystemPhase.Update, attr!.Phase);
            Assert.Equal(0, attr.Order);
        }

        [Fact]
        public void QueryAttribute_DefaultsToCached()
        {
            var method = typeof(SampleSystem).GetMethod(nameof(SampleSystem.Process));
            var attr = (QueryAttribute?)Attribute.GetCustomAttribute(method!, typeof(QueryAttribute));

            Assert.NotNull(attr);
            Assert.True(attr!.Cached);
            Assert.NotNull(attr.All);
            Assert.Single(attr.All!);
            Assert.Contains(typeof(SampleComponent), attr.All!);
        }

        [EcsComponent(IsTag = true)]
        private readonly partial struct SampleComponent { }

        [EcsSystem]
        private sealed partial class SampleSystem
        {
            [Query(All = new[] { typeof(SampleComponent) })]
            public void Process(Entity e, ref SampleComponent c) { }
        }
    }
}
