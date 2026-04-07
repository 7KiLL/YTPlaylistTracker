namespace YTPlaylistTracker.Infrastructure.Update;

public class UpdateException : Exception
{
    public string? ManualDownloadUrl { get; }

    public UpdateException(string message, string? manualDownloadUrl = null)
        : base(message) => ManualDownloadUrl = manualDownloadUrl;
}
