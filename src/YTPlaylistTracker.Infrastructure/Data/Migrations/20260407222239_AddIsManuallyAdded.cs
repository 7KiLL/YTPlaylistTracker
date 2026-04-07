using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTPlaylistTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsManuallyAdded : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyAdded",
                table: "Playlists");
        }
    }
}
