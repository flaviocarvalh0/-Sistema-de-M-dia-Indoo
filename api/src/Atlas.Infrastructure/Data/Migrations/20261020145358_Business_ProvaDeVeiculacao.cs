using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Atlas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Business_ProvaDeVeiculacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_qr",
                schema: "indoor",
                table: "midia",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Mídias que já tinham QR de link, WhatsApp ou Instagram ganham o
            // código do link rastreado (ADR-023). O WHERE com midia.id força um
            // código por linha (subconsulta correlacionada).
            migrationBuilder.Sql("""
                UPDATE indoor.midia
                   SET codigo_qr = (SELECT string_agg(substr('ABCDEFGHJKLMNPQRSTUVWXYZ23456789', 1 + floor(random() * 32)::int, 1), '')
                                      FROM generate_series(1, 8)
                                     WHERE midia.id IS NOT NULL)
                 WHERE qr_tipo IN (1, 2, 3) AND qr_conteudo IS NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "leitura_qr",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    midia_id = table.Column<long>(type: "bigint", nullable: false),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    momento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    visitante = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leitura_qr", x => x.id);
                    table.CheckConstraint("ck_leitura_qr_visitante", "visitante ~ '^[0-9a-f]{16}$'");
                });

            migrationBuilder.CreateTable(
                name: "registro_exibicao",
                schema: "indoor",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tela_id = table.Column<long>(type: "bigint", nullable: false),
                    parceiro_id = table.Column<long>(type: "bigint", nullable: false),
                    midia_id = table.Column<long>(type: "bigint", nullable: false),
                    cliente_id = table.Column<long>(type: "bigint", nullable: false),
                    id_local = table.Column<Guid>(type: "uuid", nullable: false),
                    inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracao_ms = table.Column<int>(type: "integer", nullable: false),
                    concluida = table.Column<bool>(type: "boolean", nullable: false),
                    sem_conexao = table.Column<bool>(type: "boolean", nullable: false),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registro_exibicao", x => x.id);
                    table.CheckConstraint("ck_registro_exibicao_duracao", "duracao_ms BETWEEN 0 AND 7200000");
                });

            migrationBuilder.CreateIndex(
                name: "ix_midia_codigo_qr",
                schema: "indoor",
                table: "midia",
                column: "codigo_qr",
                unique: true,
                filter: "codigo_qr IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_midia_codigo_qr",
                schema: "indoor",
                table: "midia",
                sql: "codigo_qr IS NULL OR (qr_tipo IS NOT NULL AND codigo_qr ~ '^[A-Z0-9]{8}$')");

            migrationBuilder.CreateIndex(
                name: "ix_leitura_qr_cliente_id_momento",
                schema: "indoor",
                table: "leitura_qr",
                columns: new[] { "cliente_id", "momento" });

            migrationBuilder.CreateIndex(
                name: "ix_leitura_qr_midia_id_momento",
                schema: "indoor",
                table: "leitura_qr",
                columns: new[] { "midia_id", "momento" });

            migrationBuilder.CreateIndex(
                name: "ix_registro_exibicao_cliente_id_inicio",
                schema: "indoor",
                table: "registro_exibicao",
                columns: new[] { "cliente_id", "inicio" });

            migrationBuilder.CreateIndex(
                name: "ix_registro_exibicao_tela_id_id_local",
                schema: "indoor",
                table: "registro_exibicao",
                columns: new[] { "tela_id", "id_local" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_registro_exibicao_tela_id_inicio",
                schema: "indoor",
                table: "registro_exibicao",
                columns: new[] { "tela_id", "inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leitura_qr",
                schema: "indoor");

            migrationBuilder.DropTable(
                name: "registro_exibicao",
                schema: "indoor");

            migrationBuilder.DropIndex(
                name: "ix_midia_codigo_qr",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropCheckConstraint(
                name: "ck_midia_codigo_qr",
                schema: "indoor",
                table: "midia");

            migrationBuilder.DropColumn(
                name: "codigo_qr",
                schema: "indoor",
                table: "midia");
        }
    }
}
