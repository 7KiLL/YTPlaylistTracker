# Database Schema

SQLite database located at:
- Linux: `~/.local/share/ytpt/tracker.db`
- macOS: `~/Library/Application Support/ytpt/tracker.db`
- Windows: `%LOCALAPPDATA%/ytpt/tracker.db`

File permissions: `600` (owner read/write only) on Linux/macOS.

## Tables

### Profiles
| Column           | Type     | Constraints      |
|------------------|----------|------------------|
| Id               | INTEGER  | PK, auto-incr    |
| Name             | TEXT     | NOT NULL, max 100 |
| YouTubeChannelId | TEXT     | nullable          |
| OAuthTokenPath   | TEXT     | nullable          |
| IsDefault        | INTEGER  | boolean           |
| CreatedAt        | TEXT     | datetime          |

### Playlists
| Column             | Type     | Constraints                          |
|--------------------|----------|--------------------------------------|
| Id                 | INTEGER  | PK, auto-incr                        |
| ProfileId          | INTEGER  | FK → Profiles.Id                     |
| YouTubePlaylistId  | TEXT     | NOT NULL, max 100                    |
| Title              | TEXT     | nullable                             |
| IsTracked          | INTEGER  | boolean                              |
| LastSyncedAt       | TEXT     | datetime, nullable                   |

**Unique index**: `(ProfileId, YouTubePlaylistId)`

### Videos
| Column          | Type     | Constraints                          |
|-----------------|----------|--------------------------------------|
| Id              | INTEGER  | PK, auto-incr                        |
| PlaylistId      | INTEGER  | FK → Playlists.Id                    |
| YouTubeVideoId  | TEXT     | NOT NULL, max 20                     |
| Title           | TEXT     | NOT NULL, max 500                    |
| ChannelTitle    | TEXT     | nullable, max 200                    |
| RemovalReason   | INTEGER  | nullable, enum (0-4)                 |
| DeletedAt       | TEXT     | datetime, nullable                   |

**Unique index**: `(PlaylistId, YouTubeVideoId)`

### RemovalReason Enum Values
| Value | Name           |
|-------|----------------|
| 0     | Unknown        |
| 1     | Deleted        |
| 2     | Private        |
| 3     | Unlisted       |
| 4     | RemovedByOwner |

## Soft-Delete Pattern
- Active videos: `DeletedAt IS NULL`
- Removed videos: `DeletedAt IS NOT NULL`
- No automatic pruning — manual purge via Settings dialog (F9)
