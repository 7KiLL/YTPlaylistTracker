using System.Runtime.CompilerServices;

namespace YTPlaylistTracker.E2ETests;

internal static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        // Store snapshots in Snapshots/ subdirectory relative to each test file
        DerivePathInfo(
            (sourceFile, _, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
    }
}
