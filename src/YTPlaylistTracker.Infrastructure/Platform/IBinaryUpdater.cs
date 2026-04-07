namespace YTPlaylistTracker.Infrastructure.Platform;

public interface IBinaryUpdater
{
    Task<string> ApplyAsync(string newBinaryPath, string currentBinaryPath);
}
