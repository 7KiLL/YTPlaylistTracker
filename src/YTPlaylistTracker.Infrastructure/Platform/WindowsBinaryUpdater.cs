using System.Diagnostics;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.Infrastructure.Platform;

public class WindowsBinaryUpdater(ILogger<WindowsBinaryUpdater> logger) : IBinaryUpdater
{
    public Task<string> ApplyAsync(string newBinaryPath, string currentBinaryPath)
    {
        if (!File.Exists(newBinaryPath) || new FileInfo(newBinaryPath).Length == 0)
            throw new UpdateException("Downloaded binary is missing or empty.");

        var scriptPath = Path.Combine(Path.GetTempPath(), "ytpt-update.cmd");

        try
        {
            var script = $"""
                @echo off
                timeout /t 2 /nobreak >nul
                copy /y "{newBinaryPath}" "{currentBinaryPath}"
                start "" "{currentBinaryPath}"
                del "%~f0"
                """;

            File.WriteAllText(scriptPath, script);

            logger.LogInformation("[Update] Launching update script: {Script}", scriptPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            return Task.FromResult("App will close and restart automatically.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new UpdateException("Permission denied. Try running ytpt as Administrator.");
        }
        catch (Exception ex)
        {
            throw new UpdateException($"Could not launch update script: {ex.Message}");
        }
    }
}
