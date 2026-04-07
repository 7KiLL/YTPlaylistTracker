namespace YTPlaylistTracker.Domain.Models;

public record UpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    string DownloadUrl,
    bool IsUpdateAvailable);
