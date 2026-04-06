using System.Diagnostics;

namespace YTPlaylistTracker.Infrastructure.Platform;

public interface IBrowserLauncher
{
    void Open(string url);
}

public class BrowserLauncher : IBrowserLauncher
{
    public void Open(string url)
    {
        if (!url.StartsWith("http")) url = "https://" + url;

        try
        {
            var psi = CreateProcessStartInfo(url);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using var p = Process.Start(psi);
        }
        catch
        {
            // Fallback: UseShellExecute (works on most platforms but may pollute stdout)
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex2) { Debug.WriteLine($"Could not open browser: {ex2.Message}"); }
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(string url) =>
        (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()) switch
        {
            (true, _) => new("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true },
            (_, true) => new("open", url),
            _         => new("sh", $"-c \"xdg-open '{url}' >/dev/null 2>&1 &\""),
        };
}
