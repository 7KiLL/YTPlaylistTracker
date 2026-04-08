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

    public bool TryCopyToClipboard(string text, out string? errorMessage)
    {
        var (cmd, args, hint) = ResolveClipboardCommand();
        if (cmd is null) { errorMessage = "No clipboard tool found for this platform"; return false; }

        try
        {
            var psi = new ProcessStartInfo(cmd, args)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) { errorMessage = $"Failed to start {cmd}. {hint}"; return false; }
            p.StandardInput.Write(text);
            p.StandardInput.Close();
            if (!p.WaitForExit(3000)) { errorMessage = $"{cmd} timed out"; return false; }
            if (p.ExitCode != 0) { errorMessage = $"{cmd} exited with code {p.ExitCode}. {hint}"; return false; }
            errorMessage = null;
            return true;
        }
        catch (System.ComponentModel.Win32Exception) { errorMessage = $"'{cmd}' not found. {hint}"; return false; }
        catch (Exception ex) { errorMessage = $"Clipboard failed: {ex.Message}"; return false; }
    }

    private static (string? Cmd, string Args, string Hint) ResolveClipboardCommand()
    {
        if (OperatingSystem.IsWindows())
            return ("clip", "", "clip.exe should be built into Windows");
        if (OperatingSystem.IsMacOS())
            return ("pbcopy", "", "pbcopy should be built into macOS");

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return ("wl-copy", "", "Install: sudo pacman -S wl-clipboard");
        return ("xclip", "-selection clipboard", "Install: sudo pacman -S xclip");
    }

    private static ProcessStartInfo CreateProcessStartInfo(string target) =>
        (OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()) switch
        {
            (true, _) => new("cmd", $"/c start \"\" \"{target}\"") { CreateNoWindow = true },
            (_, true) => new("open", target),
            _         => new("sh", $"-c \"xdg-open '{target}' >/dev/null 2>&1 &\""),
        };
}
