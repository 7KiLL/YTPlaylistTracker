# Architecture

## Clean Architecture Layers

```
Domain (no deps) ← Application (Domain) ← Infrastructure (Domain+App) ← UI (all)
```

### Domain (`YTPlaylistTracker.Domain`)
Pure C# entities and interfaces. Zero external dependencies.
- **Entities**: `Profile`, `Playlist`, `Video`
- **Enums**: `RemovalReason` (Unknown, Deleted, Private, Unlisted, RemovedByOwner)
- **Interfaces**: `IYouTubeApiService`, `ISyncService`, `IPlaylistRepository`, `IProfileRepository`

### Application (`YTPlaylistTracker.Application`)
Business logic and use cases. Depends only on Domain.
- **SyncService**: Core diff algorithm — compares YouTube API snapshot against DB state, detects additions, removals, re-additions, and updates
- **PlaylistUrlParser**: Extracts and validates playlist IDs from full YouTube URLs or bare IDs

### Infrastructure (`YTPlaylistTracker.Infrastructure`)
External concerns. Depends on Domain + Application.
- **AppDbContext**: EF Core SQLite with Fluent API configuration
- **Repositories**: `PlaylistRepository`, `ProfileRepository` — EF Core implementations
- **YouTubeApiService**: Google API client wrapper supporting embedded OAuth2, file-based OAuth2, and API key auth. Handles pagination.
- **AppSettings**: Cross-platform path resolution for DB, logs, OAuth tokens. Sets restrictive file permissions (700/600) on Linux/macOS.

### UI (`YTPlaylistTracker.UI`)
Composition root and presentation. Depends on all layers.
- **Program.cs**: System.CommandLine CLI entry point with `ui`, `login`, `logout`, `sync`, `status`, `reset` subcommands
- **MainWindow**: Terminal.Gui three-pane layout (Profiles → Playlists → Videos)
- **SettingsDialog**: DB path display, purge deleted videos
- **RemovedVideosDialog**: Filtered view of removed videos

## Data Flow

```
YouTube API → SyncService (diff) → PlaylistRepository → SQLite DB
                                            ↓
                                   TUI (MainWindow) ← reads from DB
```

## Auth Flow

```
ytpt login → Browser opens → Google consent → Token stored locally → Auto-refresh
```

Three auth modes (in priority order):
1. **Embedded OAuth2**: Build-time injected client ID/secret. Users run `ytpt login`. Primary for end-users.
2. **File-based OAuth2**: `client_secrets.json` in app data dir. For development.
3. **API key**: `YOUTUBE_API_KEY` env var. Public playlists only.

## Security

- OAuth tokens stored in `~/.local/share/ytpt/oauth-tokens/` with `700` permissions
- Database file set to `600` (owner read/write only)
- OAuth client ID/secret injected at build time via MSBuild properties, never in source code
- All DB queries via EF Core LINQ (parameterized, no raw SQL)

## Key Design Decisions

1. **Soft-delete over separate tables**: Videos have nullable `DeletedAt`. Simpler model, no data duplication.
2. **Profile-per-account**: Supports multiple YouTube accounts with isolated playlists.
3. **Terminal.Gui v2**: Modern TUI framework with command-based input, declarative layout, and built-in accessibility.
4. **System.CommandLine for CLI**: Auto-generates help, supports subcommands, minimal boilerplate.
5. **Serilog with rolling file**: Structured logging, defaults to Information level. `--verbose` flag for debug.
6. **EF Core Migrations**: Auto-applied on startup. Legacy v0.1.0 databases (created with `EnsureCreated`) are auto-detected and upgraded.
