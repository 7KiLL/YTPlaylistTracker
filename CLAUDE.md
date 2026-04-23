# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> ⚠️ **Discard pre-2025 Terminal.Gui training data.** Terminal.Gui v2 is a complete rewrite. If your training cutoff predates 2025, almost every TG API you "remember" (`Application.Top`, `Toplevel`, `Bounds`, `Clicked`, `RadioGroup`, `ColorScheme`, `MainLoop.AddTimeout`, …) is wrong. Read `docs/tui/README.md` (v1→v2 corrections table) and the relevant `docs/tui/*.md` page before touching any UI code.

## Project Overview

**ytpt** (YouTube Playlist Tracker) is a .NET 10 TUI application that tracks YouTube playlists and detects when videos are removed, deleted, or made private — preserving their titles and metadata.

**Target platforms**: Linux (x64, arm64), macOS (x64, arm64), Windows (x64).

## Build & Run Commands

```bash
dotnet build                                    # Build all projects
dotnet test                                     # Run all unit + integration tests
dotnet run --project src/YTPlaylistTracker.UI   # Launch TUI
dotnet run --project src/YTPlaylistTracker.UI -- login         # Sign in with Google (OAuth2)
dotnet run --project src/YTPlaylistTracker.UI -- logout        # Sign out
dotnet run --project src/YTPlaylistTracker.UI -- sync          # Sync all tracked playlists (headless)
dotnet run --project src/YTPlaylistTracker.UI -- sync <id>     # Sync specific playlist
dotnet run --project src/YTPlaylistTracker.UI -- status        # Show tracking summary
dotnet run --project src/YTPlaylistTracker.UI -- reset [--yes]  # Delete database and start fresh
dotnet run --project src/YTPlaylistTracker.UI -- export        # Export removed videos (CSV to stdout)
dotnet run --project src/YTPlaylistTracker.UI -- export --format json --output report.json  # Export as JSON to file
dotnet run --project src/YTPlaylistTracker.UI -- ui            # Launch TUI (explicit)
dotnet run --project src/YTPlaylistTracker.UI -- --verbose     # Verbose logging (any command)
# Run a specific test class (TUnit uses --treenode-filter, NOT xUnit's --filter)
cd tests/YTPlaylistTracker.UnitTests && dotnet run --no-build -- --treenode-filter "/*/*/*SyncServiceTests/*"
# Run a single test method:
cd tests/YTPlaylistTracker.UnitTests && dotnet run --no-build -- --treenode-filter "/*/*/*SyncServiceTests/MyMethodName"
```

### Build with OAuth credentials (for releases)
```bash
dotnet build -p:OAuthClientId=xxx -p:OAuthClientSecret=yyy
```

### Global Tool Install
```bash
dotnet pack src/YTPlaylistTracker.UI -c Release
dotnet tool install -g --add-source src/YTPlaylistTracker.UI/bin/Release ytpt
ytpt                  # Launch TUI
ytpt ui               # Launch TUI (explicit)
ytpt login            # Sign in with Google
ytpt logout           # Sign out
ytpt sync             # Sync all tracked playlists
ytpt sync <id>        # Sync specific playlist
ytpt status           # Show summary
ytpt reset [--yes]    # Delete database and start fresh
ytpt export           # Export removed videos report (CSV to stdout)
ytpt export -f json -o report.json  # Export as JSON to file
ytpt --verbose        # Add to any command for debug logging
```

## Architecture (Clean Architecture)

```
Domain (no deps) ← Application (Domain) ← Infrastructure (Domain+App) ← UI (all)
```

- **Domain** (`src/YTPlaylistTracker.Domain/`)
  - Entities: Profile, Playlist, Video
  - Models: YouTubeVideoSnapshot, YouTubePlaylistSnapshot
  - Enums: RemovalReason
  - Interfaces: IPlaylistRepository, IProfileRepository, ISyncService, IYouTubeApiService
- **Application** (`src/YTPlaylistTracker.Application/`): SyncService (diff algorithm), PlaylistUrlParser, sync helpers
- **Infrastructure** (`src/YTPlaylistTracker.Infrastructure/`)
  - Data: EF Core SQLite (AppDbContext), repositories (PlaylistRepository, ProfileRepository)
  - YouTube: YouTubeApiService (OAuth2, API key, lazy-init)
  - Platform: ISystemLauncher (cross-platform URL/path opener)
  - Configuration: AppSettings, build-time constants
- **UI** (`src/YTPlaylistTracker.UI/`): Terminal.Gui TUI (MainWindow, dialogs), System.CommandLine CLI

## Versioning & Releases

- **Auto-versioning**: MinVer reads git tags. Tag `v0.3.0` → version `0.3.0`. No manual .csproj edits needed.
- **Release**: Push a `v*` tag → GitHub Actions builds 5 platform binaries, creates GitHub Release with install scripts.
- **Build matrix**: linux-x64, linux-arm64, osx-arm64, osx-x64, win-x64.

## Database Migrations

Uses EF Core Migrations (not `EnsureCreated`). To add a new migration after schema changes:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/YTPlaylistTracker.Infrastructure \
  --startup-project src/YTPlaylistTracker.UI \
  --output-dir Data/Migrations
```

Legacy v0.1.0 databases (created with `EnsureCreated`) are auto-detected and upgraded on first run.

## Key Patterns

- **Soft-delete**: Videos have nullable `DeletedAt` + `RemovalReason` enum. No separate removal history table.
- **Snapshot models**: YouTubeVideoSnapshot and YouTubePlaylistSnapshot are lightweight DTOs for API responses, containing metadata (Title, Description, ThumbnailUrl, Position, PublishedAt, JsonMetadata).
- **Entity fields**: 
  - Video: YouTubeVideoId, Title, ChannelTitle, Position, Description, ThumbnailUrl, AddedAt, DeletedAt, RemovalReason, JsonMetadata
  - Playlist: YouTubePlaylistId, Title, Description, ThumbnailUrl, PublishedAt, IsTracked, LastSyncedAt, JsonMetadata
- **Profile system**: Multi-account support. Each profile has its own playlists and OAuth token.
- **Data paths**: DB/logs/tokens stored in `~/.local/share/ytpt/` (Linux), `~/Library/Application Support/ytpt/` (macOS), `%LOCALAPPDATA%/ytpt/` (Windows). Resolved by `AppSettings.AppDataDir`.
- **YouTube auth**: Embedded OAuth2 (build-time injected client ID/secret) is primary. `YOUTUBE_API_KEY` env var as fallback for public playlists only. `YTPT_CLIENT_ID`/`YTPT_CLIENT_SECRET` env vars override embedded credentials.
- **Lazy YouTube API**: LazyYouTubeApiProxy defers service initialization until first use, avoiding startup hang when not authenticated.
- **Build-time secrets**: OAuth client ID/secret injected via `-p:OAuthClientId=xxx` MSBuild properties, compiled into `BuildConstants.cs` — never in source code.
- **Shared build config**: `src/Directory.Build.props` (TargetFramework, MinVer), `tests/Directory.Build.props` (shared test packages).
- **File permissions**: All data dirs set to `700`, DB file to `600` on Linux/macOS.
- **Cross-platform system launcher**: ISystemLauncher.OpenUrl() for URLs, OpenPath() for files/directories. Uses Windows (cmd /c start), macOS (open), Linux (xdg-open).

## Tests

- **Unit tests** (`tests/YTPlaylistTracker.UnitTests/`): NSubstitute mocks, tests SyncService diff logic, error scenarios, and URL parsing
- **Integration tests** (`tests/YTPlaylistTracker.IntegrationTests/`): Real in-memory SQLite, tests repository CRUD and end-to-end sync flow

## Code Review Rules

See `.claude/rules/` (auto-loaded by Claude Code):
- `async-tui-safety.md` — Terminal.Gui threading rules (no async lambdas in Application.Invoke, timer cleanup, background work patterns)
- `code-review-checklist.md` — General checklist for async, timers, threading, tests, and security

## Docs

`docs/` is the project documentation directory (architecture decisions, DB schema, backlog).

**IMPORTANT**: Before modifying any Terminal.Gui UI code, **read the relevant `docs/tui/` reference pages first**. This includes layout (`layout.md`), navigation/focus (`navigation.md`), scrolling (`scrolling.md`), events (`events.md`), views (`views.md`), keyboard input (`keyboard.md`), and the v2 patterns rule (`.claude/rules/terminal-gui-v2-patterns.md`). Terminal.Gui v2 has many breaking changes from v1 — do not assume APIs, always verify against the docs.
