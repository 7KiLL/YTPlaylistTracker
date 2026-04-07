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

## Sub-project B: Background Sync & History
- [ ] Background sync timer (configurable interval, runs while TUI is open)
- [x] Auto-sync on startup (configurable)
- [x] Removal history/timeline view (show when videos were removed)
- [ ] Undo/restore soft-deleted videos

## Future
- [x] Export removed videos report (CSV/JSON)
- [ ] Notification when videos are removed (desktop notification or webhook)
- [ ] Track video duration/view count changes
- [ ] Playlist diff view (side-by-side before/after sync)
- [ ] Bulk import playlists from YouTube account
- [ ] Light theme toggle in TUI
- [ ] Multiple profiles in TUI (create/rename/delete profiles)
- [ ] Smoke tests with Testcontainers (Linux container)
