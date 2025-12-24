using UnifyECS;
using Xunit;

namespace UnifyEcs.Core.Tests
{
    public class BackendCapabilitiesTests
    {
        [Fact]
        public void Arch_HasBasicFeatures()
        {
            var native = BackendCapabilities.GetNativeFeatures(EcsBackend.Arch);

            Assert.True(native.HasFlag(EcsFeature.EntityLifecycle));
            Assert.True(native.HasFlag(EcsFeature.ComponentOperations));
            Assert.True(native.HasFlag(EcsFeature.BasicQueries));
            Assert.True(native.HasFlag(EcsFeature.SystemExecution));
        }

        [Theory]
        [InlineData(EcsBackend.Arch, EcsFeature.Reactive, true)]
        [InlineData(EcsBackend.Dots, EcsFeature.Reactive, true)]
        [InlineData(EcsBackend.DefaultEcs, EcsFeature.Reactive, false)]
        public void Supports_RespectsNativeAndEmulatedFeatures(EcsBackend backend, EcsFeature feature, bool expected)
        {
            var result = BackendCapabilities.Supports(backend, feature, allowEmulation: true);
            Assert.Equal(expected, result);
        }
    }
}
