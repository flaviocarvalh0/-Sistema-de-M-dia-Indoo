using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_DuracaoMaximaVolta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duracao_maxima_volta_segundos",
                schema: "indoor",
                table: "playlist",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_playlist_duracao_maxima_volta",
                schema: "indoor",
                table: "playlist",
                sql: "duracao_maxima_volta_segundos = 0 OR duracao_maxima_volta_segundos BETWEEN 30 AND 3600");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_playlist_duracao_maxima_volta",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "duracao_maxima_volta_segundos",
                schema: "indoor",
                table: "playlist");
        }
    }
}
