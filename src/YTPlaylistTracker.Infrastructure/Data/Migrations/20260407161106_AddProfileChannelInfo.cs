using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTPlaylistTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileChannelInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelThumbnailUrl",
                table: "Profiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelTitle",
                table: "Profiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelThumbnailUrl",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "ChannelTitle",
                table: "Profiles");
        }
    }
}
