using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    // DS14-start
    [DbContext(typeof(PostgresServerDbContext))]
    [Migration("20261017120001_StationAiLawset")]
    public partial class StationAiLawset : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "station_ai_lawset",
                table: "profile",
                type: "text",
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
