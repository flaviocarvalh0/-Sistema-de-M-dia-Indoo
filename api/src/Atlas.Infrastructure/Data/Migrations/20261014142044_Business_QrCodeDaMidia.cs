using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_QrCodeDaMidia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "qr_cidade_recebedor",
                schema: "indoor",
                table: "midia",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_conteudo",
                schema: "indoor",
                table: "midia",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_destino",
                schema: "indoor",
                table: "midia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_legenda",
                schema: "indoor",
                table: "midia",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_mensagem",
                schema: "indoor",
                table: "midia",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_nome_recebedor",
                schema: "indoor",
                table: "midia",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "qr_posicao",
                schema: "indoor",
                table: "midia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "qr_tamanho",
                schema: "indoor",
                table: "midia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "qr_tipo",
                schema: "indoor",
                table: "midia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "qr_valor",
                schema: "indoor",
                table: "midia",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_midia_qr_code_completo",
                schema: "indoor",
                table: "midia",
                sql: "(qr_tipo IS NULL) = (qr_conteudo IS NULL) AND (qr_tipo IS NULL) = (qr_destino IS NULL) AND (qr_tipo IS NULL) = (qr_posicao IS NULL) AND (qr_tipo IS NULL) = (qr_tamanho IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_midia_qr_valor",
                schema: "indoor",
                table: "midia",
                sql: "qr_valor IS NULL OR qr_valor > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_midia_qr_code_completo",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_midia_qr_valor",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_cidade_recebedor",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_conteudo",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_destino",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_legenda",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_mensagem",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_nome_recebedor",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_posicao",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_tamanho",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_tipo",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "qr_valor",
                schema: "indoor",
                table: "midia");
        }
    }
}
