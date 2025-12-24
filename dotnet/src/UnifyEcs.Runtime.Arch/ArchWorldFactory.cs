namespace UnifyECS
{
    /// <summary>
    /// Simple Arch world factory that creates ArchWorld instances.
    /// </summary>
    public sealed class ArchWorldFactory : IWorldFactory
    {
        public EcsBackend Backend => EcsBackend.Arch;

        public IWorld Create(WorldConfig config)
        {
            return new ArchWorld(config);
        }
    }
}
