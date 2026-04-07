namespace YTPlaylistTracker.Domain.Interfaces;

public interface IUserSettings
{
    bool AutoSyncOnStartup { get; set; }
    bool CheckForUpdatesOnStartup { get; set; }
    bool SortTrackedFirst { get; set; }
    void Save();
}
