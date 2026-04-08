using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTPlaylistTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileIsOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOffline",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOffline",
                table: "Profiles");
        }
    }
}
