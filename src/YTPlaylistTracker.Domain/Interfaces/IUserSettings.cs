namespace YTPlaylistTracker.Domain.Interfaces;

public interface IUserSettings
{
    bool AutoSyncOnStartup { get; set; }
    bool AutoInstallUpdates { get; set; }
    bool SortTrackedFirst { get; set; }
    string ThemeName { get; set; }
    void Save();
}
