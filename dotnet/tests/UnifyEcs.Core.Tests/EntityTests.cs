using UnifyECS;
using Xunit;

namespace UnifyEcs.Core.Tests
{
    public class EntityTests
    {
        [Fact]
        public void NullEntity_IsInvalid()
        {
            Assert.False(Entity.Null.IsValid);
            Assert.Equal(-1, Entity.Null.Id);
        }

        [Fact]
        public void Entities_WithSameIdAndGeneration_AreEqual()
        {
            var a = new Entity(1, 2);
            var b = new Entity(1, 2);

            Assert.True(a == b);
            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Entities_WithDifferentIdOrGeneration_AreNotEqual()
        {
            var a = new Entity(1, 2);
            var b = new Entity(2, 2);
            var c = new Entity(1, 3);

            Assert.NotEqual(a, b);
            Assert.NotEqual(a, c);
        }
    }
}
