<p align="center">
  <img src="assets/logo.png" alt="ytpt logo" width="400">
</p>

# ytpt — YouTube Playlist Tracker

Track your YouTube playlists and know exactly which videos disappeared. When a video gets deleted, made private, or removed — **ytpt** preserves the title and metadata so you always know what's gone.

## The Problem

YouTube doesn't notify you when videos are removed from your playlists. One day a playlist has 200 videos, the next it has 197 — and you have no idea which 3 vanished or why.

## Quick Start

**1. Install**

```bash
# Linux / macOS
curl -fsSL https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.sh | bash

# Windows (PowerShell)
irm https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.ps1 | iex
```

**2. Sign in**

```bash
ytpt login    # Opens browser → Sign in with Google → done
```

**3. Launch**

```bash
ytpt          # Opens the TUI — add playlists, sync, browse
```

That's it. Add a playlist URL, hit sync, and ytpt will start tracking.

## How Sync Works

```mermaid
flowchart TD
    A(["You press Sync"]) --> B["Fetch current playlist\nfrom YouTube API"]
    B --> C["Load saved videos\nfrom local database"]
    C --> D{"Compare:\nany videos\nmissing?"}

    D -- "No" --> U{"Any metadata\nchanged?"}
    U -- "Yes" --> V["Update titles, thumbnails, etc."]
    U -- "No" --> W(["Done — playlist up to date"])
    V --> W

    D -- "Yes" --> E["Ask YouTube:\nwhat happened\nto each missing video?"]

    E --> F{"Video\nstatus?"}

    F -- "Deleted from YouTube" --> G["Mark as Deleted"]
    F -- "Made private" --> H["Mark as Private"]
    F -- "Made unlisted" --> I["Mark as Unlisted"]
    F -- "Still public" --> J["Mark as Removed\nfrom playlist"]

    G --> K["Save to database\nwith original title preserved"]
    H --> K
    I --> K
    J --> K

    D -- "New videos\nappeared" --> L["Add new videos\nto database"]

    K --> W
    L --> W

    style A fill:#0d9488,color:#fff
    style W fill:#0d9488,color:#fff
    style G fill:#dc2626,color:#fff
    style H fill:#d97706,color:#fff
    style I fill:#d97706,color:#fff
    style J fill:#6366f1,color:#fff
    style L fill:#16a34a,color:#fff
```

**The key insight:** when a video vanishes from your playlist, ytpt makes a second API call to check the video directly. That's how it knows whether it was deleted, made private, or just removed from the playlist by the owner.

All original titles and metadata are preserved forever in your local database — even after YouTube deletes them.

## TUI Preview

```
┌─ Profiles ─┬─ Playlists ────┬─ Videos ("Music 2026") ───────────────┐
│ > Personal │ [x] Music 2026 │ #  │ Title         │ Channel │ Status │
│   Work     │ [x] Tech Talks │ 1  │ Song A        │ ArtistX │ Active │
│            │ [ ] Cooking    │ 2  │ Song B        │ ArtistY │ Active │
│            │                │ 3  │ Old Song      │ ArtistZ │ X Gone │
├────────────┴────────────────┴────┴───────────────┴─────────┴────────┤
│ h/l:pane  j/k:nav  Enter:detail  /:search  o:sort  ?:help  q:quit  │
└─────────────────────────────────────────────────────────────────────┘
```

## Install

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.sh | bash
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.ps1 | iex
```

### Install a specific version

```bash
YTPT_VERSION=v0.3.0 curl -fsSL https://raw.githubusercontent.com/7KiLL/YTPlaylistTracker/main/scripts/install.sh | bash
```

### Supported platforms

| Platform | Architecture |
|----------|-------------|
| Linux | x64, arm64 |
| macOS | x64 (Intel), arm64 (Apple Silicon) |
| Windows | x64 |

## Authentication

### OAuth2 (recommended — access private playlists)

```bash
ytpt login    # Opens browser → Sign in with Google → done
ytpt logout   # Remove stored tokens
```

### API Key (public playlists only)

```bash
export YOUTUBE_API_KEY=your_key_here
ytpt
```

Get an API key from [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials → Create API Key → Enable YouTube Data API v3.

## CLI Commands

| Command | Description |
|---------|-------------|
| `ytpt` or `ytpt ui` | Launch interactive TUI |
| `ytpt login [--profile name]` | Sign in with Google (OAuth2) |
| `ytpt logout [--profile name]` | Sign out, remove stored tokens |
| `ytpt sync [--profile name]` | Sync all tracked playlists (headless) |
| `ytpt sync <playlist-id>` | Sync a specific playlist |
| `ytpt status` | Show tracking summary |
| `ytpt export` | Export removed videos (CSV to stdout) |
| `ytpt export -f json -o report.json` | Export as JSON to file |
| `ytpt update` | Check for and apply updates |
| `ytpt reset [--yes]` | Delete database and start fresh |
| `ytpt --help` | Show all available commands |

Add `--verbose` or `-v` to any command for debug logging.

## TUI Keybindings

### Navigation

| Key | Action |
|-----|--------|
| `h` / `l` / Left / Right | Navigate between panes (profiles, playlists, videos) |
| `j` / `k` | Navigate up/down in focused pane |
| `J` / `K` / Shift+Up/Down | Fast scroll (5 rows) |
| Tab / Shift+Tab | Cycle focus between panes |
| `Enter` | View details / open context menu (profiles) |

### Actions

| Key | Action |
|-----|--------|
| `a` / `F1` | Add playlist (paste URL or ID) |
| `t` / `F2` | Toggle tracking on/off for selected playlist |
| `T` | Toggle all playlists in profile |
| `s` / `F5` | Sync selected playlist |
| `S` / `F6` | Sync all tracked playlists |
| `e` / `F7` | Export removed videos report |
| `H` / `F11` | Show sync history |
| `u` / Ctrl+U | Check for updates |

### Search & Filter

| Key | Action |
|-----|--------|
| `/` / `F3` | Search videos by title or channel |
| `o` / `F4` | Sort videos (Title, Channel, Date, Status) |
| `F8` | Toggle: show removed videos only |

### Profile Management (when profile pane is focused)

| Key | Action |
|-----|--------|
| `n` | New profile |
| `L` | Login / logout for selected profile |
| `r` | Rename profile |
| `d` | Set as default profile |
| `x` | Delete profile |

### General

| Key | Action |
|-----|--------|
| `?` / `F12` | Show help dialog |
| `F9` | Settings |
| `q` / `F10` | Quit |
| Ctrl+C x2 | Quick quit (double-press within 1s) |
| Esc | Close search (when searching) |

## Auto-Update

ytpt can check for new releases and update itself in place:

```bash
ytpt update             # CLI: check and apply update
```

In the TUI, press `u` or `Ctrl+U` to check for updates. Auto-install on startup can be enabled in Settings (`F9`).

## Multi-Profile Support

ytpt supports multiple Google accounts. Each profile has its own OAuth token, playlists, and videos — fully isolated.

- Create profiles in the TUI (press `n` in the profile pane) or use `--profile` with CLI commands
- Switch between profiles in the left pane of the TUI
- Set a default profile with `d`

## Data Storage

All data is stored locally:

| OS | Path |
|----|------|
| Linux | `~/.local/share/ytpt/` |
| macOS | `~/Library/Application Support/ytpt/` |
| Windows | `%LOCALAPPDATA%\ytpt\` |

Contents:
- `tracker.db` — SQLite database (playlists, videos, removal history)
- `logs/` — Rolling log files (7 day retention)
- `oauth-tokens/` — Google OAuth refresh tokens (per profile)

File permissions are set to owner-only (700/600) on Linux/macOS.

---

## Development

### Run from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download):

```bash
git clone https://github.com/7KiLL/YTPlaylistTracker.git
cd YTPlaylistTracker
dotnet build
dotnet run --project src/YTPlaylistTracker.UI
```

### Install as .NET global tool

```bash
dotnet pack src/YTPlaylistTracker.UI -c Release
dotnet tool install -g --add-source src/YTPlaylistTracker.UI/bin/Release ytpt
ytpt
```

### Tests

```bash
dotnet build                        # Build all
dotnet test                         # Run all tests
dotnet test --filter "SyncService"  # Run specific tests
```

Test projects:
- **Unit tests** — SyncService diff logic, URL parsing, error scenarios (NSubstitute mocks)
- **Integration tests** — Repository CRUD, end-to-end sync flow (in-memory SQLite)
- **Scenario tests** — TUI interaction scenarios

### Building with OAuth credentials

```bash
dotnet publish src/YTPlaylistTracker.UI -c Release \
  -p:OAuthClientId=$YTPT_CLIENT_ID \
  -p:OAuthClientSecret=$YTPT_CLIENT_SECRET
```

OAuth credentials are injected at build time — never stored in source code.

### Architecture

Clean Architecture with four layers:

```
Domain (entities, interfaces) <- Application (sync logic) <- Infrastructure (DB, API) <- UI (TUI, CLI)
```

See [docs/architecture.md](docs/architecture.md) for details.

### Tech Stack

- **.NET 10** / C#
- **Terminal.Gui** — cross-platform TUI framework
- **System.CommandLine** — CLI argument parsing (Microsoft)
- **EF Core + SQLite** — local database
- **Google.Apis.YouTube.v3** — YouTube Data API client
- **Serilog** — structured logging
- **xUnit + NSubstitute** — testing

## Known Issues

- **Emoji in video titles** — Emoji characters (e.g. 🏕, 🎮) are replaced with spaces in the TUI display. This is a Terminal.Gui limitation: its Unicode width tables don't accurately measure modern emoji, causing column misalignment. The original titles with emoji are preserved in the database and in exports.

## AI Disclosure

This project was largely built with AI assistance ([Claude Code](https://claude.ai/code)). The logo was generated with ChatGPT. All code, documentation, and CI/CD pipelines were reviewed and approved by a human maintainer.

## License

MIT
