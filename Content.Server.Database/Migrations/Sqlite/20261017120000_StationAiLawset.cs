using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    // DS14-start
    [DbContext(typeof(SqliteServerDbContext))]
    [Migration("20261017120000_StationAiLawset")]
    public partial class StationAiLawset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "station_ai_lawset",
                table: "profile",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "station_ai_lawset",
                table: "profile");
        }
    }
    // DS14-end
}
