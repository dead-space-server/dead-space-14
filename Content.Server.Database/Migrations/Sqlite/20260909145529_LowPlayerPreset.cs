using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
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
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "low_player_preset_id",
                table: "game_preset_config",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "low_player_threshold",
                table: "game_preset_config",
                type: "INTEGER",
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
