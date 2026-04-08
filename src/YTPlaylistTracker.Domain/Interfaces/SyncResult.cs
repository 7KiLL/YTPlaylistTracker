namespace YTPlaylistTracker.Domain.Interfaces;

public record SyncResult(int Added, int Removed, int Updated);
