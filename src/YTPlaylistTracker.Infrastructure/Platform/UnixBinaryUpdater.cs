using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.Infrastructure.Platform;

#pragma warning disable CA1416 // Platform compatibility — only instantiated on Unix via DI
public class UnixBinaryUpdater(ILogger<UnixBinaryUpdater> logger) : IBinaryUpdater
{
    public Task<string> ApplyAsync(string newBinaryPath, string currentBinaryPath)
    {
        if (!File.Exists(newBinaryPath) || new FileInfo(newBinaryPath).Length == 0)
            throw new UpdateException("Downloaded binary is missing or empty.");

        var backupPath = currentBinaryPath + ".old";

        try
        {
            logger.LogInformation("[Update] Renaming {Current} → {Backup}", currentBinaryPath, backupPath);
            File.Move(currentBinaryPath, backupPath, overwrite: true);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UpdateException(
                $"Permission denied. The binary at {currentBinaryPath} is not writable. Try: sudo ytpt update");
        }

        try
        {
            logger.LogInformation("[Update] Moving {New} → {Current}", newBinaryPath, currentBinaryPath);
            File.Move(newBinaryPath, currentBinaryPath);

            File.SetUnixFileMode(currentBinaryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            try { File.Delete(backupPath); }
            catch { /* non-fatal */ }

            logger.LogInformation("[Update] Binary replaced successfully");
            return Task.FromResult("Update complete. Please restart ytpt.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Update] Failed to move new binary, rolling back");
            try { File.Move(backupPath, currentBinaryPath, overwrite: true); }
            catch (Exception rollbackEx)
            {
                logger.LogError(rollbackEx, "[Update] Rollback also failed!");
            }

            throw new UpdateException($"Could not replace binary: {ex.Message}");
        }
    }
}
