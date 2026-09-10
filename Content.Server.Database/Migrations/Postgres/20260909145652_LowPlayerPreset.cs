using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LowPlayerPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enable_low_player_preset",
                table: "game_preset_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "low_player_preset_id",
                table: "game_preset_config",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "low_player_threshold",
                table: "game_preset_config",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enable_low_player_preset",
                table: "game_preset_config");

            migrationBuilder.DropColumn(
                name: "low_player_preset_id",
                table: "game_preset_config");

            migrationBuilder.DropColumn(
                name: "low_player_threshold",
                table: "game_preset_config");
        }
    }
}
