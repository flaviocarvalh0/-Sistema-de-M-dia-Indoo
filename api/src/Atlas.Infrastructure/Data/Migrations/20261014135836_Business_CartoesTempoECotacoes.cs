using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_CartoesTempoECotacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "previsao_dias",
                schema: "indoor",
                table: "previsao_clima",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "cartao_cotacoes",
                schema: "indoor",
                table: "playlist",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "cartao_tempo",
                schema: "indoor",
                table: "playlist",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "cotacao",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    moeda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    compra = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    venda = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    boletim = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cotada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fechamento_anterior = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cotacao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cotacao_moeda",
                schema: "indoor",
                table: "cotacao",
                column: "moeda",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cotacao",
                schema: "indoor");

            migrationBuilder.DropColumn(
                name: "previsao_dias",
                schema: "indoor",
                table: "previsao_clima");

            migrationBuilder.DropColumn(
                name: "cartao_cotacoes",
                schema: "indoor",
                table: "playlist");

            migrationBuilder.DropColumn(
                name: "cartao_tempo",
                schema: "indoor",
                table: "playlist");
        }
    }
}
