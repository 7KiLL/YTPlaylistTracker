namespace YTPlaylistTracker.Domain.Interfaces;

public interface IUserSettings
{
    bool AutoSyncOnStartup { get; set; }
    void Save();
}
