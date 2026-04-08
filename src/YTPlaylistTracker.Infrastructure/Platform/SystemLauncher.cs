using System.Diagnostics;

namespace YTPlaylistTracker.Infrastructure.Platform;

public class SystemLauncher : ISystemLauncher
{
    public void OpenUrl(string url)
    {
        if (!url.StartsWith("http", StringComparison.Ordinal)) url = "https://" + url;
        Launch(url);
    }

    public void OpenPath(string path)
    {
        var target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (target is null) return;
        Launch(target);
    }

    private static void Launch(string target)
    {
        try
        {
            var psi = CreateProcessStartInfo(target);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using var p = Process.Start(psi);
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { Debug.WriteLine($"Could not open: {ex.Message}"); }
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(string target) =>
        (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()) switch
        {
            (true, _) => new("cmd", $"/c start \"\" \"{target}\"") { CreateNoWindow = true },
            (_, true) => new("open", target),
            _         => new("sh", $"-c \"xdg-open '{target}' >/dev/null 2>&1 &\""),
        };
}
