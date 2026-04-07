using System.Diagnostics;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.Infrastructure.Platform;

public class WindowsBinaryUpdater(ILogger<WindowsBinaryUpdater> logger) : IBinaryUpdater
{
    public async Task<string> ApplyAsync(string newBinaryPath, string currentBinaryPath)
    {
        if (!File.Exists(newBinaryPath) || new FileInfo(newBinaryPath).Length == 0)
            throw new UpdateException("Downloaded binary is missing or empty.");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"ytpt-update-{Path.GetRandomFileName()}.cmd");

        try
        {
            var tempDir = Path.GetDirectoryName(newBinaryPath);
            var script = $"""
                @echo off
                setlocal
                timeout /t 2 /nobreak >nul
                copy /y "{newBinaryPath}" "{currentBinaryPath}"
                if errorlevel 1 (
                    echo Update failed: could not copy new binary
                    exit /b 1
                )
                start "" "{currentBinaryPath}"
                rmdir /s /q "{tempDir}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(scriptPath, script).ConfigureAwait(false);

            logger.LogInformation("[Update] Launching update script: {Script}", scriptPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            return "App will close and restart automatically.";
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
