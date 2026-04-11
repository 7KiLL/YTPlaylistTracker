using System.Runtime.CompilerServices;
using DiffEngine;

namespace YTPlaylistTracker.E2ETests;

internal static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        // Disable DiffEngine auto-launch when no GUI session is available
        // (CI, headless terminal, Claude Code). Rider users get diff tool when Rider is open.
        if (Environment.GetEnvironmentVariable("CI") is not null
            || Environment.GetEnvironmentVariable("DiffEngine_Disabled") is not null
            || !Environment.UserInteractive)
        {
            DiffRunner.Disabled = true;
        }

        // Store snapshots in Snapshots/ subdirectory relative to each test file
        DerivePathInfo(
            (sourceFile, _, type, method) =>
                new PathInfo(
                    directory: Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots"),
                    typeName: type.Name,
                    methodName: method.Name));
    }
}
