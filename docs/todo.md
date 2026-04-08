# TODO / Backlog

## Completed (v0.1.0 — v0.4.0)
- [x] Multi-profile support
- [x] Playlist tracking with soft-delete history
- [x] OAuth2 sign-in (persisted credentials)
- [x] Manual sync (individual and all)
- [x] TUI with three-pane layout (profiles, playlists, videos)
- [x] Video search (by title/channel)
- [x] Video sorting (Title, Channel, Added Date, Status)
- [x] Removed videos view (filtered display)
- [x] Detail dialogs for profiles/playlists/videos
- [x] Dark theme + Ctrl+C quit confirmation
- [x] Database reset command (ytpt reset)
- [x] Cross-platform browser launching
- [x] Lazy YouTube API initialization
- [x] EF Core Migrations (auto-upgrade from v0.1.0 databases)
- [x] Video metadata: AddedAt, Description, ThumbnailUrl, Position, JsonMetadata
- [x] Playlist metadata: Description, ThumbnailUrl, PublishedAt, JsonMetadata
- [x] Release infrastructure (GitHub Actions, cross-platform builds)
- [x] Install scripts (bash + PowerShell one-liners)
- [x] Fixture-based sync tests

## Bug Fixes (v0.4.x)
- [x] Fix MainLoop.Invoke(async () => ...) deadlock — Terminal.Gui treats async lambdas as fire-and-forget; replaced with sync lambda + .GetAwaiter().GetResult()
- [x] Fix ShowSpinner() timer leak — calling ShowSpinner twice leaked the first timer; now cleans up previous timer before creating a new one
- [x] Fix spinner flicker — reduced interval from 80ms to 200ms
- [x] Fix UserSettings test flakiness — File.Move now uses overwrite:true
- [x] Add validate-secrets CI job in release.yml
- [x] Improve API logging with [API] prefix for easier filtering

## Completed (v0.5.0)
- [x] Auto-sync on startup (configurable)
- [x] Removal history/timeline view (show when videos were removed)
- [x] Export removed videos report (CSV/JSON)
- [x] Bulk import playlists from YouTube account (auto-imports on startup via authenticated API)

## In Progress
- [x] **Enrich profile with YouTube channel info** — Fetch channel name, ID, and avatar via `channels.list?mine=true` on login/sync/startup; profile list shows channel name instead of "Default".
- [x] **Update/upgrade system** — Auto-check for updates on startup, title bar notification, in-place binary replacement via Ctrl+U / Settings / `ytpt update` CLI.

## Backlog (prioritized)

### P1 — Core UX gaps
- [x] **Profile management in TUI** — Per-profile OAuth, offline profiles, CRUD dialogs, context menu, hotkeys (n/L/r/d/x).
- [ ] **Profile pane reselection bug** — Down-arrow on single-profile list re-fires OnProfileSelected, clearing video list. Guard against reselecting same profile or suppress redundant events.
- [ ] **Video detail Status field** — Shows "Active" followed by description text bleeding into the Status line. Likely a truncation/formatting issue in DetailDialog.ForVideo().

### P2 — Polish
- [ ] **Playlist diff view** — Side-by-side before/after comparison when a sync detects changes, making removals easier to review at a glance.
- [ ] **Light theme toggle** — Add a theme switcher in TUI settings for users who prefer light terminals.

### P3 — Infrastructure / nice-to-have
- [ ] **Track video duration & view count** — Fetch `contentDetails`/`statistics` from YouTube API and store in DB for richer historical records (no UI display needed).
- [ ] **Smoke tests with Testcontainers** — End-to-end test in a Linux container to catch platform-specific issues in CI.
