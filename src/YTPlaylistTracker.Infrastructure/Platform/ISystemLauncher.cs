namespace YTPlaylistTracker.Infrastructure.Platform;

public interface ISystemLauncher
{
    void OpenUrl(string url);
    void OpenPath(string path);

    bool TryCopyToClipboard(string text, out string? errorMessage);
}
