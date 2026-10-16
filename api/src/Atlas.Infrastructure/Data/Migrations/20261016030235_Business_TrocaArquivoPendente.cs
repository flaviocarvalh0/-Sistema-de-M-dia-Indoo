using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_TrocaArquivoPendente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "motivo_recusa_troca",
                schema: "indoor",
                table: "midia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pendente_chave",
                schema: "indoor",
                table: "midia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pendente_duracao_segundos",
                schema: "indoor",
                table: "midia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pendente_enviado_em",
                schema: "indoor",
                table: "midia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pendente_hash",
                schema: "indoor",
                table: "midia",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pendente_tamanho_bytes",
                schema: "indoor",
                table: "midia",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pendente_tipo",
                schema: "indoor",
                table: "midia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pendente_tipo_conteudo",
                schema: "indoor",
                table: "midia",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_midia_troca_pendente",
                schema: "indoor",
                table: "midia",
                sql: "(pendente_chave IS NULL) = (pendente_tipo IS NULL) AND (pendente_chave IS NULL) = (pendente_tipo_conteudo IS NULL) AND (pendente_chave IS NULL) = (pendente_tamanho_bytes IS NULL) AND (pendente_chave IS NULL) = (pendente_hash IS NULL) AND (pendente_chave IS NULL) = (pendente_enviado_em IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_midia_troca_pendente",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "motivo_recusa_troca",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_chave",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_duracao_segundos",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_enviado_em",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_hash",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_tamanho_bytes",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_tipo",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "pendente_tipo_conteudo",
                schema: "indoor",
                table: "midia");
        }
    }
}
