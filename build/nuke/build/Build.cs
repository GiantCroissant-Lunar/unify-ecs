using Nuke.Common;
using Nuke.Common.IO;
using UnifyBuild.Nuke;

class Build : UnifyBuildBase
{
    public static int Main() => Execute<Build>(x => x.CompileProjects);

    // RootDirectory is where .nuke directory is located (build/nuke)
    AbsolutePath RepoRoot => RootDirectory / ".." / "..";

    protected override BuildContext Context
        => BuildContextLoader.FromJson(RepoRoot, "build/build.config.json");
}
