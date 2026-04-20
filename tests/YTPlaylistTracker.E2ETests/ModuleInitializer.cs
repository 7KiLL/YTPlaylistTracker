using System.Diagnostics;
using System.Runtime.CompilerServices;
using DiffEngine;

namespace YTPlaylistTracker.E2ETests;

internal static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        // Auto-launch the diff tool only when tests are actually executed by Rider's
        // test runner (i.e. an ancestor process is Rider/ReSharper). Running
        // `dotnet test` from a terminal or Claude Code must never trigger a popup,
        // even when the Rider IDE is open for editing.
        if (Environment.GetEnvironmentVariable("CI") is not null
            || Environment.GetEnvironmentVariable("DiffEngine_Disabled") is not null
            || !IsLaunchedByRider())
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

    private static bool IsLaunchedByRider()
    {
        // Walk the parent-process chain looking for Rider/ReSharper. Linux-only
        // ancestry via /proc; on other OSes, require the explicit env var below.
        if (!OperatingSystem.IsLinux())
        {
            return Environment.GetEnvironmentVariable("DiffEngine_Force") is not null;
        }

        try
        {
            int pid = Environment.ProcessId;
            for (int hops = 0; hops < 16 && pid > 1; hops++)
            {
                int ppid = ReadPpidFromProc(pid);
                if (ppid <= 0) return false;

                string? name = TryGetProcessName(ppid);
                if (name is not null &&
                    (name.Contains("rider", StringComparison.OrdinalIgnoreCase)
                     || name.Contains("resharper", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                pid = ppid;
            }
        }
        catch
        {
            // Process walk failed — err on the safe side (no popup).
        }
        return false;
    }

    private static int ReadPpidFromProc(int pid)
    {
        string statusPath = $"/proc/{pid}/status";
        if (!File.Exists(statusPath)) return -1;

        foreach (string line in File.ReadLines(statusPath))
        {
            if (line.StartsWith("PPid:", StringComparison.Ordinal)
                && int.TryParse(line.AsSpan("PPid:".Length).Trim(), out int ppid))
            {
                return ppid;
            }
        }
        return -1;
    }

    private static string? TryGetProcessName(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
