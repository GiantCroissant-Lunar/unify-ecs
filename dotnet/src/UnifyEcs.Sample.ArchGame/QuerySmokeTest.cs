using Arch.Core;
using Arch.Core.Utils;

namespace UnifyEcs.Sample.ArchGame
{
    internal static class QuerySmokeTest
    {
        public static void Test(World world)
        {
            var q = new QueryDescription();

            // Minimal Arch query using Position to mirror MoveRightSystem.
            world.Query(in q, (ref Position pos) =>
            {
                pos.X += 1f;
            });
        }
    }
}
