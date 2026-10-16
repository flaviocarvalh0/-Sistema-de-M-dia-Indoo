using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_AlertaTvOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "alerta_offline_em",
                schema: "indoor",
                table: "estado_tela",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alerta_offline_em",
                schema: "indoor",
                table: "estado_tela");
        }
    }
}
