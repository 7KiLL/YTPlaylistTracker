using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTPlaylistTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistKindAndIsManuallyAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyAdded",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill Kind from YouTube playlist ID prefix
            migrationBuilder.Sql("""
                UPDATE Playlists SET Kind = 1 WHERE YouTubePlaylistId LIKE 'LL%';
                UPDATE Playlists SET Kind = 2 WHERE YouTubePlaylistId LIKE 'WL%';
                UPDATE Playlists SET Kind = 3 WHERE YouTubePlaylistId LIKE 'UU%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyAdded",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Playlists");
        }
    }
}
