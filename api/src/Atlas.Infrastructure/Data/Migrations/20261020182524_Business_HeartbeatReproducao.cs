using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_HeartbeatReproducao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "alerta_fora_de_exibicao_em",
                schema: "indoor",
                table: "estado_tela",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "desvio_relogio_segundos",
                schema: "indoor",
                table: "estado_tela",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duracao_item_segundos",
                schema: "indoor",
                table: "estado_tela",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fora_de_exibicao_desde",
                schema: "indoor",
                table: "estado_tela",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "itens_hoje",
                schema: "indoor",
                table: "estado_tela",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "midia_atual_id",
                schema: "indoor",
                table: "estado_tela",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "modo_reproducao",
                schema: "indoor",
                table: "estado_tela",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "motivo_fora_de_exibicao",
                schema: "indoor",
                table: "estado_tela",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "primeiro_plano",
                schema: "indoor",
                table: "estado_tela",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reproducao_desde",
                schema: "indoor",
                table: "estado_tela",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alerta_fora_de_exibicao_em",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "desvio_relogio_segundos",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "duracao_item_segundos",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "fora_de_exibicao_desde",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "itens_hoje",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "midia_atual_id",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "modo_reproducao",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "motivo_fora_de_exibicao",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "primeiro_plano",
                schema: "indoor",
                table: "estado_tela");

            migrationBuilder.DropColumn(
                name: "reproducao_desde",
                schema: "indoor",
                table: "estado_tela");
        }
    }
}
