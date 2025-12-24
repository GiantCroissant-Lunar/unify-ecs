using System.Runtime.CompilerServices;

namespace UnifyECS
{
    internal static class ArchBackendRegistration
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            WorldFactory.Register(EcsBackend.Arch, new ArchWorldFactory());
        }
    }
}
