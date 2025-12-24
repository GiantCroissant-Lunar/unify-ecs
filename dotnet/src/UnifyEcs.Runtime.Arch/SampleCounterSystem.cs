using System;
using Arch.Core;

namespace UnifyECS
{
    /// <summary>
    /// Legacy minimal Arch system used by ArchSampleGame to demonstrate ArchSystemRunner.
    ///
    /// This type is retained only for internal smoke-testing. For real usage,
    /// write [EcsSystem]-based systems in a consumer project (see
    /// UnifyEcs.Sample.ArchGame) and rely on the Arch backend generator to
    /// implement IArchSystem.
    /// </summary>
    [Obsolete("Use [EcsSystem]-based systems in UnifyEcs.Sample.ArchGame instead.")]
    internal sealed class SampleCounterSystem : IArchSystem
    {
        private int _ticks;

        public void Execute(World world, float deltaTime)
        {
            _ticks++;
            Console.WriteLine($"[ArchSample] Tick {_ticks}, dt={deltaTime:0.0000}");
        }
    }
}
